using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Synchronisation
{
    /// <summary>
    /// Synchronisation caisse &lt;-&gt; hub. Memes routes qu'avant (apply, pending, mark-synced, statistics, health),
    /// mais la cle est maintenant propre a CHAQUE entreprise et l'entreprise est deduite de la cle.
    /// La cle se passe de preference dans l'en-tete X-Sync-Key ; l'ancien champ ApiKey (corps / query) reste
    /// accepte le temps de migrer la caisse (une cle dans l'URL finit dans les journaux).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class SyncController : ControllerBase
    {
        private readonly SyncService _syncService;
        private readonly ILogger<SyncController> _logger;

        public SyncController(SyncService syncService, ILogger<SyncController> logger)
        {
            _syncService = syncService;
            _logger = logger;
        }

        private string? LireCle(string? repli) =>
            Request.Headers["X-Sync-Key"].FirstOrDefault() ?? repli;

        private static IActionResult NonAutorise() =>
            new UnauthorizedObjectResult(new { success = false, message = "Cle de synchronisation invalide, revoquee ou entreprise inactive." });

        /// <summary>Applique un changement venant de la caisse sur la base distante.</summary>
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyChange([FromBody] SyncRequest request)
        {
            try
            {
                if (request == null) return BadRequest(new { success = false, message = "Requete vide." });

                var idEntreprise = await _syncService.ResoudreEntrepriseAsync(LireCle(request.ApiKey));
                if (idEntreprise == null)
                {
                    _logger.LogWarning("Sync apply : cle refusee");
                    return NonAutorise();
                }

                // Garde-fou de configuration : la caisse annonce une autre entreprise que celle de sa cle
                if (request.IdEntreprise != Guid.Empty && request.IdEntreprise != idEntreprise.Value)
                    return StatusCode(403, new { success = false, message = "Cette cle n'appartient pas a l'entreprise indiquee." });

                if (string.IsNullOrWhiteSpace(request.TableName) || string.IsNullOrWhiteSpace(request.Operation)
                    || (request.Operation.ToUpperInvariant() != "DELETE" && string.IsNullOrEmpty(request.DataJSON)))
                    return BadRequest(new { success = false, message = "TableName et Operation sont obligatoires, DataJSON aussi sauf pour DELETE." });

                var result = await _syncService.ApplyChangeAsync(idEntreprise.Value, request);

                // RETRY = a reessayer plus tard (503) ; APPLIED / DUPLICATE / CONFLICT / REJECTED = reponse definitive (200)
                if (result.Status == "RETRY") return StatusCode(503, result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'application du changement");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>Applique un LOT de changements (max 100) : un resultat par changement, dans l'ordre d'envoi.</summary>
        [HttpPost("apply-batch")]
        public async Task<IActionResult> ApplyBatch([FromBody] ApplyBatchRequest request)
        {
            try
            {
                if (request?.Changes == null || request.Changes.Count == 0)
                    return BadRequest(new { success = false, message = "Aucun changement." });
                if (request.Changes.Count > 100)
                    return BadRequest(new { success = false, message = "Maximum 100 changements par envoi." });

                var id = await _syncService.ResoudreEntrepriseAsync(LireCle(request.ApiKey));
                if (id == null) { _logger.LogWarning("Sync apply-batch : cle refusee"); return NonAutorise(); }

                if (request.Changes.Any(c => c.IdEntreprise != Guid.Empty && c.IdEntreprise != id.Value))
                    return StatusCode(403, new { success = false, message = "Cette cle n'appartient pas a l'entreprise indiquee." });

                var results = await _syncService.ApplyBatchAsync(id.Value, request.Changes);
                return Ok(new { success = true, count = results.Count, results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur apply-batch");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>Acquitte un LOT de changements hub -> caisse (max 200).</summary>
        [HttpPost("ack-batch")]
        public async Task<IActionResult> AckBatch([FromBody] AckBatchRequest request)
        {
            try
            {
                if (request?.Items == null || request.Items.Count == 0)
                    return BadRequest(new { success = false, message = "Rien a acquitter." });
                if (request.Items.Count > 200)
                    return BadRequest(new { success = false, message = "Maximum 200 acquittements." });

                var id = await _syncService.ResoudreEntrepriseAsync(LireCle(request.ApiKey));
                if (id == null) return NonAutorise();

                var n = await _syncService.MarkBatchAsync(id.Value, request.Items);
                if (n < 0) return StatusCode(500, new { success = false, message = "Erreur lors de la mise a jour" });
                return Ok(new { success = true, updated = n });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur ack-batch");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>Changements du hub a appliquer sur la caisse (l'entreprise vient de la cle).</summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingChanges(
            [FromQuery] Guid idEntreprise,
            [FromQuery] string? apiKey = null,
            [FromQuery] DateTime? since = null,            // ignore : conserve pour compatibilite
            [FromQuery] int maxResults = 100,
            [FromQuery] string direction = "REMOTE_TO_LOCAL") // ignore : seul REMOTE_TO_LOCAL est servi
        {
            try
            {
                var id = await _syncService.ResoudreEntrepriseAsync(LireCle(apiKey));
                if (id == null) return NonAutorise();
                if (idEntreprise != Guid.Empty && idEntreprise != id.Value)
                    return StatusCode(403, new { success = false, message = "Cette cle n'appartient pas a l'entreprise indiquee." });

                maxResults = Math.Clamp(maxResults, 1, 200);

                var result = await _syncService.GetPendingChangesAsync(new GetPendingChangesRequest
                {
                    IdEntreprise = id.Value,
                    MaxResults = maxResults,
                    Direction = "REMOTE_TO_LOCAL",
                    Since = DateTime.MinValue
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recuperation des changements");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>Acquitte (ou refuse) un changement hub -> caisse.</summary>
        [HttpPost("mark-synced")]
        public async Task<IActionResult> MarkAsSynced([FromBody] MarkSyncedRequest request)
        {
            try
            {
                if (request == null) return BadRequest(new { success = false, message = "Requete vide." });

                var id = await _syncService.ResoudreEntrepriseAsync(LireCle(request.ApiKey));
                if (id == null) return NonAutorise();

                var success = await _syncService.MarkAsSyncedAsync(id.Value, request);
                if (!success) return StatusCode(500, new { success = false, message = "Erreur lors de la mise a jour" });

                return Ok(new
                {
                    success = true,
                    message = request.Success ? "Changement marque comme synchronise" : "Echec enregistre, nouvelle tentative programmee"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du marquage");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>Statistiques de l'entreprise de la cle.</summary>
        [HttpGet("statistics/{idEntreprise}")]
        public async Task<IActionResult> GetStatistics(Guid idEntreprise, [FromQuery] string? apiKey = null)
        {
            try
            {
                var id = await _syncService.ResoudreEntrepriseAsync(LireCle(apiKey));
                if (id == null) return NonAutorise();
                if (idEntreprise != id.Value)
                    return StatusCode(403, new { success = false, message = "Cette cle n'appartient pas a l'entreprise indiquee." });

                var stats = await _syncService.GetStatisticsAsync(id.Value);
                return Ok(new { success = true, data = stats, serverTimeUtc = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la recuperation des statistiques");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>Health check (sans donnees sensibles).</summary>
        [HttpGet("health")]
        public IActionResult Health() =>
            Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "3.0.0" });

        /// <summary>Test de la cle ET de la connexion a la base (retourne l'heure du serveur).</summary>
        [HttpGet("db-test")]
        public async Task<IActionResult> DatabaseTest([FromQuery] string? apiKey = null)
        {
            try
            {
                var id = await _syncService.ResoudreEntrepriseAsync(LireCle(apiKey));
                if (id == null) return NonAutorise();
                return Ok(new { success = true, message = "Cle valide, connexion base de donnees OK", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "db-test");
                return StatusCode(500, new { success = false, message = "Erreur de connexion base de donnees" });
            }
        }
    }
}
