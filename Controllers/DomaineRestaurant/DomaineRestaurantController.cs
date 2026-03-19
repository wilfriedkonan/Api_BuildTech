using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.DomaineRestaurant
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DomaineRestaurantController : ControllerBase
    {
        private readonly DomaineRestaurantService _domaineService;
        private readonly ILogger<DomaineRestaurantController> _logger;

        public DomaineRestaurantController(
            DomaineRestaurantService domaineService,
            ILogger<DomaineRestaurantController> logger)
        {
            _domaineService = domaineService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _domaineService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération domaines restaurant");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var domaine = await _domaineService.GetByIdAsync(id);
                if (domaine == null)
                    return NotFound(new { success = false, message = "Domaine restaurant introuvable" });

                return Ok(new { success = true, data = domaine });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération domaine restaurant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateDomaineRestaurantRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var domaine = await _domaineService.CreateAsync(request);
                if (domaine == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = domaine.Id },
                    new { success = true, data = domaine });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création domaine restaurant");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDomaineRestaurantRequest request)
        {
            try
            {
                var domaine = await _domaineService.UpdateAsync(id, request);
                if (domaine == null)
                    return NotFound(new { success = false, message = "Domaine restaurant introuvable" });

                return Ok(new { success = true, data = domaine });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour domaine restaurant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _domaineService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Domaine restaurant introuvable" });

                return Ok(new { success = true, message = "Domaine restaurant désactivé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation domaine restaurant {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}