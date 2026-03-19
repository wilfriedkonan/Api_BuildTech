using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Sessions
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SessionsController : ControllerBase
    {
        private readonly SessionsService _sessionsService;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            SessionsService sessionsService,
            ILogger<SessionsController> logger)
        {
            _sessionsService = sessionsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? cloturees = null)
        {
            try
            {
                var result = await _sessionsService.GetAllAsync(cloturees);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération sessions");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("ouverte")]
        public async Task<IActionResult> GetSessionOuverte()
        {
            try
            {
                var session = await _sessionsService.GetSessionOuverteAsync();
                if (session == null)
                    return NotFound(new { success = false, message = "Aucune session ouverte" });

                return Ok(new { success = true, data = session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération session ouverte");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var session = await _sessionsService.GetByIdAsync(id);
                if (session == null)
                    return NotFound(new { success = false, message = "Session introuvable" });

                return Ok(new { success = true, data = session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération session {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateSessionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Designation))
                    return BadRequest(new { success = false, message = "Designation requise" });

                var session = await _sessionsService.CreateAsync(request);
                if (session == null)
                    return StatusCode(500, new { success = false, message = "Erreur création" });

                return CreatedAtAction(nameof(GetById), new { id = session.Id },
                    new { success = true, data = session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création session");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPost("{id}/cloturer")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Cloturer(Guid id, [FromBody] ClotureSessionRequest request)
        {
            try
            {
                var session = await _sessionsService.CloturerAsync(id, request);
                if (session == null)
                    return NotFound(new { success = false, message = "Session introuvable" });

                return Ok(new { success = true, data = session, message = "Session clôturée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur clôture session {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionRequest request)
        {
            try
            {
                var session = await _sessionsService.UpdateAsync(id, request);
                if (session == null)
                    return NotFound(new { success = false, message = "Session introuvable" });

                return Ok(new { success = true, data = session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour session {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Owner,SuperAdmin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var success = await _sessionsService.DeleteAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Session introuvable" });

                return Ok(new { success = true, message = "Session supprimée" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression session {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}