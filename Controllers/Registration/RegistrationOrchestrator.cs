using Api_BuildTech.Controllers.Entreprise;
using Api_BuildTech.Controllers.Organisation;
using Api_BuildTech.Controllers.Otp;
using Api_BuildTech.Controllers.Subscription;
using Api_BuildTech.Controllers.Users;
using Api_BuildTech.Services.messagerie;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.Registration
{
    public class RegistrationOrchestrator
    {
        private readonly string _connectionString;
        private readonly ILogger<RegistrationOrchestrator> _logger;
        private readonly OrganisationService _organisationService;
        private readonly EntrepriseService _entrepriseService;
        private readonly UsersService _usersService;
        private readonly SubscriptionService _subscriptionService;
        private readonly SmtpEmailService _smtpEmailService;
        private readonly IConfiguration _configuration;
        private readonly OtpService _otpService;
        public RegistrationOrchestrator(
            string connectionString,
            ILogger<RegistrationOrchestrator> logger,
            OrganisationService organisationService,
            EntrepriseService entrepriseService,
            UsersService usersService,
            SubscriptionService subscriptionService,
            SmtpEmailService smtpEmailService,
            IConfiguration configuration,
            OtpService otpService)
        {
            _connectionString = connectionString;
            _logger = logger;
            _organisationService = organisationService;
            _entrepriseService = entrepriseService;
            _usersService = usersService;
            _subscriptionService = subscriptionService;
            _smtpEmailService = smtpEmailService;
            _configuration = configuration;
            _otpService = otpService;
        }

        /// <summary>
        /// Orchestration complète du registre SaaS
        /// </summary>
        // ==================== Méthodes privées ====================

        private async Task<bool> EmailExisteAsync(string email)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT COUNT(*) FROM Utilisateurs WHERE Email = @Email";
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Email", email);

            int count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }
        public async Task<RegistrationResult> RegisterNewTenantAsync(RegistrationRequest request)
        {
            var result = new RegistrationResult { Success = false };

            // Variables pour rollback
            Guid? organisationId = null;
            Guid? entrepriseId = null;
            Guid? userId = null;
            Guid? subscriptionId = null;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                _logger.LogInformation($"🚀 Début inscription: {request.Email}");

                // . Vérifier si l'email existe déjà
                if (await EmailExisteAsync(request.Email))
                {
                    return new RegistrationResult
                    {
                        Success = false,
                        Message = "Cet email est déjà utilisé"
                    };
                }


                // ✅ OTP validé ! Continuer l'inscription normale
                _logger.LogInformation($"✅ OTP validé pour {request.Email}");
                // ========================================
                // ÉTAPE 1: Validation du Plan
                // ========================================
                var plan = await ValidatePlanAsync(request.IdPlan, conn, transaction);
                if (plan == null)
                {
                    result.Message = "Plan d'abonnement invalide";
                    return result;
                }

                _logger.LogInformation($"✅ Plan validé: {plan.Name}");

                // ========================================
                // ÉTAPE 2: Création ou Récupération Organisation
                // ========================================
                OrganisationDto organisation;

                if (request.IdOrganisation.HasValue && request.IdOrganisation != Guid.Empty)
                {
                    // Organisation existante
                    organisation = await GetOrganisationAsync(request.IdOrganisation.Value, conn, transaction);
                    if (organisation == null)
                    {
                        result.Message = "Organisation introuvable";
                        return result;
                    }

                    _logger.LogInformation($"📁 Organisation existante: {organisation.Designation}");
                }
                else
                {
                    // Nouvelle organisation
                    organisation = await CreateOrganisationAsync(request, conn, transaction);
                    if (organisation == null)
                    {
                        result.Message = "Erreur création organisation";
                        return result;
                    }

                    organisationId = organisation.Id;
                    _logger.LogInformation($"✅ Organisation créée: {organisation.Id}");
                }

                // ========================================
                // ÉTAPE 3: Création Entreprise
                // ========================================
                var entreprise = await CreateEntrepriseAsync(request, organisation.Id, conn, transaction);
                if (entreprise == null)
                {
                    result.Message = "Erreur création entreprise";
                    return result;
                }

                entrepriseId = entreprise.Id;
                _logger.LogInformation($"✅ Entreprise créée: {entreprise.Id} - {entreprise.Designation}");

                // ========================================
                // ÉTAPE 4: Création Souscription
                // ========================================
                var subscription = await CreateSubscriptionAsync(
                    entreprise.Id,
                    plan.Id,
                    request.SubscriptionDurationMonths,
                    conn,
                    transaction);

                if (subscription == null)
                {
                    result.Message = "Erreur création souscription";
                    return result;
                }

                subscriptionId = subscription.Id;
                _logger.LogInformation($"✅ Souscription créée: {subscription.Id}");

                // ========================================
                // ÉTAPE 5: Mise à jour Entreprise avec info souscription
                // ========================================
                await UpdateEntrepriseSubscriptionAsync(
                    entreprise.Id,
                    subscription.Status,
                    subscription.EndDate,
                    conn,
                    transaction);

                // ========================================
                // ÉTAPE 6: Création Utilisateur Owner
                // ========================================
                var owner = await CreateOwnerUserAsync(request, entreprise.Id, conn, transaction);
                if (owner == null)
                {
                    result.Message = "Erreur création utilisateur Owner";
                    return result;
                }

                userId = owner.Id;
                _logger.LogInformation($"✅ Owner créé: {owner.Id} - {owner.Email}");

                // ========================================
                // ÉTAPE 7: Génération Token de Validation
                // ========================================
                var validationToken = await GenerateValidationTokenAsync(
                    entreprise.Id,
                    subscription.EndDate,
                    conn,
                    transaction);

                _logger.LogInformation($"✅ Token de validation généré");

                // ========================================
                // ÉTAPE 8: Création Configuration Initiale (optionnel)
                // ========================================
                await CreateInitialConfigurationAsync(entreprise.Id, conn, transaction);

                // ========================================
                // COMMIT TRANSACTION
                // ========================================
                await transaction.CommitAsync();

                // Générer et envoyer OTP pour confirmer l'email
                var otpResult = await _otpService.GenerateAndSendOtpAsync(
                    request.Email,
                    "REGISTRATION"
                );

                result.Message += " Un code de vérification a été envoyé à votre email.";
                _logger.LogInformation($"🎉 Inscription complétée avec succès: {entreprise.CodeEntreprise}");

                // Construire la réponse
                result.Success = true;
                result.Message = "Inscription réussie";
                result.OrganisationId = organisation.Id;
                result.EntrepriseId = entreprise.Id;
                result.UserId = owner.Id;
                result.SubscriptionId = subscription.Id;
                result.ValidationToken = validationToken;
                result.CodeEntreprise = entreprise.CodeEntreprise;
                result.SubscriptionExpiresAt = subscription.EndDate;
                result.PlanName = plan.Name;

                return result;
            }
            catch (Exception ex)
            {
                // ROLLBACK en cas d'erreur
                await SupprimerUtilisateurAsync(userId.Value);
                await transaction.RollbackAsync();

                _logger.LogError(ex, $"❌ Erreur inscription: {request.Email}");

                result.Message = $"Erreur lors de l'inscription: {ex.Message}";
                result.Success = false;

                return result;
            }
        }

        // ========================================
        // MÉTHODES PRIVÉES D'ORCHESTRATION
        // ========================================

        private async Task<Plan?> ValidatePlanAsync(
            Guid idPlan,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            using var cmd = new SqlCommand(@"
                SELECT Id, Name, Description, Price, MaxUsers, MaxInvoicesPerMonth, 
                       MaxStorageMB, IsActive, CreatedAt
                FROM PLANS
                WHERE Id = @IdPlan AND IsActive = 1", conn, transaction);

            cmd.Parameters.AddWithValue("@IdPlan", idPlan);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Plan
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
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

        private async Task<OrganisationDto?> GetOrganisationAsync(
            Guid id,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            using var cmd = new SqlCommand(@"
                SELECT Id, Identifiant, Designation, Etat, EstActif, CreatedDate
                FROM ORGANISATION
                WHERE Id = @Id AND EstActif = 1", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new OrganisationDto
                {
                    Id = reader.GetGuid(0),
                    Identifiant = reader.GetString(1),
                    Designation = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Etat = reader.IsDBNull(3) ? null : reader.GetString(3),
                    EstActif = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                    CreatedDate = reader.GetDateTime(5)
                };
            }

            return null;
        }

        private async Task<OrganisationDto?> CreateOrganisationAsync(
            RegistrationRequest request,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            var newId = Guid.NewGuid();
            var identifiant = GenerateOrganisationIdentifiant();

            using var cmd = new SqlCommand(@"
                INSERT INTO ORGANISATION (Id, Identifiant, Designation, Etat, EstActif, CreatedDate)
                VALUES (@Id, @Identifiant, @Designation, 'Actif', 1, @CreatedDate)",
                conn, transaction);

            cmd.Parameters.AddWithValue("@Id", newId);
            cmd.Parameters.AddWithValue("@Identifiant", identifiant);
            cmd.Parameters.AddWithValue("@Designation", request.OrganisationName ?? "Org" + request.EntrepriseName);
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();

            return await GetOrganisationAsync(newId, conn, transaction);
        }

        private async Task<EntrepriseDto?> CreateEntrepriseAsync(
            RegistrationRequest request,
            Guid idOrganisation,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            var newId = Guid.NewGuid();
            var codeEntreprise = GenerateCodeEntreprise();

            using var cmd = new SqlCommand(@"
                INSERT INTO ENTREPRISE (
                    Id, Designation,Email, Contact, Localisation, 
                    Pays, Ville, Commune, CodeEntreprise, IdOrganisation,
                    IsActive, Autorisation, SubscriptionStatus, CreatedAt
                )
                VALUES (
                    @Id, @Designation,@Email, @Contact, @Localisation,
                    @Pays, @Ville, @Commune, @CodeEntreprise, @IdOrganisation,
                    0, 0, 'Active', @CreatedAt
                )", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", newId);
            cmd.Parameters.AddWithValue("@Designation", request.EntrepriseName);
            cmd.Parameters.AddWithValue("@Email", request.Email);
            cmd.Parameters.AddWithValue("@Contact", (object?)request.Contact ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Localisation", (object?)request.Localisation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pays", (object?)request.Pays ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", (object?)request.Ville ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Commune", (object?)request.Commune ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CodeEntreprise", codeEntreprise);
            cmd.Parameters.AddWithValue("@IdOrganisation", idOrganisation);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();

            return await GetEntrepriseAsync(newId, conn, transaction);
        }

        private async Task<EntrepriseDto?> GetEntrepriseAsync(
            Guid id,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            using var cmd = new SqlCommand(@"
                SELECT Id, Designation, Localisation, Contact, Email, 
                       Pays, Ville, Commune, NRC, Autorisation, CodeEntreprise, 
                       IsActive, SubscriptionStatus, SubscriptionEndsAt, CreatedAt, 
                       UpdatedAt, IdOrganisation
                FROM ENTREPRISE
                WHERE Id = @Id", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new EntrepriseDto
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    Designation = reader.IsDBNull("Designation") ? null : reader.GetString("Designation"),
                    Localisation = reader.IsDBNull("Localisation") ? null : reader.GetString("Localisation"),
                    Contact = reader.IsDBNull("Contact") ? null : reader.GetString("Contact"),
                    Email = reader.IsDBNull("Email") ? null : reader.GetString("Email"),
                    Pays = reader.IsDBNull("Pays") ? null : reader.GetString("Pays"),
                    Ville = reader.IsDBNull("Ville") ? null : reader.GetString("Ville"),
                    Commune = reader.IsDBNull("Commune") ? null : reader.GetString("Commune"),
                    NRC = reader.IsDBNull("NRC") ? null : reader.GetString("NRC"),

                    Autorisation = reader.GetBoolean(reader.GetOrdinal("Autorisation")),
                    CodeEntreprise = reader.IsDBNull("CodeEntreprise") ? null : reader.GetString("CodeEntreprise"),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),

                    SubscriptionStatus = reader.IsDBNull("SubscriptionStatus")
                     ? null
                     : reader.GetString("SubscriptionStatus"),

                    SubscriptionEndsAt = reader.IsDBNull("SubscriptionEndsAt")
                     ? null
                     : reader.GetDateTime("SubscriptionEndsAt"),

                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    UpdatedAt = reader.IsDBNull("UpdatedAt")
                     ? null
                     : reader.GetDateTime("UpdatedAt"),

                    IdOrganisation = reader.IsDBNull("IdOrganisation")
                     ? null
                     : reader.GetGuid("IdOrganisation")
                };

            }

            return null;
        }

        private async Task<Subscription.Subscription> CreateSubscriptionAsync(
            Guid idEntreprise,
            Guid idPlan,
            int durationMonths,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            var newId = Guid.NewGuid();
            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddMonths(durationMonths);

            using var cmd = new SqlCommand(@"
                INSERT INTO SUBSCRIPTIONS (
                    Id, IdEntreprise, IdPlan, Status, StartDate, EndDate, 
                    LastValidated, CreatedAt
                )
                VALUES (
                    @Id, @IdEntreprise, @IdPlan, 'Active', @StartDate, @EndDate,
                    1, @CreatedAt
                )", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", newId);
            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
            cmd.Parameters.AddWithValue("@IdPlan", idPlan);
            cmd.Parameters.AddWithValue("@StartDate", startDate);
            cmd.Parameters.AddWithValue("@EndDate", endDate);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();

            return new Subscription.Subscription
            {
                Id = newId,
                IdEntreprise = idEntreprise,
                IdPlan = idPlan,
                Status = "Active",
                StartDate = startDate,
                EndDate = endDate,
                LastValidated = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task UpdateEntrepriseSubscriptionAsync(
            Guid idEntreprise,
            string status,
            DateTime endsAt,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            using var cmd = new SqlCommand(@"
                UPDATE ENTREPRISE
                SET SubscriptionStatus = @Status,
                    SubscriptionEndsAt = @EndsAt,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", idEntreprise);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@EndsAt", endsAt);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<UserDto?> CreateOwnerUserAsync(
            RegistrationRequest request,
            Guid idEntreprise,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            var newId = Guid.NewGuid();

            // Hash du mot de passe (À IMPLÉMENTER avec BCrypt)
            var hashedPassword = HashPassword(request.Password);

            using var cmd = new SqlCommand(@"
                INSERT INTO UTILISATEURS (
                    Id, Email, MotDePasse, Nom, Prenom, IdEntreprise, IdRole,
                    IsSuperAdmin, IsActive, CreatedAt
                )
                VALUES (
                    @Id, @Email, @MotDePasse, @Nom, @Prenom, @IdEntreprise, @IdRole,
                    0, 1, @CreatedAt
                )", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", newId);
            cmd.Parameters.AddWithValue("@Email", request.Email);
            cmd.Parameters.AddWithValue("@MotDePasse", hashedPassword);
            cmd.Parameters.AddWithValue("@Nom", (object?)request.Nom ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prenom", (object?)request.Prenom ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
            cmd.Parameters.AddWithValue("@IdRole", new Guid("e29c901f-55b3-4904-b4db-a08ffbcdf23c")); // Owner Role
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();

            return new UserDto
            {
                Id = newId,
                Email = request.Email,
                Nom = request.Nom,
                Prenom = request.Prenom,
                IdEntreprise = idEntreprise,
                IdRole = new Guid("e29c901f-55b3-4904-b4db-a08ffbcdf23c"),
                RoleName = "Owner",
                IsSuperAdmin = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task<string> GenerateValidationTokenAsync(
            Guid idEntreprise,
            DateTime expiresAt,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            var token = GenerateSecureToken();
            var tokenId = Guid.NewGuid();

            using var cmd = new SqlCommand(@"
                INSERT INTO SUBSCRIPTION_VALIDATIONS (
                    Id, IdEntreprise, ValidationToken, CreatedAt, ExpiresAt, IsRevoked
                )
                VALUES (
                    @Id, @IdEntreprise, @ValidationToken, @CreatedAt, @ExpiresAt, 0
                )", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", tokenId);
            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
            cmd.Parameters.AddWithValue("@ValidationToken", token);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            await cmd.ExecuteNonQueryAsync();

            return token;
        }

        private async Task CreateInitialConfigurationAsync(
            Guid idEntreprise,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            // Créer des configurations par défaut si nécessaire
            // Par exemple: paramètres POS, types de paiement par défaut, etc.

            _logger.LogInformation($"Configuration initiale créée pour: {idEntreprise}");
        }

        // ========================================
        // UTILITAIRES
        // ========================================

        private string GenerateOrganisationIdentifiant()
        {
            const string prefix = "ORG";
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomPart = GenerateRandomString(4);

            return $"{prefix}-{datePart}-{randomPart}";
        }

        private string GenerateCodeEntreprise()
        {
            return $"ENT{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        private string GenerateSecureToken()
        {
            // Générer un token sécurisé (JWT ou token aléatoire)
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                   Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }

        private string HashPassword(string password)
        {
            // TODO: Implémenter BCrypt.Net
            // return BCrypt.Net.BCrypt.HashPassword(password);
            return password; // TEMPORAIRE - NE PAS UTILISER EN PRODUCTION
        }
        private static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            return new string(
                Enumerable.Repeat(chars, length)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray()
            );
        }

        private async Task SupprimerUtilisateurAsync(Guid utilisateurId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM Utilisateurs WHERE Id = @Id";
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", utilisateurId);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}