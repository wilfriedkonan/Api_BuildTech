using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.MatierePremiere
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MatierePremiereController : ControllerBase
    {
        private readonly MatierePremiereService _matiereService;
        private readonly ILogger<MatierePremiereController> _logger;

        public MatierePremiereController(
            MatierePremiereService matiereService,
            ILogger<MatierePremiereController> logger)
        {
            _matiereService = matiereService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _matiereService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération matières premières");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var matiere = await _matiereService.GetByIdAsync(id);
                if (matiere == null)
                    return NotFound(new { success = false, message = "Matière première introuvable" });

                return Ok(new { success = true, data = matiere });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération matière première {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateMatierePremiereRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var matiere = await _matiereService.CreateAsync(request);
                if (matiere == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = matiere.Id },
                    new { success = true, data = matiere });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création matière première");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMatierePremiereRequest request)
        {
            try
            {
                var matiere = await _matiereService.UpdateAsync(id, request);
                if (matiere == null)
                    return NotFound(new { success = false, message = "Matière première introuvable" });

                return Ok(new { success = true, data = matiere });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour matière première {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _matiereService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Matière première introuvable" });

                return Ok(new { success = true, message = "Matière première supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression matière première {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}