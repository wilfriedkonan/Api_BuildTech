using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.Authentication
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Authentification d'un utilisateur
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { success = false, message = "Email et mot de passe requis" });
                }

                var result = await _authService.LoginAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation($"✅ Login réussi: {request.Email}");
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning($"⚠️ Échec login: {request.Email} - {result.Message}");
                    return Unauthorized(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Erreur login: {request.Email}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupère les informations de l'utilisateur connecté
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var entrepriseId = User.FindFirst("entreprise_id")?.Value;
                var isSuperAdmin = User.FindFirst("is_super_admin")?.Value == "True";
                var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();

                return Ok(new
                {
                    success = true,
                    user = new
                    {
                        id = userId,
                        email,
                        name,
                        entrepriseId,
                        isSuperAdmin,
                        roles
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération user");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Déconnexion (invalidation du token côté client)
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            // Le token est invalidé côté client
            // Optionnel: ajouter le token à une blacklist
            _logger.LogInformation($"Logout user: {User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value}");

            return Ok(new { success = true, message = "Déconnexion réussie" });
        }

        /// <summary>
        /// Health check du service d'authentification
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "authentication"
            });
        }
    }
}