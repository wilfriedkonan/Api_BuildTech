using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Serveur
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ServeurController : ControllerBase
    {
        private readonly ServeurService _serveurService;
        private readonly ILogger<ServeurController> _logger;

        public ServeurController(
            ServeurService serveurService,
            ILogger<ServeurController> logger)
        {
            _serveurService = serveurService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _serveurService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération serveurs");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var serveur = await _serveurService.GetByIdAsync(id);
                if (serveur == null)
                    return NotFound(new { success = false, message = "Serveur introuvable" });

                return Ok(new { success = true, data = serveur });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération serveur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateServeurRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var serveur = await _serveurService.CreateAsync(request);
                if (serveur == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = serveur.Id },
                    new { success = true, data = serveur });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création serveur");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServeurRequest request)
        {
            try
            {
                var serveur = await _serveurService.UpdateAsync(id, request);
                if (serveur == null)
                    return NotFound(new { success = false, message = "Serveur introuvable" });

                return Ok(new { success = true, data = serveur });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour serveur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _serveurService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Serveur introuvable" });

                return Ok(new { success = true, message = "Serveur supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression serveur {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}