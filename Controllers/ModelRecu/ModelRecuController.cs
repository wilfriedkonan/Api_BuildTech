using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.ModelRecu
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ModelRecuController : ControllerBase
    {
        private readonly ModelRecuService _modelRecuService;
        private readonly ILogger<ModelRecuController> _logger;

        public ModelRecuController(
            ModelRecuService modelRecuService,
            ILogger<ModelRecuController> logger)
        {
            _modelRecuService = modelRecuService;
            _logger = logger;
        }

        [HttpGet("entreprise")]
        public async Task<IActionResult> GetByEntreprise()
        {
            try
            {
                var modele = await _modelRecuService.GetByEntrepriseAsync();
                if (modele == null)
                    return NotFound(new { success = false, message = "Modèle reçu introuvable" });

                return Ok(new { success = true, data = modele });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération modèle reçu entreprise");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var modele = await _modelRecuService.GetByIdAsync(id);
                if (modele == null)
                    return NotFound(new { success = false, message = "Modèle reçu introuvable" });

                return Ok(new { success = true, data = modele });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération modèle reçu {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateModelRecuRequest request)
        {
            try
            {
                var modele = await _modelRecuService.CreateAsync(request);
                if (modele == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = modele.Id },
                    new { success = true, data = modele });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création modèle reçu");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModelRecuRequest request)
        {
            try
            {
                var modele = await _modelRecuService.UpdateAsync(id, request);
                if (modele == null)
                    return NotFound(new { success = false, message = "Modèle reçu introuvable" });

                return Ok(new { success = true, data = modele });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour modèle reçu {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _modelRecuService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Modèle reçu introuvable" });

                return Ok(new { success = true, message = "Modèle reçu désactivé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation modèle reçu {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}