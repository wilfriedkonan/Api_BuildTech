using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.UniteMesures
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UniteMesuresController : ControllerBase
    {
        private readonly UniteMesuresService _uniteMesuresService;
        private readonly ILogger<UniteMesuresController> _logger;

        public UniteMesuresController(
            UniteMesuresService uniteMesuresService,
            ILogger<UniteMesuresController> logger)
        {
            _uniteMesuresService = uniteMesuresService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _uniteMesuresService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération unités mesure");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var uniteMesure = await _uniteMesuresService.GetByIdAsync(id);
                if (uniteMesure == null)
                    return NotFound(new { success = false, message = "Unité mesure introuvable" });

                return Ok(new { success = true, data = uniteMesure });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération unité mesure {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateUniteMesureRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var uniteMesure = await _uniteMesuresService.CreateAsync(request);
                if (uniteMesure == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = uniteMesure.Id },
                    new { success = true, data = uniteMesure });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création unité mesure");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUniteMesureRequest request)
        {
            try
            {
                var uniteMesure = await _uniteMesuresService.UpdateAsync(id, request);
                if (uniteMesure == null)
                    return NotFound(new { success = false, message = "Unité mesure introuvable" });

                return Ok(new { success = true, data = uniteMesure });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour unité mesure {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _uniteMesuresService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Unité mesure introuvable" });

                return Ok(new { success = true, message = "Unité mesure supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression unité mesure {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}