using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Livraisons
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LivraisonsController : ControllerBase
    {
        private readonly LivraisonsService _livraisonsService;
        private readonly ILogger<LivraisonsController> _logger;

        public LivraisonsController(
            LivraisonsService livraisonsService,
            ILogger<LivraisonsController> logger)
        {
            _livraisonsService = livraisonsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? enCoursOnly = null)
        {
            try
            {
                var result = await _livraisonsService.GetAllAsync(enCoursOnly);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération livraisons");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var livraison = await _livraisonsService.GetByIdAsync(id);
                if (livraison == null)
                    return NotFound(new { success = false, message = "Livraison introuvable" });

                return Ok(new { success = true, data = livraison });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération livraison {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,Employee,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateLivraisonRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var livraison = await _livraisonsService.CreateAsync(request);
                if (livraison == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = livraison.Id },
                    new { success = true, data = livraison });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création livraison");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost("{id}/terminer")]
        [Authorize(Roles = "Owner,Manager,Employee,SuperAdmin")]
        public async Task<IActionResult> Terminer(Guid id, [FromBody] TerminerLivraisonRequest request)
        {
            try
            {
                var livraison = await _livraisonsService.TerminerAsync(id, request);
                if (livraison == null)
                    return NotFound(new { success = false, message = "Livraison introuvable" });

                return Ok(new { success = true, data = livraison, message = "Livraison terminée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur terminer livraison {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLivraisonRequest request)
        {
            try
            {
                var livraison = await _livraisonsService.UpdateAsync(id, request);
                if (livraison == null)
                    return NotFound(new { success = false, message = "Livraison introuvable" });

                return Ok(new { success = true, data = livraison });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour livraison {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _livraisonsService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Livraison introuvable" });

                return Ok(new { success = true, message = "Livraison désactivée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation livraison {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}