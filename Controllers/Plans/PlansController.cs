using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Plans
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlansController : ControllerBase
    {
        private readonly PlansService _plansService;
        private readonly ILogger<PlansController> _logger;

        public PlansController(
            PlansService plansService,
            ILogger<PlansController> logger)
        {
            _plansService = plansService;
            _logger = logger;
        }

        /// <summary>
        /// Récupère tous les plans (authentification non requise pour consultation publique)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _plansService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupère uniquement les plans actifs (pour l'inscription)
        /// </summary>
        [HttpGet("actifs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActifs()
        {
            try
            {
                var result = await _plansService.GetActifsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans actifs");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupère les détails d'un plan spécifique
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var plan = await _plansService.GetByIdAsync(id);

                if (plan == null)
                    return NotFound(new { success = false, message = "Plan introuvable" });

                return Ok(new { success = true, data = plan });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération plan {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Compare les plans côte à côte (pour affichage pricing)
        /// </summary>
        [HttpGet("compare")]
        [AllowAnonymous]
        public async Task<IActionResult> ComparePlans()
        {
            try
            {
                var result = await _plansService.GetComparisonAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur comparaison plans");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupère le plan par nom
        /// </summary>
        [HttpGet("by-name/{name}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByName(string name)
        {
            try
            {
                var plan = await _plansService.GetByNameAsync(name);

                if (plan == null)
                    return NotFound(new { success = false, message = "Plan introuvable" });

                return Ok(new { success = true, data = plan });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération plan {name}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupère les plans avec leurs caractéristiques formatées
        /// </summary>
        [HttpGet("features")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlansWithFeatures()
        {
            try
            {
                var result = await _plansService.GetPlansWithFeaturesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans avec features");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}