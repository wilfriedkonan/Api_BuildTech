using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Livreur
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LivreurController : ControllerBase
    {
        private readonly LivreurService _livreurService;
        private readonly ILogger<LivreurController> _logger;

        public LivreurController(
            LivreurService livreurService,
            ILogger<LivreurController> logger)
        {
            _livreurService = livreurService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? disponiblesOnly = null)
        {
            try
            {
                var result = await _livreurService.GetAllAsync(disponiblesOnly);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération livreurs");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var livreur = await _livreurService.GetByIdAsync(id);
                if (livreur == null)
                    return NotFound(new { success = false, message = "Livreur introuvable" });

                return Ok(new { success = true, data = livreur });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération livreur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateLivreurRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designatin))
                    return BadRequest(new { success = false, message = "Nom livreur requis" });

                if (string.IsNullOrEmpty(request.Contact))
                    return BadRequest(new { success = false, message = "Contact requis" });

                var livreur = await _livreurService.CreateAsync(request);
                if (livreur == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = livreur.Id },
                    new { success = true, data = livreur });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création livreur");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLivreurRequest request)
        {
            try
            {
                var livreur = await _livreurService.UpdateAsync(id, request);
                if (livreur == null)
                    return NotFound(new { success = false, message = "Livreur introuvable" });

                return Ok(new { success = true, data = livreur });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour livreur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _livreurService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Livreur introuvable" });

                return Ok(new { success = true, message = "Livreur désactivé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation livreur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}