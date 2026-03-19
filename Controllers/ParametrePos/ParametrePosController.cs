using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.ParametrePos
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ParametrePosController : ControllerBase
    {
        private readonly ParametrePosService _parametrePosService;
        private readonly ILogger<ParametrePosController> _logger;

        public ParametrePosController(
            ParametrePosService parametrePosService,
            ILogger<ParametrePosController> logger)
        {
            _parametrePosService = parametrePosService;
            _logger = logger;
        }

        [HttpGet("entreprise")]
        public async Task<IActionResult> GetByEntreprise()
        {
            try
            {
                var parametre = await _parametrePosService.GetByEntrepriseAsync();
                if (parametre == null)
                    return NotFound(new { success = false, message = "Paramètres POS introuvables" });

                return Ok(new { success = true, data = parametre });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération paramètres POS entreprise");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var parametre = await _parametrePosService.GetByIdAsync(id);
                if (parametre == null)
                    return NotFound(new { success = false, message = "Paramètre POS introuvable" });

                return Ok(new { success = true, data = parametre });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération paramètre POS {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateParametrePosRequest request)
        {
            try
            {
                var parametre = await _parametrePosService.CreateAsync(request);
                if (parametre == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = parametre.Id },
                    new { success = true, data = parametre });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création paramètre POS");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateParametrePosRequest request)
        {
            try
            {
                var parametre = await _parametrePosService.UpdateAsync(id, request);
                if (parametre == null)
                    return NotFound(new { success = false, message = "Paramètre POS introuvable" });

                return Ok(new { success = true, data = parametre });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour paramètre POS {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _parametrePosService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Paramètre POS introuvable" });

                return Ok(new { success = true, message = "Paramètre POS supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression paramètre POS {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}