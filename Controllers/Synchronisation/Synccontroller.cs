using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Synchronisation
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly SyncService _syncService;
        private readonly ILogger<SyncController> _logger;

        public SyncController(SyncService syncService, ILogger<SyncController> logger)
        {
            _syncService = syncService;
            _logger = logger;
        }

        /// <summary>
        /// Applique un changement sur la base distante
        /// </summary>
        /// <param name="request">Détails du changement à appliquer</param>
        /// <returns>Résultat de l'opération</returns>
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyChange([FromBody] SyncRequest request)
        {
            try
            {
                _logger.LogInformation($"Réception changement: {request.TableName} ({request.Operation})");
                // Validation de la clé API
                if (!_syncService.ValidateApiKey(request.ApiKey))
                {
                    _logger.LogWarning("Tentative d'accès avec clé API invalide");
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Clé API invalide"
                    });
                }

                // Validation de l'entreprise
                if (!await _syncService.ValidateEntrepriseAsync(request.IdEntreprise))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Entreprise introuvable"
                    });
                }

                // Validation des données
                if (string.IsNullOrEmpty(request.TableName) ||
                    string.IsNullOrEmpty(request.Operation) ||
                    string.IsNullOrEmpty(request.DataJSON))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "TableName, Operation et DataJSON sont obligatoires"
                    });
                }

                // Appliquer le changement
                var result = await _syncService.ApplyChangeAsync(request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'application du changement");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Récupère les changements en attente pour une entreprise
        /// </summary>
        /// <param name="idEntreprise">ID de l'entreprise</param>
        /// <param name="apiKey">Clé API</param>
        /// <param name="since">Date depuis laquelle récupérer les changements</param>
        /// <param name="maxResults">Nombre maximum de résultats</param>
        /// <param name="direction">Direction de synchronisation</param>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingChanges(
            [FromQuery] Guid idEntreprise,
            [FromQuery] string apiKey,
            [FromQuery] DateTime? since = null,
            [FromQuery] int maxResults = 100,
            [FromQuery] string direction = "REMOTE_TO_LOCAL")
        {
            try
            {
                //    // Validation de la clé API
                if (!_syncService.ValidateApiKey(apiKey))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Clé API invalide"
                    });
                }

                // Validation de l'entreprise
                if (!await _syncService.ValidateEntrepriseAsync(idEntreprise))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Entreprise introuvable"
                    });
                }

                // Limiter le nombre de résultats
                if (maxResults > 500)
                {
                    maxResults = 500;
                }

                var request = new GetPendingChangesRequest
                {
                    ApiKey = apiKey,
                    IdEntreprise = idEntreprise,
                    Since = since ?? DateTime.UtcNow.AddDays(-30), // Par défaut, 30 derniers jours
                    MaxResults = maxResults,
                    Direction = direction
                };

                var result = await _syncService.GetPendingChangesAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des changements");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Marque un changement comme synchronisé ou enregistre l'erreur
        /// </summary>
        [HttpPost("mark-synced")]
        public async Task<IActionResult> MarkAsSynced([FromBody] MarkSyncedRequest request)
        {
            try
            {
                // Validation de la clé API
                if (!_syncService.ValidateApiKey(request.ApiKey))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Clé API invalide"
                    });
                }

                var success = await _syncService.MarkAsSyncedAsync(request);

                if (success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = request.Success
                            ? "Changement marqué comme synchronisé"
                            : "Échec enregistré, retry programmé"
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Erreur lors de la mise à jour"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Récupère les statistiques de synchronisation pour une entreprise
        /// </summary>
        [HttpGet("statistics/{idEntreprise}")]
        public async Task<IActionResult> GetStatistics(
            Guid idEntreprise,
            [FromQuery] string apiKey)
        {
            try
            {
                // Validation de la clé API
                if (!_syncService.ValidateApiKey(apiKey))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Clé API invalide"
                    });
                }

                var stats = await _syncService.GetStatisticsAsync(idEntreprise);

                return Ok(new
                {
                    success = true,
                    data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Health check de l'API
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        /// <summary>
        /// Test de connectivité avec la base de données
        /// </summary>
        [HttpGet("db-test")]
        public async Task<IActionResult> DatabaseTest([FromQuery] string apiKey)
        {
            try
            {
                // Validation de la clé API
                if (!_syncService.ValidateApiKey(apiKey))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Clé API invalide"
                    });
                }

                // Tester une entreprise au hasard
                var testGuid = Guid.NewGuid();
                var exists = await _syncService.ValidateEntrepriseAsync(testGuid);

                return Ok(new
                {
                    success = true,
                    message = "Connexion base de données OK",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur de connexion base de données",
                    error = ex.Message
                });
            }
        }
    }
}