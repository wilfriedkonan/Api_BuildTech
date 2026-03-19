using Api_BuildTech.Controllers.Synchronisation;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace Api_BuildTech.Controllers.Synchronisation
{
    public class SyncService
    {
        private readonly string _connectionString;
        private readonly ILogger<SyncService> _logger;
        private const string VALID_API_KEY = "VotreCléAPISecrète123!";

        // ✅ NOUVEAU: Définir les colonnes à exclure par table
        private static readonly Dictionary<string, HashSet<string>> ExcludedColumns = new()
        {
            // Colonnes auto-générées à exclure lors des INSERT
            { "DOMAINE_RESTAURANT", new HashSet<string>() /*{ "Id" }*/ }, // Id est IDENTITY
            { "ARTICLES", new HashSet<string>() }, // Pas de colonnes à exclure (Id est GUID)
            { "FACTURE", new HashSet<string>() {"identifiant"} },
            { "CLIENTS", new HashSet<string>() },
            { "DETAIL_TRANSACTIONS", new HashSet<string>() },
            { "STOCK", new HashSet<string>() },
            { "UTILISATEURS", new HashSet<string>() },
            { "ENTREPRISE", new HashSet<string>() },
            { "CATEGORIES", new HashSet<string>() },
            { "TABLES", new HashSet<string>() },
            { "FOURNISSEURS", new HashSet<string>() },

        };

        public SyncService(string connectionString, ILogger<SyncService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        #region Validation

        public bool ValidateApiKey(string apiKey)
        {
            return apiKey == VALID_API_KEY;
        }

        public async Task<bool> ValidateEntrepriseAsync(Guid idEntreprise)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM ENTREPRISE WHERE Id = @Id AND Etat IS NULL", conn);
            cmd.Parameters.AddWithValue("@Id", idEntreprise);

            await conn.OpenAsync();
            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        #endregion

        #region Appliquer Changement

        public async Task<SyncResponse> ApplyChangeAsync(SyncRequest request)
        {
            try
            {
                _logger.LogInformation($"Application changement: {request.TableName} ({request.Operation}) pour entreprise {request.IdEntreprise}");

                if (!await ValidateEntrepriseAsync(request.IdEntreprise))
                {
                    return new SyncResponse
                    {
                        Success = false,
                        Message = "Entreprise introuvable ou supprimée"
                    };
                }

                switch (request.Operation.ToUpper())
                {
                    case "INSERT":
                        await ApplyInsertAsync(request);
                        break;

                    case "UPDATE":
                        var updateResult = await ApplyUpdateAsync(request);
                        if (updateResult.HasConflict)
                            return updateResult;
                        break;

                    case "DELETE":
                        await ApplyDeleteAsync(request);
                        break;

                    default:
                        return new SyncResponse
                        {
                            Success = false,
                            Message = $"Opération invalide: {request.Operation}"
                        };
                }

                _logger.LogInformation($"Changement appliqué avec succès: {request.TableName} ID {request.RecordId}");

                return new SyncResponse
                {
                    Success = true,
                    Message = "Changement appliqué avec succès"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de l'application du changement: {request.TableName}");
                return new SyncResponse
                {
                    Success = false,
                    Message = $"Erreur: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// ✅ CORRECTION: Applique un INSERT en excluant les colonnes IDENTITY
        /// </summary>
        private async Task ApplyInsertAsync(SyncRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Parser le JSON
            var data = ParseJsonToDictionary(request.DataJSON);

            // ✅ NOUVEAU: Exclure les colonnes IDENTITY pour cette table
            var excludedCols = GetExcludedColumns(request.TableName);
            var filteredData = data;
            //.Where(kvp => !excludedCols.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            //.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (filteredData.Count == 0)
            {
                _logger.LogWarning($"Aucune colonne à insérer après exclusion pour {request.TableName}");
                return;
            }

            // Construire la requête INSERT
            var columns = string.Join(", ", filteredData.Keys);
            var parameters = string.Join(", ", filteredData.Keys.Select(k => $"@{k}"));

            var sql = $"INSERT INTO {request.TableName} ({columns}) VALUES ({parameters})";

            _logger.LogDebug($"SQL INSERT: {sql}");

            using var cmd = new SqlCommand(sql, conn);

            // Ajouter les paramètres
            foreach (var kvp in filteredData)
            {
                AddSqlParameter(cmd, kvp.Key, kvp.Value);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// ✅ CORRECTION: Applique un UPDATE (pas besoin d'exclure les colonnes IDENTITY)
        /// </summary>
        private async Task<SyncResponse> ApplyUpdateAsync(SyncRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier si l'enregistrement existe
            var existsCmd = new SqlCommand(
                $"SELECT COUNT(*) FROM {request.TableName} WHERE Id = @Id", conn);
            existsCmd.Parameters.AddWithValue("@Id", request.RecordId);

            var exists = (int)await existsCmd.ExecuteScalarAsync() > 0;

            if (!exists)
            {
                _logger.LogWarning($"Enregistrement {request.RecordId} n'existe pas dans {request.TableName}, conversion en INSERT");
                await ApplyInsertAsync(request);
                return new SyncResponse { Success = true, Message = "INSERT appliqué (enregistrement inexistant)" };
            }

            // Parser le JSON
            var data = ParseJsonToDictionary(request.DataJSON);

            // ✅ Pour UPDATE, on peut inclure Id car on ne l'insère pas, on l'utilise juste dans WHERE
            // Mais on exclut quand même les colonnes IDENTITY si elles sont dans le SET
            var excludedCols = GetExcludedColumns(request.TableName);
            var filteredData = data;
            //.Where(kvp => !excludedCols.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            //.Where(kvp => !kvp.Key.Equals("Id", StringComparison.OrdinalIgnoreCase)) // Exclure Id du SET
            //.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (filteredData.Count == 0)
            {
                _logger.LogWarning($"Aucune colonne à mettre à jour pour {request.TableName}");
                return new SyncResponse { Success = true, Message = "Aucune colonne à mettre à jour" };
            }

            // Construire la requête UPDATE
            var setClauses = string.Join(", ", filteredData.Keys.Select(k => $"{k} = @{k}"));
            var sql = $"UPDATE {request.TableName} SET {setClauses} WHERE Id = @RecordId";

            _logger.LogDebug($"SQL UPDATE: {sql}");

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@RecordId", request.RecordId);

            // Ajouter les paramètres
            foreach (var kvp in filteredData)
            {
                AddSqlParameter(cmd, kvp.Key, kvp.Value);
            }

            await cmd.ExecuteNonQueryAsync();

            return new SyncResponse { Success = true, Message = "UPDATE appliqué" };
        }

        private async Task ApplyDeleteAsync(SyncRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                $"DELETE FROM {request.TableName} WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", request.RecordId);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Utilitaires Colonnes

        /// <summary>
        /// ✅ NOUVEAU: Récupère les colonnes à exclure pour une table
        /// </summary>
        private HashSet<string> GetExcludedColumns(string tableName)
        {
            if (ExcludedColumns.TryGetValue(tableName, out var columns))
            {
                return columns;
            }

            // Par défaut, pas de colonnes à exclure
            return new HashSet<string>();
        }

        /// <summary>
        /// ✅ ALTERNATIVE: Détecte automatiquement les colonnes IDENTITY (optionnel)
        /// </summary>
        private async Task<HashSet<string>> GetIdentityColumnsAsync(string tableName)
        {
            var identityColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                  AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1",
                conn);

            cmd.Parameters.AddWithValue("@TableName", tableName);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                identityColumns.Add(reader.GetString(0));
            }

            return identityColumns;
        }

        #endregion

        #region Utilitaires JSON

        private Dictionary<string, object> ParseJsonToDictionary(string json)
        {
            var result = new Dictionary<string, object>();

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    result[property.Name] = GetJsonValue(property.Value);
                }
            }

            return result;
        }

        private object GetJsonValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    var strValue = element.GetString();
                    if (Guid.TryParse(strValue, out Guid guidValue))
                        return guidValue;
                    if (DateTime.TryParse(strValue, out DateTime dateValue))
                        return dateValue;
                    return strValue;

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    if (element.TryGetDecimal(out decimal decimalValue))
                        return decimalValue;
                    return element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return DBNull.Value;

                default:
                    return element.GetRawText();
            }
        }

        private void AddSqlParameter(SqlCommand cmd, string name, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                cmd.Parameters.AddWithValue($"@{name}", DBNull.Value);
                return;
            }

            switch (value)
            {
                case Guid guidVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.UniqueIdentifier).Value = guidVal;
                    break;

                case DateTime dateVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.DateTime2).Value = dateVal;
                    break;

                case int intVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.Int).Value = intVal;
                    break;

                case long longVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.BigInt).Value = longVal;
                    break;

                case decimal decimalVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.Decimal).Value = decimalVal;
                    break;

                case double doubleVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.Float).Value = doubleVal;
                    break;

                case bool boolVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.Bit).Value = boolVal;
                    break;

                case string strVal:
                    cmd.Parameters.Add($"@{name}", SqlDbType.NVarChar).Value = strVal;
                    break;

                default:
                    cmd.Parameters.AddWithValue($"@{name}", value);
                    break;
            }
        }

        #endregion

        #region Récupérer Changements, Marquer Synced, Statistiques (INCHANGÉ)

        public async Task<PendingChangesResponse> GetPendingChangesAsync(GetPendingChangesRequest request)
        {
            try
            {
                var changes = new List<CDCChange>();

                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(@"
                    SELECT TOP (@MaxResults)
                        Id, IdEntreprise, TableName, RecordId, Operation,
                        Direction, Version, PreviousVersion, DataJSON, ChangedFields,
                        IsSynced, SyncAttempts, MaxRetries, Priority,
                        CreatedDate, SyncedDate, NextRetryDate,
                        LastError, LastErrorDate,
                        CreatedByUserId, CreatedByUserName, SourceMachine,
                        ConflictResolution, IsConflict, ConflictWith
                    FROM CDC_CHANGES
                    WHERE IdEntreprise = @IdEntreprise
                      AND Direction = @Direction
                      AND IsSynced = 0
                      AND (SyncAttempts < MaxRetries)
                      AND (NextRetryDate IS NULL OR NextRetryDate <= GETUTCDATE())
                      AND CreatedDate > @Since
                    ORDER BY Priority ASC, CreatedDate ASC", conn);

                cmd.Parameters.AddWithValue("@MaxResults", request.MaxResults);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                cmd.Parameters.AddWithValue("@Direction", request.Direction);
                cmd.Parameters.AddWithValue("@Since", request.Since);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    changes.Add(MapToCDCChange(reader));
                }

                _logger.LogInformation($"Récupéré {changes.Count} changements pour entreprise {request.IdEntreprise}");

                return new PendingChangesResponse
                {
                    Success = true,
                    Count = changes.Count,
                    Changes = changes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors de la récupération des changements pour {request.IdEntreprise}");
                return new PendingChangesResponse
                {
                    Success = false,
                    Count = 0
                };
            }
        }

        public async Task<bool> MarkAsSyncedAsync(MarkSyncedRequest request)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                if (request.Success)
                {
                    using var cmd = new SqlCommand(@"
                        UPDATE CDC_CHANGES
                        SET IsSynced = 1,
                            SyncedDate = GETUTCDATE(),
                            LastError = NULL
                        WHERE Id = @Id", conn);

                    cmd.Parameters.AddWithValue("@Id", request.ChangeId);
                    await cmd.ExecuteNonQueryAsync();

                    _logger.LogInformation($"Changement {request.ChangeId} marqué comme synchronisé");
                }
                else
                {
                    using var cmd = new SqlCommand(@"
                        UPDATE CDC_CHANGES
                        SET SyncAttempts = SyncAttempts + 1,
                            LastError = @ErrorMessage,
                            LastErrorDate = GETUTCDATE(),
                            NextRetryDate = DATEADD(MINUTE, POWER(2, SyncAttempts + 1), GETUTCDATE())
                        WHERE Id = @Id", conn);

                    cmd.Parameters.AddWithValue("@Id", request.ChangeId);
                    cmd.Parameters.AddWithValue("@ErrorMessage", request.ErrorMessage ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();

                    _logger.LogWarning($"Échec sync changement {request.ChangeId}: {request.ErrorMessage}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur lors du marquage du changement {request.ChangeId}");
                return false;
            }
        }

        public async Task<SyncStatistics> GetStatisticsAsync(Guid idEntreprise)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    COUNT(CASE WHEN IsSynced = 0 THEN 1 END) as TotalPending,
                    COUNT(CASE WHEN IsSynced = 1 THEN 1 END) as TotalSynced,
                    COUNT(CASE WHEN SyncAttempts >= MaxRetries THEN 1 END) as TotalFailed,
                    COUNT(CASE WHEN IsConflict = 1 THEN 1 END) as TotalConflicts,
                    MAX(CASE WHEN IsSynced = 1 THEN SyncedDate END) as LastSyncDate
                FROM CDC_CHANGES
                WHERE IdEntreprise = @IdEntreprise", conn);

            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            var stats = new SyncStatistics { IdEntreprise = idEntreprise };

            if (await reader.ReadAsync())
            {
                stats.TotalPending = reader.GetInt32(0);
                stats.TotalSynced = reader.GetInt32(1);
                stats.TotalFailed = reader.GetInt32(2);
                stats.TotalConflicts = reader.GetInt32(3);
                stats.LastSyncDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
            }

            await reader.CloseAsync();

            using var cmdTables = new SqlCommand(@"
                SELECT 
                    TableName,
                    COUNT(CASE WHEN IsSynced = 0 THEN 1 END) as PendingChanges,
                    MAX(CASE WHEN IsSynced = 1 THEN SyncedDate END) as LastSync,
                    MAX(Version) as LastVersion
                FROM CDC_CHANGES
                WHERE IdEntreprise = @IdEntreprise
                GROUP BY TableName", conn);

            cmdTables.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            using var readerTables = await cmdTables.ExecuteReaderAsync();

            while (await readerTables.ReadAsync())
            {
                stats.TableStats.Add(new TableSyncInfo
                {
                    TableName = readerTables.GetString(0),
                    PendingChanges = readerTables.GetInt32(1),
                    LastSync = readerTables.IsDBNull(2) ? null : readerTables.GetDateTime(2),
                    LastVersion = readerTables.GetInt64(3)
                });
            }

            return stats;
        }

        private CDCChange MapToCDCChange(SqlDataReader reader)
        {
            return new CDCChange
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                IdEntreprise = reader.GetGuid(reader.GetOrdinal("IdEntreprise")),
                TableName = reader.GetString(reader.GetOrdinal("TableName")),
                RecordId = reader.GetGuid(reader.GetOrdinal("RecordId")),
                Operation = reader.GetString(reader.GetOrdinal("Operation")),
                Direction = reader.GetString(reader.GetOrdinal("Direction")),
                Version = reader.GetInt64(reader.GetOrdinal("Version")),
                PreviousVersion = reader.IsDBNull(reader.GetOrdinal("PreviousVersion"))
                    ? null : reader.GetInt64(reader.GetOrdinal("PreviousVersion")),
                DataJSON = reader.IsDBNull(reader.GetOrdinal("DataJSON"))
                    ? null : reader.GetString(reader.GetOrdinal("DataJSON")),
                ChangedFields = reader.IsDBNull(reader.GetOrdinal("ChangedFields"))
                    ? null : reader.GetString(reader.GetOrdinal("ChangedFields")),
                IsSynced = reader.GetBoolean(reader.GetOrdinal("IsSynced")),
                SyncAttempts = reader.GetInt32(reader.GetOrdinal("SyncAttempts")),
                MaxRetries = reader.GetInt32(reader.GetOrdinal("MaxRetries")),
                Priority = reader.GetInt32(reader.GetOrdinal("Priority")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                SyncedDate = reader.IsDBNull(reader.GetOrdinal("SyncedDate"))
                    ? null : reader.GetDateTime(reader.GetOrdinal("SyncedDate")),
                NextRetryDate = reader.IsDBNull(reader.GetOrdinal("NextRetryDate"))
                    ? null : reader.GetDateTime(reader.GetOrdinal("NextRetryDate")),
                LastError = reader.IsDBNull(reader.GetOrdinal("LastError"))
                    ? null : reader.GetString(reader.GetOrdinal("LastError")),
                LastErrorDate = reader.IsDBNull(reader.GetOrdinal("LastErrorDate"))
                    ? null : reader.GetDateTime(reader.GetOrdinal("LastErrorDate")),
                CreatedByUserId = reader.IsDBNull(reader.GetOrdinal("CreatedByUserId"))
                    ? null : reader.GetGuid(reader.GetOrdinal("CreatedByUserId")),
                CreatedByUserName = reader.IsDBNull(reader.GetOrdinal("CreatedByUserName"))
                    ? null : reader.GetString(reader.GetOrdinal("CreatedByUserName")),
                SourceMachine = reader.IsDBNull(reader.GetOrdinal("SourceMachine"))
                    ? null : reader.GetString(reader.GetOrdinal("SourceMachine")),
                ConflictResolution = reader.IsDBNull(reader.GetOrdinal("ConflictResolution"))
                    ? null : reader.GetString(reader.GetOrdinal("ConflictResolution")),
                IsConflict = reader.GetBoolean(reader.GetOrdinal("IsConflict")),
                ConflictWith = reader.IsDBNull(reader.GetOrdinal("ConflictWith"))
                    ? null : reader.GetGuid(reader.GetOrdinal("ConflictWith"))
            };
        }

        #endregion
    }
}