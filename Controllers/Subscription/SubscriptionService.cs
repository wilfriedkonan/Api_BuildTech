using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api_BuildTech.Controllers.Subscription
{
    public class SubscriptionService : DatabaseService
    {
        private readonly IConfiguration _configuration;

        public SubscriptionService(
            string connectionString,
            ILogger<SubscriptionService> logger,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Valide l'abonnement d'une entreprise et génère un token JWT
        /// </summary>
        public async Task<SubscriptionValidationResponse> ValidateSubscriptionAsync(Guid idEntreprise)
        {
            try
            {
                // Récupérer l'abonnement actif
                var subscription = await GetActiveSubscriptionAsync(idEntreprise);

                if (subscription == null)
                {
                    return new SubscriptionValidationResponse
                    {
                        IsValid = false,
                        Message = "Aucun abonnement actif trouvé",
                        BlockAccess = true
                    };
                }

                // Vérifier si expiré
                if (subscription.EndDate < DateTime.UtcNow)
                {
                    return new SubscriptionValidationResponse
                    {
                        IsValid = false,
                        Message = "Votre abonnement a expiré",
                        BlockAccess = true
                    };
                }

                // Récupérer le plan
                var plan = await GetPlanAsync(subscription.IdPlan);

                if (plan == null)
                {
                    return new SubscriptionValidationResponse
                    {
                        IsValid = false,
                        Message = "Plan introuvable",
                        BlockAccess = true
                    };
                }

                // Générer token JWT signé (valide 24h)
                var validationToken = GenerateValidationToken(subscription, plan);

                // Enregistrer la validation
                await SaveValidationTokenAsync(idEntreprise, validationToken);

                // Mettre à jour LastValidated
                await UpdateLastValidatedAsync(subscription.Id);

                return new SubscriptionValidationResponse
                {
                    IsValid = true,
                    Message = "Abonnement valide",
                    ValidationToken = validationToken,
                    PlanName = plan.Name,
                    ExpiresAt = subscription.EndDate,
                    Limits = new SubscriptionLimits
                    {
                        MaxUsers = plan.MaxUsers,
                        MaxInvoicesPerMonth = plan.MaxInvoicesPerMonth,
                        MaxStorageMB = plan.MaxStorageMB
                    },
                    BlockAccess = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur validation abonnement {idEntreprise}");
                return new SubscriptionValidationResponse
                {
                    IsValid = false,
                    Message = "Erreur lors de la validation",
                    BlockAccess = true
                };
            }
        }

        /// <summary>
        /// Valide une opération critique (ajout user, création facture, etc.)
        /// </summary>
        public async Task<ValidateOperationResponse> ValidateOperationAsync(ValidateOperationRequest request)
        {
            try
            {
                // Vérifier le token JWT
                if (!ValidateToken(request.ValidationToken, request.IdEntreprise))
                {
                    return new ValidateOperationResponse
                    {
                        IsAllowed = false,
                        Message = "Token de validation invalide ou expiré"
                    };
                }

                // Récupérer usage actuel
                var usage = await GetSubscriptionUsageAsync(request.IdEntreprise);

                // Vérifier selon le type d'opération
                switch (request.OperationType.ToLower())
                {
                    case "adduser":
                        if (usage.CurrentUsers >= usage.MaxUsers)
                        {
                            return new ValidateOperationResponse
                            {
                                IsAllowed = false,
                                Message = $"Limite d'utilisateurs atteinte ({usage.MaxUsers} max)",
                                CurrentUsage = usage.CurrentUsers,
                                Limit = usage.MaxUsers,
                                Remaining = 0
                            };
                        }
                        break;

                    case "createinvoice":
                        if (usage.MaxInvoicesPerMonth != -1 &&
                            usage.InvoicesThisMonth >= usage.MaxInvoicesPerMonth)
                        {
                            return new ValidateOperationResponse
                            {
                                IsAllowed = false,
                                Message = $"Limite de factures atteinte ({usage.MaxInvoicesPerMonth} max/mois)",
                                CurrentUsage = usage.InvoicesThisMonth,
                                Limit = usage.MaxInvoicesPerMonth,
                                Remaining = 0
                            };
                        }
                        break;

                    case "upload":
                        if (usage.MaxStorageMB != -1 &&
                            usage.StorageUsedMB >= usage.MaxStorageMB)
                        {
                            return new ValidateOperationResponse
                            {
                                IsAllowed = false,
                                Message = $"Limite de stockage atteinte ({usage.MaxStorageMB} MB max)",
                                CurrentUsage = usage.StorageUsedMB,
                                Limit = usage.MaxStorageMB,
                                Remaining = 0
                            };
                        }
                        break;
                }

                return new ValidateOperationResponse
                {
                    IsAllowed = true,
                    Message = "Opération autorisée"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur validation opération {request.OperationType}");
                return new ValidateOperationResponse
                {
                    IsAllowed = false,
                    Message = "Erreur lors de la validation"
                };
            }
        }

        /// <summary>
        /// Récupère l'usage actuel de l'abonnement
        /// </summary>
        public async Task<SubscriptionUsage> GetSubscriptionUsageAsync(Guid idEntreprise)
        {
            using var conn = await GetConnectionAsync(); // SuperAdmin pour voir les stats

            var usage = new SubscriptionUsage { IdEntreprise = idEntreprise };

            // Récupérer les limites du plan
            var cmdPlan = new SqlCommand(@"
                SELECT p.MaxUsers, p.MaxInvoicesPerMonth, p.MaxStorageMB
                FROM SUBSCRIPTIONS s
                INNER JOIN PLANS p ON s.IdPlan = p.Id
                WHERE s.IdEntreprise = @IdEntreprise
                AND s.Status = 'Active'
                AND s.EndDate > GETUTCDATE()", conn);
            cmdPlan.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            using var readerPlan = await cmdPlan.ExecuteReaderAsync();
            if (await readerPlan.ReadAsync())
            {
                usage.MaxUsers = readerPlan.GetInt32(0);
                usage.MaxInvoicesPerMonth = readerPlan.GetInt32(1);
                usage.MaxStorageMB = readerPlan.GetInt32(2);
            }
            await readerPlan.CloseAsync();

            // Compter les utilisateurs actifs
            var cmdUsers = new SqlCommand(@"
                SELECT COUNT(*) FROM UTILISATEURS
                WHERE IdEntreprise = @IdEntreprise AND IsActive = 1", conn);
            cmdUsers.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
            usage.CurrentUsers = (int)await cmdUsers.ExecuteScalarAsync();

            // Compter les factures du mois
            var cmdInvoices = new SqlCommand(@"
                SELECT COUNT(*) FROM FACTURE
                WHERE IdEntreprise = @IdEntreprise
                AND MONTH(Date) = MONTH(GETUTCDATE())
                AND YEAR(Date) = YEAR(GETUTCDATE())", conn);
            cmdInvoices.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
            usage.InvoicesThisMonth = (int)await cmdInvoices.ExecuteScalarAsync();

            // TODO: Calculer stockage utilisé
            usage.StorageUsedMB = 0;

            return usage;
        }

        #region Méthodes Privées

        private async Task<Subscription?> GetActiveSubscriptionAsync(Guid idEntreprise)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT Id, IdEntreprise, IdPlan, Status, StartDate, EndDate,
                       AutoRenew, LastValidated, CreatedAt, UpdatedAt
                FROM SUBSCRIPTIONS
                WHERE IdEntreprise = @IdEntreprise
                AND Status = 'Active'
                AND EndDate > GETUTCDATE()
                ORDER BY EndDate DESC", conn);

            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Subscription
                {
                    Id = reader.GetGuid(0),
                    IdEntreprise = reader.GetGuid(1),
                    IdPlan = reader.GetGuid(2),
                    Status = reader.GetString(3),
                    StartDate = reader.GetDateTime(4),
                    EndDate = reader.GetDateTime(5),
                    LastValidated = reader.GetBoolean(reader.GetOrdinal( "LastValidated")),
                    CreatedAt = reader.GetDateTime(8),
                    UpdatedAt = ReadNullableDateTime(reader, "UpdatedAt")
                };
            }

            return null;
        }

        private async Task<Plan?> GetPlanAsync(Guid idPlan)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT Id, Name, Description, Price, MaxUsers,
                       MaxInvoicesPerMonth, MaxStorageMB, IsActive, CreatedAt
                FROM PLANS
                WHERE Id = @Id AND IsActive = 1", conn);

            cmd.Parameters.AddWithValue("@Id", idPlan);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Plan
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    Description = ReadNullableString(reader, "Description"),
                    Price = reader.GetDecimal(3),
                    MaxUsers = reader.GetInt32(4),
                    MaxInvoicesPerMonth = reader.GetInt32(5),
                    MaxStorageMB = reader.GetInt32(6),
                    IsActive = reader.GetBoolean(7),
                    CreatedAt = reader.GetDateTime(8)
                };
            }

            return null;
        }

        private string GenerateValidationToken(Subscription subscription, Plan plan)
        {
            var claims = new[]
            {
                new Claim("entreprise_id", subscription.IdEntreprise.ToString()),
                new Claim("plan_name", plan.Name),
                new Claim("max_users", plan.MaxUsers.ToString()),
                new Claim("max_invoices", plan.MaxInvoicesPerMonth.ToString()),
                new Claim("max_storage", plan.MaxStorageMB.ToString()),
                new Claim("expires_at", subscription.EndDate.ToString("o")),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey manquante")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "TSALACH-Server",
                audience: "TSALACH-Client",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private bool ValidateToken(string token, Guid expectedEntrepriseId)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? "");

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = "TSALACH-Server",
                    ValidateAudience = true,
                    ValidAudience = "TSALACH-Client",
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var entrepriseId = jwtToken.Claims.First(x => x.Type == "entreprise_id").Value;

                return Guid.Parse(entrepriseId) == expectedEntrepriseId;
            }
            catch
            {
                return false;
            }
        }

        private async Task SaveValidationTokenAsync(Guid idEntreprise, string token)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO SUBSCRIPTION_VALIDATIONS (Id, IdEntreprise, ValidationToken, CreatedAt, ExpiresAt, IsRevoked)
                VALUES (@Id, @IdEntreprise, @ValidationToken, GETUTCDATE(), DATEADD(HOUR, 24, GETUTCDATE()), 0)", conn);

            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
            cmd.Parameters.AddWithValue("@ValidationToken", token);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateLastValidatedAsync(Guid subscriptionId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE SUBSCRIPTIONS
                SET LastValidated = GETUTCDATE()
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", subscriptionId);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion
    }
}