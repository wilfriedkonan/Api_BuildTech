using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Organisation
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrganisationController : ControllerBase
    {
        private readonly OrganisationService _organisationService;
        private readonly ILogger<OrganisationController> _logger;

        public OrganisationController(
            OrganisationService organisationService,
            ILogger<OrganisationController> logger)
        {
            _organisationService = organisationService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _organisationService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération organisations");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("actives")]
        public async Task<IActionResult> GetActives()
        {
            try
            {
                var result = await _organisationService.GetActivesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération organisations actives");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var organisation = await _organisationService.GetByIdAsync(id);
                if (organisation == null)
                    return NotFound(new { success = false, message = "Organisation introuvable" });

                return Ok(new { success = true, data = organisation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération organisation {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("identifiant/{identifiant}")]
        public async Task<IActionResult> GetByIdentifiant(string identifiant)
        {
            try
            {
                var organisation = await _organisationService.GetByIdentifiantAsync(identifiant);
                if (organisation == null)
                    return NotFound(new { success = false, message = "Organisation introuvable" });

                return Ok(new { success = true, data = organisation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération organisation {identifiant}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrganisationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Identifiant))
                    return BadRequest(new { success = false, message = "Identifiant requis" });

                if (request.Identifiant.Length > 12)
                    return BadRequest(new { success = false, message = "Identifiant ne doit pas dépasser 12 caractères" });

                // Vérifier si l'identifiant existe déjà
                var existant = await _organisationService.GetByIdentifiantAsync(request.Identifiant);
                if (existant != null)
                    return BadRequest(new { success = false, message = "Cet identifiant existe déjà" });

                var organisation = await _organisationService.CreateAsync(request);
                if (organisation == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = organisation.Id },
                    new { success = true, data = organisation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création organisation");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganisationRequest request)
        {
            try
            {
                var organisation = await _organisationService.UpdateAsync(id, request);
                if (organisation == null)
                    return NotFound(new { success = false, message = "Organisation introuvable" });

                return Ok(new { success = true, data = organisation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour organisation {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}