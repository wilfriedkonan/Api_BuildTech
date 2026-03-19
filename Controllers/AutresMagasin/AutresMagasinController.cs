using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.AutresMagasin
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AutresMagasinController : ControllerBase
    {
        private readonly AutresMagasinService _magasinService;
        private readonly ILogger<AutresMagasinController> _logger;

        public AutresMagasinController(
            AutresMagasinService magasinService,
            ILogger<AutresMagasinController> logger)
        {
            _magasinService = magasinService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _magasinService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération autres magasins");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var magasin = await _magasinService.GetByIdAsync(id);
                if (magasin == null)
                    return NotFound(new { success = false, message = "Magasin introuvable" });

                return Ok(new { success = true, data = magasin });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération magasin {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateAutresMagasinRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var magasin = await _magasinService.CreateAsync(request);
                if (magasin == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = magasin.Id },
                    new { success = true, data = magasin });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création magasin");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAutresMagasinRequest request)
        {
            try
            {
                var magasin = await _magasinService.UpdateAsync(id, request);
                if (magasin == null)
                    return NotFound(new { success = false, message = "Magasin introuvable" });

                return Ok(new { success = true, data = magasin });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour magasin {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _magasinService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Magasin introuvable" });

                return Ok(new { success = true, message = "Magasin supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression magasin {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}