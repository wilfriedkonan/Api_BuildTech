using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;


namespace Api_BuildTech.Controllers.ArticleStock
{
    public class ArticleStockService : DatabaseService
    {
        public ArticleStockService(
            string connectionString,
            ILogger<ArticleStockService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // GET ALL AVEC FILTRES ET PAGINATION
        // ========================================

        /// <summary>
        /// Récupère tous les stocks avec filtres et pagination
        /// </summary>
        public async Task<StockArticleListResponse> GetAllAsync(StockArticleFilterRequest filter)
        {
            var result = new StockArticleListResponse { Success = true };

            try
            {
                // Validation pagination
                if (filter.Page < 1) filter.Page = 1;
                if (filter.PageSize < 1) filter.PageSize = 20;
                if (filter.PageSize > 100) filter.PageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("v");

                // Construire filtres
                var filters = BuildFilters(filter);

                // ========================================
                // ÉTAPE 1 : COMPTER LE TOTAL
                // ========================================
                int totalRecords = 0;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM V_STOCK_ARTICLES_ENTREPRISE v
                    WHERE 1=1 {whereClause} {filters}", conn))
                {
                    AddEntrepriseParameter(cmdCount);
                    AddFilterParameters(cmdCount, filter);
                    totalRecords = (int)await cmdCount.ExecuteScalarAsync();
                }

                if (totalRecords == 0)
                {
                    result.Pagination = CreateEmptyPagination(filter.Page, filter.PageSize);
                    return result;
                }

                // Calculer pagination
                int totalPages = (int)Math.Ceiling(totalRecords / (double)filter.PageSize);
                int offset = (filter.Page - 1) * filter.PageSize;

                // Construire ORDER BY
                var orderBy = BuildOrderBy(filter.OrderBy, filter.OrderDirection);

                // ========================================
                // ÉTAPE 2 : RÉCUPÉRER DONNÉES PAGINÉES
                // ========================================
                using (var cmd = new SqlCommand($@"
                    SELECT 
                        v.IdEntreprise, v.IdArticle, v.CodeArticle, v.Designation, v.Description,
                        v.CodeBarre, v.PrixAchat, v.PrixVente, v.PrixExterieur, v.EstPos,
                        v.position, v.EstExonerer, v.TypeRepas, v.Etat, v.DatePerenption,
                        v.EstStockable, v.EstEnStock, v.EstEnPorter, v.IdCathegorie, v.IdType_Repas,
                        v.PrixPromo, v.EstPromo, v.EstComposer, v.EstVendableSansComposition,
                        v.AfficherStockPOS, v.ImageURL, v.TauxTva, v.Stock, v.SeuilAlerte,
                        v.Statut, v.DateCreate, v.idCreateUser, v.DateLastUpdate, v.idLastUpdateUser,
                        v.DesignationCategorie, v.NomCreateur, v.PrenomCreateur, 
                        v.NomModificateur, v.PrenomModificateur,
                        v.StockActuel, v.SeuilStock, v.StatutStock
                    FROM V_STOCK_ARTICLES_ENTREPRISE v
                    WHERE 1=1 {whereClause} {filters}
                    {orderBy}
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    AddFilterParameters(cmd, filter);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", filter.PageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var stock = MapStockFromReader(reader);
                        result.Stocks.Add(stock);

                        // Compteurs
                        if (stock.StatutStock == "EN STOCK") result.TotalEnStock++;
                        else if (stock.StatutStock == "STOCK FAIBLE") result.TotalEnAlerte++;
                        else if (stock.StatutStock == "RUPTURE") result.TotalEnRupture++;

                        // Valeur totale
                        result.ValeurStockTotal += stock.ValeurStock;
                    }
                }

                result.Total = totalRecords;
                result.Pagination = CreatePagination(filter.Page, filter.PageSize, totalPages, totalRecords);

                _logger.LogInformation($"✅ Stocks récupérés: Page {filter.Page}/{totalPages}, {result.Stocks.Count} articles");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération stocks");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET STATISTIQUES
        // ========================================

        /// <summary>
        /// Récupère les statistiques globales de stock
        /// </summary>
        public async Task<StockStatisticsDto> GetStatisticsAsync()
        {
            var stats = new StockStatisticsDto();

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("v");

                // Statistiques globales
                using (var cmd = new SqlCommand($@"
                    SELECT 
                        COUNT(*) AS TotalArticles,
                        SUM(CASE WHEN StatutStock = 'En stock' THEN 1 ELSE 0 END) AS TotalEnStock,
                        SUM(CASE WHEN StatutStock = 'Alerte' THEN 1 ELSE 0 END) AS TotalEnAlerte,
                        SUM(CASE WHEN StatutStock = 'Rupture' THEN 1 ELSE 0 END) AS TotalEnRupture,
                        SUM(CASE WHEN EstStockable = 1 THEN 1 ELSE 0 END) AS TotalStockables,
                        SUM(CASE WHEN EstPos = 1 THEN 1 ELSE 0 END) AS TotalPos,
                        SUM(CASE WHEN EstPromo = 1 THEN 1 ELSE 0 END) AS TotalPromo,
                        SUM(StockActuel) AS StockTotalUnites,
                        SUM(StockActuel * ISNULL(PrixAchat, 0)) AS ValeurStockTotal
                    FROM V_STOCK_ARTICLES_ENTREPRISE v
                    WHERE 1=1 {whereClause}", conn))
                {
                    AddEntrepriseParameter(cmd);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        stats.TotalArticles = reader.GetInt32(0);
                        stats.TotalEnStock = reader.GetInt32(1);
                        stats.TotalEnAlerte = reader.GetInt32(2);
                        stats.TotalEnRupture = reader.GetInt32(3);
                        stats.TotalStockables = reader.GetInt32(4);
                        stats.TotalPos = reader.GetInt32(5);
                        stats.TotalPromo = reader.GetInt32(6);
                        stats.StockTotalUnites = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7);
                        stats.ValeurStockTotal = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8);

                        // Pourcentages
                        if (stats.TotalArticles > 0)
                        {
                            stats.PourcentageEnStock = (stats.TotalEnStock * 100m) / stats.TotalArticles;
                            stats.PourcentageEnAlerte = (stats.TotalEnAlerte * 100m) / stats.TotalArticles;
                            stats.PourcentageEnRupture = (stats.TotalEnRupture * 100m) / stats.TotalArticles;
                        }
                    }
                }

                // Top 5 Alertes
                stats.Top5Alertes = await GetTopAsync("Alerte", 5);

                // Top 5 Ruptures
                stats.Top5Ruptures = await GetTopAsync("Rupture", 5);

                // Top 5 Valeur Stock
                stats.Top5ValeurStock = await GetTopByValueAsync(5);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération statistiques stock");
            }

            return stats;
        }

        // ========================================
        // GET BY ID
        // ========================================

        /// <summary>
        /// Récupère un stock par IdArticle
        /// </summary>
        public async Task<StockArticleViewDto?> GetByIdAsync(Guid idArticle)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("v");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        v.IdEntreprise, v.IdArticle, v.CodeArticle, v.Designation, v.Description,
                        v.CodeBarre, v.PrixAchat, v.PrixVente, v.PrixExterieur, v.EstPos,
                        v.position, v.EstExonerer, v.TypeRepas, v.Etat, v.DatePerenption,
                        v.EstStockable, v.EstEnStock, v.EstEnPorter, v.IdCathegorie, v.IdType_Repas,
                        v.PrixPromo, v.EstPromo, v.EstComposer, v.EstVendableSansComposition,
                        v.AfficherStockPOS, v.ImageURL, v.TauxTva, v.Stock, v.SeuilAlerte,
                        v.Statut, v.DateCreate, v.idCreateUser, v.DateLastUpdate, v.idLastUpdateUser,
                        v.DesignationCategorie, v.NomCreateur, v.PrenomCreateur, 
                        v.NomModificateur, v.PrenomModificateur,
                        v.StockActuel, v.SeuilStock, v.StatutStock
                    FROM V_STOCK_ARTICLES_ENTREPRISE v
                    WHERE v.IdArticle = @IdArticle {whereClause}", conn);

                cmd.Parameters.AddWithValue("@IdArticle", idArticle);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapStockFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération stock article {idArticle}");
            }

