using Api_BuildTech.Controllers.Ecommerce;
using Api_BuildTech.Services.messagerie;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using static Api_BuildTech.Services.messagerie.Whatsappservice;

namespace Api_BuildTech.Controllers.Boutique
{
    /// <summary>
    /// Endpoints publics pour la résolution Boutique (QR code / lien direct) et son catalogue.
    /// Aucune authentification requise - accessible aux clients finaux.
    /// </summary>
    [ApiController]
    [Route("api/public/boutique")]
    [AllowAnonymous]
    public class BoutiquePublicController : ControllerBase
    {
        private readonly BoutiquePublicService _boutiquePublicService;
        private readonly Whatsappservice _whatsappService;
        private readonly EcommercePublicService _commandePublicService;
        private readonly ILogger<BoutiquePublicController> _logger;

        public BoutiquePublicController(
            BoutiquePublicService boutiquePublicService,
             Whatsappservice whatsappService,
              EcommercePublicService commandePublicService,
            ILogger<BoutiquePublicController> logger)
        {
            _boutiquePublicService = boutiquePublicService;
            _whatsappService = whatsappService;
            _commandePublicService = commandePublicService;
            _logger = logger;
        }

        /// <summary>
        /// GET - Résolution par CodeQR (accès QR code, mode consultation)
        /// </summary>
        [HttpGet("qr/{codeQR}")]
        public async Task<IActionResult> GetByQRCode(string codeQR)
        {
            try
            {
                var boutique = await _boutiquePublicService.GetByQRCodeAsync(codeQR);

                if (boutique == null)
                    return NotFound(new { success = false, message = "Boutique introuvable ou inactive" });

                _logger.LogInformation($"📱 Accès QR: {boutique.NomBoutique} ({boutique.TypeBoutique})");

                return Ok(new { success = true, data = boutique });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur résolution QR {codeQR}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Résolution par Slug (lien direct générique)
        /// </summary>
        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            try
            {
                var boutique = await _boutiquePublicService.GetBySlugAsync(slug);

                if (boutique == null)
                    return NotFound(new { success = false, message = "Boutique introuvable ou inactive" });

                _logger.LogInformation($"🔗 Accès direct: {boutique.NomBoutique} ({boutique.TypeBoutique})");

                return Ok(new { success = true, data = boutique });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur résolution slug {slug}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Résolution par Type + Slug (route /restaurant/{slug}/menu ou /magasin/{slug}/menu)
        /// </summary>
        [HttpGet("{typeBoutique}/{slug}")]
        public async Task<IActionResult> GetByTypeAndSlug(string typeBoutique, string slug)
        {
            try
            {
                if (typeBoutique != "Restaurant" && typeBoutique != "Magasin")
                    return BadRequest(new { success = false, message = "Type de boutique invalide" });

                var typeNormalized = typeBoutique == "Restaurant" ? "Restaurant" : "Magasin";

                var boutique = await _boutiquePublicService.GetByTypeAndSlugAsync(typeNormalized, slug);

                if (boutique == null)
                    return NotFound(new { success = false, message = "Boutique introuvable ou inactive" });

                _logger.LogInformation($"🔗 Accès direct: {boutique.NomBoutique} ({boutique.TypeBoutique})");

                return Ok(new { success = true, data = boutique });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur résolution {typeBoutique}/{slug}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Catalogue complet d'une boutique (mode "view" = QR/lecture seule, "shop" = commande)
        /// </summary>
        [HttpGet("{idBoutique}/catalogue")]
        public async Task<IActionResult> GetCatalogue(Guid idBoutique, [FromQuery] string mode = "shop")
        {
            try
            {
                if (mode != "view" && mode != "shop")
                    mode = "shop";

                var catalogue = await _boutiquePublicService.GetCatalogueAsync(idBoutique, mode);

                if (catalogue == null)
                    return NotFound(new { success = false, message = "Catalogue introuvable" });

                return Ok(new { success = true, data = catalogue });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération catalogue {idBoutique}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // 2. Ajouter le champ + injecter dans le constructeur existant :
        //
        //    private readonly Whatsappservice _whatsappService;
        //
        //    public BoutiquePublicController(
        //        BoutiquePublicService boutiquePublicService,
        //        Whatsappservice whatsappService,           // ← nouveau paramètre
        //        ILogger<BoutiquePublicController> logger)
        //    {
        //        _boutiquePublicService = boutiquePublicService;
        //        _whatsappService = whatsappService;         // ← nouvelle affectation
        //        _logger = logger;
        //    }

        // 3. Ajouter cette méthode dans la classe :

        /// <summary>
        /// POST - Valide une commande e-commerce et notifie boutique + client via WhatsApp.
        /// TODO: persister la commande en base une fois le flux WhatsApp validé.
        /// </summary>
        /// <summary>
        /// POST - Valide une commande e-commerce : persiste en base PUIS notifie boutique + client.
        /// </summary>
        [HttpPost("{idBoutique}/commande/whatsapp")]
        public async Task<IActionResult> EnvoyerCommandeWhatsApp(Guid idBoutique, [FromBody] CommandeWhatsAppRequest request)
        {
            try
            {
                // ── Validation ────────────────────────────────────────────────
                if (request.Lignes == null || request.Lignes.Count == 0)
                    return BadRequest(new { success = false, message = "Le panier ne peut pas être vide" });

                var clientWhatsAppNormalise = (request.ClientWhatsApp ?? "").Replace(" ", "").Replace("+", "");
                if (!Regex.IsMatch(clientWhatsAppNormalise, @"^225\d{10}$"))
                    return BadRequest(new { success = false, message = "Numéro WhatsApp invalide (format attendu: 225 0725691200)" });

                var boutique = await _boutiquePublicService.GetBoutiqueByIdInternalAsync(idBoutique);
                if (boutique == null)
                    return NotFound(new { success = false, message = "Boutique introuvable ou inactive" });

                if (string.IsNullOrWhiteSpace(boutique.TelephoneBoutique))
                {
                    _logger.LogError($"❌ Boutique {idBoutique} sans TelephoneBoutique - impossible de notifier");
                    return StatusCode(500, new { success = false, message = "Boutique non joignable actuellement" });
                }

                var telephoneBoutiqueNormalise = boutique.TelephoneBoutique.Replace(" ", "").Replace("+", "");

                // ── [ÉTAPE 1] Persister la commande en base (état initial: EnAttente) ──
                var idCommande = await _commandePublicService.CreerCommandeAsync(
                    idBoutique: idBoutique,
                    idEntreprise: boutique.IdEntreprise,
                    clientWhatsApp: clientWhatsAppNormalise,
                    livraisonTexte: request.LivraisonTexte,
                    livraisonLat: request.LivraisonLat,
                    livraisonLng: request.LivraisonLng,
                    total: request.Total,
                    lignes: request.Lignes
                );

                if (idCommande == null)
                {
                    _logger.LogError($"❌ Échec persistance commande pour boutique {idBoutique}");
                    return StatusCode(500, new { success = false, message = "Erreur lors de l'enregistrement de la commande" });
                }

                // ── Construction des lignes pour les messages WhatsApp ───────
                var lignesWhatsApp = request.Lignes.Select(l => new LigneCommandeWhatsApp
                {
                    NomArticle = l.NomArticle,
                    Quantite = l.Quantite,
                    PrixUnitaire = l.PrixUnitaire
                }).ToList();

                // ── [ÉTAPE 2] Notifier la BOUTIQUE ────────────────────────────
                var notifBoutiqueOk = await _whatsappService.SendNotificationNouvelleCommandeAsync(
                    toPhoneBoutique: telephoneBoutiqueNormalise,
                    nomBoutique: boutique.NomBoutique,
                    clientWhatsApp: clientWhatsAppNormalise,
                    lignes: lignesWhatsApp,
                    total: request.Total,
                    livraisonTexte: request.LivraisonTexte,
                    livraisonLat: request.LivraisonLat,
                    livraisonLng: request.LivraisonLng
                );

                // ── [ÉTAPE 3] Confirmer au CLIENT (seulement si étape 2 réussie) ──
                var confirmationClientOk = false;
                if (notifBoutiqueOk)
                {
                    confirmationClientOk = await _whatsappService.SendConfirmationCommandeClientAsync(
                        toPhoneClient: clientWhatsAppNormalise,
                        nomBoutique: boutique.NomBoutique,
                        lignes: lignesWhatsApp,
                        total: request.Total
                    );
                }
                else
                {
                    _logger.LogError($"❌ Échec notification WhatsApp boutique {idBoutique} (commande {idCommande} déjà enregistrée)");
                }

                // ── Mettre à jour les flags de notification sur la commande déjà créée ──
                await _commandePublicService.MettreAJourFlagsNotificationAsync(
                    idCommande.Value, notifBoutiqueOk, confirmationClientOk);

                // La commande EST enregistrée même si WhatsApp échoue - le POS la verra
                // quand même dans sa liste "EnAttente" (fiabilité > notification instantanée).
                return Ok(new
                {
                    success = true,
                    message = notifBoutiqueOk
                        ? "Commande envoyée avec succès"
                        : "Commande enregistrée, mais la notification WhatsApp a échoué - la boutique la verra tout de même sur son interface",
                    idCommande = idCommande.Value,
                    boutiqueNotifiee = notifBoutiqueOk,
                    clientNotifie = confirmationClientOk
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur envoi commande WhatsApp boutique {idBoutique}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }


        // ════════════════════════════════════════════════════════════════════════════
        // DTO de requête (à ajouter en bas du fichier, dans le namespace)
        // ════════════════════════════════════════════════════════════════════════════

        public class CommandeWhatsAppRequest
        {
            public string ClientWhatsApp { get; set; } = "";
            public double? LivraisonLat { get; set; }
            public double? LivraisonLng { get; set; }
            public string? LivraisonTexte { get; set; }
            public List<LigneCommandeRequest> Lignes { get; set; } = new();
            public decimal Total { get; set; }
        }

        public class LigneCommandeRequest
        {
            public Guid IdArticle { get; set; }
            public string NomArticle { get; set; } = "";
            public decimal PrixUnitaire { get; set; }
            public int Quantite { get; set; }
        }
    }
}

