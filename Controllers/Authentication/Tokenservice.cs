using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Api_BuildTech.Controllers.Authentication
{
    /// <summary>Données minimales de l'utilisateur nécessaires pour fabriquer un JWT.</summary>
    public record AuthUserSnapshot(Guid Id, string Email, Guid IdOrganisation, Guid IdEntreprise, bool IsSuperAdmin, string? Role, string? RoleCaisse = null);

    public record IssuedRefreshToken(string Token, DateTime ExpiresAt);

    public class RefreshResult
    {
        public bool Success { get; init; }
        /// <summary>Raison technique, pour les logs uniquement (ne jamais l'envoyer au client).</summary>
        public string? Failure { get; init; }
        public AuthUserSnapshot? User { get; init; }
        public IssuedRefreshToken? NewRefreshToken { get; init; }
    }

    /// <summary>
    /// Fabrique les access tokens JWT (mêmes claims que AuthService.GenerateAccessToken)
    /// et gère les refresh tokens : stockage haché, rotation, détection de rejeu, révocation.
    /// </summary>
    public class TokenService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;
        private readonly AuthCookieSettings _settings;
        private readonly ILogger<TokenService> _logger;

        public TokenService(
            IConfiguration config,
            IOptions<AuthCookieSettings> settings,
            ILogger<TokenService> logger)
        {
            _config = config;
            _settings = settings.Value;
            _logger = logger;
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' manquante");
        }

        // ====================================================================
        // ACCESS TOKEN (JWT)
        // ====================================================================

        /// <summary>Rôle web (COMPTES_WEB.Role) -> rôles historiques (ROLES.Name) acceptés par les anciens contrôleurs.</summary>
        private static IEnumerable<string> LegacyRolesFor(string? webRole) => webRole switch
        {
            "Proprietaire" => new[] { "Owner", "Manager" },
            "Administrateur" => new[] { "Manager" },
            "Gestionnaire" => new[] { "Manager" },
            _ => Array.Empty<string>()   // Lecteur : lecture seule ([Authorize] simple)
        };

        public (string Token, DateTime ExpiresAt) CreateAccessToken(AuthUserSnapshot user, TimeSpan lifetime)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("organisation_id", user.IdOrganisation.ToString()),
                new Claim("entreprise_id", user.IdEntreprise.ToString()),
                new Claim("is_super_admin", user.IsSuperAdmin.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Rôle du compte web (Proprietaire / Administrateur / Gestionnaire / Lecteur) en premier,
            // puis "SuperAdmin" pour les administrateurs de la plateforme.
            if (!string.IsNullOrEmpty(user.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role));
            }
            if (user.IsSuperAdmin || user.Role == "SuperAdmin")
            {
                claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
            }

            // COMPATIBILITE : les anciens controleurs (PosController, ArticlesController...) testent les noms de la
            // table ROLES ("Owner", "Manager"...) via [Authorize(Roles = "Owner,Manager,...")]. Le rôle du compte web
            // (Proprietaire...) ne les satisfait pas -> 403. On ajoute donc le rôle historique équivalent.
            // (Proprietaire = IdRole Owner à l'inscription.) Le rôle web reste le PREMIER claim de rôle.
            foreach (var legacy in LegacyRolesFor(user.Role))
            {
                if (!claims.Any(c => c.Type == ClaimTypes.Role && c.Value == legacy))
                    claims.Add(new Claim(ClaimTypes.Role, legacy));
            }

            // ROLE EXISTANT (table ROLES : Owner / Manager / Cashier / Waiter) du compte, via COMPTES_WEB.IdRole.
            // C'est lui qui donne l'acces aux routes [Authorize(Roles = ...)] pour les caissiers et serveurs.
            // Le claim "role_caisse" permet de le retrouver lors du changement d'entreprise.
            if (!string.IsNullOrEmpty(user.RoleCaisse))
            {
                if (!claims.Any(c => c.Type == ClaimTypes.Role && c.Value == user.RoleCaisse))
                    claims.Add(new Claim(ClaimTypes.Role, user.RoleCaisse));
                claims.Add(new Claim("role_caisse", user.RoleCaisse));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _config["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey manquante")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.Add(lifetime);
            var jwt = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
        }

        // ====================================================================
        // REFRESH TOKEN : ÉMISSION (nouvelle session après login)
        // ====================================================================

        /// <param name="userId">COMPTES_WEB.Id</param>
        /// <param name="idEntrepriseActive">Entreprise active de la session (conservée à chaque rotation).</param>
        public async Task<IssuedRefreshToken> IssueRefreshTokenAsync(Guid userId, Guid? idEntrepriseActive, string? ip, string? userAgent)
        {
            var now = DateTime.UtcNow;
            var (token, hash) = NewOpaqueToken();
            var expires = now.AddDays(Math.Min(_settings.RefreshTokenDays, _settings.SessionMaxDays));

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await InsertAsync(conn, null, userId, Guid.NewGuid(), hash, expires, now, idEntrepriseActive, ip, userAgent);

            return new IssuedRefreshToken(token, expires);
        }

        // ====================================================================
        // REFRESH TOKEN : ROTATION
        // ====================================================================

        public async Task<RefreshResult> RotateAsync(string presentedToken, string? ip, string? userAgent)
        {
            var hash = Hash(presentedToken);
            var now = DateTime.UtcNow;

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Retrouver le token présenté
            TokenRow? row = null;
            using (var cmd = new SqlCommand(@"
                SELECT Id, UserId, FamilyId, ExpiresAt, SessionStartedAt, RevokedAt, IdEntrepriseActive
                FROM REFRESH_TOKENS
                WHERE TokenHash = @Hash", conn))
            {
                cmd.Parameters.AddWithValue("@Hash", hash);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    row = new TokenRow(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetGuid(2),
                        reader.GetDateTime(3),
                        reader.GetDateTime(4),
                        reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        reader.IsDBNull(6) ? (Guid?)null : reader.GetGuid(6));
                }
            }

            if (row == null) return Fail("unknown_token");

            // 2. Token déjà consommé ou révoqué
            if (row.RevokedAt != null)
            {
                // Deux onglets qui rafraîchissent en même temps : le second présente un token
                // qui vient d'être tourné. Le navigateur a déjà reçu le nouveau cookie : on refuse
                // simplement, sans casser la session.
                if ((now - row.RevokedAt.Value).TotalSeconds <= _settings.RefreshReuseGraceSeconds)
                {
                    return Fail("concurrent_refresh");
                }

                // Rejeu d'un ancien token => vol probable : on révoque toute la session.
                _logger.LogWarning(
                    "Rejeu d'un refresh token détecté (user {UserId}, famille {FamilyId}, ip {Ip}) : session révoquée",
                    row.UserId, row.FamilyId, ip);
                await RevokeFamilyAsync(conn, row.FamilyId, now);
                return Fail("reuse_detected");
            }

            if (row.ExpiresAt <= now) return Fail("expired");

            var sessionDeadline = row.SessionStartedAt.AddDays(_settings.SessionMaxDays);
            if (sessionDeadline <= now) return Fail("session_max_age");

            // 3. L'utilisateur doit toujours exister et être actif
            var user = await LoadUserAsync(conn, row.UserId, row.IdEntrepriseActive);
            if (user == null)
            {
                await RevokeFamilyAsync(conn, row.FamilyId, now);
                return Fail("user_inactive");
            }

            // 4. Rotation atomique : révoquer l'ancien + insérer le nouveau
            var (newToken, newHash) = NewOpaqueToken();
            var slidingExpiry = now.AddDays(_settings.RefreshTokenDays);
            var newExpiry = slidingExpiry < sessionDeadline ? slidingExpiry : sessionDeadline;

            using var tx = conn.BeginTransaction();
            try
            {
                using (var upd = new SqlCommand(@"
                    UPDATE REFRESH_TOKENS
                    SET RevokedAt = @Now, ReplacedByHash = @NewHash
                    WHERE Id = @Id AND RevokedAt IS NULL", conn, tx))
                {
                    upd.Parameters.AddWithValue("@Now", now);
                    upd.Parameters.AddWithValue("@NewHash", newHash);
                    upd.Parameters.AddWithValue("@Id", row.Id);

                    if (await upd.ExecuteNonQueryAsync() == 0)
                    {
                        await tx.RollbackAsync();
                        return Fail("concurrent_refresh");
                    }
                }

                await InsertAsync(conn, tx, row.UserId, row.FamilyId, newHash, newExpiry, row.SessionStartedAt,
                    user.IdEntreprise == Guid.Empty ? (Guid?)null : user.IdEntreprise, ip, userAgent);
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return new RefreshResult
            {
                Success = true,
                User = user,
                NewRefreshToken = new IssuedRefreshToken(newToken, newExpiry)
            };
        }

        // ====================================================================
        // RÉVOCATION
        // ====================================================================

        /// <summary>Logout : révoque toute la session (famille) du refresh token présenté.</summary>
        public async Task RevokeSessionAsync(string presentedToken)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE REFRESH_TOKENS
                SET RevokedAt = @Now
                WHERE RevokedAt IS NULL
                  AND FamilyId = (SELECT FamilyId FROM REFRESH_TOKENS WHERE TokenHash = @Hash)", conn);

            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Hash", Hash(presentedToken));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Change l'entreprise active de la session (famille du refresh token présenté). Le token doit appartenir
        /// à <paramref name="userId"/>. Renvoie false si le token est inconnu, révoqué ou d'un autre compte.
        /// L'appelant doit avoir vérifié que l'entreprise appartient à l'organisation du compte.
        /// </summary>
        public async Task<bool> SwitchEntrepriseAsync(string presentedToken, Guid userId, Guid idEntreprise)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE REFRESH_TOKENS
                SET IdEntrepriseActive = @Ent
                WHERE RevokedAt IS NULL
                  AND FamilyId = (SELECT FamilyId FROM REFRESH_TOKENS
                                  WHERE TokenHash = @Hash AND UserId = @UserId AND RevokedAt IS NULL)", conn);

            cmd.Parameters.AddWithValue("@Ent", idEntreprise);
            cmd.Parameters.AddWithValue("@Hash", Hash(presentedToken));
            cmd.Parameters.AddWithValue("@UserId", userId);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        /// <summary>À appeler lors d'un changement de mot de passe, d'une désactivation de compte, etc.</summary>
        public async Task RevokeAllForUserAsync(Guid userId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE REFRESH_TOKENS
                SET RevokedAt = @Now
                WHERE UserId = @UserId AND RevokedAt IS NULL", conn);

            cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ====================================================================
        // PRIVÉ
        // ====================================================================

        private sealed record TokenRow(
            Guid Id, Guid UserId, Guid FamilyId,
            DateTime ExpiresAt, DateTime SessionStartedAt, DateTime? RevokedAt, Guid? IdEntrepriseActive);

        private static RefreshResult Fail(string reason) => new() { Success = false, Failure = reason };

        private static async Task InsertAsync(
            SqlConnection conn, SqlTransaction? tx,
            Guid userId, Guid familyId, string hash,
            DateTime expiresAt, DateTime sessionStartedAt,
            Guid? idEntrepriseActive,
            string? ip, string? userAgent)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO REFRESH_TOKENS
                    (UserId, FamilyId, TokenHash, ExpiresAt, SessionStartedAt, IdEntrepriseActive, CreatedByIp, UserAgent)
                VALUES
                    (@UserId, @FamilyId, @Hash, @ExpiresAt, @SessionStartedAt, @IdEntrepriseActive, @Ip, @UserAgent)", conn, tx);

            cmd.Parameters.AddWithValue("@IdEntrepriseActive", (object?)idEntrepriseActive ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@FamilyId", familyId);
            cmd.Parameters.AddWithValue("@Hash", hash);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);
            cmd.Parameters.AddWithValue("@SessionStartedAt", sessionStartedAt);
            cmd.Parameters.AddWithValue("@Ip", (object?)Truncate(ip, 45) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserAgent", (object?)Truncate(userAgent, 256) ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task RevokeFamilyAsync(SqlConnection conn, Guid familyId, DateTime now)
        {
            using var cmd = new SqlCommand(@"
                UPDATE REFRESH_TOKENS
                SET RevokedAt = @Now
                WHERE FamilyId = @FamilyId AND RevokedAt IS NULL", conn);

            cmd.Parameters.AddWithValue("@Now", now);
            cmd.Parameters.AddWithValue("@FamilyId", familyId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Recharge le COMPTE WEB (COMPTES_WEB) et résout l'entreprise active : celle de la session si elle est
        /// toujours valide pour l'organisation, sinon la première entreprise valide. Null si le compte n'existe plus,
        /// est désactivé, ou si l'organisation n'a plus aucune entreprise valide (hors super-admin).
        /// </summary>
        private static async Task<AuthUserSnapshot?> LoadUserAsync(SqlConnection conn, Guid userId, Guid? entrepriseSouhaitee)
        {
            Guid id, idOrganisation;
            string email;
            string? role, roleCaisse;
            bool estActif, isSuperAdmin;

            using (var cmd = new SqlCommand(@"
                SELECT c.Id, c.Email, c.IdOrganisation, c.[Role], c.EstActif, c.IsSuperAdmin, r.Name
                FROM COMPTES_WEB c
                LEFT JOIN ROLES r ON r.Id = c.IdRole
                WHERE c.Id = @Id", conn))
            {
                cmd.Parameters.AddWithValue("@Id", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;

                id = reader.GetGuid(0);
                email = reader.GetString(1);
                idOrganisation = reader.GetGuid(2);
                role = reader.IsDBNull(3) ? null : reader.GetString(3);
                estActif = reader.GetBoolean(4);
                isSuperAdmin = reader.GetBoolean(5);
                roleCaisse = reader.IsDBNull(6) ? null : reader.GetString(6);
            }

            if (!estActif) return null; // compte désactivé

            var entreprises = await EntrepriseAccess.ListValidesAsync(conn, idOrganisation);
            if (entreprises.Count == 0 && !isSuperAdmin) return null;

            var active = entrepriseSouhaitee.HasValue && entreprises.Any(e => e.Id == entrepriseSouhaitee.Value)
                ? entrepriseSouhaitee.Value
                : (entreprises.Count > 0 ? entreprises[0].Id : Guid.Empty);

            return new AuthUserSnapshot(id, email, idOrganisation, active, isSuperAdmin, role, roleCaisse);
        }

        private static (string Token, string Hash) NewOpaqueToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var token = Base64UrlEncoder.Encode(bytes);
            return (token, Hash(token));
        }

        private static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private static string? Truncate(string? value, int max) =>
            value == null ? null : (value.Length <= max ? value : value[..max]);
    }
}