            return null;
        }

        // ========================================
        // MÉTHODES HELPER
        // ========================================

        private string BuildFilters(StockArticleFilterRequest filter)
        {
            var filters = new List<string>();

            if (!string.IsNullOrEmpty(filter.StatutStock))
                filters.Add("AND v.StatutStock = @StatutStock");

            if (!string.IsNullOrEmpty(filter.Etat))
                filters.Add("AND v.Etat = @Etat");

            if (!string.IsNullOrEmpty(filter.DesignationCategorie))
                filters.Add("AND v.DesignationCategorie = @DesignationCategorie");

            if (!string.IsNullOrEmpty(filter.SearchTerm))
                filters.Add("AND (v.Designation LIKE @SearchTerm OR v.CodeArticle LIKE @SearchTerm)");

            if (filter.StockMin.HasValue)
                filters.Add("AND v.StockActuel >= @StockMin");

            if (filter.StockMax.HasValue)
                filters.Add("AND v.StockActuel <= @StockMax");

            if (filter.AlertesOnly == true)
                filters.Add("AND v.StatutStock IN ('Alerte', 'Rupture')");

            if (filter.EstPosOnly == true)
                filters.Add("AND v.EstPos = 1");

            if (filter.EstStockableOnly == true)
                filters.Add("AND v.EstStockable = 1");

            if (filter.EstPromoOnly == true)
                filters.Add("AND v.EstPromo = 1");

            return string.Join(" ", filters);
        }

