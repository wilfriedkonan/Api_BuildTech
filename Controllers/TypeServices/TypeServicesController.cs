using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.TypeServices
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TypeServicesController : ControllerBase
    {
        private readonly TypeServicesService _typeServicesService;
        private readonly ILogger<TypeServicesController> _logger;

        public TypeServicesController(
            TypeServicesService typeServicesService,
            ILogger<TypeServicesController> logger)
        {
            _typeServicesService = typeServicesService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _typeServicesService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types services");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var typeService = await _typeServicesService.GetByIdAsync(id);
                if (typeService == null)
                    return NotFound(new { success = false, message = "Type service introuvable" });

                return Ok(new { success = true, data = typeService });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération type service {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateTypeServiceRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var typeService = await _typeServicesService.CreateAsync(request);
                if (typeService == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = typeService.Id },
                    new { success = true, data = typeService });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création type service");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTypeServiceRequest request)
        {
            try
            {
                var typeService = await _typeServicesService.UpdateAsync(id, request);
                if (typeService == null)
                    return NotFound(new { success = false, message = "Type service introuvable" });

                return Ok(new { success = true, data = typeService });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour type service {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _typeServicesService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Type service introuvable" });

                return Ok(new { success = true, message = "Type service supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression type service {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}