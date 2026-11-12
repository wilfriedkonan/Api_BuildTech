using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Api_BuildTech.Controllers.Authentication
{
    /// <summary>
    /// Pose / supprime les cookies d'authentification.
    /// Tous les cookies sont HttpOnly + Secure + SameSite=Strict.
    /// </summary>
    public class AuthCookieService
    {
        private readonly AuthCookieSettings _s;

        public AuthCookieService(IOptions<AuthCookieSettings> settings)
        {
            _s = settings.Value;
        }

        public string NewCsrfToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        /// <summary>Access token (JWT) : envoyé à toute l'API.</summary>
        public void SetAccessCookie(HttpResponse response, string accessToken, DateTime expiresAtUtc)
        {
            response.Cookies.Append(_s.AccessCookieName, accessToken, Build("/", expiresAtUtc));
        }

        /// <summary>Refresh token : envoyé uniquement vers /api/auth/* (portée réduite).</summary>
        public void SetRefreshCookie(HttpResponse response, string refreshToken, DateTime expiresAtUtc)
        {
            response.Cookies.Append(_s.RefreshCookieName, refreshToken, Build(_s.RefreshCookiePath, expiresAtUtc));
        }

        /// <summary>Jeton CSRF (double-submit). HttpOnly : le front le récupère via /api/auth/csrf ou la réponse du login.</summary>
        public void SetCsrfCookie(HttpResponse response, string csrfToken, DateTime expiresAtUtc)
        {
            response.Cookies.Append(_s.CsrfCookieName, csrfToken, Build("/", expiresAtUtc));
        }

        public void ClearAll(HttpResponse response)
        {
            // Pour supprimer un cookie, Path et Domain doivent être identiques à ceux utilisés à la création.
            response.Cookies.Delete(_s.AccessCookieName, Build("/", null));
            response.Cookies.Delete(_s.RefreshCookieName, Build(_s.RefreshCookiePath, null));
            response.Cookies.Delete(_s.CsrfCookieName, Build("/", null));
        }

        private CookieOptions Build(string path, DateTime? expiresUtc)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = _s.RequireSecure,
                SameSite = SameSiteMode.Strict,
                Path = path,
                IsEssential = true
            };

            if (!string.IsNullOrWhiteSpace(_s.CookieDomain))
            {
                options.Domain = _s.CookieDomain;
            }

            if (expiresUtc.HasValue)
            {
                options.Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc));
            }

            return options;
        }
    }
}