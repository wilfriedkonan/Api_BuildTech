using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Api_BuildTech.Controllers.Authentication
{
    public class AuthService : DatabaseService
    {
        private readonly IConfiguration _configuration;

        public AuthService(
            string connectionString,
            ILogger<AuthService> logger,
            IHttpContextAccessor httpContextAccessor, // ✅ AJOUTÉ
            IConfiguration configuration)
            : base(connectionString, logger, httpContextAccessor) // ✅ AJOUTÉ
        {
            _configuration = configuration;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // 1. Récupérer l'utilisateur
                var user = await GetUserByEmailAsync(request.Email);

                if (user == null || !VerifyPassword(request.Password, user.MotDePasse))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Email ou mot de passe incorrect"
                    };
                }

                // 2. Vérifier que l'utilisateur est actif
                if (!user.IsActive)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Compte désactivé"
                    };
                }

                // 3. Vérifier que l'entreprise est active
                if (!await ValidateEntrepriseAsync(user.IdEntreprise))
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Entreprise inactive ou souscription expirée"
                    };
                }

                // 4. Récupérer les infos complémentaires
                var role = await GetRoleAsync(user.IdRole);
                var entrepriseName = await GetEntrepriseNameAsync(user.IdEntreprise);
                
                // 5. Générer les tokens
                var accessToken = GenerateAccessToken(user, role?.Name);
                var refreshToken = GenerateRefreshToken();

                // 6. Mettre à jour la dernière connexion
                await UpdateLastLoginAsync(user.Id);

                // 7. Construire la réponse
                return new LoginResponse
                {
                    Success = true,
                    Message = "Authentification réussie",
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Nom = user.Nom,
                        Prenom = user.Prenom,
                        IdEntreprise = user.IdEntreprise,
                        NomEntreprise = entrepriseName,
                        IsSuperAdmin = user.IsSuperAdmin,
                        Role = role?.Name,
                        IsActive = user.IsActive
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur login: {request.Email}");
                return new LoginResponse
                {
                    Success = false,
                    Message = "Erreur serveur lors de l'authentification"
                };
            }
        }

        private async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Email, Telephone, Nom, Prenom, MotDePasse, IdEntreprise,
                           IdRole, IdPermission, IsSuperAdmin, IsActive,
                           LastLoginAt, CreatedAt, UpdatedAt
                    FROM UTILISATEURS
                    WHERE Email = @Email", conn);

                cmd.Parameters.AddWithValue("@Email", email);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new User
                    {
                        Id = reader.GetGuid(reader.GetOrdinal("Id")),
                        Email = reader.GetString(reader.GetOrdinal("Email")), // ✅ CORRIGÉ
                        Telephone = ReadNullableString(reader, "Telephone") ?? "", // ✅ CORRIGÉ - gère NULL
                        Nom = ReadNullableString(reader, "Nom"), // ✅ CORRIGÉ
                        Prenom = ReadNullableString(reader, "Prenom"), // ✅ CORRIGÉ
                        MotDePasse = ReadNullableString(reader, "MotDePasse") ?? "", // ✅ CORRIGÉ
                        IdEntreprise = reader.GetGuid(reader.GetOrdinal("IdEntreprise")),
                        IdRole = ReadNullableGuid(reader, "IdRole"), // ✅ CORRIGÉ - gère NULL
                        IdPermission = ReadNullableGuid(reader, "IdPermission"), // ✅ CORRIGÉ
                        IsSuperAdmin = reader.GetBoolean(reader.GetOrdinal("IsSuperAdmin")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        LastLoginAt = ReadNullableDateTime(reader, "LastLoginAt"), // ✅ CORRIGÉ
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                        UpdatedAt = ReadNullableDateTime(reader, "UpdatedAt") // ✅ CORRIGÉ
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération user: {email}");
            }

            return null;
        }

        private async Task<bool> ValidateEntrepriseAsync(Guid idEntreprise)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Vérifier que l'entreprise est active ET que l'abonnement est valide
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM ENTREPRISE e
                    LEFT JOIN SUBSCRIPTIONS s ON e.Id = s.IdEntreprise 
                        AND s.Status = 'Active' 
                        AND s.EndDate > GETUTCDATE()
                    WHERE e.Id = @IdEntreprise 
                      AND e.IsActive = 1
                      AND (s.Id IS NOT NULL OR e.Autorisation = 1)", conn);

                cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

                var count = (int?)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur validation entreprise: {idEntreprise}");
                return false;
            }
        }

        private async Task<Role?> GetRoleAsync(Guid? idRole)
        {
            if (!idRole.HasValue)
                return null;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Name, Description, CreatedAt
                    FROM ROLES
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", idRole.Value);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new Role
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Description = ReadNullableString(reader, "Description"),
                        CreatedAt = reader.GetDateTime(3)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération role: {idRole}");
            }

            return null;
        }

        private async Task<string?> GetEntrepriseNameAsync(Guid idEntreprise)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Designation 
                    FROM ENTREPRISE 
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", idEntreprise);

                return (string?)await cmd.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération nom entreprise: {idEntreprise}");
                return null;
            }
        }

        private string GenerateAccessToken(User user, string? roleName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("entreprise_id", user.IdEntreprise.ToString()),
                new Claim("is_super_admin", user.IsSuperAdmin.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Ajouter le nom complet si disponible
            if (!string.IsNullOrEmpty(user.Prenom) || !string.IsNullOrEmpty(user.Nom))
            {
                var fullName = $"{user.Prenom} {user.Nom}".Trim();
                if (!string.IsNullOrEmpty(fullName))
                    claims.Add(new Claim(ClaimTypes.Name, fullName));
            }

            // Ajouter le rôle
            if (!string.IsNullOrEmpty(roleName))
            {
                claims.Add(new Claim(ClaimTypes.Role, roleName));
            }

            // SuperAdmin a tous les rôles
            if (user.IsSuperAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey manquante")));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private bool VerifyPassword(string password, string hash)
        {
            // TODO: Implémenter BCrypt.Net
            // return BCrypt.Net.BCrypt.Verify(password, hash);

            // TEMPORAIRE - UNIQUEMENT POUR DÉVELOPPEMENT
            return password == hash;
        }

        private async Task UpdateLastLoginAsync(Guid userId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE UTILISATEURS
                    SET LastLoginAt = GETUTCDATE(),
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", userId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour LastLogin: {userId}");
            }
        }
    }
}