using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Paiments
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaimentsController : ControllerBase
    {
        private readonly PaimentsService _paimentsService;
        private readonly ILogger<PaimentsController> _logger;

        public PaimentsController(
            PaimentsService paimentsService,
            ILogger<PaimentsController> logger)
        {
            _paimentsService = paimentsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? dateDebut = null, [FromQuery] DateTime? dateFin = null)
        {
            try
            {
                var result = await _paimentsService.GetAllAsync(dateDebut, dateFin);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération paiements");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var paiement = await _paimentsService.GetByIdAsync(id);
                if (paiement == null)
                    return NotFound(new { success = false, message = "Paiement introuvable" });

                return Ok(new { success = true, data = paiement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération paiement {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,Employee,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreatePaiementRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var paiement = await _paimentsService.CreateAsync(request);
                if (paiement == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = paiement.Id },
                    new { success = true, data = paiement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création paiement");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePaiementRequest request)
        {
            try
            {
                var paiement = await _paimentsService.UpdateAsync(id, request);
                if (paiement == null)
                    return NotFound(new { success = false, message = "Paiement introuvable" });

                return Ok(new { success = true, data = paiement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour paiement {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _paimentsService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Paiement introuvable" });

                return Ok(new { success = true, message = "Paiement supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression paiement {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}