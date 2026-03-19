using Microsoft.AspNetCore.Mvc;
using Api_BuildTech.Services;

namespace Api_BuildTech.Controllers.Registration
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationController : ControllerBase
    {
        private readonly RegistrationOrchestrator _orchestrator;
        private readonly ILogger<RegistrationController> _logger;

        public RegistrationController(
            RegistrationOrchestrator orchestrator,
            ILogger<RegistrationController> logger)
        {
            _orchestrator = orchestrator;
            _logger = logger;
        }

        /// <summary>
        /// Inscription complète d'un nouveau tenant
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
        {
            try
            {
                //Validation des données

                // Propriétaire
                if (string.IsNullOrEmpty(request.Nom))
                    return BadRequest(new { success = false, message = "Non du propriétaire requis" });

                if (string.IsNullOrEmpty(request.Email))
                    return BadRequest(new { success = false, message = "Email requis" });

                if (string.IsNullOrEmpty(request.Telephone))
                    return BadRequest(new { success = false, message = "Le numero de telephone est requis" });

                // Entreprise
                if (string.IsNullOrEmpty(request.EntrepriseName))
                    return BadRequest(new { success = false, message = "Nom d'entreprise requis" });

                // Souscription
                if (request.IdPlan == Guid.Empty)
                    return BadRequest(new { success = false, message = "Plan d'abonnement requis" });

                if (request.SubscriptionDurationMonths <= 0)
                    return BadRequest(new { success = false, message = "Le nombre de mois est requis" });

                // Orchestration de l'inscription
                var result = await _orchestrator.RegisterNewTenantAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation($"✅ Inscription réussie: {result.CodeEntreprise}");
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning($"⚠️ Échec inscription: {result.Message}");
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'inscription");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur lors de l'inscription",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Vérifier la disponibilité d'un email
        /// </summary>
        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmailAvailability([FromQuery] string email)
        {
            try
            {
                // TODO: Implémenter la vérification
                return Ok(new { available = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur vérification email: {email}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupérer les plans disponibles
        /// </summary>
        [HttpGet("plans")]
        public async Task<IActionResult> GetAvailablePlans()
        {
            try
            {
                // TODO: Implémenter la récupération des plans
                return Ok(new { success = true, plans = new List<object>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}