using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Api_BuildTech.Controllers.Authentication
{
    /// <summary>
    /// Protection CSRF pour les requêtes authentifiées par COOKIE.
    ///
    /// Couches (en plus de SameSite=Strict) :
    ///   1. Contrôle de l'en-tête Origin (ou Referer) contre la liste AllowedOrigins.
    ///   2. Double-submit : l'en-tête X-CSRF-Token doit être égal au cookie bt_csrf.
    ///
    /// Ne s'applique PAS :
    ///   - aux méthodes sûres (GET, HEAD, OPTIONS, TRACE) ;
    ///   - aux requêtes avec en-tête Authorization (Bearer) : non vulnérables au CSRF,
    ///     c'est le cas des apps WPF existantes ;
    ///   - aux requêtes sans cookie d'authentification (rien à falsifier).
    ///
    /// /api/auth/login et /api/auth/logout : seul le contrôle Origin s'applique
    /// (le front n'a pas encore de jeton CSRF avant le login).
    /// </summary>
    public class CsrfProtectionMiddleware
    {
        public const string HeaderName = "X-CSRF-Token";

        private static readonly HashSet<string> SafeMethods =
            new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

        private static readonly string[] OriginOnlyPaths = { "/api/auth/login", "/api/auth/logout" };

        private readonly RequestDelegate _next;
        private readonly AuthCookieSettings _s;
        private readonly HashSet<string> _allowedOrigins;
        private readonly ILogger<CsrfProtectionMiddleware> _logger;

        public CsrfProtectionMiddleware(
            RequestDelegate next,
            IOptions<AuthCookieSettings> settings,
            ILogger<CsrfProtectionMiddleware> logger)
        {
            _next = next;
            _s = settings.Value;
            _logger = logger;
            _allowedOrigins = new HashSet<string>(
                _s.AllowedOrigins.Select(o => o.TrimEnd('/')),
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;

            if (SafeMethods.Contains(request.Method)
                || request.Headers.ContainsKey("Authorization")
                || !HasAuthCookie(request))
            {
                await _next(context);
                return;
            }

            // 1. Origin / Referer
            if (!IsAllowedOrigin(request))
            {
                _logger.LogWarning("CSRF: origine refusée pour {Method} {Path} (Origin={Origin})",
                    request.Method, request.Path, request.Headers.Origin.ToString());
                await Reject(context, "Origine non autorisée");
                return;
            }

            // 2. Double-submit (sauf login/logout)
            var originOnly = OriginOnlyPaths.Any(p =>
                request.Path.Equals(p, StringComparison.OrdinalIgnoreCase));

            if (!originOnly)
            {
                var header = request.Headers[HeaderName].ToString();
                request.Cookies.TryGetValue(_s.CsrfCookieName, out var cookie);

                if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(cookie) || !ConstantTimeEquals(header, cookie))
                {
                    _logger.LogWarning("CSRF: jeton absent ou invalide pour {Method} {Path}", request.Method, request.Path);
                    await Reject(context, "Jeton CSRF manquant ou invalide");
                    return;
                }
            }

            await _next(context);
        }

        private bool HasAuthCookie(HttpRequest request) =>
            request.Cookies.ContainsKey(_s.AccessCookieName) || request.Cookies.ContainsKey(_s.RefreshCookieName);

        private bool IsAllowedOrigin(HttpRequest request)
        {
            var origin = request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin))
            {
                return _allowedOrigins.Contains(origin.TrimEnd('/'));
            }

            // Pas d'Origin : repli sur Referer (on ne garde que schéma + hôte + port)
            var referer = request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                return _allowedOrigins.Contains(uri.GetLeftPart(UriPartial.Authority));
            }

            return false;
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            var x = Encoding.UTF8.GetBytes(a);
            var y = Encoding.UTF8.GetBytes(b);
            return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
        }

        private static async Task Reject(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"success\":false,\"message\":\"{message}\"}}");
        }
    }
}