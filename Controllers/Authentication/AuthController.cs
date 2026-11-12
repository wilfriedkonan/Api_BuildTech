using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api_BuildTech.Controllers.Authentication
{
    /// <summary>
    /// Deux modes, choisis par le client via l'en-tête "X-Auth-Mode" :
    ///
    ///  - "cookie" (nouveau, front Next.js) : access token + refresh token dans des cookies
    ///    HttpOnly/Secure/SameSite=Strict. AUCUN token dans le corps des réponses.
    ///    Protégé par le middleware CSRF.
    ///
    ///  - absent (existant, apps WPF) : comportement inchangé, tokens dans le corps JSON et
    ///    Authorization: Bearer. Le refresh token est maintenant réellement utilisable via
    ///    POST /api/auth/refresh { "refreshToken": "..." }.
    /// </summary>
    /// <summary>Corps de /refresh et /logout en mode Bearer (apps WPF). Vide en mode cookie.</summary>
    public class RefreshBodyRequest
    {
        public string? RefreshToken { get; set; }
    }

    /// <summary>Réponse de /refresh en mode Bearer.</summary>
    public class RefreshBodyResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const string AuthModeHeader = "X-Auth-Mode";

        private readonly AuthService _authService;
        private readonly TokenService _tokens;
        private readonly AuthCookieService _cookies;
        private readonly AuthCookieSettings _s;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AuthService authService,
            TokenService tokens,
            AuthCookieService cookies,
            IOptions<AuthCookieSettings> settings,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _tokens = tokens;
            _cookies = cookies;
            _s = settings.Value;
            _logger = logger;
        }

        private bool IsCookieMode =>
            string.Equals(Request.Headers[AuthModeHeader].ToString(), "cookie", StringComparison.OrdinalIgnoreCase);

        private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
        private string UserAgent => Request.Headers.UserAgent.ToString();

        private void NoStore() => Response.Headers.CacheControl = "no-store";

        // ====================================================================
        // POST /api/auth/login
        // ====================================================================
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

                if (!result.Success || result.User == null)
                {
                    _logger.LogWarning("Échec login: {Email}", request.Email);
                    return Unauthorized(new LoginResponse { Success = false, Message = result.Message });
                }

                _logger.LogInformation("Login réussi: {Email}", request.Email);
                NoStore();

                var u = result.User;
                var snapshot = new AuthUserSnapshot(u.Id, u.Email, u.OrganisationId, u.IdEntreprise, u.IsSuperAdmin, u.Role, u.RoleCaisse);
                var cookieMode = IsCookieMode;

                // On ignore les tokens générés par AuthService (access 24h + refresh jamais stocké)
                // et on émet les nôtres.
                var lifetime = cookieMode
                    ? TimeSpan.FromMinutes(_s.AccessTokenMinutes)
                    : TimeSpan.FromHours(_s.LegacyAccessTokenHours);

                var (accessToken, accessExp) = _tokens.CreateAccessToken(snapshot, lifetime);
                var refresh = await _tokens.IssueRefreshTokenAsync(
                    u.Id, u.IdEntreprise == Guid.Empty ? (Guid?)null : u.IdEntreprise, ClientIp, UserAgent);

                if (cookieMode)
                {
                    var csrf = _cookies.NewCsrfToken(); // nouvelle session => nouveau jeton CSRF
                    _cookies.SetAccessCookie(Response, accessToken, accessExp);
                    _cookies.SetRefreshCookie(Response, refresh.Token, refresh.ExpiresAt);
                    _cookies.SetCsrfCookie(Response, csrf, refresh.ExpiresAt);

                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        expiresAt = accessExp,
                        csrfToken = csrf,
                        user = u
                    });
                }

                result.AccessToken = accessToken;
                result.RefreshToken = refresh.Token;
                result.ExpiresAt = accessExp;
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur login");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ====================================================================
        // POST /api/auth/refresh
        // ====================================================================
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
            RefreshBodyRequest? request)
        {
            try
            {
                NoStore();
                var cookieMode = IsCookieMode;

                var presented = cookieMode
                    ? Request.Cookies[_s.RefreshCookieName]
                    : request?.RefreshToken;

                if (string.IsNullOrWhiteSpace(presented))
                {
                    return Unauthorized(new { success = false, message = "Session expirée" });
                }

                var result = await _tokens.RotateAsync(presented, ClientIp, UserAgent);

                if (!result.Success || result.User == null || result.NewRefreshToken == null)
                {
                    _logger.LogInformation("Refresh refusé: {Reason}", result.Failure);

                    // Deux onglets simultanés : le navigateur détient déjà les nouveaux cookies,
                    // on ne les efface surtout pas. Le front réessaie simplement.
                    if (cookieMode && result.Failure != "concurrent_refresh")
                    {
                        _cookies.ClearAll(Response);
                    }

                    return Unauthorized(new { success = false, message = "Session expirée" });
                }

                var lifetime = cookieMode
                    ? TimeSpan.FromMinutes(_s.AccessTokenMinutes)
                    : TimeSpan.FromHours(_s.LegacyAccessTokenHours);

                var (accessToken, accessExp) = _tokens.CreateAccessToken(result.User, lifetime);
                var newRefresh = result.NewRefreshToken;

                if (cookieMode)
                {
                    // On conserve le jeton CSRF existant pour ne pas désynchroniser les autres onglets.
                    var csrf = Request.Cookies[_s.CsrfCookieName];
                    if (string.IsNullOrEmpty(csrf)) csrf = _cookies.NewCsrfToken();

                    _cookies.SetAccessCookie(Response, accessToken, accessExp);
                    _cookies.SetRefreshCookie(Response, newRefresh.Token, newRefresh.ExpiresAt);
                    _cookies.SetCsrfCookie(Response, csrf, newRefresh.ExpiresAt);

                    return Ok(new { success = true, expiresAt = accessExp, csrfToken = csrf });
                }

                return Ok(new RefreshBodyResponse
                {
                    Success = true,
                    Message = "Token renouvelé",
                    AccessToken = accessToken,
                    RefreshToken = newRefresh.Token,
                    ExpiresAt = accessExp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur refresh");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ====================================================================
        // POST /api/auth/logout
        // ====================================================================
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout(
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
            RefreshBodyRequest? request)
        {
            try
            {
                var cookieMode = IsCookieMode;

                var presented = cookieMode
                    ? Request.Cookies[_s.RefreshCookieName]
                    : request?.RefreshToken;

                if (!string.IsNullOrWhiteSpace(presented))
                {
                    await _tokens.RevokeSessionAsync(presented);
                }

                if (cookieMode)
                {
                    _cookies.ClearAll(Response);
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur logout");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ====================================================================
        // GET /api/auth/csrf : le front y récupère son jeton CSRF (après un rechargement de page)
        // ====================================================================
        [HttpGet("csrf")]
        [AllowAnonymous]
        public IActionResult Csrf()
        {
            NoStore();

            var token = Request.Cookies[_s.CsrfCookieName];
            if (string.IsNullOrEmpty(token))
            {
                token = _cookies.NewCsrfToken();
                _cookies.SetCsrfCookie(Response, token, DateTime.UtcNow.AddDays(_s.RefreshTokenDays));
            }

            return Ok(new { csrfToken = token });
        }

        // ====================================================================
        // POST /api/auth/switch-entreprise : change l'entreprise ACTIVE de la session (mode cookie)
        // ====================================================================
        [HttpPost("switch-entreprise")]
        [Authorize]
        public async Task<IActionResult> SwitchEntreprise([FromBody] SwitchEntrepriseRequest request)
        {
            try
            {
                NoStore();

                if (!IsCookieMode)
                {
                    return BadRequest(new { success = false, message = "Changement d'entreprise disponible en mode cookie uniquement" });
                }
                if (request == null || request.IdEntreprise == Guid.Empty)
                {
                    return BadRequest(new { success = false, message = "Identifiant d'entreprise requis" });
                }

                var userIdText = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var orgText = User.FindFirst("organisation_id")?.Value;
                if (!Guid.TryParse(userIdText, out var userId) || !Guid.TryParse(orgText, out var organisationId) || email == null)
                {
                    return Unauthorized(new { success = false, message = "Session invalide" });
                }

                var refreshToken = Request.Cookies[_s.RefreshCookieName];
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return Unauthorized(new { success = false, message = "Session expirée" });
                }

                // L'entreprise doit appartenir à l'ORGANISATION du compte et être valide (jamais déduit du client)
                var entreprises = await _authService.ListEntreprisesAsync(organisationId);
                var active = entreprises.FirstOrDefault(e => e.Id == request.IdEntreprise);
                if (active == null)
                {
                    return StatusCode(403, new { success = false, message = "Entreprise non accessible" });
                }

                if (!await _tokens.SwitchEntrepriseAsync(refreshToken, userId, request.IdEntreprise))
                {
                    return Unauthorized(new { success = false, message = "Session expirée" });
                }

                var isSuperAdmin = User.FindFirst("is_super_admin")?.Value == "True";
                var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                var roleCaisse = User.FindFirst("role_caisse")?.Value;
                var snapshot = new AuthUserSnapshot(userId, email, organisationId, request.IdEntreprise, isSuperAdmin, role, roleCaisse);

                var (accessToken, accessExp) = _tokens.CreateAccessToken(snapshot, TimeSpan.FromMinutes(_s.AccessTokenMinutes));
                _cookies.SetAccessCookie(Response, accessToken, accessExp);

                return Ok(new { success = true, expiresAt = accessExp, entreprise = active });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur switch-entreprise");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ====================================================================
        // GET /api/auth/me
        // ====================================================================
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                NoStore();

                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var organisationId = User.FindFirst("organisation_id")?.Value;
                var entrepriseId = User.FindFirst("entreprise_id")?.Value;
                var isSuperAdmin = User.FindFirst("is_super_admin")?.Value == "True";
                var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                var entreprises = Guid.TryParse(organisationId, out var orgGuid)
                    ? await _authService.ListEntreprisesAsync(orgGuid)
                    : new List<EntrepriseSummary>();

                return Ok(new
                {
                    success = true,
                    user = new { id = userId, email, organisationId, entrepriseId, isSuperAdmin, role, entreprises }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération user");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ====================================================================
        // GET /api/auth/health
        // ====================================================================
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow, service = "auth" });
        }
    }
}