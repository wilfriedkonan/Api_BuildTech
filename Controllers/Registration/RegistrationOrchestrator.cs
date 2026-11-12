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

        /// <summary>Id du rôle "Owner" dans ROLES (le même que celui utilisé à l'origine pour le propriétaire).</summary>
        private static readonly Guid OwnerRoleId = new Guid("e29c901f-55b3-4904-b4db-a08ffbcdf23c");
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
            // Les comptes web (COMPTES_WEB) sont les seuls comptes de connexion : l'unicité de l'email se vérifie ici.
            // UTILISATEURS (comptes de caisse) n'est plus concerné.
            return !await EmailWebDisponibleAsync(email);
        }

        public async Task<RegistrationResult> RegisterNewTenantAsync(RegistrationRequest request)
        {
            var result = new RegistrationResult { Success = false };
            var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

            var erreur = ValiderEmailEtMotDePasse(email, request.Password);
            if (erreur != null) { result.Message = erreur; result.ErrorCode = "DONNEES_INVALIDES"; return result; }

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            // Après le commit, plus de rollback possible : on ne doit pas traiter une erreur d'OTP comme un échec d'inscription
            var commite = false;

            try
            {
                _logger.LogInformation($"🚀 Début inscription: {email}");

                // Email déjà utilisé par un compte web ?
                if (await EmailExisteAsync(email))
                {
                    result.ErrorCode = "EMAIL_UTILISE";
                    result.Message = "Cet email est déjà utilisé";
                    return result;
                }

                // ÉTAPE 1: Validation du Plan
                var plan = await ValidatePlanAsync(request.IdPlan, conn, transaction);
                if (plan == null)
                {
                    result.ErrorCode = "PLAN_INVALIDE";
                    result.Message = "Plan d'abonnement invalide";
                    await SafeRollbackAsync(transaction);
                    return result;
                }
                _logger.LogInformation($"✅ Plan validé: {plan.Name}");

                // ÉTAPE 2: Organisation existante (réservé aux super-admins, contrôlé par le contrôleur) ou nouvelle
                OrganisationDto organisation;
                if (request.IdOrganisation.HasValue && request.IdOrganisation != Guid.Empty)
                {
                    var existante = await GetOrganisationAsync(request.IdOrganisation.Value, conn, transaction);
                    if (existante == null)
                    {
                        result.ErrorCode = "ORGANISATION_INTROUVABLE";
                        result.Message = "Organisation introuvable";
                        await SafeRollbackAsync(transaction);
                        return result;
                    }
                    organisation = existante;
                    _logger.LogInformation($"📁 Organisation existante: {organisation.Designation}");
                }
                else
                {
                    var creee = await CreateOrganisationAsync(request, conn, transaction);
                    if (creee == null) throw new InvalidOperationException("Erreur création organisation");
                    organisation = creee;
                    _logger.LogInformation($"✅ Organisation créée: {organisation.Id}");
                }

                // ÉTAPE 3: Entreprise
                var entreprise = await CreateEntrepriseAsync(request, organisation.Id, conn, transaction);
                if (entreprise == null) throw new InvalidOperationException("Erreur création entreprise");
                _logger.LogInformation($"✅ Entreprise créée: {entreprise.Id} - {entreprise.Designation}");

                // ÉTAPE 4: Souscription
                var subscription = await CreateSubscriptionAsync(
                    entreprise.Id, plan.Id, request.SubscriptionDurationMonths, conn, transaction);
                if (subscription == null) throw new InvalidOperationException("Erreur création souscription");
                _logger.LogInformation($"✅ Souscription créée: {subscription.Id}");

                // ÉTAPE 5: Informations de souscription sur l'entreprise
                await UpdateEntrepriseSubscriptionAsync(entreprise.Id, subscription.Status, subscription.EndDate, conn, transaction);

                // ÉTAPE 6: Compte web PROPRIÉTAIRE (COMPTES_WEB, mot de passe haché BCrypt)
                //          Une organisation n'a qu'un propriétaire actif : on refuse d'en ajouter un second.
                if (await OrganisationADejaUnProprietaireAsync(organisation.Id, conn, transaction))
                {
                    result.ErrorCode = "PROPRIETAIRE_EXISTANT";
                    result.Message = "Un compte propriétaire existe déjà pour cette organisation.";
                    await SafeRollbackAsync(transaction);
                    return result;
                }

                var idCompte = await CreateOwnerAccountAsync(request, email, organisation.Id, conn, transaction);
                _logger.LogInformation($"✅ Compte web propriétaire créé: {idCompte} - {email}");

                // ÉTAPE 7: Token de validation
                var validationToken = await GenerateValidationTokenAsync(entreprise.Id, subscription.EndDate, conn, transaction);

                // ÉTAPE 8: Configuration initiale (optionnel)
                await CreateInitialConfigurationAsync(entreprise.Id, conn, transaction);

                await transaction.CommitAsync();
                commite = true;

                result.Success = true;
                result.Message = "Inscription réussie";
                result.OrganisationId = organisation.Id;
                result.EntrepriseId = entreprise.Id;
                result.UserId = idCompte;
                result.SubscriptionId = subscription.Id;
                result.ValidationToken = validationToken;
                result.CodeEntreprise = entreprise.CodeEntreprise;
                result.SubscriptionExpiresAt = subscription.EndDate;
                result.PlanName = plan.Name;
                result.EntrepriseCreee = true;
                result.OrganisationCreee = !(request.IdOrganisation.HasValue && request.IdOrganisation != Guid.Empty);

                // Code de vérification par WhatsApp : son échec ne doit pas annuler une inscription déjà enregistrée
                try
                {
                    await _otpService.GenerateAndSendOtpAsyncByWha(
                        email, "REGISTRATION", request.Telephone, request.Nom, request.EntrepriseName, plan.Price);
                    result.Message = "Inscription réussie. Un code de vérification vous a été envoyé.";
                }
                catch (Exception exOtp)
                {
                    _logger.LogError(exOtp, $"⚠️ Inscription enregistrée mais envoi du code impossible: {email}");
                    result.Message = "Inscription réussie, mais l'envoi du code de vérification a échoué. Contactez le support.";
                }

                _logger.LogInformation($"🎉 Inscription complétée avec succès: {entreprise.CodeEntreprise}");
                return result;
            }
            catch (SqlException ex) when (!commite && (ex.Number == 2601 || ex.Number == 2627))
            {
                await SafeRollbackAsync(transaction);
                result.ErrorCode = "EMAIL_UTILISE";
                result.Message = "Cet email, ou le code entreprise généré, est déjà utilisé. Réessayez.";
                return result;
            }
            catch (Exception ex)
            {
                if (!commite) await SafeRollbackAsync(transaction);   // le compte web est dans la transaction : rien à supprimer à la main
                _logger.LogError(ex, $"❌ Erreur inscription: {email}");

                if (commite) return result;   // ne devrait pas arriver : le commit est suivi de code protégé
                result.Success = false;
                result.ErrorCode = "ERREUR";
                result.Message = $"Erreur lors de l'inscription: {ex.Message}";
                return result;
            }
        }

        private static async Task<bool> OrganisationADejaUnProprietaireAsync(
            Guid idOrganisation, SqlConnection conn, SqlTransaction transaction)
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM COMPTES_WEB WITH (UPDLOCK, HOLDLOCK)
                WHERE IdOrganisation = @Org AND Role = N'Proprietaire' AND EstActif = 1", conn, transaction);
            cmd.Parameters.AddWithValue("@Org", idOrganisation);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        private static async Task<Guid> CreateOwnerAccountAsync(
            RegistrationRequest request, string email, Guid idOrganisation,
            SqlConnection conn, SqlTransaction transaction)
        {
            var id = Guid.NewGuid();
            var hash = BCrypt.Net.BCrypt.HashPassword(request.Password!, 12);

            using var cmd = new SqlCommand(@"
                INSERT INTO COMPTES_WEB (Id, IdOrganisation, Email, MotPasseHash, Nom, Prenom, Role, IdRole, EstActif, CreatedDate)
                VALUES (@Id, @Org, @Email, @Hash, @Nom, @Prenom, N'Proprietaire', @IdRole, 1, @CreatedDate)", conn, transaction);
            cmd.Parameters.AddWithValue("@IdRole", OwnerRoleId);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Org", idOrganisation);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Hash", hash);
            cmd.Parameters.AddWithValue("@Nom", (object?)request.Nom?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prenom", (object?)request.Prenom?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
            return id;
        }


        // ========================================
        // ENTREPRISE EXISTANTE : création du compte web (COMPTES_WEB)
        // ========================================

        /// <summary>
        /// Crée le compte web PROPRIÉTAIRE d'une entreprise identifiée par son Id.
        ///  - Entreprise déjà sur la plateforme : on rattache le compte à son ORGANISATION (générée si elle n'en a pas).
        ///  - Entreprise absente (ex. seulement sur la caisse locale) : elle est CRÉÉE avec cet Id, avec une organisation
        ///    générée automatiquement (ou celle indiquée) et, si un plan + une durée sont fournis, sa souscription.
        /// COMPTES_WEB est un compte d'organisation (voit toutes les entreprises de l'organisation). N'écrit rien dans
        /// UTILISATEURS (comptes de caisse). Refuse si l'organisation a déjà un propriétaire actif.
        /// </summary>
        public async Task<RegistrationResult> RegisterForExistingEntrepriseAsync(RegistrationRequest request)
        {
            var result = new RegistrationResult { Success = false };
            var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

            var erreur = ValiderEmailEtMotDePasse(email, request.Password);
            if (erreur != null) { result.Message = erreur; result.ErrorCode = "DONNEES_INVALIDES"; return result; }

            Guid idEntreprise = request.IdEntreprise ?? Guid.Empty;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1) L'entreprise existe-t-elle déjà sur la plateforme ?
                string? designation = null, codeEntreprise = null, etat = null;
                Guid? idOrganisation = null;
                DateTime? finAbonnement = null;
                bool trouvee = false;

                using (var cmd = new SqlCommand(@"
                    SELECT Designation, CodeEntreprise, Etat, IdOrganisation, SubscriptionEndsAt
                    FROM ENTREPRISE
                    WHERE Id = @Id", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", idEntreprise);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        trouvee = true;
                        designation = reader.IsDBNull(0) ? null : reader.GetString(0);
                        codeEntreprise = reader.IsDBNull(1) ? null : reader.GetString(1);
                        etat = reader.IsDBNull(2) ? null : reader.GetString(2);
                        idOrganisation = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
                        finAbonnement = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
                    }
                }

                if (trouvee && string.Equals(etat, "Supprimer", StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorCode = "ENTREPRISE_SUPPRIMEE";
                    result.Message = "Cette entreprise a été supprimée de la plateforme.";
                    await transaction.RollbackAsync();
                    return result;
                }

                // 2) Email unique sur toute la plateforme (vérifié tôt : rien à créer si l'email est pris)
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM COMPTES_WEB WHERE Email = @Email", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0)
                    {
                        result.ErrorCode = "EMAIL_UTILISE";
                        result.Message = "Cet email est déjà utilisé";
                        await transaction.RollbackAsync();
                        return result;
                    }
                }

                // 3) Entreprise absente : on la CRÉE avec CET Id (même Id que sur la caisse locale), comme une nouvelle inscription
                bool entrepriseCreee = false, organisationCreee = false;
                Guid? idSouscription = null;
                string? planName = null;
                string? validationToken = null;

                if (!trouvee)
                {
                    if (string.IsNullOrWhiteSpace(request.EntrepriseName))
                    {
                        result.ErrorCode = "DONNEES_INVALIDES";
                        result.Message = "Nom d'entreprise requis : cette entreprise n'existe pas encore sur la plateforme et va être créée.";
                        await transaction.RollbackAsync();
                        return result;
                    }

                    // Plan facultatif : souscription créée seulement si un plan ET une durée sont fournis
                    Plan? plan = null;
                    bool avecSouscription = request.IdPlan != Guid.Empty && request.SubscriptionDurationMonths > 0;
                    if (avecSouscription)
                    {
                        plan = await ValidatePlanAsync(request.IdPlan, conn, transaction);
                        if (plan == null)
                        {
                            result.ErrorCode = "PLAN_INVALIDE";
                            result.Message = "Plan d'abonnement invalide";
                            await transaction.RollbackAsync();
                            return result;
                        }
                    }

                    // Organisation : celle indiquée, sinon générée automatiquement (comme pour un nouveau compte)
                    if (request.IdOrganisation.HasValue && request.IdOrganisation.Value != Guid.Empty)
                    {
                        var org = await GetOrganisationAsync(request.IdOrganisation.Value, conn, transaction);
                        if (org == null)
                        {
                            result.ErrorCode = "ORGANISATION_INTROUVABLE";
                            result.Message = "Organisation introuvable";
                            await transaction.RollbackAsync();
                            return result;
                        }
                        idOrganisation = org.Id;
                    }
                    else
                    {
                        var org = await CreateOrganisationNommeeAsync(request.OrganisationName, request.EntrepriseName, conn, transaction);
                        if (org == null) throw new InvalidOperationException("Erreur création organisation");
                        idOrganisation = org.Id;
                        organisationCreee = true;
                    }

                    var entreprise = await CreateEntrepriseAsync(request, idOrganisation.Value, conn, transaction, idEntreprise, true);
                    if (entreprise == null) throw new InvalidOperationException("Erreur création entreprise");
                    entrepriseCreee = true;
                    designation = entreprise.Designation;
                    codeEntreprise = entreprise.CodeEntreprise;

                    if (plan != null)
                    {
                        var subscription = await CreateSubscriptionAsync(idEntreprise, plan.Id, request.SubscriptionDurationMonths, conn, transaction);
                        idSouscription = subscription.Id;
                        planName = plan.Name;
                        finAbonnement = subscription.EndDate;
                        await UpdateEntrepriseSubscriptionAsync(idEntreprise, subscription.Status, subscription.EndDate, conn, transaction);
                        validationToken = await GenerateValidationTokenAsync(idEntreprise, subscription.EndDate, conn, transaction);
                    }
                }
                else if (!idOrganisation.HasValue || idOrganisation.Value == Guid.Empty)
                {
                    // Entreprise présente mais sans organisation : on lui en génère une et on la rattache
                    var org = await CreateOrganisationNommeeAsync(request.OrganisationName, designation ?? request.EntrepriseName, conn, transaction);
                    if (org == null) throw new InvalidOperationException("Erreur création organisation");
                    idOrganisation = org.Id;
                    organisationCreee = true;

                    using var cmd = new SqlCommand(@"
                        UPDATE ENTREPRISE SET IdOrganisation = @Org, IsDefault = 1, UpdatedAt = @Now WHERE Id = @Id", conn, transaction);
                    cmd.Parameters.AddWithValue("@Org", idOrganisation.Value);
                    cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@Id", idEntreprise);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 4) L'organisation ne doit pas avoir déjà un propriétaire actif (verrou : pas de course entre deux demandes)
                int proprietaires;
                using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM COMPTES_WEB WITH (UPDLOCK, HOLDLOCK)
                    WHERE IdOrganisation = @Org AND Role = N'Proprietaire' AND EstActif = 1", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Org", idOrganisation!.Value);
                    proprietaires = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
                if (proprietaires > 0)
                {
                    result.ErrorCode = "PROPRIETAIRE_EXISTANT";
                    result.Message = "Un compte propriétaire existe déjà pour l'organisation de cette entreprise. " +
                                     "Demandez au propriétaire de créer votre compte.";
                    await transaction.RollbackAsync();   // annule aussi l'entreprise / l'organisation créées plus haut
                    return result;
                }

                // 5) Création du compte (mot de passe HACHÉ, jamais stocké en clair)
                var idCompte = Guid.NewGuid();
                var hash = BCrypt.Net.BCrypt.HashPassword(request.Password!, 12);

                using (var cmd = new SqlCommand(@"
                    INSERT INTO COMPTES_WEB (Id, IdOrganisation, Email, MotPasseHash, Nom, Prenom, Role, IdRole, EstActif, CreatedDate)
                    VALUES (@Id, @Org, @Email, @Hash, @Nom, @Prenom, N'Proprietaire', @IdRole, 1, @CreatedDate)", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@IdRole", OwnerRoleId);
                    cmd.Parameters.AddWithValue("@Id", idCompte);
                    cmd.Parameters.AddWithValue("@Org", idOrganisation.Value);
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Hash", hash);
                    cmd.Parameters.AddWithValue("@Nom", (object?)request.Nom?.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Prenom", (object?)request.Prenom?.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedDate", DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 6) Plan courant (entreprise déjà existante) : information de retour, facultatif
                if (!entrepriseCreee)
                {
                    using var cmd = new SqlCommand(@"
                        SELECT TOP 1 p.Name, s.EndDate
                        FROM SUBSCRIPTIONS s JOIN PLANS p ON p.Id = s.IdPlan
                        WHERE s.IdEntreprise = @Id
                        ORDER BY s.EndDate DESC", conn, transaction);
                    cmd.Parameters.AddWithValue("@Id", idEntreprise);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        planName = reader.IsDBNull(0) ? null : reader.GetString(0);
                        if (!finAbonnement.HasValue && !reader.IsDBNull(1)) finAbonnement = reader.GetDateTime(1);
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"✅ Compte web {idCompte} créé pour l'entreprise {idEntreprise} ({designation}) — entreprise créée: {entrepriseCreee}, organisation créée: {organisationCreee}");

                result.Success = true;
                result.Message = entrepriseCreee ? "Entreprise et compte créés" : "Compte créé";
                result.OrganisationId = idOrganisation;
                result.EntrepriseId = idEntreprise;
                result.UserId = idCompte;
                result.SubscriptionId = idSouscription;
                result.ValidationToken = validationToken;
                result.CodeEntreprise = codeEntreprise;
                result.PlanName = planName;
                result.SubscriptionExpiresAt = finAbonnement;
                result.EntrepriseCreee = entrepriseCreee;
                result.OrganisationCreee = organisationCreee;
                return result;
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                await SafeRollbackAsync(transaction);
                result.ErrorCode = "EMAIL_UTILISE";
                result.Message = "Cet email, ou le code entreprise généré, est déjà utilisé. Réessayez.";
                return result;
            }
            catch (Exception ex)
            {
                await SafeRollbackAsync(transaction);
                _logger.LogError(ex, $"❌ Erreur création compte entreprise: {idEntreprise}");
                result.ErrorCode = "ERREUR";
                result.Message = $"Erreur lors de la création du compte: {ex.Message}";
                return result;
            }
        }

        /// <summary>Crée une organisation avec un nom explicite (défaut : "Org" + nom d'entreprise, comme l'inscription classique).</summary>
        private async Task<OrganisationDto?> CreateOrganisationNommeeAsync(
            string? organisationName,
            string? entrepriseName,
            SqlConnection conn,
            SqlTransaction transaction)
        {
            var newId = Guid.NewGuid();

            using var cmd = new SqlCommand(@"
                INSERT INTO ORGANISATION (Id, Identifiant, Designation, Etat, EstActif, CreatedDate)
                VALUES (@Id, @Identifiant, @Designation, 'Actif', 1, @CreatedDate)", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", newId);
            cmd.Parameters.AddWithValue("@Identifiant", GenerateOrganisationIdentifiant());
            cmd.Parameters.AddWithValue("@Designation",
                string.IsNullOrWhiteSpace(organisationName) ? "Org" + entrepriseName : organisationName.Trim());
            cmd.Parameters.AddWithValue("@CreatedDate", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();

            return await GetOrganisationAsync(newId, conn, transaction);
        }

        /// <summary>Vrai si aucun compte web n'utilise déjà cet email.</summary>
        public async Task<bool> EmailWebDisponibleAsync(string email)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM COMPTES_WEB WHERE Email = @Email", connection);
            cmd.Parameters.AddWithValue("@Email", (email ?? string.Empty).Trim().ToLowerInvariant());
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0;
        }

        private static string? ValiderEmailEtMotDePasse(string email, string? motDePasse)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !email.Contains('@') ||
                email.StartsWith("@") || email.EndsWith("@") || email.Contains(' ') || !email.Substring(email.IndexOf('@')).Contains('.'))
                return "Email invalide";
            if (string.IsNullOrEmpty(motDePasse) || motDePasse.Length < 10 ||
                !motDePasse.Any(char.IsLetter) || !motDePasse.Any(char.IsDigit))
                return "Mot de passe : 10 caractères minimum, au moins une lettre et un chiffre";
            return null;
        }

        private static async Task SafeRollbackAsync(SqlTransaction transaction)
        {
            try { await transaction.RollbackAsync(); } catch (Exception) { /* transaction déjà terminée */ }
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
            SqlTransaction transaction,
            Guid? idImpose = null,
            bool estActive = false)
        {
            var newId = idImpose ?? Guid.NewGuid();
            var codeEntreprise = GenerateCodeEntreprise();

            // Entreprise par défaut de l'organisation (celle ouverte à la connexion) : Default = 1 à l'inscription.
            // Une seule par organisation (index unique filtré) : si l'organisation en a déjà une, la nouvelle ne la remplace pas.
            bool estParDefaut;
            using (var check = new SqlCommand(
                "SELECT COUNT(*) FROM ENTREPRISE WHERE IdOrganisation = @Org AND IsDefault = 1", conn, transaction))
            {
                check.Parameters.AddWithValue("@Org", idOrganisation);
                estParDefaut = Convert.ToInt32(await check.ExecuteScalarAsync()) == 0;
            }

            using var cmd = new SqlCommand(@"
                INSERT INTO ENTREPRISE (
                    Id, Designation,Email, Contact, Localisation, 
                    Pays, Ville, Commune, CodeEntreprise, IdOrganisation,
                    IsActive, IsDefault, Autorisation, SubscriptionStatus, CreatedAt
                )
                VALUES (
                    @Id, @Designation,@Email, @Contact, @Localisation,
                    @Pays, @Ville, @Commune, @CodeEntreprise, @IdOrganisation,
                    @EstActive, @IsDefault, 0, 'Active', @CreatedAt
                )", conn, transaction);

            cmd.Parameters.AddWithValue("@EstActive", estActive);
            cmd.Parameters.AddWithValue("@IsDefault", estParDefaut);

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
    }
}