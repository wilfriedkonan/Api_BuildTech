using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Composants
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ComposantsController : ControllerBase
    {
        private readonly ComposantsService _composantsService;
        private readonly ILogger<ComposantsController> _logger;

        public ComposantsController(
            ComposantsService composantsService,
            ILogger<ComposantsController> logger)
        {
            _composantsService = composantsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _composantsService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération composants");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("article/{idArticle}")]
        public async Task<IActionResult> GetByArticle(Guid idArticle)
        {
            try
            {
                var result = await _composantsService.GetByArticleAsync(idArticle);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération composants article {idArticle}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var composant = await _composantsService.GetByIdAsync(id);
                if (composant == null)
                    return NotFound(new { success = false, message = "Composant introuvable" });

                return Ok(new { success = true, data = composant });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération composant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateComposantRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var composant = await _composantsService.CreateAsync(request);
                if (composant == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = composant.Id },
                    new { success = true, data = composant });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création composant");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateComposantRequest request)
        {
            try
            {
                var composant = await _composantsService.UpdateAsync(id, request);
                if (composant == null)
                    return NotFound(new { success = false, message = "Composant introuvable" });

                return Ok(new { success = true, data = composant });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour composant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _composantsService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Composant introuvable" });

                return Ok(new { success = true, message = "Composant désactivé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation composant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}