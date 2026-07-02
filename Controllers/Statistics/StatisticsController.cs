using Api_BuildTech.Controllers.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Statistics
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly StatisticsService _statisticsService;
        private readonly ILogger<StatisticsController> _logger;

        public StatisticsController(
            StatisticsService statisticsService,
            ILogger<StatisticsController> logger)
        {
            _statisticsService = statisticsService;
            _logger = logger;
        }

        /// <summary>
        /// GET - Dashboard complet (activité + graphique)
        /// </summary>
        /// <param name="period">Période: 7days, 30days, 90days, 1year (défaut: 7days)</param>
        [HttpGet("dashboard")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetDashboard([FromQuery] string period = "7days")
        {
            try
            {

                _logger.LogInformation($"📊 Chargement dashboard pour entreprise , période: {period}");

                var dashboard = await _statisticsService.GetDashboardDataAsync(period);

                return Ok(new
                {
                    success = true,
                    data = dashboard
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetDashboard");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Données d'activité uniquement
        /// </summary>
        [HttpGet("activity")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetActivityData()
        {
            try
            {

                _logger.LogInformation($"📊 Chargement activité entreprise ");

                var dashboard = await _statisticsService.GetDashboardDataAsync();

                return Ok(new
                {
                    success = true,
                    data = dashboard.ActivityData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetActivityData");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Ventes
        /// </summary>
        [HttpGet("sales")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetSalesStats()
        {
            try
            {

                _logger.LogInformation($"📊 Chargement ventes entreprise ");

                var dashboard = await _statisticsService.GetDashboardDataAsync();

                return Ok(new
                {
                    success = true,
                    data = dashboard.ActivityData.Sales
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetSalesStats");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Revenus
        /// </summary>
        [HttpGet("revenue")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetRevenueStats()
        {
            try
            {

                _logger.LogInformation($"💰 Chargement revenus entreprise ");

                var dashboard = await _statisticsService.GetDashboardDataAsync();

                return Ok(new
                {
                    success = true,
                    data = dashboard.ActivityData.Revenue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRevenueStats");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Clients
        /// </summary>
        [HttpGet("customers")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetCustomersStats()
        {
            try
            {

                _logger.LogInformation($"👥 Chargement clients entreprise ");

                var dashboard = await _statisticsService.GetDashboardDataAsync();

                return Ok(new
                {
                    success = true,
                    data = dashboard.ActivityData.Customers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetCustomersStats");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Inventaire
        /// </summary>
        [HttpGet("inventory")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetInventoryStats()
        {
            try
            {

                _logger.LogInformation($"📦 Chargement inventaire entreprise ");

                var dashboard = await _statisticsService.GetDashboardDataAsync();

                return Ok(new
                {
                    success = true,
                    data = dashboard.ActivityData.Inventory
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetInventoryStats");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Données du graphique de ventes
        /// </summary>
        /// <param name="period">Période: 7days, 30days, 90days, 1year</param>
        [HttpGet("chart")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetChartData([FromQuery] string period = "7days")
        {
            try
            {

                _logger.LogInformation($"📈 Chargement graphique entreprise , période: {period}");

                var dashboard = await _statisticsService.GetDashboardDataAsync(period);

                return Ok(new
                {
                    success = true,
                    data = dashboard.ChartData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetChartData");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupérer l'IdEntreprise depuis le token JWT
        /// </summary>
        private int GetIdEntreprise()
        {
            var idEntrepriseStr = User.FindFirst("IdEntreprise")?.Value;
            if (int.TryParse(idEntrepriseStr, out var idEntreprise))
            {
                return idEntreprise;
            }
            throw new UnauthorizedAccessException("IdEntreprise non trouvé dans le token");
        }
    }
}