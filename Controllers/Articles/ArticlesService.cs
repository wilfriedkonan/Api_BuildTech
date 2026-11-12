using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.Articles
{
    public class ArticlesService : DatabaseService
    {

        public ArticlesService(
            string connectionString,
            ILogger<ArticlesService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // MÉTHODE : CALCULER STOCK ACTUEL
        // ========================================

        /// <summary>
        /// Calcule le stock actuel d'un article basé sur les mouvements
        /// Stock Actuel = Stock + Total Entrées - Total Sorties
        /// </summary>
        public async Task<decimal> CalculateStockActuelAsync(Guid idArticle)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // 1. Récupérer le stock initial
                decimal Stock = 0;
                using (var cmdStock = new SqlCommand(@"
                    SELECT ISNULL(Stock, 0) 
                    FROM ARTICLES 
                    WHERE Id = @Id", conn))
                {
                    cmdStock.Parameters.AddWithValue("@Id", idArticle);
                    var result = await cmdStock.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        Stock = Convert.ToDecimal(result);
                    }
                }

                // 2. Calculer total entrées
                decimal totalEntrees = 0;
                using (var cmdEntrees = new SqlCommand(@"
                    SELECT ISNULL(SUM(Quantite), 0)
                    FROM MOUVEMENT_STOCK
                    WHERE IdArticle = @IdArticle
                    AND TypeMouvement = 'Entree'
                    AND ISNULL(EstSupprimer, 0) = 0", conn))
                {
                    cmdEntrees.Parameters.AddWithValue("@IdArticle", idArticle);
                    var result = await cmdEntrees.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        totalEntrees = Convert.ToDecimal(result);
                    }
                }

                // 3. Calculer total sorties
                decimal totalSorties = 0;
                using (var cmdSorties = new SqlCommand(@"
                    SELECT ISNULL(SUM(Quantite), 0)
                    FROM MOUVEMENT_STOCK
                    WHERE IdArticle = @IdArticle
                    AND TypeMouvement = 'Sortie'
                    AND ISNULL(EstSupprimer, 0) = 0", conn))
                {
                    cmdSorties.Parameters.AddWithValue("@IdArticle", idArticle);
                    var result = await cmdSorties.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        totalSorties = Convert.ToDecimal(result);
                    }
                }

                // 4. Stock actuel = Initial + Entrées - Sorties
                var stockActuel = Stock + totalEntrees - totalSorties;

                _logger.LogDebug($"Stock calculé pour {idArticle}: Initial={Stock}, +Entrées={totalEntrees}, -Sorties={totalSorties}, =Actuel={stockActuel}");

                return stockActuel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur calcul stock article {idArticle}");
                return 0;
            }
        }

        // ========================================
        // GET ALL
        // ========================================

        public async Task<ArticleListResponse> GetAllAsync()
        {
            var result = new ArticleListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
                        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
                        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
                        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
                        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
                        a.EstComposer, a.EstVendableSansComposition,
                        a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
                        a.ImageURL, a.Statut,
                        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
                        c.Designation AS NomCategorie,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
                    WHERE ISNULL(a.Etat, 'Actif') != 'Supprimer' {whereClause}
                    ORDER BY c.Ordre, a.Position, a.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var article = MapArticleFromReader(reader);
                    result.Articles.Add(article);
                }

                result.Total = result.Articles.Count;

                // Calculer stock actuel pour articles stockables
                foreach (var article in result.Articles.Where(a => a.EstStockable == true))
                {
                    article.StockActuel = await CalculateStockActuelAsync(article.Id);

                    // Vérifier alerte stock
                    if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
                    {
                        article.AlerteStock = true;
                        result.TotalEnAlerte++;
                    }

                    result.TotalStockables++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles");
                result.Success = false;
            }

            return result;
        }

        public async Task<ArticleListResponse> GetAllWithPaginationAsync(GetArticlesFilterRequest filter)
        {
            var result = new ArticleListResponse { Success = true };

            //try
            //{
            // Validation
            if (filter.Page < 1) filter.Page = 1;
            if (filter.PageSize < 1) filter.PageSize = 20;
            if (filter.PageSize > 100) filter.PageSize = 100;

            using var conn = await GetConnectionAsync();

            // ════════════════════════════════════════════════════════════════════════════
            // ÉTAPE 1: COMPTER LE TOTAL (avant pagination)
            // ════════════════════════════════════════════════════════════════════════════

            var whereClause = BuildWhereClause("a");
            var filterClause = BuildFilterClause(filter);

            using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*) as Total
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    WHERE ISNULL(a.Etat, 'Actif') != 'Supprimer' 
                    {whereClause}
                    {filterClause}", conn))
            {
                AddEntrepriseParameter(cmdCount);
                AddFilterParameters(cmdCount, filter);

                var totalCount = (int)await cmdCount.ExecuteScalarAsync();
                result.Total = totalCount;

                // Calculer pagination
                var totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize);
                var offset = (filter.Page - 1) * filter.PageSize;

                result.Pagination = new PaginationMetadata
                {
                    CurrentPage = filter.Page,
                    PageSize = filter.PageSize,
                    TotalPages = totalPages,
                    TotalRecords = totalCount,
                    HasPrevious = filter.Page > 1,
                    HasNext = filter.Page < totalPages
                };
            }

            // ════════════════════════════════════════════════════════════════════════════
            // ÉTAPE 2: RÉCUPÉRER LES ARTICLES (avec pagination et tri)
            // ════════════════════════════════════════════════════════════════════════════

            var orderByClause = BuildOrderByClause(filter);
            var offset2 = (filter.Page - 1) * filter.PageSize;

            using (var cmd = new SqlCommand($@"
                    SELECT 
                        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
                        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
                        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
                        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
                        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
                        a.EstComposer, a.EstVendableSansComposition,
                        a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
                        a.EstVisibleCatalogue,
                        a.ImageURL, a.Statut,
                        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
                        c.Designation AS NomCategorie,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
                    WHERE ISNULL(a.Etat, 'Actif') != 'Supprimer' 
                    {whereClause}
                    {filterClause}
                    {orderByClause}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", conn))
            {
                AddEntrepriseParameter(cmd);
                AddFilterParameters(cmd, filter);
                cmd.Parameters.AddWithValue("@Offset", offset2);
                cmd.Parameters.AddWithValue("@PageSize", filter.PageSize);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var article = MapArticleFromReader(reader);
                    result.Articles.Add(article);
                }
            }

            // ════════════════════════════════════════════════════════════════════════════
            // ÉTAPE 3: CALCULER LE STOCK ACTUEL (pour articles stockables)
            // ════════════════════════════════════════════════════════════════════════════

            foreach (var article in result.Articles.Where(a => a.EstStockable == true))
            {
                article.StockActuel = await CalculateStockActuelAsync(article.Id);

                // Vérifier alerte stock
                if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
                {
                    article.AlerteStock = true;
                    result.TotalEnAlerte++;
                }

                result.TotalStockables++;
            }

            _logger.LogInformation($"Articles récupérés - Page {filter.Page}/{result.Pagination?.TotalPages}, Total: {result.Total}");
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Erreur récupération articles avec pagination");
            //    result.Success = false;
            //}

            return result;
        }
        private string BuildFilterClause(GetArticlesFilterRequest filter)
        {
            var conditions = new List<string>();

            // Recherche texte
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                conditions.Add(@"(
                    a.Designation LIKE @SearchTerm 
                    OR a.CodeArticle LIKE @SearchTerm 
                    OR a.CodeBarre LIKE @SearchTerm
                    OR a.Description LIKE @SearchTerm
                )");
            }

            // Filtre catégorie
            if (filter.IdCathegorie.HasValue && filter.IdCathegorie != Guid.Empty)
            {
                conditions.Add("a.IdCathegorie = @IdCathegorie");
            }

            // Filtre stockable
            if (filter.EstStockable.HasValue)
            {
                conditions.Add($"a.EstStockable = @EstStockable");
            }

            // Filtre visible catalogue
            if (filter.EstVisibleCatalogue.HasValue)
            {
                conditions.Add($"ISNULL(a.EstVisibleCatalogue, 1) = @EstVisibleCatalogue");
            }

            // Filtre POS
            if (filter.EstPos.HasValue)
            {
                conditions.Add($"a.EstPos = @EstPos");
            }

            // Filtre promo
            if (filter.EstPromo.HasValue)
            {
                conditions.Add($"a.EstPromo = @EstPromo");
            }

            // Filtre état
            if (!string.IsNullOrWhiteSpace(filter.Etat))
            {
                conditions.Add("a.Etat = @Etat");
            }

            // Filtre prix min
            if (filter.PrixVenteMin.HasValue)
            {
                conditions.Add("a.PrixVente >= @PrixVenteMin");
            }

            // Filtre prix max
            if (filter.PrixVenteMax.HasValue)
            {
                conditions.Add("a.PrixVente <= @PrixVenteMax");
            }

            if (conditions.Count == 0)
                return string.Empty;

            return "AND " + string.Join(" AND ", conditions);
        }

        /// <summary>
        /// Ajouter les paramètres SQL pour les filtres
        /// </summary>
        private void AddFilterParameters(SqlCommand cmd, GetArticlesFilterRequest filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                cmd.Parameters.AddWithValue("@SearchTerm", $"%{filter.SearchTerm}%");
            }

            if (filter.IdCathegorie.HasValue && filter.IdCathegorie != Guid.Empty)
            {
                cmd.Parameters.AddWithValue("@IdCathegorie", filter.IdCathegorie.Value);
            }

            if (filter.EstStockable.HasValue)
            {
                cmd.Parameters.AddWithValue("@EstStockable", filter.EstStockable.Value ? 1 : 0);
            }

            if (filter.EstVisibleCatalogue.HasValue)
            {
                cmd.Parameters.AddWithValue("@EstVisibleCatalogue", filter.EstVisibleCatalogue.Value ? 1 : 0);
            }

            if (filter.EstPos.HasValue)
            {
                cmd.Parameters.AddWithValue("@EstPos", filter.EstPos.Value ? 1 : 0);
            }

            if (filter.EstPromo.HasValue)
            {
                cmd.Parameters.AddWithValue("@EstPromo", filter.EstPromo.Value ? 1 : 0);
            }

            if (!string.IsNullOrWhiteSpace(filter.Etat))
            {
                cmd.Parameters.AddWithValue("@Etat", filter.Etat);
            }

            if (filter.PrixVenteMin.HasValue)
            {
                cmd.Parameters.AddWithValue("@PrixVenteMin", filter.PrixVenteMin.Value);
            }

            if (filter.PrixVenteMax.HasValue)
            {
                cmd.Parameters.AddWithValue("@PrixVenteMax", filter.PrixVenteMax.Value);
            }
        }

        /// <summary>
        /// Construire la clause ORDER BY
        /// </summary>
        private string BuildOrderByClause(GetArticlesFilterRequest filter)
        {
            var orderColumn = filter.OrderBy.ToLower() switch
            {
                "prixvente" => "a.PrixVente",
                "prixachat" => "a.PrixAchat",
                "stock" => "a.Stock",
                "etat" => "a.Etat",
                "categorie" => "c.Designation",
                "date" => "a.DateCreate",
                _ => "a.Designation"
            };

            var orderDir = filter.OrderDirection.ToLower() == "desc" ? "DESC" : "ASC";

            return $"ORDER BY {orderColumn} {orderDir}";
        }
        // ========================================
        // GET BY ID
        // ========================================

        public async Task<ArticleDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
                        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
                        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
                        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
                        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
                        a.EstComposer, a.EstVendableSansComposition,
                        a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
                        a.ImageURL, a.Statut,
                        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
                        c.Designation AS NomCategorie,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
                    WHERE a.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var article = MapArticleFromReader(reader);

                    // Calculer stock si stockable
                    if (article.EstStockable == true)
                    {
                        article.StockActuel = await CalculateStockActuelAsync(id);

                        if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
                        {
                            article.AlerteStock = true;
                        }
                    }

                    return article;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération article {id}");
            }

            return null;
        }

        // ========================================
        // GET BY CODE BARRE
        // ========================================

        public async Task<ArticleDto?> GetByCodeBarreAsync(string codeBarre)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
                        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
                        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
                        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
                        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
                        a.EstComposer, a.EstVendableSansComposition,
                        a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
                        a.ImageURL, a.Statut,
                        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
                        c.Designation AS NomCategorie,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
                    WHERE a.CodeBarre = @CodeBarre {whereClause}", conn);

                cmd.Parameters.AddWithValue("@CodeBarre", codeBarre);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var article = MapArticleFromReader(reader);

                    if (article.EstStockable == true)
                    {
                        article.StockActuel = await CalculateStockActuelAsync(article.Id);

                        if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
                        {
                            article.AlerteStock = true;
                        }
                    }

                    return article;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération article code barre {codeBarre}");
            }

            return null;
        }

        // ========================================
        // GET BY CATEGORIE
        // ========================================

        public async Task<ArticleListResponse> GetByCategorieAsync(Guid idCategorie)
        {
            var result = new ArticleListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
                        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
                        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
                        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
                        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
                        a.EstComposer, a.EstVendableSansComposition,
                        a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
                        a.ImageURL, a.Statut,
                        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
                        c.Designation AS NomCategorie,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
                    WHERE a.IdCathegorie = @IdCategorie 
                      AND ISNULL(a.Etat, 'Actif') = 'Actif' {whereClause}
                    ORDER BY a.Position, a.Designation", conn);

                cmd.Parameters.AddWithValue("@IdCategorie", idCategorie);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var article = MapArticleFromReader(reader);
                    result.Articles.Add(article);
                }

                result.Total = result.Articles.Count;

                foreach (var article in result.Articles.Where(a => a.EstStockable == true))
                {
                    article.StockActuel = await CalculateStockActuelAsync(article.Id);

                    if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
                    {
                        article.AlerteStock = true;
                        result.TotalEnAlerte++;
                    }

                    result.TotalStockables++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération articles catégorie {idCategorie}");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET POS ARTICLES
        // ========================================

        public async Task<ArticleListResponse> GetPosArticlesAsync()
        {
            var result = new ArticleListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
                        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
                        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
                        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
                        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
                        a.EstComposer, a.EstVendableSansComposition, 
                        a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
                        a.ImageURL, a.Statut,
                        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
                        c.Designation AS NomCategorie,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
                    WHERE a.EstPos = 1 
                      AND ISNULL(a.Etat, 'Actif') = 'Actif' {whereClause}
                    ORDER BY c.Ordre, a.Position, a.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var article = MapArticleFromReader(reader);
                    result.Articles.Add(article);
                }

                result.Total = result.Articles.Count;

                foreach (var article in result.Articles.Where(a => a.EstStockable == true))
                {
                    article.StockActuel = await CalculateStockActuelAsync(article.Id);

                    if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
                    {
                        article.AlerteStock = true;
                        result.TotalEnAlerte++;
                    }

                    result.TotalStockables++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles POS");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET PROMO ARTICLES
        // ========================================

        public async Task<ArticleListResponse> GetPromoArticlesAsync()
        {
            var result = new ArticleListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
                        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
                        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
                        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
                        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
                        a.EstComposer, a.EstVendableSansComposition,
                        a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
                        a.ImageURL, a.Statut,
                        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
                        c.Designation AS NomCategorie,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM ARTICLES a
                    LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
                    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
                    WHERE a.EstPromo = 1 
                      AND ISNULL(a.Etat, 'Actif') = 'Actif' {whereClause}
                    ORDER BY a.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var article = MapArticleFromReader(reader);
                    result.Articles.Add(article);
                }

                result.Total = result.Articles.Count;

                foreach (var article in result.Articles.Where(a => a.EstStockable == true))
                {
                    article.StockActuel = await CalculateStockActuelAsync(article.Id);

                    if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
                    {
                        article.AlerteStock = true;
                        result.TotalEnAlerte++;
                    }

                    result.TotalStockables++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles promo");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET STOCK ARTICLES
        // ========================================

        //public async Task<ArticleListResponse> GetStockArticlesAsync()
        //{
        //    var result = new ArticleListResponse { Success = true };

        //    try
        //    {
        //        using var conn = await GetConnectionAsync();
        //        var whereClause = BuildWhereClause("a");

        //        using var cmd = new SqlCommand($@"
        //            SELECT 
        //                a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
        //                a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
        //                a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
        //                a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
        //                a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
        //                a.EstComposer, a.EstVendableSansComposition,
        //                a.AfficherStockPOS, a.TauxTva, a.Stock, a.SeuilAlerte,
        //                a.ImageURL, a.Statut,
        //                a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
        //                c.Designation AS NomCategorie,
        //                u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
        //                u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
        //            FROM ARTICLES a
        //            LEFT JOIN CATEGORIE c ON a.IdCathegorie = c.Id
        //            LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
        //            LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
        //            WHERE a.EstStockable = 1 
        //              AND ISNULL(a.Etat, 'Actif') != 'Supprimer' {whereClause}
        //            ORDER BY a.Designation", conn);

        //        AddEntrepriseParameter(cmd);

        //        using var reader = await cmd.ExecuteReaderAsync();

        //        while (await reader.ReadAsync())
        //        {
        //            var article = MapArticleFromReader(reader);
        //            result.Articles.Add(article);
        //        }

        //        result.Total = result.Articles.Count;
        //        result.TotalStockables = result.Total;

        //        // Calculer stock pour tous
        //        foreach (var article in result.Articles)
        //        {
        //            article.StockActuel = await CalculateStockActuelAsync(article.Id);

        //            if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
        //            {
        //                article.AlerteStock = true;
        //                result.TotalEnAlerte++;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération articles stock");
        //        result.Success = false;
        //    }

        //    return result;
        //}


        // ========================================
        // GET STOCK ARTICLES AVEC PAGINATION
        // ========================================

        /// <summary>
        /// Récupère tous les articles stockables avec pagination
        /// </summary>
        /// <param name="page">Numéro de page (1-based)</param>
        /// <param name="pageSize">Nombre d'éléments par page (max 100)</param>
        //public async Task<ArticleListResponse> GetStockArticlesAsync(int page = 1, int pageSize = 20)
        //{
        //    var result = new ArticleListResponse { Success = true };

        //    try
        //    {
        //        // Validation pagination
        //        if (page < 1) page = 1;
        //        if (pageSize < 1) pageSize = 20;
        //        if (pageSize > 100) pageSize = 100; // Limite max

        //        using var conn = await GetConnectionAsync();
        //        var whereClause = BuildWhereClause("a");

        //        // ========================================
        //        // ÉTAPE 1 : Compter le total (sans pagination)
        //        // ========================================
        //        int totalRecords = 0;
        //        using (var cmdCount = new SqlCommand($@"
        //    SELECT COUNT(*)
        //    FROM ARTICLES a
        //    WHERE a.EstStockable = 1 
        //      AND ISNULL(a.Etat, 'Actif') != 'Supprimer' {whereClause}", conn))
        //        {
        //            AddEntrepriseParameter(cmdCount);
        //            totalRecords = (int)await cmdCount.ExecuteScalarAsync();
        //        }

        //        // Si aucun résultat
        //        if (totalRecords == 0)
        //        {
        //            result.Total = 0;
        //            result.TotalStockables = 0;
        //            result.Pagination = new PaginationMetadata
        //            {
        //                CurrentPage = page,
        //                PageSize = pageSize,
        //                TotalPages = 0,
        //                TotalRecords = 0,
        //                HasPrevious = false,
        //                HasNext = false
        //            };
        //            return result;
        //        }

        //        // Calculer métadonnées pagination
        //        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        //        int offset = (page - 1) * pageSize;

        //        // ========================================
        //        // ÉTAPE 2 : Récupérer données paginées
        //        // ========================================
        //        using (var cmd = new SqlCommand($@"
        //    SELECT 
        //        a.Id, a.CodeArticle, a.Designation, a.Description, a.CodeBarre,
        //        a.PrixAchat, a.PrixVente, a.PrixExterieur, a.EstPos, a.Position,
        //        a.EstExonerer, a.TypeRepas, a.Etat, a.DatePerenption,
        //        a.EstStockable, a.EstEnStock, a.EstEnPorter, a.IdEntreprise,
        //        a.IdCathegorie, a.IdType_Repas, a.PrixPromo, a.EstPromo,
        //        a.EstComposer, a.EstVendableSansComposition, a.IdStock,
        //        a.AfficherStockPOS, a.TauxTva, a.StockInitial, a.SeuilAlerte,
        //        a.ImageURL, a.Statut,
        //        a.DateCreate, a.idCreateUser, a.DateLastUpdate, a.idLastUpdateUser,
        //        c.Designation AS NomCategorie,
        //        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
        //        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
        //    FROM ARTICLES a
        //    LEFT JOIN CATEGORIES c ON a.IdCathegorie = c.Id
        //    LEFT JOIN UTILISATEURS u1 ON a.idCreateUser = u1.Id
        //    LEFT JOIN UTILISATEURS u2 ON a.idLastUpdateUser = u2.Id
        //    WHERE a.EstStockable = 1 
        //      AND ISNULL(a.Etat, 'Actif') != 'Supprimer' {whereClause}
        //    ORDER BY a.Designation
        //    OFFSET @Offset ROWS
        //    FETCH NEXT @PageSize ROWS ONLY", conn))
        //        {
        //            AddEntrepriseParameter(cmd);
        //            cmd.Parameters.AddWithValue("@Offset", offset);
        //            cmd.Parameters.AddWithValue("@PageSize", pageSize);

        //            using var reader = await cmd.ExecuteReaderAsync();

        //            while (await reader.ReadAsync())
        //            {
        //                var article = MapArticleFromReader(reader);
        //                result.Articles.Add(article);
        //            }
        //        }

        //        // ========================================
        //        // ÉTAPE 3 : Calculer stock actuel
        //        // ========================================
        //        foreach (var article in result.Articles)
        //        {
        //            article.StockActuel = await CalculateStockActuelAsync(article.Id);

        //            if (article.SeuilAlerte.HasValue && article.StockActuel < article.SeuilAlerte.Value)
        //            {
        //                article.AlerteStock = true;
        //                result.TotalEnAlerte++;
        //            }
        //        }

        //        // ========================================
        //        // ÉTAPE 4 : Construire métadonnées pagination
        //        // ========================================
        //        result.Total = totalRecords;
        //        result.TotalStockables = totalRecords;
        //        result.Pagination = new PaginationMetadata
        //        {
        //            CurrentPage = page,
        //            PageSize = pageSize,
        //            TotalPages = totalPages,
        //            TotalRecords = totalRecords,
        //            HasPrevious = page > 1,
        //            HasNext = page < totalPages
        //        };

        //        _logger.LogInformation($"✅ Articles stock récupérés: Page {page}/{totalPages}, {result.Articles.Count} articles");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération articles stock");
        //        result.Success = false;
        //    }

        //    return result;
        //}



        // ========================================
        // CREATE WITH STOCK MOVEMENT
        // ========================================

        public async Task<ArticleDto?> CreateAsync(CreateArticleRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                var newId = Guid.NewGuid();
                var currentUserId = GetUserIdFromContext();
                var currentDate = DateTime.Now;

                _logger.LogInformation($"🔨 Création article: {request.Designation} par utilisateur {currentUserId}");

                // 1. Créer l'article avec tracking utilisateur
                using (var cmd = new SqlCommand(@"
                    INSERT INTO ARTICLES (
                        Id, CodeArticle, Designation, Description, CodeBarre,
                        PrixAchat, PrixVente, PrixExterieur, IdCathegorie,
                        EstPos, EstStockable, Etat, TauxTva, Stock, SeuilAlerte,
                        ImageURL, Statut,IdEntreprise,
                        DateCreate, idCreateUser, DateLastUpdate, idLastUpdateUser
                    )
                    VALUES (
                        @Id, @CodeArticle, @Designation, @Description, @CodeBarre,
                        @PrixAchat, @PrixVente, @PrixExterieur, @IdCathegorie,
                        @EstPos, @EstStockable, @Etat, @TauxTva, @Stock, @SeuilAlerte,
                        @ImageURL, @Statut, @IdEntreprise,
                        @DateCreate, @idCreateUser, @DateLastUpdate, @idLastUpdateUser
                    )", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", newId);
                    AddParameter(cmd, "@CodeArticle", request.CodeArticle);
                    cmd.Parameters.AddWithValue("@Designation", request.Designation);
                    AddParameter(cmd, "@Description", request.Description);
                    AddParameter(cmd, "@CodeBarre", request.CodeBarre);
                    cmd.Parameters.AddWithValue("@PrixAchat", request.PrixAchat);
                    cmd.Parameters.AddWithValue("@PrixVente", request.PrixVente);
                    AddParameter(cmd, "@PrixExterieur", request.PrixExterieur);
                    AddGuidParameter(cmd, "@IdCathegorie", request.IdCathegorie);
                    cmd.Parameters.AddWithValue("@EstPos", request.EstPos);
                    cmd.Parameters.AddWithValue("@EstStockable", request.EstStockable);
                    cmd.Parameters.AddWithValue("@Etat", request.Etat ?? "Actif");
                    cmd.Parameters.AddWithValue("@TauxTva", request.TauxTva);
                    cmd.Parameters.AddWithValue("@Stock", request.Stock);
                    cmd.Parameters.AddWithValue("@SeuilAlerte", request.SeuilAlerte);
                    AddParameter(cmd, "@ImageURL", request.ImageURL);
                    cmd.Parameters.AddWithValue("@Statut", request.Statut);

                    // ✅ Tracking utilisateur
                    cmd.Parameters.AddWithValue("@DateCreate", currentDate);
                    cmd.Parameters.AddWithValue("@idCreateUser", currentUserId);
                    cmd.Parameters.AddWithValue("@DateLastUpdate", currentDate);
                    cmd.Parameters.AddWithValue("@idLastUpdateUser", currentUserId);

                    //idEnpreprise
                    AddEntrepriseParameter(cmd);

                    await cmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation($"✅ Article créé: {newId}");

                // 2. ✅ Créer mouvement de stock initial SI Stock > 0
                if (request.Stock > 0)
                {
                    var mouvementId = Guid.NewGuid();
                    var montant = request.Stock * request.PrixAchat;

                    using (var cmdMvt = new SqlCommand(@"
                        INSERT INTO MOUVEMENT_STOCK (
                            Id, DateTransaction, TypeMouvement, Quantite, 
                            PrixUnitaire, Montant, Reference, Commentaire,
                            IdArticle, IdEntreprise,idUtilsateur
                        )
                        VALUES (
                            @Id, @DateTransaction, 'Entree', @Quantite,
                            @PrixUnitaire, @Montant, @Reference, @Commentaire,
                            @IdArticle, @IdEntreprise, @idUtilsateur
                        )", conn, transaction))
                    {
                        cmdMvt.Parameters.AddWithValue("@Id", mouvementId);
                        cmdMvt.Parameters.AddWithValue("@DateTransaction", currentDate);
                        cmdMvt.Parameters.AddWithValue("@Quantite", request.Stock);
                        cmdMvt.Parameters.AddWithValue("@PrixUnitaire", request.PrixAchat);
                        cmdMvt.Parameters.AddWithValue("@Montant", montant);
                        cmdMvt.Parameters.AddWithValue("@Reference", $"STOCK-INIT-{newId.ToString().Substring(0, 8)}");
                        cmdMvt.Parameters.AddWithValue("@Commentaire", "Stock initial à la création de l'article");
                        cmdMvt.Parameters.AddWithValue("@IdArticle", newId);
                        cmdMvt.Parameters.AddWithValue("@idUtilsateur", currentUserId);

                        //idEnpreprise
                        AddEntrepriseParameter(cmdMvt);

                        await cmdMvt.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation($"✅ Mouvement stock initial créé: {request.Stock} unités, montant {montant:C}");
                }

                transaction.Commit();
                _logger.LogInformation($"🎉 Article et stock créés avec succès: {newId}");

                // Récupérer l'article complet
                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"❌ Erreur création article: {request.Designation}");
                return null;
            }
        }

        // ========================================
        // UPDATE WITH TRACKING
        // ========================================

        public async Task<ArticleDto?> UpdateAsync(Guid id, UpdateArticleRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();
                var currentUserId = GetUserIdFromContext();
                var currentDate = DateTime.Now;

                using var cmd = new SqlCommand($@"
                    UPDATE ARTICLES
                    SET CodeArticle = COALESCE(@CodeArticle, CodeArticle),
                        Designation = COALESCE(@Designation, Designation),
                        Description = COALESCE(@Description, Description),
                        CodeBarre = COALESCE(@CodeBarre, CodeBarre),
                        PrixAchat = COALESCE(@PrixAchat, PrixAchat),
                        PrixVente = COALESCE(@PrixVente, PrixVente),
                        PrixExterieur = COALESCE(@PrixExterieur, PrixExterieur),
                        IdCathegorie = COALESCE(@IdCathegorie, IdCathegorie),
                        EstPos = COALESCE(@EstPos, EstPos),
                        Position = COALESCE(@Position, Position),
                        EstExonerer = COALESCE(@EstExonerer, EstExonerer),
                        Etat = COALESCE(@Etat, Etat),
                        EstPromo = COALESCE(@EstPromo, EstPromo),
                        PrixPromo = COALESCE(@PrixPromo, PrixPromo),
                        EstComposer = COALESCE(@EstComposer, EstComposer),
                        EstVendableSansComposition = COALESCE(@EstVendableSansComposition, EstVendableSansComposition),
                        TauxTva = COALESCE(@TauxTva, TauxTva),
                        Stock = COALESCE(@Stock, Stock),
                        SeuilAlerte = COALESCE(@SeuilAlerte, SeuilAlerte),
                        ImageURL = COALESCE(@ImageURL, ImageURL),
                        Statut = COALESCE(@Statut, Statut),
                        DateLastUpdate = @DateLastUpdate,
                        idLastUpdateUser = @idLastUpdateUser
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@CodeArticle", request.CodeArticle);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Description", request.Description);
                AddParameter(cmd, "@CodeBarre", request.CodeBarre);
                AddParameter(cmd, "@PrixAchat", request.PrixAchat);
                AddParameter(cmd, "@PrixVente", request.PrixVente);
                AddParameter(cmd, "@PrixExterieur", request.PrixExterieur);
                AddGuidParameter(cmd, "@IdCathegorie", request.IdCathegorie);
                AddParameter(cmd, "@EstPos", request.EstPos);
                AddParameter(cmd, "@Position", request.Position);
                AddParameter(cmd, "@EstExonerer", request.EstExonerer);
                AddParameter(cmd, "@Etat", request.Etat);
                AddParameter(cmd, "@EstPromo", request.EstPromo);
                AddParameter(cmd, "@PrixPromo", request.PrixPromo);
                AddParameter(cmd, "@EstComposer", request.EstComposer);
                AddParameter(cmd, "@EstVendableSansComposition", request.EstVendableSansComposition);
                AddParameter(cmd, "@TauxTva", request.TauxTva);
                AddParameter(cmd, "@Stock", request.Stock);
                AddParameter(cmd, "@SeuilAlerte", request.SeuilAlerte);
                AddParameter(cmd, "@ImageURL", request.ImageURL);
                AddParameter(cmd, "@Statut", request.Statut);

                // ✅ Tracking utilisateur
                cmd.Parameters.AddWithValue("@DateLastUpdate", currentDate);
                cmd.Parameters.AddWithValue("@idLastUpdateUser", currentUserId);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Article mis à jour: {id} par utilisateur {currentUserId}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour article {id}");
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
                var currentUserId = GetUserIdFromContext();

                using var cmd = new SqlCommand($@"
                    UPDATE ARTICLES
                    SET Etat = 'Supprimer',
                        DateLastUpdate = @DateLastUpdate,
                        idLastUpdateUser = @idLastUpdateUser
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@DateLastUpdate", DateTime.Now);
                cmd.Parameters.AddWithValue("@idLastUpdateUser", currentUserId);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Article supprimé: {id} par utilisateur {currentUserId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression article {id}");
                return false;
            }
        }


        // ========================================
        // MÉTHODE À AJOUTER DANS ArticlesService.cs
        // ========================================

        /// <summary>
        /// Récupère les stocks depuis la vue V_STOCK_ARTICLES_ENTREPRISE avec pagination et filtres
        /// </summary>
        //public async Task<StockArticleListResponse> GetStockArticlesFromViewAsync(
        //    StockArticleFilterRequest? filter = null,
        //    int page = 1,
        //    int pageSize = 20)
        //{
        //    var result = new StockArticleListResponse { Success = true };

        //    try
        //    {
        //        // Validation pagination
        //        if (page < 1) page = 1;
        //        if (pageSize < 1) pageSize = 20;
        //        if (pageSize > 100) pageSize = 100;

        //        filter ??= new StockArticleFilterRequest();

        //        using var conn = await GetConnectionAsync();
        //        var whereClause = BuildWhereClause("v");

        //        // ========================================
        //        // CONSTRUIRE FILTRES DYNAMIQUES
        //        // ========================================
        //        var additionalFilters = new List<string>();
        //        var parameters = new Dictionary<string, object>();

        //        // Filtre par statut
        //        if (!string.IsNullOrEmpty(filter.StatutStock))
        //        {
        //            additionalFilters.Add("v.StatutStock = @StatutStock");
        //            parameters["@StatutStock"] = filter.StatutStock;
        //        }

        //        // Filtre par catégorie
        //        if (!string.IsNullOrEmpty(filter.DesignationCategorie))
        //        {
        //            additionalFilters.Add("v.DesignationCategorie = @DesignationCategorie");
        //            parameters["@DesignationCategorie"] = filter.DesignationCategorie;
        //        }

        //        // Recherche par désignation
        //        if (!string.IsNullOrEmpty(filter.SearchTerm))
        //        {
        //            additionalFilters.Add("v.DesignationArticle LIKE @SearchTerm");
        //            parameters["@SearchTerm"] = $"%{filter.SearchTerm}%";
        //        }

        //        // Filtre stock minimum
        //        if (filter.StockMin.HasValue)
        //        {
        //            additionalFilters.Add("v.StockActuel >= @StockMin");
        //            parameters["@StockMin"] = filter.StockMin.Value;
        //        }

        //        // Filtre stock maximum
        //        if (filter.StockMax.HasValue)
        //        {
        //            additionalFilters.Add("v.StockActuel <= @StockMax");
        //            parameters["@StockMax"] = filter.StockMax.Value;
        //        }

        //        // Afficher uniquement les alertes
        //        if (filter.AlertesOnly == true)
        //        {
        //            additionalFilters.Add("v.StatutStock IN ('Alerte', 'Rupture')");
        //        }

        //        string additionalWhere = additionalFilters.Count > 0
        //            ? " AND " + string.Join(" AND ", additionalFilters)
        //            : "";

        //        // ========================================
        //        // CONSTRUIRE ORDER BY
        //        // ========================================
        //        string orderByClause = filter.OrderBy?.ToLower() switch
        //        {
        //            "stock" => "v.StockActuel",
        //            "categorie" => "v.DesignationCategorie, v.DesignationArticle",
        //            "statut" => "v.StatutStock, v.StockActuel",
        //            _ => "v.DesignationArticle"
        //        };

        //        string orderDirection = filter.OrderDirection?.ToLower() == "desc" ? "DESC" : "ASC";

        //        // ========================================
        //        // ÉTAPE 1 : COMPTER LE TOTAL
        //        // ========================================
        //        int totalRecords = 0;
        //        using (var cmdCount = new SqlCommand($@"
        //    SELECT COUNT(*)
        //    FROM V_STOCK_ARTICLES_ENTREPRISE v
        //    WHERE 1=1 {whereClause} {additionalWhere}", conn))
        //        {
        //            AddEntrepriseParameter(cmdCount);
        //            foreach (var param in parameters)
        //            {
        //                cmdCount.Parameters.AddWithValue(param.Key, param.Value);
        //            }
        //            totalRecords = (int)await cmdCount.ExecuteScalarAsync();
        //        }

        //        if (totalRecords == 0)
        //        {
        //            result.Pagination = CreateEmptyPagination(page, pageSize);
        //            return result;
        //        }

        //        // Calculer pagination
        //        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        //        int offset = (page - 1) * pageSize;

        //        // ========================================
        //        // ÉTAPE 2 : RÉCUPÉRER DONNÉES PAGINÉES
        //        // ========================================
        //        using (var cmd = new SqlCommand($@"
        //    SELECT 
        //        v.IdEntreprise,
        //        v.IdArticle,
        //        v.DesignationArticle,
        //        v.DesignationCategorie,
        //        v.StockActuel,
        //        v.SeuilStock,
        //        v.StatutStock
        //    FROM V_STOCK_ARTICLES_ENTREPRISE v
        //    WHERE 1=1 {whereClause} {additionalWhere}
        //    ORDER BY {orderByClause} {orderDirection}
        //    OFFSET @Offset ROWS
        //    FETCH NEXT @PageSize ROWS ONLY", conn))
        //        {
        //            AddEntrepriseParameter(cmd);
        //            foreach (var param in parameters)
        //            {
        //                cmd.Parameters.AddWithValue(param.Key, param.Value);
        //            }
        //            cmd.Parameters.AddWithValue("@Offset", offset);
        //            cmd.Parameters.AddWithValue("@PageSize", pageSize);

        //            using var reader = await cmd.ExecuteReaderAsync();

        //            while (await reader.ReadAsync())
        //            {
        //                var stock = new StockArticleViewDto
        //                {
        //                    IdEntreprise = reader.GetGuid(0),
        //                    IdArticle = reader.GetGuid(1),
        //                    DesignationArticle = ReadNullableString(reader, "DesignationArticle"),
        //                    DesignationCategorie = ReadNullableString(reader, "DesignationCategorie"),
        //                    StockActuel = reader.GetDecimal(4),
        //                    SeuilStock = reader.GetDecimal(5),
        //                    StatutStock = ReadNullableString(reader, "StatutStock")
        //                };

        //                result.Stocks.Add(stock);

        //                // Compteurs par statut
        //                if (stock.StatutStock == "En stock")
        //                    result.TotalEnStock++;
        //                else if (stock.StatutStock == "Alerte")
        //                    result.TotalEnAlerte++;
        //                else if (stock.StatutStock == "Rupture")
        //                    result.TotalEnRupture++;
        //            }
        //        }

        //        // ========================================
        //        // ÉTAPE 3 : CONSTRUIRE MÉTADONNÉES
        //        // ========================================
        //        result.Total = totalRecords;
        //        result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);

        //        _logger.LogInformation($"✅ Stocks récupérés: Page {page}/{totalPages}, {result.Stocks.Count} articles");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération stocks depuis vue");
        //        result.Success = false;
        //    }

        //    return result;
        //}

        /// <summary>
        /// Récupère les statistiques globales de stock
        /// </summary>
        // public async Task<StockStatisticsDto> GetStockStatisticsAsync()
        // {
        //     var stats = new StockStatisticsDto();

        //     try
        //     {
        //         using var conn = await GetConnectionAsync();
        //         var whereClause = BuildWhereClause("v");

        //         using var cmd = new SqlCommand($@"
        //     SELECT 
        //         COUNT(*) AS TotalArticles,
        //         SUM(CASE WHEN StatutStock = 'En stock' THEN 1 ELSE 0 END) AS TotalEnStock,
        //         SUM(CASE WHEN StatutStock = 'Alerte' THEN 1 ELSE 0 END) AS TotalEnAlerte,
        //         SUM(CASE WHEN StatutStock = 'Rupture' THEN 1 ELSE 0 END) AS TotalEnRupture,
        //         SUM(StockActuel) AS StockTotalUnites
        //     FROM V_STOCK_ARTICLES_ENTREPRISE v
        //     WHERE 1=1 {whereClause}", conn);

        //         AddEntrepriseParameter(cmd);

        //         using (var reader = await cmd.ExecuteReaderAsync())
        //         {
        //             if (await reader.ReadAsync())
        //             {
        //                 stats.TotalArticles = reader.GetInt32(0);
        //                 stats.TotalEnStock = reader.GetInt32(1);
        //                 stats.TotalEnAlerte = reader.GetInt32(2);
        //                 stats.TotalEnRupture = reader.GetInt32(3);
        //                 stats.StockTotalUnites = reader.GetDecimal(4);

        //                 // Calculer pourcentages
        //                 if (stats.TotalArticles > 0)
        //                 {
        //                     stats.PourcentageEnStock = (stats.TotalEnStock / (decimal)stats.TotalArticles) * 100;
        //                     stats.PourcentageEnAlerte = (stats.TotalEnAlerte / (decimal)stats.TotalArticles) * 100;
        //                     stats.PourcentageEnRupture = (stats.TotalEnRupture / (decimal)stats.TotalArticles) * 100;
        //                 }
        //             }
        //         }

        //         // Récupérer Top 5 alertes
        //         using (var cmdAlertes = new SqlCommand($@"
        //     SELECT TOP 5
        //         IdEntreprise, IdArticle, DesignationArticle, DesignationCategorie,
        //         StockActuel, SeuilStock, StatutStock
        //     FROM V_STOCK_ARTICLES_ENTREPRISE v
        //     WHERE StatutStock = 'Alerte' {whereClause}
        //     ORDER BY (StockActuel / NULLIF(SeuilStock, 0)) ASC", conn))
        //         {
        //             AddEntrepriseParameter(cmdAlertes);
        //             using var reader = await cmdAlertes.ExecuteReaderAsync();
        //             while (await reader.ReadAsync())
        //             {
        //                 stats.Top5Alertes.Add(new StockArticleViewDto
        //                 {
        //                     IdEntreprise = reader.GetGuid(0),
        //                     IdArticle = reader.GetGuid(1),
        //                     DesignationArticle = ReadNullableString(reader, "DesignationArticle"),
        //                     DesignationCategorie = ReadNullableString(reader, "DesignationCategorie"),
        //                     StockActuel = reader.GetDecimal(4),
        //                     SeuilStock = reader.GetDecimal(5),
        //                     StatutStock = ReadNullableString(reader, "StatutStock")
        //                 });
        //             }
        //         }

        //         // Récupérer Top 5 ruptures
        //         using (var cmdRuptures = new SqlCommand($@"
        //     SELECT TOP 5
        //         IdEntreprise, IdArticle, DesignationArticle, DesignationCategorie,
        //         StockActuel, SeuilStock, StatutStock
        //     FROM V_STOCK_ARTICLES_ENTREPRISE v
        //     WHERE StatutStock = 'Rupture' {whereClause}
        //     ORDER BY DesignationArticle", conn))
        //         {
        //             AddEntrepriseParameter(cmdRuptures);
        //             using var reader = await cmdRuptures.ExecuteReaderAsync();
        //             while (await reader.ReadAsync())
        //             {
        //                 stats.Top5Ruptures.Add(new StockArticleViewDto
        //                 {
        //                     IdEntreprise = reader.GetGuid(0),
        //                     IdArticle = reader.GetGuid(1),
        //                     DesignationArticle = ReadNullableString(reader, "DesignationArticle"),
        //                     DesignationCategorie = ReadNullableString(reader, "DesignationCategorie"),
        //                     StockActuel = reader.GetDecimal(4),
        //                     SeuilStock = reader.GetDecimal(5),
        //                     StatutStock = ReadNullableString(reader, "StatutStock")
        //                 });
        //             }
        //         }

        //         _logger.LogInformation($"✅ Statistiques stock: {stats.TotalArticles} articles, {stats.TotalEnAlerte} alertes, {stats.TotalEnRupture} ruptures");
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Erreur récupération statistiques stock");
        //     }

        //     return stats;
        // }

        // // ========================================
        // // HELPER : MAP ARTICLE FROM READER
        // // ========================================
        /// <summary>
        /// Récupère les catégories avec statistiques (articles, prix min/max, promos)
        /// Utilise V_CATALOGUE_CATEGORIES pour performance
        /// </summary>
        public async Task<CatalogueResponseDto> GetCategoriesWithArticlesAsync(
            int page, int pageSize, string orderBy, string orderDirection)
        {
            var result = new CatalogueResponseDto { Success = true };

            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();

                // Compter total catégories
                using (var cmdCount = new SqlCommand(@"
                    SELECT COUNT(*) FROM V_CATALOGUE_CATEGORIES", conn))
                {
                    var totalCount = (int)await cmdCount.ExecuteScalarAsync();
                    result.Total = totalCount;

                    var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                    var offset = (page - 1) * pageSize;

                    result.Pagination = new PaginationMetadata
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = totalPages,
                        TotalRecords = totalCount,
                        HasPrevious = page > 1,
                        HasNext = page < totalPages
                    };
                }

                // Récupérer les catégories
                var orderByClause = BuildOrderByCategories(orderBy);
                var orderDir = orderDirection.ToLower() == "desc" ? "DESC" : "ASC";

                using (var cmd = new SqlCommand($@"
                    SELECT 
                        IdCategorie,
                        NomCategorie,
                        OrdreCategorie,
                        ImageURL,
                        Description,
                        NombreArticles,
                        NombreArticlesPromo,
                        PrixMin,
                        PrixMax,
                        PrixMoyen
                    FROM V_CATALOGUE_CATEGORIES
                    ORDER BY {orderByClause} {orderDir}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var category = new CatalogueCategory
                        {
                            IdCategorie = (Guid)reader["IdCategorie"],
                            NomCategorie = reader["NomCategorie"]?.ToString() ?? "",
                            OrdreCategorie = reader["OrdreCategorie"] != DBNull.Value ? (int)reader["OrdreCategorie"] : 0,
                            ImageURL = reader["ImageURL"]?.ToString(),
                            Description = reader["Description"]?.ToString(),
                            NombreArticles = reader["NombreArticles"] != DBNull.Value ? (int)reader["NombreArticles"] : 0,
                            NombreArticlesPromo = reader["NombreArticlesPromo"] != DBNull.Value ? (int)reader["NombreArticlesPromo"] : 0,
                            ////PrixMin = reader["PrixMin"] != DBNull.Value ? (decimal)reader["PrixMin"] : 0,
                            ////PrixMax = reader["PrixMax"] != DBNull.Value ? (decimal)reader["PrixMax"] : 0,
                            ////PrixMoyen = reader["PrixMoyen"] != DBNull.Value ? Math.Round((decimal)reader["PrixMoyen"], 2) : 0
                            PrixMin = reader["PrixMin"] != DBNull.Value ? Convert.ToDecimal(reader["PrixMin"]) : 0,
                            PrixMax = reader["PrixMax"] != DBNull.Value ? Convert.ToDecimal(reader["PrixMax"]) : 0,
                            PrixMoyen = reader["PrixMoyen"] != DBNull.Value ? Math.Round(Convert.ToDecimal(reader["PrixMoyen"]), 2) : 0
                        };

                        result.Categories.Add(category);
                    }
                }

                _logger.LogInformation($"Catégories catalogue récupérées - Page {page}/{result.Pagination?.TotalPages}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories catalogue");
                result.Success = false;
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // MÉTHODE 2: ARTICLES VISIBLES AU CATALOGUE
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Récupère les articles visibles au catalogue avec filtres
        /// Utilise V_CATALOGUE_ARTICLES pour performance
        /// </summary>
        public async Task<CatalogueArticlesResponseDto> GetArticlesPublicAsync(
            CatalogueArticleFilterRequest filter)
        {
            var result = new CatalogueArticlesResponseDto { Success = true };

            try
            {
                if (filter.Page < 1) filter.Page = 1;
                if (filter.PageSize < 1) filter.PageSize = 20;
                if (filter.PageSize > 100) filter.PageSize = 100;

                using var conn = await GetConnectionAsync();

                // Construire les clauses WHERE et paramètres
                var filterClause = BuildCatalogueFilterClause(filter);

                // Compter total
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(DISTINCT IdArticle) 
                    FROM V_CATALOGUE_ARTICLES
                    WHERE 1=1 {filterClause}", conn))
                {
                    AddCatalogueFilterParameters(cmdCount, filter);
                    var totalCount = (int)await cmdCount.ExecuteScalarAsync();
                    result.Total = totalCount;

                    var totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize);

                    result.Pagination = new PaginationMetadata
                    {
                        CurrentPage = filter.Page,
                        PageSize = filter.PageSize,
                        TotalPages = totalPages,
                        TotalRecords = totalCount,
                        HasPrevious = filter.Page > 1,
                        HasNext = filter.Page < totalPages
                    };
                }

                // Récupérer les articles
                var orderByClause = BuildOrderByArticles(filter.OrderBy);
                var orderDir = filter.OrderDirection.ToLower() == "desc" ? "DESC" : "ASC";
                var offset = (filter.Page - 1) * filter.PageSize;

                using (var cmd = new SqlCommand($@"
                    SELECT 
                        IdArticle,
                        CodeArticle,
                        NomArticle,
                        DescriptionArticle,
                        CodeBarre,
                        PrixVente,
                        PrixPromo,
                        EstPromo,
                        Stock,
                        EstStockable,
                        AfficherStockPOS,
                        ImageArticle,
                        NomCategorie,
                        IdCategorie,
                        Position,
                        TauxTva,
                        DateCreate,
                        DateLastUpdate
                    FROM V_CATALOGUE_ARTICLES
                    WHERE 1=1 {filterClause}
                    ORDER BY {orderByClause} {orderDir}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddCatalogueFilterParameters(cmd, filter);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", filter.PageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var article = new CatalogueArticle
                        {
                            IdArticle = (Guid)reader["IdArticle"],
                            CodeArticle = reader["CodeArticle"]?.ToString(),
                            NomArticle = reader["NomArticle"]?.ToString() ?? "",
                            DescriptionArticle = reader["DescriptionArticle"]?.ToString(),
                            CodeBarre = reader["CodeBarre"]?.ToString(),
                            PrixVente = (decimal)reader["PrixVente"],
                            PrixPromo = reader["PrixPromo"] != DBNull.Value ? (decimal)reader["PrixPromo"] : null,
                            EstPromo = reader["EstPromo"] != DBNull.Value && (bool)reader["EstPromo"],
                            Stock = reader["Stock"] != DBNull.Value ? (decimal)reader["Stock"] : 0,
                            EstStockable = reader["EstStockable"] != DBNull.Value && (bool)reader["EstStockable"],
                            AfficherStockPOS = reader["AfficherStockPOS"] != DBNull.Value ? reader["AfficherStockPOS"].ToString() : null,
                            ImageURL = reader["ImageArticle"]?.ToString(),
                            NomCategorie = reader["NomCategorie"]?.ToString(),
                            IdCategorie = reader["IdCategorie"] != DBNull.Value ? (Guid)reader["IdCategorie"] : Guid.Empty,
                            Position = reader["Position"] != DBNull.Value ? (int)reader["Position"] : 0,
                            TauxTva = reader["TauxTva"] != DBNull.Value ? (decimal)reader["TauxTva"] : 0,
                            DateCreate = reader["DateCreate"] != DBNull.Value ? (DateTime)reader["DateCreate"] : null,
                            DateLastUpdate = reader["DateLastUpdate"] != DBNull.Value ? (DateTime)reader["DateLastUpdate"] : null
                        };

                        result.Articles.Add(article);
                    }
                }

                _logger.LogInformation($"Articles catalogue récupérés - Page {filter.Page}/{result.Pagination?.TotalPages}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles catalogue");
                result.Success = false;
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // MÉTHODE 3: ARTICLE PAR ID
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Récupère un article spécifique du catalogue
        /// </summary>
        public async Task<CatalogueArticle?> GetArticlePublicAsync(Guid idArticle)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    SELECT TOP 1
                        IdArticle,
                        CodeArticle,
                        NomArticle,
                        DescriptionArticle,
                        CodeBarre,
                        PrixVente,
                        PrixAchat,
                        PrixExterieur,
                        PrixPromo,
                        EstPromo,
                        Stock,
                        SeuilAlerte,
                        EstStockable,
                        EstPos,
                        AfficherStockPOS,
                        ImageArticle,
                        NomCategorie,
                        IdCategorie,
                        Position,
                        TauxTva,
                        DateCreate,
                        DateLastUpdate
                    FROM V_CATALOGUE_ARTICLES
                    WHERE IdArticle = @IdArticle", conn)
                {
                    CommandTimeout = 10
                };

                cmd.Parameters.AddWithValue("@IdArticle", idArticle);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new CatalogueArticle
                    {
                        IdArticle = (Guid)reader["IdArticle"],
                        CodeArticle = reader["CodeArticle"]?.ToString(),
                        NomArticle = reader["NomArticle"]?.ToString() ?? "",
                        DescriptionArticle = reader["DescriptionArticle"]?.ToString(),
                        CodeBarre = reader["CodeBarre"]?.ToString(),
                        PrixVente = (decimal)reader["PrixVente"],
                        PrixAchat = reader["PrixAchat"] != DBNull.Value ? (decimal)reader["PrixAchat"] : null,
                        PrixExterieur = reader["PrixExterieur"] != DBNull.Value ? (decimal)reader["PrixExterieur"] : null,
                        PrixPromo = reader["PrixPromo"] != DBNull.Value ? (decimal)reader["PrixPromo"] : null,
                        EstPromo = reader["EstPromo"] != DBNull.Value && (bool)reader["EstPromo"],
                        Stock = reader["Stock"] != DBNull.Value ? (decimal)reader["Stock"] : 0,
                        SeuilAlerte = reader["SeuilAlerte"] != DBNull.Value ? (decimal)reader["SeuilAlerte"] : 0,
                        EstStockable = reader["EstStockable"] != DBNull.Value && (bool)reader["EstStockable"],
                        EstPos = reader["EstPos"] != DBNull.Value && (bool)reader["EstPos"],
                        AfficherStockPOS = reader["AfficherStockPOS"] != DBNull.Value ? reader["AfficherStockPOS"].ToString() : null,
                        ImageURL = reader["ImageArticle"]?.ToString(),
                        NomCategorie = reader["NomCategorie"]?.ToString(),
                        IdCategorie = reader["IdCategorie"] != DBNull.Value ? (Guid)reader["IdCategorie"] : Guid.Empty,
                        Position = reader["Position"] != DBNull.Value ? (int)reader["Position"] : 0,
                        TauxTva = reader["TauxTva"] != DBNull.Value ? (decimal)reader["TauxTva"] : 0,
                        DateCreate = reader["DateCreate"] != DBNull.Value ? (DateTime)reader["DateCreate"] : null,
                        DateLastUpdate = reader["DateLastUpdate"] != DBNull.Value ? (DateTime)reader["DateLastUpdate"] : null
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération article catalogue {idArticle}");
                return null;
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // MÉTHODE 4: ARTICLES PAR CATÉGORIE
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Récupère les articles d'une catégorie spécifique
        /// </summary>
        public async Task<CatalogueArticlesResponseDto> GetArticlesByCategoryAsync(
            Guid idCategorie, int page, int pageSize, string orderBy, string orderDirection)
        {
            var result = new CatalogueArticlesResponseDto { Success = true };

            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();

                // Vérifier que la catégorie existe
                using (var cmdCat = new SqlCommand(@"
                    SELECT NomCategorie FROM V_CATALOGUE_CATEGORIES 
                    WHERE IdCategorie = @IdCategorie", conn))
                {
                    cmdCat.Parameters.AddWithValue("@IdCategorie", idCategorie);
                    var categoryName = await cmdCat.ExecuteScalarAsync();

                    if (categoryName == null)
                        return result; // Catégorie non trouvée

                    result.CategoryName = categoryName.ToString();
                }

                // Compter articles
                using (var cmdCount = new SqlCommand(@"
                    SELECT COUNT(*) FROM V_CATALOGUE_ARTICLES 
                    WHERE IdCategorie = @IdCategorie", conn))
                {
                    cmdCount.Parameters.AddWithValue("@IdCategorie", idCategorie);
                    var totalCount = (int)await cmdCount.ExecuteScalarAsync();
                    result.Total = totalCount;

                    var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                    result.Pagination = new PaginationMetadata
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = totalPages,
                        TotalRecords = totalCount,
                        HasPrevious = page > 1,
                        HasNext = page < totalPages
                    };
                }

                // Récupérer articles
                var orderByClause = BuildOrderByArticles(orderBy);
                var orderDir = orderDirection.ToLower() == "desc" ? "DESC" : "ASC";

                using (var cmd = new SqlCommand($@"
                    SELECT 
                        IdArticle, CodeArticle, NomArticle, DescriptionArticle, CodeBarre,
                        PrixVente, PrixPromo, EstPromo, Stock, EstStockable,
                        AfficherStockPOS, ImageArticle, Position, TauxTva,
                        DateCreate, DateLastUpdate
                    FROM V_CATALOGUE_ARTICLES
                    WHERE IdCategorie = @IdCategorie
                    ORDER BY {orderByClause} {orderDir}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    cmd.Parameters.AddWithValue("@IdCategorie", idCategorie);
                    cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var article = new CatalogueArticle
                        {
                            IdArticle = (Guid)reader["IdArticle"],
                            CodeArticle = reader["CodeArticle"]?.ToString(),
                            NomArticle = reader["NomArticle"]?.ToString() ?? "",
                            DescriptionArticle = reader["DescriptionArticle"]?.ToString(),
                            CodeBarre = reader["CodeBarre"]?.ToString(),
                            PrixVente = (decimal)reader["PrixVente"],
                            PrixPromo = reader["PrixPromo"] != DBNull.Value ? (decimal)reader["PrixPromo"] : null,
                            EstPromo = reader["EstPromo"] != DBNull.Value && (bool)reader["EstPromo"],
                            Stock = reader["Stock"] != DBNull.Value ? (decimal)reader["Stock"] : 0,
                            EstStockable = reader["EstStockable"] != DBNull.Value && (bool)reader["EstStockable"],
                            AfficherStockPOS = reader["AfficherStockPOS"] != DBNull.Value ? reader["AfficherStockPOS"].ToString() : null,
                            ImageURL = reader["ImageArticle"]?.ToString(),
                            Position = reader["Position"] != DBNull.Value ? (int)reader["Position"] : 0,
                            TauxTva = reader["TauxTva"] != DBNull.Value ? (decimal)reader["TauxTva"] : 0,
                            DateCreate = reader["DateCreate"] != DBNull.Value ? (DateTime)reader["DateCreate"] : null,
                            DateLastUpdate = reader["DateLastUpdate"] != DBNull.Value ? (DateTime)reader["DateLastUpdate"] : null
                        };

                        result.Articles.Add(article);
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération articles catégorie {idCategorie}");
                result.Success = false;
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // MÉTHODES PRIVÉES D'AIDE
        // ════════════════════════════════════════════════════════════════════════════

        private string BuildCatalogueFilterClause(CatalogueArticleFilterRequest filter)
        {
            var conditions = new List<string>();

            if (filter.IdCategorie.HasValue && filter.IdCategorie != Guid.Empty)
                conditions.Add("IdCategorie = @IdCategorie");

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                conditions.Add("(NomArticle LIKE @SearchTerm OR CodeArticle LIKE @SearchTerm OR CodeBarre LIKE @SearchTerm)");

            if (filter.EstPromo.HasValue)
                conditions.Add("EstPromo = @EstPromo");

            if (filter.PrixMin.HasValue)
                conditions.Add("PrixVente >= @PrixMin");

            if (filter.PrixMax.HasValue)
                conditions.Add("PrixVente <= @PrixMax");

            return conditions.Count == 0 ? "" : "AND " + string.Join(" AND ", conditions);
        }

        private void AddCatalogueFilterParameters(SqlCommand cmd, CatalogueArticleFilterRequest filter)
        {
            if (filter.IdCategorie.HasValue && filter.IdCategorie != Guid.Empty)
                cmd.Parameters.AddWithValue("@IdCategorie", filter.IdCategorie.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                cmd.Parameters.AddWithValue("@SearchTerm", $"%{filter.SearchTerm}%");

            if (filter.EstPromo.HasValue)
                cmd.Parameters.AddWithValue("@EstPromo", filter.EstPromo.Value ? 1 : 0);

            if (filter.PrixMin.HasValue)
                cmd.Parameters.AddWithValue("@PrixMin", filter.PrixMin.Value);

            if (filter.PrixMax.HasValue)
                cmd.Parameters.AddWithValue("@PrixMax", filter.PrixMax.Value);
        }

        private string BuildOrderByCategories(string orderBy)
        {
            return orderBy.ToLower() switch
            {
                "nom" => "NomCategorie",
                "articles" => "NombreArticles",
                "prix" => "PrixMoyen",
                _ => "OrdreCategorie"
            };
        }

        private string BuildOrderByArticles(string orderBy)
        {
            return orderBy.ToLower() switch
            {
                "prix" => "PrixVente",
                "categorie" => "NomCategorie",
                "date" => "DateCreate",
                "position" => "Position",
                _ => "NomArticle"
            };
        }

        private ArticleDto MapArticleFromReader(SqlDataReader reader)
        {
            return new ArticleDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                CodeArticle = ReadNullableString(reader, "CodeArticle"),
                Designation = ReadNullableString(reader, "Designation"),
                Description = ReadNullableString(reader, "Description"),
                CodeBarre = ReadNullableString(reader, "CodeBarre"),
                PrixAchat = ReadNullableDecimal(reader, "PrixAchat"),
                PrixVente = ReadNullableDecimal(reader, "PrixVente"),
                PrixExterieur = ReadNullableDecimal(reader, "PrixExterieur"),
                EstPos = ReadNullableBool(reader, "EstPos"),
                Position = ReadNullableInt(reader, "Position"),
                EstExonerer = ReadNullableBool(reader, "EstExonerer"),
                TypeRepas = ReadNullableString(reader, "TypeRepas"),
                Etat = ReadNullableString(reader, "Etat"),
                DatePerenption = ReadNullableDateTime(reader, "DatePerenption"),
                EstStockable = ReadNullableBool(reader, "EstStockable"),
                EstEnStock = ReadNullableBool(reader, "EstEnStock"),
                EstEnPorter = ReadNullableBool(reader, "EstEnPorter"),
                IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                IdCathegorie = ReadNullableGuid(reader, "IdCathegorie"),
                IdType_Repas = ReadNullableGuid(reader, "IdType_Repas"),
                PrixPromo = ReadNullableDecimal(reader, "PrixPromo"),
                EstPromo = ReadNullableBool(reader, "EstPromo"),
                EstComposer = ReadNullableBool(reader, "EstComposer"),
                EstVendableSansComposition = ReadNullableBool(reader, "EstVendableSansComposition"),
                AfficherStockPOS = ReadNullableString(reader, "AfficherStockPOS"),
                TauxTva = ReadNullableDecimal(reader, "TauxTva"),
                Stock = ReadNullableDecimal(reader, "Stock"),
                SeuilAlerte = ReadNullableDecimal(reader, "SeuilAlerte"),
                ImageURL = ReadNullableString(reader, "ImageURL"),
                Statut = ReadNullableBool(reader, "Statut"),
                NomCategorie = ReadNullableString(reader, "NomCategorie"),

                // ✅ Tracking utilisateur
                DateCreate = ReadNullableDateTime(reader, "DateCreate"),
                idCreateUser = ReadNullableGuid(reader, "idCreateUser"),
                NomCreateUser = ReadNullableString(reader, "NomCreateUser"),
                DateLastUpdate = ReadNullableDateTime(reader, "DateLastUpdate"),
                idLastUpdateUser = ReadNullableGuid(reader, "idLastUpdateUser"),
                NomLastUpdateUser = ReadNullableString(reader, "NomLastUpdateUser")
            };
        }

        // private PaginationMetadata CreatePagination(
        //int currentPage, int pageSize, int totalPages, int totalRecords)
        // {
        //     return new PaginationMetadata
        //     {
        //         CurrentPage = currentPage,
        //         PageSize = pageSize,
        //         TotalPages = totalPages,
        //         TotalRecords = totalRecords,
        //         HasPrevious = currentPage > 1,
        //         HasNext = currentPage < totalPages
        //     };
        // }

        // private PaginationMetadata CreateEmptyPagination(int page, int pageSize)
        // {
        //     return new PaginationMetadata
        //     {
        //         CurrentPage = page,
        //         PageSize = pageSize,
        //         TotalPages = 0,
        //         TotalRecords = 0,
        //         HasPrevious = false,
        //         HasNext = false
        //     };
        // }
    }
}