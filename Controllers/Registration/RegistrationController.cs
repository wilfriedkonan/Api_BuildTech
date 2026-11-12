using Microsoft.AspNetCore.Mvc;
using Api_BuildTech.Services;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Api_BuildTech.Controllers.Registration
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationController : ControllerBase
    {
        private readonly RegistrationOrchestrator _orchestrator;
        private readonly ILogger<RegistrationController> _logger;
        private readonly IConfiguration _config;

        // ── Code opérateur (TEMPORAIRE, en attendant l'interface super-admin) ─────────
        // Variable d'environnement / appsettings : REGISTRATION_OPERATOR_KEY (>= 16 car.).
        // Vide ou absente => cette voie est DESACTIVEE (seule la session super-admin passe).
        private const string OperatorKeyHeader = "X-Operator-Key";
        private const int MaxEchecs = 5;
        private static readonly TimeSpan FenetreVerrou = TimeSpan.FromMinutes(15);
        private static readonly ConcurrentDictionary<string, (int Echecs, DateTime Debut)> _echecsParIp = new();

        public RegistrationController(
            RegistrationOrchestrator orchestrator,
            ILogger<RegistrationController> logger,
            IConfiguration config)
        {
            _orchestrator = orchestrator;
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// true = code opérateur valide. Sinon false (voie désactivée, code absent ou faux) ; refusVerrou est
        /// renseigné (429) quand l'IP a dépassé 5 échecs en 15 min. Ne révèle jamais si la voie est activée.
        /// </summary>
        private bool CodeOperateurValide(out IActionResult? refusVerrou)
        {
            refusVerrou = null;
            var attendu = _config["REGISTRATION_OPERATOR_KEY"] ?? _config["Registration:OperatorKey"];
            if (string.IsNullOrWhiteSpace(attendu) || attendu.Length < 16) return false;   // voie désactivée

            var fourni = Request.Headers[OperatorKeyHeader].FirstOrDefault();
            if (string.IsNullOrEmpty(fourni)) return false;

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
            var maintenant = DateTime.UtcNow;
            if (_echecsParIp.TryGetValue(ip, out var e) && maintenant - e.Debut < FenetreVerrou && e.Echecs >= MaxEchecs)
            {
                refusVerrou = StatusCode(429, new { success = false, message = "Trop de tentatives. Réessayez plus tard." });
                return false;
            }

            // Comparaison à temps constant (empreintes SHA-256 de même longueur)
            var ok = CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(fourni)),
                SHA256.HashData(Encoding.UTF8.GetBytes(attendu)));

            if (ok) { _echecsParIp.TryRemove(ip, out _); return true; }

            _echecsParIp.AddOrUpdate(ip,
                _ => (1, maintenant),
                (_, old) => maintenant - old.Debut >= FenetreVerrou ? (1, maintenant) : (old.Echecs + 1, old.Debut));
            _logger.LogWarning("Code opérateur invalide depuis {Ip}", ip);
            return false;
        }

        /// <summary>
        /// Le mode "entreprise via son Id" est reserve aux SUPER-ADMINS, identifies par la session (JWT du cookie
        /// bt_access, ou Authorization: Bearer). Meme critere que GET /api/auth/me : claim "is_super_admin" = "True"
        /// (ou role SuperAdmin). Voie temporaire en plus : en-tete X-Operator-Key = REGISTRATION_OPERATOR_KEY (serveur), saisi a la main, jamais stocke dans le front.
        /// 401 = pas de session, 403 = session valide mais pas super-admin.
        /// </summary>
        private IActionResult? RefuserSiNonSuperAdmin()
        {
            // Voie temporaire : code opérateur saisi à la main (en-tête X-Operator-Key)
            if (CodeOperateurValide(out var verrou)) return null;
            if (verrou != null) return verrou;

            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized(new { success = false, message = "Connexion requise." });

            var estSuperAdmin = User.FindFirst("is_super_admin")?.Value == "True" || User.IsInRole("SuperAdmin");
            if (!estSuperAdmin)
            {
                _logger.LogWarning("Inscription par Id d'entreprise refusée : utilisateur {UserId} non super-admin",
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
                return StatusCode(403, new { success = false, message = "Accès réservé aux administrateurs de la plateforme." });
            }
            return null;
        }

        /// <summary>
        /// Inscription complète d'un nouveau tenant
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
        {
            try
            {
                // ===== Mode "entreprise existante" : création du compte web pour une entreprise déjà présente =====
                if (request.IdEntreprise.HasValue)
                {
                    var refus = RefuserSiNonSuperAdmin();
                    if (refus != null) return refus;

                    if (request.IdEntreprise.Value == Guid.Empty)
                        return BadRequest(new { success = false, message = "Identifiant d'entreprise invalide" });
                    if (string.IsNullOrWhiteSpace(request.Email))
                        return BadRequest(new { success = false, message = "Email requis" });
                    if (string.IsNullOrWhiteSpace(request.Nom))
                        return BadRequest(new { success = false, message = "Nom requis" });
                    if (string.IsNullOrEmpty(request.Password))
                        return BadRequest(new { success = false, message = "Mot de passe requis" });

                    var resultExistante = await _orchestrator.RegisterForExistingEntrepriseAsync(request);
                    if (resultExistante.Success)
                    {
                        _logger.LogInformation($"✅ Compte web créé pour l'entreprise existante {resultExistante.EntrepriseId}");
                        return Ok(resultExistante);
                    }
                    _logger.LogWarning($"⚠️ Échec compte entreprise existante: {resultExistante.ErrorCode} - {resultExistante.Message}");
                    return BadRequest(resultExistante);
                }

                // Inscription publique : on ne peut PAS rattacher une entreprise à une organisation existante
                // (sinon n'importe qui pourrait ajouter une entreprise à l'organisation d'un autre). Réservé aux super-admins.
                if (request.IdOrganisation.HasValue && request.IdOrganisation.Value != Guid.Empty)
                {
                    var refusOrg = RefuserSiNonSuperAdmin();
                    if (refusOrg != null) return refusOrg;
                }

                //Validation des données

                // Propriétaire
                if (string.IsNullOrEmpty(request.Nom))
                    return BadRequest(new { success = false, message = "Non du propriétaire requis" });

                if (string.IsNullOrEmpty(request.Email))
                    return BadRequest(new { success = false, message = "Email requis" });

                if (string.IsNullOrEmpty(request.Telephone))
                    return BadRequest(new { success = false, message = "Le numero de telephone est requis" });

                // Entreprise
                if (string.IsNullOrEmpty(request.EntrepriseName))
                    return BadRequest(new { success = false, message = "Nom d'entreprise requis" });

                // Souscription
                if (request.IdPlan == Guid.Empty)
                    return BadRequest(new { success = false, message = "Plan d'abonnement requis" });

                if (request.SubscriptionDurationMonths <= 0)
                    return BadRequest(new { success = false, message = "Le nombre de mois est requis" });

                // Orchestration de l'inscription
                var result = await _orchestrator.RegisterNewTenantAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation($"✅ Inscription réussie: {result.CodeEntreprise}");
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning($"⚠️ Échec inscription: {result.Message}");
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'inscription");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur lors de l'inscription",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Vérifier la disponibilité d'un email
        /// </summary>
        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmailAvailability([FromQuery] string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return BadRequest(new { available = false, message = "Email requis" });

                return Ok(new { available = await _orchestrator.EmailWebDisponibleAsync(email) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur vérification email: {email}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Récupérer les plans disponibles
        /// </summary>
        [HttpGet("plans")]
        public async Task<IActionResult> GetAvailablePlans()
        {
            try
            {
                // TODO: Implémenter la récupération des plans
                return Ok(new { success = true, plans = new List<object>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }
    }
}