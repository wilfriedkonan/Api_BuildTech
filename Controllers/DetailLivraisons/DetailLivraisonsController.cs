using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.DetailLivraisons
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DetailLivraisonsController : ControllerBase
    {
        private readonly DetailLivraisonsService _detailService;
        private readonly ILogger<DetailLivraisonsController> _logger;

        public DetailLivraisonsController(
            DetailLivraisonsService detailService,
            ILogger<DetailLivraisonsController> logger)
        {
            _detailService = detailService;
            _logger = logger;
        }

        [HttpGet("livraison/{idLivraison}")]
        public async Task<IActionResult> GetByLivraison(Guid idLivraison)
        {
            try
            {
                var result = await _detailService.GetByLivraisonAsync(idLivraison);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détails livraison {idLivraison}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var detail = await _detailService.GetByIdAsync(id);
                if (detail == null)
                    return NotFound(new { success = false, message = "Détail livraison introuvable" });

                return Ok(new { success = true, data = detail });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détail livraison {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,Employee,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateDetailLivraisonRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var detail = await _detailService.CreateAsync(request);
                if (detail == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = detail.Id },
                    new { success = true, data = detail });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création détail livraison");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,Employee,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDetailLivraisonRequest request)
        {
            try
            {
                var detail = await _detailService.UpdateAsync(id, request);
                if (detail == null)
                    return NotFound(new { success = false, message = "Détail livraison introuvable" });

                return Ok(new { success = true, data = detail });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour détail livraison {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _detailService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Détail livraison introuvable" });

                return Ok(new { success = true, message = "Détail livraison annulé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur annulation détail livraison {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}