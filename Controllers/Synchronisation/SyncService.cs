using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Api_BuildTech.Controllers.Synchronisation
{
    public class SyncService
    {
        private readonly string _connectionString;
        private readonly ILogger<SyncService> _logger;

        public SyncService(string connectionString, ILogger<SyncService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        #region Authentification par cle d'entreprise

        /// <summary>
        /// Retrouve l'entreprise a partir de sa cle secrete (hash SHA-256 dans SYNC_CLES).
        /// Retourne null si la cle est inconnue, revoquee, ou si l'entreprise est supprimee / desactivee
        /// (ENTREPRISE.Etat = 'Supprimer' ou CongDistantActif = 0).
        /// </summary>
        public async Task<Guid?> ResoudreEntrepriseAsync(string? cle)
        {
            if (string.IsNullOrWhiteSpace(cle) || cle.Length > 128) return null;

            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(cle.Trim()));
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            Guid? idCle = null, idEntreprise = null;
            using (var cmd = new SqlCommand(@"
                SELECT TOP (1) k.Id, k.IdEntreprise
                FROM dbo.SYNC_CLES k
                JOIN dbo.ENTREPRISE e ON e.Id = k.IdEntreprise
                WHERE k.CleHash = @h AND k.EstActif = 1
                  AND ISNULL(e.Etat, N'') <> N'Supprimer'
                  AND ISNULL(e.CongDistantActif, 1) = 1", conn))
            {
                cmd.Parameters.Add("@h", SqlDbType.VarBinary, 32).Value = hash;
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    idCle = r.GetGuid(0);
                    idEntreprise = r.GetGuid(1);
                }
            }

            if (idCle != null)
            {
                using var upd = new SqlCommand(@"
                    UPDATE dbo.SYNC_CLES SET LastUsedDate = SYSUTCDATETIME()
                    WHERE Id = @id AND (LastUsedDate IS NULL OR LastUsedDate < DATEADD(MINUTE, -1, SYSUTCDATETIME()))", conn);
                upd.Parameters.AddWithValue("@id", idCle.Value);
                await upd.ExecuteNonQueryAsync();
            }
            return idEntreprise;
        }

        #endregion

        #region Appliquer un changement (caisse -> hub)

        /// <summary>
        /// Applique UN changement via dbo.usp_SyncAppliquer : liste blanche de tables, colonnes lues dans la base,
        /// IdEntreprise force a celui de la cle, upsert, idempotence (ChangeId), conflits, SyncOrigin.
        /// </summary>
        public async Task<SyncResponse> ApplyChangeAsync(Guid idEntreprise, SyncRequest request)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                return await ApplyOneAsync(conn, idEntreprise, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync apply : erreur technique sur {Table} {Record}", request.TableName, request.RecordId);
                return new SyncResponse { Success = false, Status = "RETRY", Message = "Erreur serveur.", ChangeId = request.ChangeId };
            }
        }

        /// <summary>Applique un lot (une seule connexion, un resultat par changement, ordre conserve).</summary>
        public async Task<List<ApplyBatchItemResult>> ApplyBatchAsync(Guid idEntreprise, IReadOnlyList<SyncRequest> changes)
        {
            var results = new List<ApplyBatchItemResult>(changes.Count);
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                foreach (var ch in changes)
                {
                    var r = await ApplyOneAsync(conn, idEntreprise, ch);
                    results.Add(new ApplyBatchItemResult { ChangeId = ch.ChangeId ?? Guid.Empty, Status = r.Status ?? "RETRY", Message = r.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync apply-batch : erreur technique");
                // Les changements non traites sont simplement absents de la reponse : la caisse les considere a reessayer
            }
            return results;
        }

        private async Task<SyncResponse> ApplyOneAsync(SqlConnection conn, Guid idEntreprise, SyncRequest request)
        {
            var resp = new SyncResponse { ChangeId = request.ChangeId };
            try
            {
                if (request.ChangeId == null || request.ChangeId == Guid.Empty)
                    _logger.LogWarning("Sync apply sans ChangeId (client ancien) : pas d'idempotence pour {Table} {Record}", request.TableName, request.RecordId);

                if (string.IsNullOrWhiteSpace(request.TableName) || request.TableName.Length > 128
                    || (request.DataJSON != null && request.DataJSON.Length > 2_000_000))
                {
                    resp.Status = "REJECTED";
                    resp.Message = "Changement mal forme ou trop volumineux.";
                    return resp;
                }

                using var cmd = new SqlCommand("dbo.usp_SyncAppliquer", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 30
                };
                cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
                cmd.Parameters.AddWithValue("@ChangeId", request.ChangeId is { } id && id != Guid.Empty ? id : Guid.NewGuid());
                cmd.Parameters.Add("@TableName", SqlDbType.NVarChar, 128).Value = request.TableName ?? "";
                cmd.Parameters.AddWithValue("@RecordId", request.RecordId);
                cmd.Parameters.Add("@Operation", SqlDbType.VarChar, 10).Value = (request.Operation ?? "").ToUpperInvariant();
                cmd.Parameters.Add("@DataJSON", SqlDbType.NVarChar, -1).Value = (object?)request.DataJSON ?? DBNull.Value;
                cmd.Parameters.Add("@CreatedDate", SqlDbType.DateTime2).Value = (object?)request.CreatedDate ?? DBNull.Value;
                var pStatut = cmd.Parameters.Add("@Statut", SqlDbType.VarChar, 10);
                pStatut.Direction = ParameterDirection.Output;
                var pMessage = cmd.Parameters.Add("@Message", SqlDbType.NVarChar, 400);
                pMessage.Direction = ParameterDirection.Output;

                await cmd.ExecuteNonQueryAsync();

                resp.Status = pStatut.Value as string ?? "RETRY";
                resp.Message = pMessage.Value as string ?? resp.Status;
                resp.Success = resp.Status is "APPLIED" or "DUPLICATE";
                resp.HasConflict = resp.Status == "CONFLICT";
            }
            catch (SqlException ex)
            {
                // Erreur technique (connexion, timeout...) : a reessayer, la caisse n'est pas en tort
                _logger.LogError(ex, "Sync apply : erreur SQL sur {Table} {Record}", request.TableName, request.RecordId);
                resp.Success = false;
                resp.Status = "RETRY";
                resp.Message = "Erreur serveur.";
            }
            return resp;
        }

        #endregion

        #region Changements hub -> caisse, acquittement, statistiques

        /// <summary>
        /// Changements du hub a appliquer sur la caisse : toujours REMOTE_TO_LOCAL, uniquement les tables de
        /// sens DOWN/BOTH (SYNC_TABLES), dans l'ordre parents -> enfants. Plus de fenetre "Since" : tout ce qui
        /// n'est pas acquitte est renvoye, meme apres une longue panne de la caisse.
        /// </summary>
        public async Task<PendingChangesResponse> GetPendingChangesAsync(GetPendingChangesRequest request)
        {
            try
            {
                var changes = new List<CDCChange>();

                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand(@"
                    SELECT TOP (@MaxResults)
                        c.Id, c.IdEntreprise,
                        -- COMPAT_CATEGORIE : les caisses gardent la table locale 'CATHEGORIE'
                        CASE WHEN c.TableName = N'CATEGORIE' THEN N'CATHEGORIE' ELSE c.TableName END AS TableName,
                        c.RecordId, c.Operation,
                        c.Direction, c.Version, c.PreviousVersion, c.DataJSON, c.ChangedFields,
                        c.IsSynced, c.SyncAttempts, c.MaxRetries, c.Priority,
                        c.CreatedDate, c.SyncedDate, c.NextRetryDate,
                        c.LastError, c.LastErrorDate,
                        c.CreatedByUserId, c.CreatedByUserName, c.SourceMachine,
                        c.ConflictResolution, c.IsConflict, c.ConflictWith
                    FROM dbo.CDC_CHANGES c
                    JOIN dbo.SYNC_TABLES s ON s.TableName = c.TableName AND s.Sens IN ('DOWN', 'BOTH')
                    WHERE c.IdEntreprise = @IdEntreprise
                      AND c.Direction = 'REMOTE_TO_LOCAL'
                      AND c.IsSynced = 0
                      AND c.SyncAttempts < c.MaxRetries
                      AND (c.NextRetryDate IS NULL OR c.NextRetryDate <= SYSUTCDATETIME())
                    ORDER BY s.Ordre, c.CreatedDate", conn);

                cmd.Parameters.AddWithValue("@MaxResults", request.MaxResults);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await conn.OpenAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    changes.Add(MapToCDCChange(reader));
                }

                _logger.LogInformation("Sync pending : {Count} changements pour l'entreprise {Id}", changes.Count, request.IdEntreprise);
                return new PendingChangesResponse { Success = true, Count = changes.Count, Changes = changes };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recuperation des changements pour {Id}", request.IdEntreprise);
                return new PendingChangesResponse { Success = false, Count = 0 };
            }
        }

        /// <summary>
        /// Acquitte un changement hub -> caisse. Limite a l'entreprise de la cle (une cle ne peut pas
        /// acquitter les changements d'une autre entreprise). Retry = true : pas de tentative comptee.
        /// </summary>
        public async Task<bool> MarkAsSyncedAsync(Guid idEntreprise, MarkSyncedRequest request)
        {
            return await MarkBatchAsync(idEntreprise, new[] { request }) == 1;
        }

        /// <summary>Acquitte plusieurs changements (une connexion). Retourne le nombre de changements du hub mis a jour.</summary>
        public async Task<int> MarkBatchAsync(Guid idEntreprise, IReadOnlyList<MarkSyncedRequest> items)
        {
            int n = 0;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                foreach (var request in items)
                {
                    using var cmd = new SqlCommand(@"
                        UPDATE dbo.CDC_CHANGES SET
                            IsSynced      = CASE WHEN @ok = 1 THEN 1 ELSE IsSynced END,
                            SyncedDate    = CASE WHEN @ok = 1 THEN SYSUTCDATETIME() ELSE SyncedDate END,
                            LastError     = CASE WHEN @ok = 1 THEN NULL ELSE LEFT(@err, 4000) END,
                            LastErrorDate = CASE WHEN @ok = 1 THEN LastErrorDate ELSE SYSUTCDATETIME() END,
                            SyncAttempts  = CASE WHEN @ok = 0 AND @retry = 0 THEN SyncAttempts + 1 ELSE SyncAttempts END,
                            NextRetryDate = CASE WHEN @ok = 1 THEN NULL
                                                 ELSE DATEADD(MINUTE, CASE WHEN SyncAttempts >= 10 THEN 60 ELSE POWER(2, SyncAttempts) END, SYSUTCDATETIME()) END
                        WHERE Id = @Id AND IdEntreprise = @e AND Direction = 'REMOTE_TO_LOCAL'", conn);
                    cmd.Parameters.AddWithValue("@ok", request.Success ? 1 : 0);
                    cmd.Parameters.AddWithValue("@retry", request.Retry ? 1 : 0);
                    cmd.Parameters.Add("@err", SqlDbType.NVarChar, 4000).Value = (object?)request.ErrorMessage ?? DBNull.Value;
                    cmd.Parameters.AddWithValue("@Id", request.ChangeId);
                    cmd.Parameters.AddWithValue("@e", idEntreprise);
                    n += await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage de {Count} changements", items.Count);
                return -1;
            }
            return n;
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
