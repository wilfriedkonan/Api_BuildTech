using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Articles
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ArticlesController : ControllerBase
    {
        private readonly ArticlesService _articlesService;
        private readonly ILogger<ArticlesController> _logger;

        public ArticlesController(
            ArticlesService articlesService,
            ILogger<ArticlesController> logger)
        {
            _articlesService = articlesService;
            _logger = logger;
        }

        /// <summary>
        /// GET ALL - Récupère tous les articles
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _articlesService.GetAllAsync();

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalStockables = result.TotalStockables,
                    totalEnAlerte = result.TotalEnAlerte,
                    data = result.Articles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
        [HttpGet("list")]
        public async Task<IActionResult> GetAllWithPagination(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? searchTerm = null,
    [FromQuery] Guid? idCategorie = null,
    [FromQuery] bool? estStockable = null,
    [FromQuery] bool? estVisibleCatalogue = null,
    [FromQuery] bool? estPos = null,
    [FromQuery] bool? estPromo = null,
    [FromQuery] string? etat = null,
    [FromQuery] decimal? prixVenteMin = null,
    [FromQuery] decimal? prixVenteMax = null,
    [FromQuery] string orderBy = "designation",
    [FromQuery] string orderDirection = "asc")
        {
            try
            {
                // Validation pagination
                if (page < 1)
                    return BadRequest(new { success = false, message = "Le numéro de page doit être >= 1" });

                if (pageSize < 1 || pageSize > 100)
                    return BadRequest(new { success = false, message = "pageSize doit être entre 1 et 100" });

                // Créer le filtre
                var filter = new GetArticlesFilterRequest
                {
                    Page = page,
                    PageSize = pageSize,
                    SearchTerm = searchTerm,
                    IdCathegorie = idCategorie,
                    EstStockable = estStockable,
                    EstVisibleCatalogue = estVisibleCatalogue,
                    EstPos = estPos,
                    EstPromo = estPromo,
                    Etat = etat,
                    PrixVenteMin = prixVenteMin,
                    PrixVenteMax = prixVenteMax,
                    OrderBy = orderBy,
                    OrderDirection = orderDirection
                };

                // Récupérer les articles
                var result = await _articlesService.GetAllWithPaginationAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalStockables = result.TotalStockables,
                    totalEnAlerte = result.TotalEnAlerte,
                    pagination = result.Pagination,
                    data = result.Articles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles avec pagination");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
        /// <summary>
        /// GET BY ID - Récupère un article par son ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var article = await _articlesService.GetByIdAsync(id);

                if (article == null)
                    return NotFound(new { success = false, message = "Article introuvable" });

                return Ok(new { success = true, data = article });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET BY CODE BARRE - Récupère un article par son code barre
        /// </summary>
        [HttpGet("codebarre/{codeBarre}")]
        public async Task<IActionResult> GetByCodeBarre(string codeBarre)
        {
            try
            {
                var article = await _articlesService.GetByCodeBarreAsync(codeBarre);

                if (article == null)
                    return NotFound(new { success = false, message = "Article introuvable" });

                return Ok(new { success = true, data = article });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération article code barre {codeBarre}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET BY CATEGORIE - Récupère tous les articles d'une catégorie
        /// </summary>
        [HttpGet("categorie/{idCategorie}")]
        public async Task<IActionResult> GetByCategorie(Guid idCategorie)
        {
            try
            {
                var result = await _articlesService.GetByCategorieAsync(idCategorie);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalStockables = result.TotalStockables,
                    totalEnAlerte = result.TotalEnAlerte,
                    data = result.Articles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération articles catégorie {idCategorie}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET POS - Récupère tous les articles POS
        /// </summary>
        [HttpGet("pos")]
        public async Task<IActionResult> GetPosArticles()
        {
            try
            {
                var result = await _articlesService.GetPosArticlesAsync();

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalStockables = result.TotalStockables,
                    totalEnAlerte = result.TotalEnAlerte,
                    data = result.Articles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles POS");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET PROMO - Récupère tous les articles en promo
        /// </summary>
        [HttpGet("promo")]
        public async Task<IActionResult> GetPromoArticles()
        {
            try
            {
                var result = await _articlesService.GetPromoArticlesAsync();

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalStockables = result.TotalStockables,
                    totalEnAlerte = result.TotalEnAlerte,
                    data = result.Articles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles en promo");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET STOCK - Récupère tous les articles stockables
        /// </summary>
        // GET STOCK
        //[HttpGet("stock")]
        //public async Task<IActionResult> GetStockArticles(
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20)
        //{
        //    var result = await _articlesService.GetStockArticlesAsync(page, pageSize);
        //    return Ok(new
        //    {
        //        success = result.Success,
        //        total = result.Total,
        //        pagination = result.Pagination,
        //        data = result.Articles
        //    });
        //}

        /// <summary>
        /// GET STOCK ACTUEL - Calcule le stock actuel d'un article
        /// </summary>
        [HttpGet("{id}/stock")]
        public async Task<IActionResult> GetStockActuel(Guid id)
        {
            try
            {
                var article = await _articlesService.GetByIdAsync(id);

                if (article == null)
                    return NotFound(new { success = false, message = "Article introuvable" });

                if (article.EstStockable != true)
                    return BadRequest(new { success = false, message = "Cet article n'est pas stockable" });

                var stockActuel = await _articlesService.CalculateStockActuelAsync(id);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        idArticle = id,
                        designation = article.Designation,
                        Stock = article.Stock ?? 0,
                        stockActuel = stockActuel,
                        seuilAlerte = article.SeuilAlerte ?? 0,
                        alerteStock = article.SeuilAlerte.HasValue && stockActuel < article.SeuilAlerte.Value
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur calcul stock article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// POST - Crée un nouvel article
        /// </summary>
        [HttpPost]
        //[Authorize(Roles = "Owner,Manager,SuperAdmin")]
        [Authorize]

        public async Task<IActionResult> Create([FromBody] CreateArticleRequest request)
        {
            try
            {
                // Validations
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                if (request.PrixVente < 0)
                    return BadRequest(new { success = false, message = "Prix de vente invalide" });

                if (request.Stock < 0)
                    return BadRequest(new { success = false, message = "Stock initial ne peut pas être négatif" });

                // Créer l'article
                var article = await _articlesService.CreateAsync(request);

                if (article == null)
                    return StatusCode(500, new { success = false, message = "Erreur création article" });

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = article.Id },
                    new
                    {
                        success = true,
                        message = request.Stock > 0
                            ? $"Article créé avec succès. Stock initial de {request.Stock} unités enregistré."
                            : "Article créé avec succès",
                        data = article
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création article");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// PUT - Met à jour un article
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateArticleRequest request)
        {
            try
            {
                var article = await _articlesService.UpdateAsync(id, request);

                if (article == null)
                    return NotFound(new { success = false, message = "Article introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Article mis à jour avec succès",
                    data = article
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// DELETE - Supprime un article (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _articlesService.DeleteAsync(id);

                if (!success)
                    return NotFound(new { success = false, message = "Article introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Article supprimé avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET ALERTES - Récupère les articles en alerte de stock
        /// </summary>
        //[HttpGet("alertes")]
        //public async Task<IActionResult> GetArticlesEnAlerte()
        //{
        //    try
        //    {
        //        var result = await _articlesService.GetStockArticlesAsync();

        //        var articlesEnAlerte = result.Articles
        //            .Where(a => a.AlerteStock)
        //            .OrderBy(a => a.StockActuel)
        //            .ToList();

        //        return Ok(new
        //        {
        //            success = true,
        //            total = articlesEnAlerte.Count,
        //            data = articlesEnAlerte
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération alertes stock");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}

        ///// <summary>
        ///// GET RUPTURE - Récupère les articles en rupture de stock
        ///// </summary>
        //[HttpGet("rupture")]
        //public async Task<IActionResult> GetArticlesEnRupture()
        //{
        //    try
        //    {
        //        var result = await _articlesService.GetStockArticlesAsync();

        //        var articlesEnRupture = result.Articles
        //            .Where(a => a.StockActuel <= 0)
        //            .OrderBy(a => a.Designation)
        //            .ToList();

        //        return Ok(new
        //        {
        //            success = true,
        //            total = articlesEnRupture.Count,
        //            data = articlesEnRupture
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération ruptures stock");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}

        ///// <summary>
        ///// GET STATISTIQUES - Statistiques globales des articles
        ///// </summary>
        //[HttpGet("statistiques")]
        //public async Task<IActionResult> GetStatistiques()
        //{
        //    try
        //    {
        //        var result = await _articlesService.GetAllAsync();

        //        var stats = new
        //        {
        //            totalArticles = result.Total,
        //            totalActifs = result.Articles.Count(a => a.Etat == "Actif"),
        //            totalInactifs = result.Articles.Count(a => a.Etat != "Actif"),
        //            totalStockables = result.TotalStockables,
        //            totalEnAlerte = result.TotalEnAlerte,
        //            totalEnRupture = result.Articles.Count(a => a.EstStockable == true && a.StockActuel <= 0),
        //            totalPOS = result.Articles.Count(a => a.EstPos == true),
        //            totalPromo = result.Articles.Count(a => a.EstPromo == true),
        //            valeurStockTotal = result.Articles
        //                .Where(a => a.EstStockable == true)
        //                .Sum(a => a.StockActuel * (a.PrixAchat ?? 0)),
        //            prixMoyenVente = result.Articles.Any()
        //                ? result.Articles.Average(a => a.PrixVente ?? 0)
        //                : 0,
        //            prixMoyenAchat = result.Articles.Any()
        //                ? result.Articles.Average(a => a.PrixAchat ?? 0)
        //                : 0
        //        };

        //        return Ok(new
        //        {
        //            success = true,
        //            data = stats
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération statistiques");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}


        ///// <summary>
        ///// GET STOCK VIEW - Récupère les stocks depuis la vue avec pagination et filtres
        ///// </summary>
        //[HttpGet("stock/view")]
        //public async Task<IActionResult> GetStockArticlesFromView(
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20,
        //    [FromQuery] string? statutStock = null,
        //    [FromQuery] string? designationCategorie = null,
        //    [FromQuery] string? searchTerm = null,
        //    [FromQuery] decimal? stockMin = null,
        //    [FromQuery] decimal? stockMax = null,
        //    [FromQuery] bool? alertesOnly = null,
        //    [FromQuery] string orderBy = "designation",
        //    [FromQuery] string orderDirection = "asc")
        //{
        //    try
        //    {
        //        var filter = new StockArticleFilterRequest
        //        {
        //            StatutStock = statutStock,
        //            DesignationCategorie = designationCategorie,
        //            SearchTerm = searchTerm,
        //            StockMin = stockMin,
        //            StockMax = stockMax,
        //            AlertesOnly = alertesOnly,
        //            OrderBy = orderBy,
        //            OrderDirection = orderDirection
        //        };

        //        var result = await _articlesService.GetStockArticlesFromViewAsync(filter, page, pageSize);

        //        return Ok(new
        //        {
        //            success = result.Success,
        //            total = result.Total,
        //            totalEnStock = result.TotalEnStock,
        //            totalEnAlerte = result.TotalEnAlerte,
        //            totalEnRupture = result.TotalEnRupture,
        //            pagination = result.Pagination,
        //            data = result.Stocks
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération stocks vue");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}

        ///// <summary>
        ///// GET STOCK STATISTICS - Statistiques globales de stock
        ///// </summary>
        //[HttpGet("stock/statistics")]
        //public async Task<IActionResult> GetStockStatistics()
        //{
        //    try
        //    {
        //        var stats = await _articlesService.GetStockStatisticsAsync();

        //        return Ok(new
        //        {
        //            success = true,
        //            data = stats
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération statistiques stock");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}

        ///// <summary>
        ///// GET STOCK ALERTS - Uniquement les articles en alerte ou rupture
        ///// </summary>
        //[HttpGet("stock/alerts")]
        //public async Task<IActionResult> GetStockAlerts(
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20)
        //{
        //    try
        //    {
        //        var filter = new StockArticleFilterRequest
        //        {
        //            AlertesOnly = true,
        //            OrderBy = "statut",
        //            OrderDirection = "desc"
        //        };

        //        var result = await _articlesService.GetStockArticlesFromViewAsync(filter, page, pageSize);

        //        return Ok(new
        //        {
        //            success = result.Success,
        //            total = result.Total,
        //            totalAlerte = result.TotalEnAlerte,
        //            totalRupture = result.TotalEnRupture,
        //            pagination = result.Pagination,
        //            data = result.Stocks
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur récupération alertes stock");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}

        ///// <summary>
        ///// GET STOCK BY STATUS - Articles par statut spécifique
        ///// </summary>
        //[HttpGet("stock/by-status/{status}")]
        //public async Task<IActionResult> GetStockByStatus(
        //    string status,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20)
        //{
        //    try
        //    {
        //        // Valider le statut
        //        var validStatuses = new[] { "En stock", "Alerte", "Rupture" };
        //        if (!validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = $"Statut invalide. Valeurs autorisées: {string.Join(", ", validStatuses)}"
        //            });
        //        }

        //        var filter = new StockArticleFilterRequest
        //        {
        //            StatutStock = status,
        //            OrderBy = "stock",
        //            OrderDirection = "asc"
        //        };

        //        var result = await _articlesService.GetStockArticlesFromViewAsync(filter, page, pageSize);

        //        return Ok(new
        //        {
        //            success = result.Success,
        //            status = status,
        //            total = result.Total,
        //            pagination = result.Pagination,
        //            data = result.Stocks
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Erreur récupération stock par statut {status}");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}

        ///// <summary>
        ///// GET STOCK BY CATEGORY - Articles par catégorie
        ///// </summary>
        //[HttpGet("stock/by-category/{categorie}")]
        //public async Task<IActionResult> GetStockByCategory(
        //    string categorie,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20)
        //{
        //    try
        //    {
        //        var filter = new StockArticleFilterRequest
        //        {
        //            DesignationCategorie = categorie,
        //            OrderBy = "stock",
        //            OrderDirection = "asc"
        //        };

        //        var result = await _articlesService.GetStockArticlesFromViewAsync(filter, page, pageSize);

        //        return Ok(new
        //        {
        //            success = result.Success,
        //            categorie = categorie,
        //            total = result.Total,
        //            totalEnAlerte = result.TotalEnAlerte,
        //            totalEnRupture = result.TotalEnRupture,
        //            pagination = result.Pagination,
        //            data = result.Stocks
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Erreur récupération stock catégorie {categorie}");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}

        ///// <summary>
        ///// SEARCH STOCK - Recherche d'articles par terme
        ///// </summary>
        //[HttpGet("stock/search")]
        //public async Task<IActionResult> SearchStock(
        //    [FromQuery] string term,
        //    [FromQuery] int page = 1,
        //    [FromQuery] int pageSize = 20)
        //{
        //    try
        //    {
        //        if (string.IsNullOrWhiteSpace(term))
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "Le terme de recherche est requis"
        //            });
        //        }

        //        var filter = new StockArticleFilterRequest
        //        {
        //            SearchTerm = term,
        //            OrderBy = "designation",
        //            OrderDirection = "asc"
        //        };

        //        var result = await _articlesService.GetStockArticlesFromViewAsync(filter, page, pageSize);

        //        return Ok(new
        //        {
        //            success = result.Success,
        //            searchTerm = term,
        //            total = result.Total,
        //            pagination = result.Pagination,
        //            data = result.Stocks
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Erreur recherche stock: {term}");
        //        return StatusCode(500, new { success = false, message = "Erreur serveur" });
        //    }
        //}





        // ════════════════════════════════════════════════════════════════════════════
        // ENDPOINT 1: CATÉGORIES AVEC STATS
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET - Récupère les catégories avec leurs articles visibles (PUBLIC)
        /// Utilise la vue V_CATALOGUE_CATEGORIES pour performance optimale
        /// </summary>
        /// <example>
        /// GET /api/catalogue/public/categories
        /// GET /api/catalogue/public/categories?page=1&pageSize=20&orderBy=nom&orderDirection=asc
        /// </example>
        [HttpGet("public/categories")]
        [AllowAnonymous]  // ← PAS D'AUTHENTIFICATION
        public async Task<IActionResult> GetCategoriesPublic(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string orderBy = "ordre",    // ordre, nom, articles
            [FromQuery] string orderDirection = "asc")  // asc, desc
        {
            try
            {
                // Validation
                if (page < 1)
                    return BadRequest(new { success = false, message = "Page >= 1" });

                if (pageSize < 1 || pageSize > 100)
                    return BadRequest(new { success = false, message = "pageSize entre 1-100" });

                // Récupérer les catégories
                var result = await _articlesService.GetCategoriesWithArticlesAsync(
                    page, pageSize, orderBy, orderDirection);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Categories
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories catalogue");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ENDPOINT 2: ARTICLES VISIBLES AU CATALOGUE
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET - Récupère les articles visibles au catalogue (PUBLIC)
        /// Utilise la vue V_CATALOGUE_ARTICLES pour performance optimale
        /// </summary>
        /// <example>
        /// GET /api/catalogue/public/articles
        /// GET /api/catalogue/public/articles?page=1&pageSize=50&idCategorie=xxx&orderBy=prix&orderDirection=asc
        /// GET /api/catalogue/public/articles?searchTerm=produit&estPromo=true
        /// </example>
        [HttpGet("public/articles")]
        [AllowAnonymous]  // ← PAS D'AUTHENTIFICATION
        public async Task<IActionResult> GetArticlesPublic(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? idCategorie = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool? estPromo = null,
            [FromQuery] decimal? prixMin = null,
            [FromQuery] decimal? prixMax = null,
            [FromQuery] string orderBy = "designation",  // designation, prix, categorie, date
            [FromQuery] string orderDirection = "asc")
        {
            try
            {
                // Validation
                if (page < 1)
                    return BadRequest(new { success = false, message = "Page >= 1" });

                if (pageSize < 1 || pageSize > 100)
                    return BadRequest(new { success = false, message = "pageSize entre 1-100" });

                // Créer le filtre
                var filter = new CatalogueArticleFilterRequest
                {
                    Page = page,
                    PageSize = pageSize,
                    IdCategorie = idCategorie,
                    SearchTerm = searchTerm,
                    EstPromo = estPromo,
                    PrixMin = prixMin,
                    PrixMax = prixMax,
                    OrderBy = orderBy,
                    OrderDirection = orderDirection
                };

                // Récupérer les articles
                var result = await _articlesService.GetArticlesPublicAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Articles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles catalogue");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ENDPOINT 3: ARTICLE PAR ID (PUBLIC)
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET - Récupère un article spécifique du catalogue (PUBLIC)
        /// </summary>
        /// <example>
        /// GET /api/catalogue/public/articles/550e8400-e29b-41d4-a716-446655440000
        /// </example>
        [HttpGet("public/articles/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticlePublic(Guid id)
        {
            try
            {
                var article = await _articlesService.GetArticlePublicAsync(id);

                if (article == null)
                    return NotFound(new { success = false, message = "Article non trouvé" });

                return Ok(new
                {
                    success = true,
                    data = article
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ENDPOINT 4: ARTICLES PAR CATÉGORIE (PUBLIC)
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET - Récupère tous les articles d'une catégorie (PUBLIC)
        /// </summary>
        /// <example>
        /// GET /api/catalogue/public/categories/550e8400-e29b-41d4-a716-446655440000/articles
        /// GET /api/catalogue/public/categories/550e8400-e29b-41d4-a716-446655440000/articles?orderBy=prix&orderDirection=desc
        /// </example>
        [HttpGet("public/categories/{idCategorie}/articles")]
        [AllowAnonymous]
        public async Task<IActionResult> GetArticlesByCategoryPublic(
            Guid idCategorie,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string orderBy = "designation",
            [FromQuery] string orderDirection = "asc")
        {
            try
            {
                if (page < 1 || pageSize < 1 || pageSize > 100)
                    return BadRequest(new { success = false, message = "Paramètres invalides" });

                var result = await _articlesService.GetArticlesByCategoryAsync(
                    idCategorie, page, pageSize, orderBy, orderDirection);

                if (!result.Success)
                    return NotFound(new { success = false, message = "Catégorie non trouvée" });

                return Ok(new
                {
                    success = result.Success,
                    categoryName = result.CategoryName,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Articles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération articles catégorie {idCategorie}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}
