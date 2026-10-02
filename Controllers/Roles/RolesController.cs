using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Roles
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly RolesService _rolesService;
        private readonly ILogger<RolesController> _logger;

        public RolesController(RolesService rolesService, ILogger<RolesController> logger)
        {
            _rolesService = rolesService;
            _logger = logger;
        }

        /// <summary>
        /// GET - Liste des rôles attribuables (exclut "Owner")
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var roles = await _rolesService.GetAllExceptOwnerAsync();
                return Ok(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération rôles");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}