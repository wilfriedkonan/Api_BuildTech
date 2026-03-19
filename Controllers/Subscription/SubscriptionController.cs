using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Subscription
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly SubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            SubscriptionService subscriptionService,
            ILogger<SubscriptionController> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>
        /// Valide l'abonnement d'une entreprise et retourne un token JWT
        /// </summary>
        /// <param name="idEntreprise">ID de l'entreprise</param>
        /// <param name="apiKey">Clé API</param>
        [HttpGet("validate/{idEntreprise}")]
        public async Task<IActionResult> ValidateSubscription(
            Guid idEntreprise,
            [FromQuery] string apiKey)
        {
            try
            {
                // Validation API Key (pour les clients)
                if (string.IsNullOrEmpty(apiKey) || !_subscriptionService.ValidateApiKey(apiKey))
                {
                    return Unauthorized(new { success = false, message = "Clé API invalide" });
                }

                var result = await _subscriptionService.ValidateSubscriptionAsync(idEntreprise);

                if (result.IsValid)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(403, result); // Forbidden
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur validation abonnement {idEntreprise}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Valide une opération critique (ajout user, création facture, etc.)
        /// </summary>
        [HttpPost("validate-operation")]
        public async Task<IActionResult> ValidateOperation([FromBody] ValidateOperationRequest request)
        {
            try
            {
                var result = await _subscriptionService.ValidateOperationAsync(request);

                if (result.IsAllowed)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(403, result); // Forbidden
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur validation opération");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Récupère les limites de l'abonnement
        /// </summary>
        [HttpGet("limits/{idEntreprise}")]
        [Authorize] // Nécessite JWT
        public async Task<IActionResult> GetLimits(Guid idEntreprise)
        {
            try
            {
                var validation = await _subscriptionService.ValidateSubscriptionAsync(idEntreprise);

                if (!validation.IsValid)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        message = validation.Message
                    });
                }

                return Ok(new
                {
                    success = true,
                    limits = validation.Limits
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération limites {idEntreprise}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Récupère l'usage actuel de l'abonnement
        /// </summary>
        [HttpGet("usage/{idEntreprise}")]
        [Authorize] // Nécessite JWT
        public async Task<IActionResult> GetUsage(Guid idEntreprise)
        {
            try
            {
                var usage = await _subscriptionService.GetSubscriptionUsageAsync(idEntreprise);

                return Ok(new
                {
                    success = true,
                    usage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération usage {idEntreprise}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }
    }
}