        private void AddFilterParameters(SqlCommand cmd, StockArticleFilterRequest filter)
        {
            if (!string.IsNullOrEmpty(filter.StatutStock))
                cmd.Parameters.AddWithValue("@StatutStock", filter.StatutStock);

            if (!string.IsNullOrEmpty(filter.Etat))
                cmd.Parameters.AddWithValue("@Etat", filter.Etat);

            if (!string.IsNullOrEmpty(filter.DesignationCategorie))
                cmd.Parameters.AddWithValue("@DesignationCategorie", filter.DesignationCategorie);

            if (!string.IsNullOrEmpty(filter.SearchTerm))
                cmd.Parameters.AddWithValue("@SearchTerm", $"%{filter.SearchTerm}%");

            if (filter.StockMin.HasValue)
                cmd.Parameters.AddWithValue("@StockMin", filter.StockMin.Value);

            if (filter.StockMax.HasValue)
                cmd.Parameters.AddWithValue("@StockMax", filter.StockMax.Value);
        }

        private string BuildOrderBy(string orderBy, string direction)
        {
            var dir = direction.ToLower() == "desc" ? "DESC" : "ASC";

            return orderBy.ToLower() switch
            {
                "stock" => $"ORDER BY v.StockActuel {dir}",
                "categorie" => $"ORDER BY v.DesignationCategorie {dir}, v.Designation {dir}",
                "statut" => $"ORDER BY v.StatutStock {dir}, v.Designation {dir}",
                "prix" => $"ORDER BY v.PrixVente {dir}, v.Designation {dir}",
                "valeur" => $"ORDER BY (v.StockActuel * ISNULL(v.PrixAchat, 0)) {dir}",
                _ => $"ORDER BY v.Designation {dir}"
            };
        }

