using Api_BuildTech.Controllers.ArticleStock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.ArticleStock
{

    [ApiController]
    [Route("api/stock")]
    [Authorize]
    public class ArticleStockController : ControllerBase
    {
        private readonly ArticleStockService _stockService;
        private readonly ILogger<ArticleStockController> _logger;

        public ArticleStockController(
            ArticleStockService stockService,
            ILogger<ArticleStockController> logger)
        {
            _stockService = stockService;
            _logger = logger;
        }

        /// <summary>
        /// GET ALL - Liste complète des stocks avec filtres et pagination
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? statutStock = null,
            [FromQuery] string? etat = null,
            [FromQuery] string? designationCategorie = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] decimal? stockMin = null,
            [FromQuery] decimal? stockMax = null,
            [FromQuery] bool? alertesOnly = null,
            [FromQuery] bool? estPosOnly = null,
            [FromQuery] bool? estStockableOnly = null,
            [FromQuery] bool? estPromoOnly = null,
            [FromQuery] string orderBy = "designation",
            [FromQuery] string orderDirection = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var filter = new StockArticleFilterRequest
                {
                    StatutStock = statutStock,
                    Etat = etat,
                    DesignationCategorie = designationCategorie,
                    SearchTerm = searchTerm,
                    StockMin = stockMin,
                    StockMax = stockMax,
                    AlertesOnly = alertesOnly,
                    EstPosOnly = estPosOnly,
                    EstStockableOnly = estStockableOnly,
                    EstPromoOnly = estPromoOnly,
                    OrderBy = orderBy,
                    OrderDirection = orderDirection,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _stockService.GetAllAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalEnStock = result.TotalEnStock,
                    totalEnAlerte = result.TotalEnAlerte,
                    totalEnRupture = result.TotalEnRupture,
                    valeurStockTotal = result.ValeurStockTotal,
                    pagination = result.Pagination,
                    data = result.Stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération stocks");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET STATISTICS - Statistiques globales de stock
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var stats = await _stockService.GetStatisticsAsync();

                return Ok(new
                {
                    success = true,
                    data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération statistiques stock");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET ALERTS - Articles en alerte uniquement
        /// </summary>
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var filter = new StockArticleFilterRequest
                {
                    AlertesOnly = true,
                    OrderBy = "stock",
                    OrderDirection = "asc",
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _stockService.GetAllAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    totalEnAlerte = result.TotalEnAlerte,
                    totalEnRupture = result.TotalEnRupture,
                    pagination = result.Pagination,
                    data = result.Stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération alertes stock");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET BY STATUS - Articles par statut
        /// </summary>
        [HttpGet("by-status/{status}")]
        public async Task<IActionResult> GetByStatus(
            string status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // Valider statut
                if (status != "En stock" && status != "Alerte" && status != "Rupture")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Statut invalide. Valeurs acceptées: 'En stock', 'Alerte', 'Rupture'"
                    });
                }

                var filter = new StockArticleFilterRequest
                {
                    StatutStock = status,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _stockService.GetAllAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    status,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération stocks par statut {status}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET BY CATEGORY - Articles par catégorie
        /// </summary>
        [HttpGet("by-category/{categorie}")]
        public async Task<IActionResult> GetByCategory(
            string categorie,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var filter = new StockArticleFilterRequest
                {
                    DesignationCategorie = categorie,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _stockService.GetAllAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    categorie,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération stocks catégorie {categorie}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET SEARCH - Recherche d'articles
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? term,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrEmpty(term))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Terme de recherche requis"
                    });
                }

                var filter = new StockArticleFilterRequest
                {
                    SearchTerm = term,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _stockService.GetAllAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    searchTerm = term,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche stocks");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET POS ONLY - Articles POS uniquement
        /// </summary>
        [HttpGet("pos")]
        public async Task<IActionResult> GetPosOnly(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var filter = new StockArticleFilterRequest
                {
                    EstPosOnly = true,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _stockService.GetAllAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles POS");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET PROMO ONLY - Articles en promo uniquement
        /// </summary>
        [HttpGet("promo")]
        public async Task<IActionResult> GetPromoOnly(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var filter = new StockArticleFilterRequest
                {
                    EstPromoOnly = true,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _stockService.GetAllAsync(filter);

                return Ok(new
                {
                    success = result.Success,
                    total = result.Total,
                    pagination = result.Pagination,
                    data = result.Stocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération articles promo");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET BY ID - Stock d'un article spécifique
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var stock = await _stockService.GetByIdAsync(id);

                if (stock == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Article introuvable"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = stock
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération stock article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}