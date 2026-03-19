using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Entreprise
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EntrepriseController : ControllerBase
    {
        private readonly EntrepriseService _entrepriseService;
        private readonly ILogger<EntrepriseController> _logger;

        public EntrepriseController(
            EntrepriseService entrepriseService,
            ILogger<EntrepriseController> logger)
        {
            _entrepriseService = entrepriseService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _entrepriseService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération entreprises");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var entreprise = await _entrepriseService.GetByIdAsync(id);

                if (entreprise == null)
                {
                    return NotFound(new { success = false, message = "Entreprise introuvable" });
                }

                return Ok(new { success = true, data = entreprise });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération entreprise {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateEntrepriseRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                {
                    return BadRequest(new { success = false, message = "Designation requise" });
                }

                var entreprise = await _entrepriseService.CreateAsync(request);

                if (entreprise == null)
                {
                    return StatusCode(500, new { success = false, message = "Erreur création" });
                }

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = entreprise.Id },
                    new { success = true, data = entreprise });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création entreprise");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEntrepriseRequest request)
        {
            try
            {
                var entreprise = await _entrepriseService.UpdateAsync(id, request);

                if (entreprise == null)
                {
                    return NotFound(new { success = false, message = "Entreprise introuvable" });
                }

                return Ok(new { success = true, data = entreprise });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour entreprise {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _entrepriseService.DeleteAsync(id);

                if (!success)
                {
                    return NotFound(new { success = false, message = "Entreprise introuvable" });
                }

                return Ok(new { success = true, message = "Entreprise désactivée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation entreprise {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}