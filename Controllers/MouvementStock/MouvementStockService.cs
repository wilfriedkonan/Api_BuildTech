using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.MouvementStock
{
    public class MouvementStockService : DatabaseService
    {
        public MouvementStockService(
            string connectionString,
            ILogger<MouvementStockService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // GET ALL AVEC PAGINATION
        // ========================================

        /// <summary>
        /// Récupère tous les mouvements de stock avec pagination
        /// </summary>
        /// <param name="dateDebut">Date début filtre (optionnel)</param>
        /// <param name="dateFin">Date fin filtre (optionnel)</param>
        /// <param name="page">Numéro de page (1-based)</param>
        /// <param name="pageSize">Nombre d'éléments par page</param>
        public async Task<MouvementStockListResponse> GetAllAsync(
            DateTime? dateDebut = null,
            DateTime? dateFin = null,
            int page = 1,
            int pageSize = 20)
        {
            var result = new MouvementStockListResponse { Success = true };

            try
            {
                // Validation pagination
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("m");

                // Construire filtre de dates
                var dateFilter = "";
                if (dateDebut.HasValue && dateFin.HasValue)
                {
                    dateFilter = "AND m.DateTransaction BETWEEN @DateDebut AND @DateFin";
                }
                else if (dateDebut.HasValue)
                {
                    dateFilter = "AND m.DateTransaction >= @DateDebut";
                }
                else if (dateFin.HasValue)
                {
                    dateFilter = "AND m.DateTransaction <= @DateFin";
                }

                // ========================================
                // ÉTAPE 1 : COMPTER LE TOTAL
                // ========================================
                int totalRecords = 0;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM MOUVEMENT_STOCK m
                    WHERE 1 = 1 {whereClause} {dateFilter}", conn))
                {
                    AddEntrepriseParameter(cmdCount);
                    if (dateDebut.HasValue)
                        cmdCount.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                    if (dateFin.HasValue)
                        cmdCount.Parameters.AddWithValue("@DateFin", dateFin.Value);

                    totalRecords = (int)await cmdCount.ExecuteScalarAsync();
                }

                // Si aucun résultat
                if (totalRecords == 0)
                {
                    result.Total = 0;
                    result.Pagination = CreateEmptyPagination(page, pageSize);
                    return result;
                }

                // Calculer métadonnées pagination
                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                int offset = (page - 1) * pageSize;

                // ========================================
                // ÉTAPE 2 : RÉCUPÉRER DONNÉES PAGINÉES
                // ========================================
                using (var cmd = new SqlCommand($@"
                    SELECT 
                       m.Id, m.DateTransaction, m.TypeMouvement, m.Quantite, 
                        m.PrixUnitaire, m.Montant, m.Reference, m.Commentaire, m.Motif,
                        m.IdArticle, m.IdStock,
                        m.IdEntreprise, 
                        a.Designation AS NomArticle

                    FROM MOUVEMENT_STOCK m
                    LEFT JOIN ARTICLES a ON m.IdArticle = a.Id
                    WHERE 1 = 1 {whereClause} {dateFilter}
                    ORDER BY m.DateTransaction DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    if (dateDebut.HasValue)
                        cmd.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                    if (dateFin.HasValue)
                        cmd.Parameters.AddWithValue("@DateFin", dateFin.Value);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var mouvement = new MouvementStockDto
                        {
                            Id = reader.GetGuid(0),
                            DateTransaction = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                            TypeMouvement = ReadNullableString(reader, "TypeMouvement"),
                            Quantite = reader.GetDecimal("Quantite"),
                            PrixUnitaire = reader.GetDecimal("PrixUnitaire"),
                            Montant = reader.GetDecimal("Montant"),
                            Reference = ReadNullableString(reader, "Reference"),
                            Commentaire = ReadNullableString(reader, "Commentaire"),
                            Motif = ReadNullableString(reader, "Motif"),
                            IdArticle = ReadNullableGuid(reader, "IdArticle"),
                            IdStock = ReadNullableGuid(reader, "IdStock"),
                            IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                            NomArticle = ReadNullableString(reader, "NomArticle")

                        };

                        result.Mouvements.Add(mouvement);
                    }
                }

                // ========================================
                // ÉTAPE 3 : CALCULER TOTAUX ET SOLDES
                // ========================================
                using (var cmdTotaux = new SqlCommand($@"
                    SELECT 
                        SUM(CASE WHEN TypeMouvement = 'Entree' THEN Quantite ELSE 0 END) AS TotalEntrees,
                        SUM(CASE WHEN TypeMouvement = 'Sortie' THEN Quantite ELSE 0 END) AS TotalSorties,
                        SUM(CASE WHEN TypeMouvement = 'Entree' THEN Montant ELSE 0 END) AS ValeurEntrees,
                        SUM(CASE WHEN TypeMouvement = 'Sortie' THEN Montant ELSE 0 END) AS ValeurSorties
                    FROM MOUVEMENT_STOCK m
                    WHERE 1 = 1 {whereClause} {dateFilter}", conn))
                {
                    AddEntrepriseParameter(cmdTotaux);
                    if (dateDebut.HasValue)
                        cmdTotaux.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                    if (dateFin.HasValue)
                        cmdTotaux.Parameters.AddWithValue("@DateFin", dateFin.Value);

                    using var readerTotaux = await cmdTotaux.ExecuteReaderAsync();
                    if (await readerTotaux.ReadAsync())
                    {
                        result.TotalEntrees = readerTotaux.IsDBNull(0) ? 0 : readerTotaux.GetDecimal(0);
                        result.TotalSorties = readerTotaux.IsDBNull(1) ? 0 : readerTotaux.GetDecimal(1);
                        result.ValeurEntrees = readerTotaux.IsDBNull(2) ? 0 : readerTotaux.GetDecimal(2);
                        result.ValeurSorties = readerTotaux.IsDBNull(3) ? 0 : readerTotaux.GetDecimal(3);

                        // Calculer soldes
                        result.SoldeQuantite = result.TotalEntrees - result.TotalSorties;
                        result.SoldeValeur = result.ValeurEntrees - result.ValeurSorties;
                    }
                }

                // ========================================
                // ÉTAPE 4 : MÉTADONNÉES PAGINATION
                // ========================================
                result.Total = totalRecords;
                result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);

                _logger.LogInformation($"✅ Mouvements stock récupérés: Page {page}/{totalPages}, {result.Mouvements.Count} mouvements");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération mouvements stock");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET BY ID
        // ========================================

        public async Task<MouvementStockDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("m");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        m.Id, m.DateTransaction, m.TypeMouvement, m.Quantite, 
                        m.PrixUnitaire, m.Montant, m.Reference, m.Commentaire, m.Motif,
                        m.IdArticle, m.IdStock, m.idUtilsateur,
                        m.IdEntreprise

                    FROM MOUVEMENT_STOCK m
                    LEFT JOIN ARTICLES a ON m.IdArticle = a.Id
                    WHERE m.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new MouvementStockDto
                    {
                        Id = reader.GetGuid(0),
                        DateTransaction = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                        TypeMouvement = ReadNullableString(reader, "TypeMouvement"),
                        Quantite = reader.GetDecimal("Quantite"),
                        PrixUnitaire = reader.GetDecimal("PrixUnitaire"),
                        Montant = reader.GetDecimal("Montant"),
                        Reference = ReadNullableString(reader, "Reference"),
                        Commentaire = ReadNullableString(reader, "Commentaire"),
                        Motif = ReadNullableString(reader, "Motif"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdStock = ReadNullableGuid(reader, "IdStock"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération mouvement stock {id}");
            }

            return null;
        }

        // ========================================
        // CREATE
        // ========================================

        public async Task<MouvementStockDto?> CreateAsync(CreateMouvementStockRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();
                var montant = request.Quantite * request.PrixUnitaire;
                var currentUserId = GetUserIdFromContext();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO MOUVEMENT_STOCK (
                        Id, DateTransaction, TypeMouvement, Quantite, PrixUnitaire, 
                        Montant, Reference, Commentaire, Motif, IdArticle,  
                        IdStock, IdEntreprise, idUtilsateur
                    )
                    VALUES (
                        @Id, @DateTransaction, @TypeMouvement, @Quantite, @PrixUnitaire, 
                        @Montant, @Reference, @Commentaire, @Motif, @IdArticle, 
                        @IdStock, @IdEntreprise, @idUtilsateur
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@DateTransaction", request.DateTransaction);
                cmd.Parameters.AddWithValue("@TypeMouvement", request.TypeMouvement);
                cmd.Parameters.AddWithValue("@Quantite", request.Quantite);
                cmd.Parameters.AddWithValue("@PrixUnitaire", request.PrixUnitaire);
                cmd.Parameters.AddWithValue("@Montant", montant);
                // ✅ Tracking utilisateur
                cmd.Parameters.AddWithValue("@idUtilsateur", currentUserId);

                AddParameter(cmd, "@Reference", request.Reference);
                AddParameter(cmd, "@Commentaire", request.Commentaire);
                AddParameter(cmd, "@Motif", request.Motif);
                AddParameter(cmd, "@IdArticle", request.IdArticle);

                AddParameter(cmd, "@IdStock", request.IdStock);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Mouvement stock créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création mouvement stock");
                return null;
            }
        }

        // ========================================
        // UPDATE
        // ========================================

        public async Task<MouvementStockDto?> UpdateAsync(Guid id, UpdateMouvementStockRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE MOUVEMENT_STOCK
                    SET Quantite = COALESCE(@Quantite, Quantite),
                        PrixUnitaire = COALESCE(@PrixUnitaire, PrixUnitaire),
                        Montant = COALESCE(@Quantite, Quantite) * COALESCE(@PrixUnitaire, PrixUnitaire),
                        Commentaire = COALESCE(@Commentaire, Commentaire)
                        Motif = COALESCE(@Motif, Motif)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Quantite", request.Quantite);
                AddParameter(cmd, "@PrixUnitaire", request.PrixUnitaire);
                AddParameter(cmd, "@Commentaire", request.Commentaire);
                AddParameter(cmd, "@Motif", request.Motif);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Mouvement stock mis à jour: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour mouvement stock {id}");
                return null;
            }
        }

        // ========================================
        // DELETE (SOFT)
        // ========================================

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE MOUVEMENT_STOCK
                    SET EstSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Mouvement stock supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression mouvement stock {id}");
                return false;
            }
        }

        // ========================================
        // MÉTHODES HELPER PAGINATION
        // ========================================

        /// <summary>
        /// Crée les métadonnées de pagination
        /// </summary>
        private PaginationMetadata CreatePagination(
            int currentPage,
            int pageSize,
            int totalPages,
            int totalRecords)
        {
            return new PaginationMetadata
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalRecords = totalRecords,
                HasPrevious = currentPage > 1,
                HasNext = currentPage < totalPages
            };
        }

        /// <summary>
        /// Crée pagination vide (aucun résultat)
        /// </summary>
        private PaginationMetadata CreateEmptyPagination(int page, int pageSize)
        {
            return new PaginationMetadata
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = 0,
                TotalRecords = 0,
                HasPrevious = false,
                HasNext = false
            };
        }
    }
}