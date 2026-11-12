namespace Api_BuildTech.Controllers.Authentication
{
    /// <summary>
    /// Section "AuthCookies" de appsettings.json
    /// </summary>
    public class AuthCookieSettings
    {
        public const string SectionName = "AuthCookies";

        // Noms des cookies
        public string AccessCookieName { get; set; } = "bt_access";
        public string RefreshCookieName { get; set; } = "bt_refresh";
        public string CsrfCookieName { get; set; } = "bt_csrf";

        // Durées
        public int AccessTokenMinutes { get; set; } = 15;          // mode cookie (navigateur)
        public int LegacyAccessTokenHours { get; set; } = 24;      // mode Bearer (apps WPF existantes)
        public int RefreshTokenDays { get; set; } = 14;            // glissant, renouvelé à chaque refresh
        public int SessionMaxDays { get; set; } = 30;             // plafond absolu d'une session

        // Attributs des cookies
        public string? CookieDomain { get; set; }                   // null = cookie lié à l'hôte de l'API
        public string RefreshCookiePath { get; set; } = "/api/auth"; // le refresh token n'est envoyé qu'à /api/auth/*
        public bool RequireSecure { get; set; } = true;             // false uniquement en dev HTTP

        // Origines autorisées (CORS + contrôle Origin du middleware CSRF)
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

        // Fenêtre de tolérance (secondes) pour deux refresh simultanés (2 onglets)
        public int RefreshReuseGraceSeconds { get; set; } = 10;
    }
}