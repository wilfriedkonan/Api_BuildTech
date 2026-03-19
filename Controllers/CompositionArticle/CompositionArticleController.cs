using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.CompositionArticle
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CompositionArticleController : ControllerBase
    {
        private readonly CompositionArticleService _compositionService;
        private readonly ILogger<CompositionArticleController> _logger;

        public CompositionArticleController(
            CompositionArticleService compositionService,
            ILogger<CompositionArticleController> logger)
        {
            _compositionService = compositionService;
            _logger = logger;
        }

        [HttpGet("article/{idArticle}")]
        public async Task<IActionResult> GetByArticle(Guid idArticle)
        {
            try
            {
                var result = await _compositionService.GetByArticleAsync(idArticle);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération compositions article {idArticle}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var composition = await _compositionService.GetByIdAsync(id);
                if (composition == null)
                    return NotFound(new { success = false, message = "Composition article introuvable" });

                return Ok(new { success = true, data = composition });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération composition article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateCompositionArticleRequest request)
        {
            try
            {
                var composition = await _compositionService.CreateAsync(request);
                if (composition == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = composition.Id },
                    new { success = true, data = composition });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création composition article");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompositionArticleRequest request)
        {
            try
            {
                var composition = await _compositionService.UpdateAsync(id, request);
                if (composition == null)
                    return NotFound(new { success = false, message = "Composition article introuvable" });

                return Ok(new { success = true, data = composition });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour composition article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _compositionService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Composition article introuvable" });

                return Ok(new { success = true, message = "Composition article supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression composition article {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}