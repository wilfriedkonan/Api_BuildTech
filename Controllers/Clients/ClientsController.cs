using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Clients
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClientsController : ControllerBase
    {
        private readonly ClientsService _clientsService;
        private readonly ILogger<ClientsController> _logger;

        public ClientsController(
            ClientsService clientsService,
            ILogger<ClientsController> logger)
        {
            _clientsService = clientsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var result = await _clientsService.GetAllAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération clients");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("abonnes")]
        public async Task<IActionResult> GetAbonnes()
        {
            try
            {
                var result = await _clientsService.GetAbonnesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération clients abonnés");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? nom, [FromQuery] string? telephone)
        {
            try
            {
                var result = await _clientsService.SearchAsync(nom, telephone);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche clients");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var client = await _clientsService.GetByIdAsync(id);
                if (client == null)
                    return NotFound(new { success = false, message = "Client introuvable" });

                return Ok(new { success = true, data = client });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération client {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin,Caissier")]
        public async Task<IActionResult> Create([FromBody] CreateClientRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Nom))
                    return BadRequest(new { success = false, message = "Nom requis" });

                var client = await _clientsService.CreateAsync(request);
                if (client == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = client.Id },
                    new { success = true, data = client });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création client");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin,Caissier")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request)
        {
            try
            {
                var client = await _clientsService.UpdateAsync(id, request);
                if (client == null)
                    return NotFound(new { success = false, message = "Client introuvable" });

                return Ok(new { success = true, data = client });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour client {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}/abonnement")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> UpdateAbonnement(Guid id, [FromBody] UpdateAbonnementRequest request)
        {
            try
            {
                var client = await _clientsService.UpdateAbonnementAsync(id, request);
                if (client == null)
                    return NotFound(new { success = false, message = "Client introuvable" });

                return Ok(new { success = true, data = client });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour abonnement client {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _clientsService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Client introuvable" });

                return Ok(new { success = true, message = "Client supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression client {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}