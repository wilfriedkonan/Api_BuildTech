using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.TypePaiement
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TypePaiementController : ControllerBase
    {
        private readonly TypePaiementService _typePaiementService;
        private readonly ILogger<TypePaiementController> _logger;

        public TypePaiementController(
            TypePaiementService typePaiementService,
            ILogger<TypePaiementController> logger)
        {
            _typePaiementService = typePaiementService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _typePaiementService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types paiement");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var typePaiement = await _typePaiementService.GetByIdAsync(id);
                if (typePaiement == null)
                    return NotFound(new { success = false, message = "Type paiement introuvable" });

                return Ok(new { success = true, data = typePaiement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération type paiement {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateTypePaiementRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var typePaiement = await _typePaiementService.CreateAsync(request);
                if (typePaiement == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = typePaiement.Id },
                    new { success = true, data = typePaiement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création type paiement");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTypePaiementRequest request)
        {
            try
            {
                var typePaiement = await _typePaiementService.UpdateAsync(id, request);
                if (typePaiement == null)
                    return NotFound(new { success = false, message = "Type paiement introuvable" });

                return Ok(new { success = true, data = typePaiement });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour type paiement {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _typePaiementService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Type paiement introuvable" });

                return Ok(new { success = true, message = "Type paiement supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression type paiement {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}