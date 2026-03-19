using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Categorie
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategorieController : ControllerBase
    {
        private readonly CategorieService _cathegorieService;
        private readonly ILogger<CategorieController> _logger;

        public CategorieController(
            CategorieService cathegorieService,
            ILogger<CategorieController> logger)
        {
            _cathegorieService = cathegorieService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _cathegorieService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("restaurant")]
        public async Task<IActionResult> GetRestaurant()
        {
            try
            {
                var result = await _cathegorieService.GetRestaurantAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories restaurant");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("emporte")]
        public async Task<IActionResult> GetEmporte()
        {
            try
            {
                var result = await _cathegorieService.GetEmporteAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories à emporter");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var categorie = await _cathegorieService.GetByIdAsync(id);
                if (categorie == null)
                    return NotFound(new { success = false, message = "Catégorie introuvable" });

                return Ok(new { success = true, data = categorie });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération catégorie {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateCathegorieRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var categorie = await _cathegorieService.CreateAsync(request);
                if (categorie == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = categorie.Id },
                    new { success = true, data = categorie });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création catégorie");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCathegorieRequest request)
        {
            try
            {
                var categorie = await _cathegorieService.UpdateAsync(id, request);
                if (categorie == null)
                    return NotFound(new { success = false, message = "Catégorie introuvable" });

                return Ok(new { success = true, data = categorie });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour catégorie {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _cathegorieService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Catégorie introuvable" });

                return Ok(new { success = true, message = "Catégorie supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression catégorie {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}