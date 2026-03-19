using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.CategorieComposants
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategorieComposantsController : ControllerBase
    {
        private readonly CategorieComposantsService _categorieService;
        private readonly ILogger<CategorieComposantsController> _logger;

        public CategorieComposantsController(
            CategorieComposantsService categorieService,
            ILogger<CategorieComposantsController> logger)
        {
            _categorieService = categorieService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _categorieService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories composants");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var categorie = await _categorieService.GetByIdAsync(id);
                if (categorie == null)
                    return NotFound(new { success = false, message = "Catégorie composant introuvable" });

                return Ok(new { success = true, data = categorie });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération catégorie composant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateCategorieComposantRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var categorie = await _categorieService.CreateAsync(request);
                if (categorie == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = categorie.Id },
                    new { success = true, data = categorie });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création catégorie composant");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategorieComposantRequest request)
        {
            try
            {
                var categorie = await _categorieService.UpdateAsync(id, request);
                if (categorie == null)
                    return NotFound(new { success = false, message = "Catégorie composant introuvable" });

                return Ok(new { success = true, data = categorie });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour catégorie composant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _categorieService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Catégorie composant introuvable" });

                return Ok(new { success = true, message = "Catégorie composant désactivée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation catégorie composant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}