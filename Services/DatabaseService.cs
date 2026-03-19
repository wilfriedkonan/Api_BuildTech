using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Api_BuildTech.Services
{
    /// <summary>
    /// Service de base pour l'isolation multi-tenant
    /// Filtrage manuel par IdEntreprise dans chaque requête
    /// </summary>
    public class DatabaseService
    {
        protected readonly string _connectionString;
        protected readonly ILogger _logger;
        protected readonly IHttpContextAccessor? _httpContextAccessor;

        public DatabaseService(
            string connectionString,
            ILogger logger,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _connectionString = connectionString;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Obtient une connexion SQL simple
        /// </summary>
        protected async Task<SqlConnection> GetConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        /// <summary>
        /// Récupère l'IdEntreprise depuis le JWT token
        /// </summary>
        protected Guid GetEntrepriseIdFromContext()
        {
            if (_httpContextAccessor?.HttpContext == null)
            {
                _logger.LogWarning("⚠️ HttpContext null - impossible de récupérer IdEntreprise");
                return Guid.Empty;
            }

            var entrepriseIdClaim = _httpContextAccessor.HttpContext.User
                .FindFirst("entreprise_id")?.Value;

            if (string.IsNullOrEmpty(entrepriseIdClaim))
            {
                _logger.LogWarning("⚠️ Claim 'entreprise_id' absent du JWT");
                return Guid.Empty;
            }

            if (Guid.TryParse(entrepriseIdClaim, out Guid entrepriseId))
            {
                _logger.LogDebug($"✅ IdEntreprise récupéré: {entrepriseId}");
                return entrepriseId;
            }

            _logger.LogError($"❌ Impossible de parser entreprise_id: {entrepriseIdClaim}");
            return Guid.Empty;
        }

        /// <summary>
        /// Vérifie si l'utilisateur est SuperAdmin
        /// </summary>
        protected bool GetIsSuperAdminFromContext()
        {
            if (_httpContextAccessor?.HttpContext == null)
                return false;

            // Vérifier le rôle SuperAdmin
            var isSuperAdmin = _httpContextAccessor.HttpContext.User.IsInRole("SuperAdmin");

            // Vérifier aussi le claim is_super_admin
            var isSuperAdminClaim = _httpContextAccessor.HttpContext.User
                .FindFirst("is_super_admin")?.Value;

            return isSuperAdmin || isSuperAdminClaim == "True";
        }

        /// <summary>
        /// Récupère l'ID de l'utilisateur connecté
        /// </summary>
        protected Guid GetUserIdFromContext()
        {
            if (_httpContextAccessor?.HttpContext == null)
                return Guid.Empty;

            var userIdClaim = _httpContextAccessor.HttpContext.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out Guid userId))
            {
                return userId;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Récupère l'email de l'utilisateur connecté
        /// </summary>
        protected string? GetUserEmailFromContext()
        {
            if (_httpContextAccessor?.HttpContext == null)
                return null;

            return _httpContextAccessor.HttpContext.User
                .FindFirst(ClaimTypes.Email)?.Value;
        }

        /// <summary>
        /// Construit une clause WHERE pour filtrage multi-tenant
        /// </summary>
        /// <param name="tableAlias">Alias de la table (ex: "a" pour "FROM ARTICLES a")</param>
        /// <returns>Clause WHERE avec filtrage IdEntreprise</returns>
        protected string BuildWhereClause(string tableAlias = "")
        {
            var isSuperAdmin = GetIsSuperAdminFromContext();

            // SuperAdmin voit tout - pas de filtrage
            if (isSuperAdmin)
            {
                _logger.LogDebug("✅ SuperAdmin - pas de filtrage");
                return "";
            }

            var idEntreprise = GetEntrepriseIdFromContext();

            // Pas d'IdEntreprise = Bloquer tout accès
            if (idEntreprise == Guid.Empty)
            {
                _logger.LogWarning("⚠️ IdEntreprise vide - blocage de l'accès");
                return " AND 1 = 0"; // Bloque toutes les données
            }

            // Construire le préfixe (avec ou sans alias)
            var prefix = string.IsNullOrEmpty(tableAlias) ? "" : tableAlias + ".";

            _logger.LogDebug($"✅ Filtrage par IdEntreprise: {idEntreprise}");
            return $" AND {prefix}IdEntreprise = @IdEntreprise";
        }

        /// <summary>
        /// Ajoute le paramètre @IdEntreprise au SqlCommand si nécessaire
        /// UTILISATION: Après avoir construit la requête SQL avec BuildWhereClause()
        /// </summary>
        /// <param name="cmd">SqlCommand à paramétrer</param>
        protected void AddEntrepriseParameter(SqlCommand cmd)
        {
            // Vérifier si l'utilisateur est SuperAdmin
            var isSuperAdmin = GetIsSuperAdminFromContext();

            if (isSuperAdmin)
            {
                // SuperAdmin n'a pas besoin du paramètre
                _logger.LogDebug("✅ SuperAdmin - pas de paramètre IdEntreprise ajouté");
                return;
            }

            // Récupérer l'IdEntreprise depuis le JWT
            var idEntreprise = GetEntrepriseIdFromContext();

            if (idEntreprise == Guid.Empty)
            {
                _logger.LogError("❌ IdEntreprise vide - impossible d'ajouter le paramètre");
                throw new InvalidOperationException("IdEntreprise introuvable dans le contexte");
            }

            // Ajouter le paramètre seulement s'il n'existe pas déjà
            if (!cmd.Parameters.Contains("@IdEntreprise"))
            {
                cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
                _logger.LogDebug($"✅ Paramètre @IdEntreprise ajouté: {idEntreprise}");
            }
            else
            {
                _logger.LogDebug("⚠️ Paramètre @IdEntreprise déjà présent");
            }
        }

        /// <summary>
        /// Version avec validation stricte - Lance une exception si IdEntreprise manquant
        /// </summary>
        protected void AddEntrepriseParameterStrict(SqlCommand cmd)
        {
            var isSuperAdmin = GetIsSuperAdminFromContext();

            if (isSuperAdmin)
                return;

            var idEntreprise = GetEntrepriseIdFromContext();

            if (idEntreprise == Guid.Empty)
            {
                var userEmail = GetUserEmailFromContext();
                _logger.LogError($"❌ SÉCURITÉ: Tentative d'accès sans IdEntreprise par {userEmail}");
                throw new UnauthorizedAccessException("IdEntreprise requis pour cette opération");
            }

            if (!cmd.Parameters.Contains("@IdEntreprise"))
            {
                cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
            }
        }

        /// <summary>
        /// Validation entreprise active
        /// </summary>
        protected async Task<bool> ValidateEntrepriseAsync(Guid idEntreprise)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM ENTREPRISE 
                    WHERE Id = @Id 
                      AND IsActive = 1", conn);

                cmd.Parameters.AddWithValue("@Id", idEntreprise);

                var count = (int)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur validation entreprise {idEntreprise}");
                return false;
            }
        }

        /// <summary>
        /// Validation API Key (pour accès externe)
        /// </summary>
        public bool ValidateApiKey(string apiKey)
        {
            // TODO: Charger depuis configuration ou base de données
            const string VALID_API_KEY = "VotreCléAPISecrète123!";
            return apiKey == VALID_API_KEY;
        }

        // ========================================
        // HELPERS POUR PARAMÈTRES SQL
        // ========================================

        /// <summary>
        /// Ajoute un paramètre en gérant les valeurs NULL
        /// </summary>
        protected void AddParameter(SqlCommand cmd, string name, object? value)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        /// <summary>
        /// Ajoute un paramètre Guid nullable
        /// </summary>
        protected void AddGuidParameter(SqlCommand cmd, string name, Guid? value)
        {
            cmd.Parameters.AddWithValue(name, value.HasValue ? value.Value : DBNull.Value);
        }

        /// <summary>
        /// Ajoute un paramètre DateTime nullable
        /// </summary>
        protected void AddDateTimeParameter(SqlCommand cmd, string name, DateTime? value)
        {
            cmd.Parameters.AddWithValue(name, value.HasValue ? value.Value : DBNull.Value);
        }

        // ========================================
        // HELPERS POUR LECTURE SqlDataReader
        // ========================================

        protected Guid? ReadNullableGuid(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
        }

        protected string? ReadNullableString(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        protected DateTime? ReadNullableDateTime(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        protected int? ReadNullableInt(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }

        protected double? ReadNullableDouble(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
        }

        protected decimal? ReadNullableDecimal(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
        }

        protected bool? ReadNullableBool(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
        }

        protected byte[]? ReadNullableBytes(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            var length = reader.GetBytes(ordinal, 0, null, 0, 0);
            var buffer = new byte[length];
            reader.GetBytes(ordinal, 0, buffer, 0, (int)length);
            return buffer;
        }
    }
}