        private async Task<List<StockArticleViewDto>> GetTopAsync(string statut, int top)
        {
            var result = new List<StockArticleViewDto>();

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("v");

                using var cmd = new SqlCommand($@"
                    SELECT TOP {top}
                        v.IdEntreprise, v.IdArticle, v.CodeArticle, v.Designation, v.Description,
                        v.CodeBarre, v.PrixAchat, v.PrixVente, v.PrixExterieur, v.EstPos,
                        v.position, v.EstExonerer, v.TypeRepas, v.Etat, v.DatePerenption,
                        v.EstStockable, v.EstEnStock, v.EstEnPorter, v.IdCathegorie, v.IdType_Repas,
                        v.PrixPromo, v.EstPromo, v.EstComposer, v.EstVendableSansComposition,
                        v.AfficherStockPOS, v.ImageURL, v.TauxTva, v.Stock, v.SeuilAlerte,
                        v.Statut, v.DateCreate, v.idCreateUser, v.DateLastUpdate, v.idLastUpdateUser,
                        v.DesignationCategorie, v.NomCreateur, v.PrenomCreateur, 
                        v.NomModificateur, v.PrenomModificateur,
                        v.StockActuel, v.SeuilStock, v.StatutStock
                    FROM V_STOCK_ARTICLES_ENTREPRISE v
                    WHERE v.StatutStock = @Statut {whereClause}
                    ORDER BY v.StockActuel ASC", conn);

                cmd.Parameters.AddWithValue("@Statut", statut);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(MapStockFromReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération top {statut}");
            }

            return result;
        }

        private async Task<List<StockArticleViewDto>> GetTopByValueAsync(int top)
        {
            var result = new List<StockArticleViewDto>();

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("v");

                using var cmd = new SqlCommand($@"
                    SELECT TOP {top}
                        v.IdEntreprise, v.IdArticle, v.CodeArticle, v.Designation, v.Description,
                        v.CodeBarre, v.PrixAchat, v.PrixVente, v.PrixExterieur, v.EstPos,
                        v.position, v.EstExonerer, v.TypeRepas, v.Etat, v.DatePerenption,
                        v.EstStockable, v.EstEnStock, v.EstEnPorter, v.IdCathegorie, v.IdType_Repas,
                        v.PrixPromo, v.EstPromo, v.EstComposer, v.EstVendableSansComposition,
                        v.AfficherStockPOS, v.ImageURL, v.TauxTva, v.Stock, v.SeuilAlerte,
                        v.Statut, v.DateCreate, v.idCreateUser, v.DateLastUpdate, v.idLastUpdateUser,
                        v.DesignationCategorie, v.NomCreateur, v.PrenomCreateur, 
                        v.NomModificateur, v.PrenomModificateur,
                        v.StockActuel, v.SeuilStock, v.StatutStock
                    FROM V_STOCK_ARTICLES_ENTREPRISE v
                    WHERE 1=1 {whereClause}
                    ORDER BY (v.StockActuel * ISNULL(v.PrixAchat, 0)) DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(MapStockFromReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération top valeur");
            }

            return result;
        }

        private StockArticleViewDto MapStockFromReader(SqlDataReader reader)
        {
            return new StockArticleViewDto
            {
                IdEntreprise = reader.GetGuid(reader.GetOrdinal("IdEntreprise")),
                IdArticle = reader.GetGuid(reader.GetOrdinal("IdArticle")),
                CodeArticle = ReadNullableString(reader, "CodeArticle"),
                Designation = ReadNullableString(reader, "Designation"),
                Description = ReadNullableString(reader, "Description"),
                CodeBarre = ReadNullableString(reader, "CodeBarre"),
                PrixAchat = ReadNullableDecimal(reader, "PrixAchat"),
                PrixVente = ReadNullableDecimal(reader, "PrixVente"),
                PrixExterieur = ReadNullableDecimal(reader, "PrixExterieur"),
                EstPos = ReadNullableBool(reader, "EstPos"),
                position = ReadNullableString(reader, "position"),
                EstExonerer = ReadNullableBool(reader, "EstExonerer"),
                TypeRepas = ReadNullableString(reader, "TypeRepas"),
                Etat = ReadNullableString(reader, "Etat"),
                DatePerenption = ReadNullableDateTime(reader, "DatePerenption"),
                EstStockable = ReadNullableBool(reader, "EstStockable"),
                EstEnStock = ReadNullableBool(reader, "EstEnStock"),
                EstEnPorter = ReadNullableBool(reader, "EstEnPorter"),
                IdCathegorie = ReadNullableGuid(reader, "IdCathegorie"),
                IdType_Repas = ReadNullableGuid(reader, "IdType_Repas"),
                PrixPromo = ReadNullableDecimal(reader, "PrixPromo"),
                EstPromo = ReadNullableBool(reader, "EstPromo"),
                EstComposer = ReadNullableBool(reader, "EstComposer"),
                EstVendableSansComposition = ReadNullableBool(reader, "EstVendableSansComposition"),
                AfficherStockPOS = ReadNullableBool(reader, "AfficherStockPOS"),
                ImageURL = ReadNullableString(reader, "ImageURL"),
                TauxTva = ReadNullableDecimal(reader, "TauxTva"),
                Stock = reader.GetDecimal(reader.GetOrdinal("Stock")),
                SeuilAlerte = reader.GetDecimal(reader.GetOrdinal("SeuilAlerte")),
                Statut = ReadNullableBool(reader, "Statut"),
                DateCreate = ReadNullableDateTime(reader, "DateCreate"),
                idCreateUser = ReadNullableGuid(reader, "idCreateUser"),
                DateLastUpdate = ReadNullableDateTime(reader, "DateLastUpdate"),
                idLastUpdateUser = ReadNullableGuid(reader, "idLastUpdateUser"),
                DesignationCategorie = ReadNullableString(reader, "DesignationCategorie"),
                NomCreateur = ReadNullableString(reader, "NomCreateur"),
                PrenomCreateur = ReadNullableString(reader, "PrenomCreateur"),
                NomModificateur = ReadNullableString(reader, "NomModificateur"),
                PrenomModificateur = ReadNullableString(reader, "PrenomModificateur"),
                StockActuel = reader.GetDecimal(reader.GetOrdinal("StockActuel")),
                SeuilStock = reader.GetDecimal(reader.GetOrdinal("SeuilStock")),
                StatutStock = ReadNullableString(reader, "StatutStock")
            };
        }

        public PaginationMetadata CreatePagination(int currentPage, int pageSize, int totalPages, int totalRecords)
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

        public PaginationMetadata CreateEmptyPagination(int page, int pageSize)
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