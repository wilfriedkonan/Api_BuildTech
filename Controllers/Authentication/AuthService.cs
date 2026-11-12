using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Authentication
{
    /// <summary>
    /// Authentification des COMPTES WEB (table COMPTES_WEB, comptes d'organisation, mots de passe BCrypt).
    /// La table UTILISATEURS (comptes de caisse) n'est PLUS utilisée ici : elle ne sert qu'à la synchronisation.
    /// Les jetons (JWT + refresh) sont fabriqués par TokenService ; ce service ne fait que vérifier l'identité.
    /// </summary>
    public class AuthService : DatabaseService
    {
        private const int MaxEchecs = 5;           // échecs consécutifs avant verrouillage
        private const int VerrouMinutes = 15;      // durée du verrouillage

        // Hash factice : on vérifie toujours un mot de passe, même quand l'email n'existe pas,
        // pour que le temps de réponse ne révèle pas l'existence d'un compte.
        private static readonly Lazy<string> DummyHash =
            new(() => BCrypt.Net.BCrypt.HashPassword("compte-inexistant-" + Guid.NewGuid(), 12));

        private const string MessageIdentifiantsInvalides = "Email ou mot de passe incorrect";

        private readonly IConfiguration _configuration;

        public AuthService(
            string connectionString,
            ILogger<AuthService> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
            : base(connectionString, logger, httpContextAccessor)
        {
            _configuration = configuration;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

                // 1. Compte web
                var compte = await GetCompteByEmailAsync(email);

                if (compte == null)
                {
                    VerifyPassword(request.Password, DummyHash.Value); // égalise le temps de réponse
                    return Echec(MessageIdentifiantsInvalides);
                }

                // 2. Verrouillage temporaire (trop d'échecs)
                if (compte.VerrouJusqua.HasValue && compte.VerrouJusqua.Value > DateTime.UtcNow)
                {
                    _logger.LogWarning("Login refusé (compte verrouillé): {Email}", email);
                    return Echec("Compte temporairement verrouillé après plusieurs échecs. Réessayez dans quelques minutes.");
                }

                // 3. Mot de passe
                if (!VerifyPassword(request.Password, compte.MotPasseHash))
                {
                    await EnregistrerEchecAsync(compte.Id);
                    return Echec(MessageIdentifiantsInvalides);
                }

                // 4. Compte actif (vérifié APRÈS le mot de passe : pas de fuite d'information)
                if (!compte.EstActif)
                {
                    return Echec("Compte désactivé");
                }

                // 5. Entreprises accessibles de l'organisation (actives + abonnement valide)
                //    Le compte doit appartenir à une organisation qui a AU MOINS UNE entreprise active (et en règle).
                //    Un super-admin (opérateur de la plateforme) est dispensé de cette condition.
                List<EntrepriseSummary> entreprises;
                int actives;
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    entreprises = await EntrepriseAccess.ListValidesAsync(conn, compte.IdOrganisation);
                    actives = entreprises.Count > 0
                        ? entreprises.Count
                        : await EntrepriseAccess.CompterActivesAsync(conn, compte.IdOrganisation);
                }

                if (entreprises.Count == 0 && !compte.IsSuperAdmin)
                {
                    _logger.LogInformation("Login refusé (aucune entreprise active et en règle): {Email}", email);
                    return Echec(actives == 0
                        ? "Aucune entreprise active : votre compte sera activé après validation de votre paiement."
                        : "Souscription expirée : renouvelez l'abonnement pour vous connecter.");
                }

                // 6. Entreprise active : l'entreprise PAR DÉFAUT de l'organisation (IsDefault) si elle est valide,
                //    sinon la première valide ; le front peut en changer (switch-entreprise)
                var active = entreprises.FirstOrDefault();

                await EnregistrerSuccesAsync(compte.Id);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Authentification réussie",
                    User = new UserInfo
                    {
                        Id = compte.Id,
                        Email = compte.Email,
                        Nom = compte.Nom,
                        Prenom = compte.Prenom,
                        OrganisationId = compte.IdOrganisation,
                        IdEntreprise = active?.Id ?? Guid.Empty,
                        NomEntreprise = active?.Designation,
                        IsSuperAdmin = compte.IsSuperAdmin,
                        Role = compte.Role,
                        RoleCaisse = compte.RoleCaisse,
                        IsActive = compte.EstActif,
                        Entreprises = entreprises
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur login: {Email}", request.Email);
                return Echec("Erreur serveur lors de l'authentification");
            }
        }

        /// <summary>Entreprises accessibles de l'organisation (sélecteur du front, contrôle du changement d'entreprise).</summary>
        public async Task<List<EntrepriseSummary>> ListEntreprisesAsync(Guid idOrganisation)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return await EntrepriseAccess.ListValidesAsync(conn, idOrganisation);
        }

        // ====================================================================
        // PRIVÉ
        // ====================================================================

        private static LoginResponse Echec(string message) => new() { Success = false, Message = message };

        private sealed class CompteWeb
        {
            public Guid Id { get; init; }
            public Guid IdOrganisation { get; init; }
            public string Email { get; init; } = string.Empty;
            public string MotPasseHash { get; init; } = string.Empty;
            public string? Nom { get; init; }
            public string? Prenom { get; init; }
            public string? Role { get; init; }
            public string? RoleCaisse { get; init; }
            public bool EstActif { get; init; }
            public bool IsSuperAdmin { get; init; }
            public DateTime? VerrouJusqua { get; init; }
        }

        private async Task<CompteWeb?> GetCompteByEmailAsync(string email)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(@"
                SELECT c.Id, c.IdOrganisation, c.Email, c.MotPasseHash, c.Nom, c.Prenom, c.[Role],
                       c.EstActif, c.IsSuperAdmin, c.VerrouJusqua, r.Name
                FROM COMPTES_WEB c
                LEFT JOIN ROLES r ON r.Id = c.IdRole
                WHERE c.Email = @Email", conn);
            cmd.Parameters.AddWithValue("@Email", email);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new CompteWeb
            {
                Id = reader.GetGuid(0),
                IdOrganisation = reader.GetGuid(1),
                Email = reader.GetString(2),
                MotPasseHash = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Nom = reader.IsDBNull(4) ? null : reader.GetString(4),
                Prenom = reader.IsDBNull(5) ? null : reader.GetString(5),
                Role = reader.IsDBNull(6) ? null : reader.GetString(6),
                EstActif = reader.GetBoolean(7),
                IsSuperAdmin = reader.GetBoolean(8),
                VerrouJusqua = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                RoleCaisse = reader.IsDBNull(10) ? null : reader.GetString(10)
            };
        }

        /// <summary>Compare à un hash BCrypt ($2a$/$2b$, y compris les comptes migrés). Un hash invalide = faux.</summary>
        private static bool VerifyPassword(string? password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Incrémente le compteur d'échecs (atomique) et verrouille le compte au-delà du seuil.</summary>
        private async Task EnregistrerEchecAsync(Guid idCompte)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE COMPTES_WEB
                    SET VerrouJusqua = CASE WHEN ISNULL(EchecsConsecutifs, 0) + 1 >= @Max
                                            THEN DATEADD(MINUTE, @Minutes, GETUTCDATE())
                                            ELSE VerrouJusqua END,
                        EchecsConsecutifs = CASE WHEN ISNULL(EchecsConsecutifs, 0) + 1 >= @Max
                                                 THEN 0
                                                 ELSE ISNULL(EchecsConsecutifs, 0) + 1 END,
                        UpdatedDate = GETUTCDATE()
                    WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Max", MaxEchecs);
                cmd.Parameters.AddWithValue("@Minutes", VerrouMinutes);
                cmd.Parameters.AddWithValue("@Id", idCompte);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur enregistrement échec de connexion: {Id}", idCompte);
            }
        }

        private async Task EnregistrerSuccesAsync(Guid idCompte)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE COMPTES_WEB
                    SET EchecsConsecutifs = 0,
                        VerrouJusqua = NULL,
                        DerniereConnexion = GETUTCDATE(),
                        UpdatedDate = GETUTCDATE()
                    WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", idCompte);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur mise à jour dernière connexion: {Id}", idCompte);
            }
        }
    }
}