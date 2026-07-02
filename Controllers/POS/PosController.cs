using Api_BuildTech.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_BuildTech.Controllers.POS
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PosController : ControllerBase
    {
        private readonly PosService _posService;
        private readonly RapportsPdfService _rapportsPdfService;
        private readonly ILogger<PosController> _logger;

        public PosController(
            PosService posService,
            RapportsPdfService rapportsPdfService,
            ILogger<PosController> logger)
        {
            _posService = posService;
            _rapportsPdfService = rapportsPdfService;
            _logger = logger;
        }

        // ========================================
        // FACTURES - CRUD
        // ========================================

        /// <summary>
        /// POST - Créer une nouvelle facture POS
        /// </summary>
        [HttpPost("factures")]
        [Authorize(Roles = "Owner,Manager,Serveur,SuperAdmin")]
        public async Task<IActionResult> CreateFacture([FromBody] CreatePosFactureRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NumeroFacture))
                    return BadRequest(new { success = false, message = "NumeroFacture requis" });

                if (request.IdEntreprise == Guid.Empty)
                    return BadRequest(new { success = false, message = "IdEntreprise requis" });

                var facture = await _posService.CreateFactureAsync(request);

                if (facture == null)
                    return StatusCode(500, new { success = false, message = "Erreur création facture" });

                return CreatedAtAction(
                    nameof(GetFactureComplete),
                    new { id = facture.Id },
                    new
                    {
                        success = true,
                        message = "Facture créée avec succès",
                        data = facture
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création facture");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Récupère une facture complète avec ses détails
        /// </summary>
        [HttpGet("factures/{id}")]
        public async Task<IActionResult> GetFactureComplete(Guid id)
        {
            try
            {
                var result = await _posService.GetFactureCompleteAsync(id);

                if (!result.Success)
                    return NotFound(new { success = false, message = result.Message });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération facture {id}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ========================================
        // DÉTAILS FACTURE
        // ========================================

        /// <summary>
        /// POST - Ajouter un détail (ligne) à une facture POS
        /// </summary>
        [HttpPost("factures/{idFacture}/details")]
        [Authorize(Roles = "Owner,Manager,Serveur,SuperAdmin")]
        public async Task<IActionResult> AddDetailToFacture(
            Guid idFacture,
            [FromBody] AddPosDetailRequest request)
        {
            try
            {
                if (request.IdArticle == Guid.Empty)
                    return BadRequest(new { success = false, message = "IdArticle requis" });

                if (request.Quantite <= 0)
                    return BadRequest(new { success = false, message = "Quantité doit être > 0" });

                var detail = await _posService.AddDetailToFactureAsync(idFacture, request);

                if (detail == null)
                    return StatusCode(500, new { success = false, message = "Erreur ajout détail" });

                return CreatedAtAction(
                    nameof(GetFactureComplete),
                    new { id = idFacture },
                    new
                    {
                        success = true,
                        message = "Détail ajouté avec succès",
                        data = detail
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur ajout détail facture");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ========================================
        // GESTION STATUTS
        // ========================================

        /// <summary>
        /// PUT - Endpoint INTELLIGENT pour mettre en attente POS
        /// 
        /// ⚡ DOUBLE FONCTIONNALITÉ :
        /// 
        /// 📌 CAS 1 : Créer facture + Ajouter articles + Mettre en attente en UN seul appel
        ///    Request:
        ///    {
        ///      "idFacture": null,              // ← null ou absent
        ///      "numeroFacture": "POS-2024-001",
        ///      "articles": [
        ///        {
        ///          "idArticle": "...",
        ///          "quantité": 2,
        ///          "prixUnitaireHT": 100,
        ///          "tauxTVA": 18
        ///        }
        ///      ],
        ///      "motif": "Client pas prêt"
        ///    }
        ///    Résultat: Facture créée + articles ajoutés + mise en attente
        ///
        /// 📌 CAS 2 : Facture existe → Mettre en attente uniquement
        ///    Request:
        ///    {
        ///      "idFacture": "...",             // ← ID fourni
        ///      "motif": "Client pas prêt"
        ///    }
        ///    Résultat: Facture mise en attente
        /// </summary>
        [HttpPut("put-on-hold")]
        [Authorize(Roles = "Owner,Manager,Serveur,SuperAdmin")]
        public async Task<IActionResult> PutOnHoldSmart([FromBody] PutOnHoldSmartRequest request)
        {
            try
            {
                // Déterminer le cas
                bool isCaseOne = request.IdFacture == null || request.IdFacture == Guid.Empty;

                if (isCaseOne)
                {
                    // CAS 1 : Créer facture + articles + mettre en attente
                    //if (string.IsNullOrEmpty(request.NumeroFacture))
                    //    return BadRequest(new { success = false, message = "CAS 1 : NumeroFacture requis" });

                    if (request.Articles == null || request.Articles.Count == 0)
                        return BadRequest(new { success = false, message = "CAS 1 : Articles requis" });

                    var response = await _posService.CreateAndPutOnHoldAsync(request);

                    if (!response.Success)
                        return StatusCode(500, response);

                    return Ok(response);
                }
                else
                {
                    // CAS 2 : Facture existe → Mettre en attente
                    var response = await _posService.PutExistingOnHoldAsync(request);

                    if (!response.Success)
                        return StatusCode(404, response);

                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur mise en attente POS");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// PUT - Mettre une facture en attente (ANCIEN - Compatible)
        /// ⚠️ Utiliser plutôt PUT /put-on-hold (endpoint intelligent)
        /// </summary>
        [HttpPut("factures/{idFacture}/put-on-hold")]
        [Authorize(Roles = "Owner,Manager,Serveur,SuperAdmin")]
        public async Task<IActionResult> PutOnHold(
            Guid idFacture,
            [FromBody] PutPosOnHoldRequest request)
        {
            try
            {
                var facture = await _posService.PutOnHoldAsync(idFacture, request.Motif);

                if (facture == null)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Facture mise en attente",
                    data = facture
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise en attente {idFacture}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// GET - Récupère toutes les factures en attente
        /// </summary>
        [HttpGet("factures/en-attente")]
        public async Task<IActionResult> GetAllOnHold()
        {
            try
            {
                var result = await _posService.GetAllOnHoldAsync();

                return Ok(new
                {
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures en attente");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// PUT - Reprendre une facture en attente
        /// </summary>
        [HttpPut("factures/{idFacture}/resume")]
        [Authorize(Roles = "Owner,Manager,Serveur,SuperAdmin")]
        public async Task<IActionResult> ResumeFacture(Guid idFacture)
        {
            try
            {
                var facture = await _posService.ResumeFactureAsync(idFacture);

                if (facture == null)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Facture reprise",
                    data = facture
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur reprise facture {idFacture}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// PUT - Annuler une facture POS
        /// </summary>
        [HttpPut("factures/{idFacture}/cancel")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> CancelFacture(
            Guid idFacture,
            [FromBody] CancelPosFactureRequest request)
        {
            try
            {
                var facture = await _posService.CancelFactureAsync(idFacture, request.Motif);

                if (facture == null)
                    return NotFound(new { success = false, message = "Facture introuvable" });

                return Ok(new
                {
                    success = true,
                    message = "Facture annulée",
                    data = facture
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur annulation facture {idFacture}");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ========================================
        // PAIEMENT - ENDPOINT INTELLIGENT
        // ========================================

        /// <summary>
        /// GET - Récupère tous les types de paiement
        /// </summary>
        [HttpGet("payment-types")]
        public async Task<IActionResult> GetPaymentTypes()
        {
            try
            {
                var types = await _posService.GetPaymentTypesAsync();

                return Ok(new
                {
                    success = true,
                    total = types.Count,
                    data = types
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types paiement");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// POST - Endpoint INTELLIGENT pour le paiement POS
        /// 
        /// ⚡ DOUBLE FONCTIONNALITÉ :
        /// 
        /// 📌 CAS 1 : Créer facture + Ajouter articles + Payer en UN seul appel
        ///    Request:
        ///    {
        ///      "idFacture": null,              // ← null ou absent
        ///      "numeroFacture": "POS-2024-001",
        ///      "articles": [
        ///        {
        ///          "idArticle": "...",
        ///          "quantité": 2,
        ///          "prixUnitaireHT": 100,
        ///          "tauxTVA": 18
        ///        }
        ///      ],
        ///      "idPayement": "...",
        ///      "montantVerser": 2360
        ///    }
        ///    Résultat: Facture créée + articles ajoutés + stock mouvements créés + payée
        ///
        /// 📌 CAS 2 : Facture existe → Payer uniquement
        ///    Request:
        ///    {
        ///      "idFacture": "...",             // ← ID fourni
        ///      "idPayement": "...",
        ///      "montantVerser": 2360
        ///    }
        ///    Résultat: Facture payée + stock mouvements créés
        /// 
        /// ✅ Crée automatiquement les mouvements de stock dans les 2 cas
        /// </summary>
        [HttpPost("confirm-payment")]
        [Authorize(Roles = "Owner,Manager,Serveur,SuperAdmin")]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPosPaymentRequest request)
        {
            try
            {
                // Validation basique
                if (request.IdPayement == Guid.Empty)
                    return BadRequest(new { success = false, message = "IdPayement requis" });

                if (request.MontantVerser < 0)
                    return BadRequest(new { success = false, message = "MontantVerser invalide" });

                // Déterminer le cas
                bool isCaseOne = request.IdFacture == null || request.IdFacture == Guid.Empty;

                if (isCaseOne)
                {
                    // CAS 1 : Créer facture + articles + payer
                    //if (string.IsNullOrEmpty(request.NumeroFacture))
                    //    return BadRequest(new { success = false, message = "CAS 1 : NumeroFacture requis" });

                    if (request.Articles == null || request.Articles.Count == 0)
                        return BadRequest(new { success = false, message = "CAS 1 : Articles requis" });

                    var response = await _posService.CreateAndPayFactureAsync(request);

                    if (!response.Success)
                        return StatusCode(500, response);

                    return Ok(response);
                }
                else
                {
                    // CAS 2 : Facture existe → Payer
                    var response = await _posService.PayExistingFactureAsync(request);

                    if (!response.Success)
                        return StatusCode(404, response);

                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur confirmation paiement POS");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // AJOUTER À PosController.cs - REMPLACER LES 4 ENDPOINTS PRÉCÉDENTS
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET - Statistiques complètes du jour (3 en 1)
        /// 
        /// Retourne en un seul appel:
        /// - Nombre de ventes aujourd'hui
        /// - Chiffre d'affaires aujourd'hui
        /// - Panier moyen
        /// 
        /// ⚡ Performance: <30ms (une seule requête SQL)
        /// </summary>
        [HttpGet("statistiques/jour")]
        [Authorize(Roles = "Owner,Manager,Caissier,SuperAdmin")]
        public async Task<IActionResult> GetStatistiquesJourAsync()
        {
            try
            {
                var result = await _posService.GetStatistiquesJourAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetStatistiquesJourAsync");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur serveur",
                    nombreVentes = 0,
                    chiffreAffaires = 0,
                    panierMoyen = 0
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // AJOUTER À PosController.cs
        // Endpoints pour générer les rapports PDF et JSON
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET - Rapport Ventes (par dates)
        /// Paramètres: dateDebut, dateFin, page, exporterPDF
        /// Retourne: PDF ou JSON avec pagination
        /// </summary>
        [HttpGet("rapports/ventes")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetRapportVentesAsync(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int page = 1,
            [FromQuery] bool downloadPDF = false)
        {
            try
            {
                var debut = dateDebut ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var fin = dateFin ?? DateTime.Now;

                _logger.LogInformation($"📊 Rapport Ventes: {debut:yyyy-MM-dd} à {fin:yyyy-MM-dd}, Page {page}");

                // Récupérer les données
                var rapport = await _posService.GetRapportVentesAsync(debut, fin, page);

                // Retourner JSON
                if (!downloadPDF)
                {
                    return Ok(rapport);
                }

                // Générer et télécharger PDF
                try
                {
                    var pdfBytes = _rapportsPdfService.GenerateRapportVentesPDF(rapport);

                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        _logger.LogError("PDF généré avec 0 octets");
                        return StatusCode(500, new { success = false, message = "Erreur génération PDF" });
                    }

                    var nomFichier = $"Rapport_Ventes_{debut:yyyyMMdd}_{fin:yyyyMMdd}_p{page}.pdf";
                    _logger.LogInformation($"✅ PDF généré: {nomFichier} ({pdfBytes.Length} octets)");

                    return File(pdfBytes, "application/pdf", nomFichier);
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError(pdfEx, "Erreur génération PDF Ventes");
                    return StatusCode(500, new { success = false, message = "Erreur génération PDF: " + pdfEx.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRapportVentesAsync");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Rapport Ventes Quantité + Valeur (JSON pour grid, PDF optionnel)
        /// </summary>
        [HttpGet("rapports/ventes-quantite")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetRapportVentesQuantiteAsync(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int page = 1,
            [FromQuery] bool downloadPDF = false)
        {
            try
            {
                var debut = dateDebut ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var fin = dateFin ?? DateTime.Now;

                _logger.LogInformation($"📊 Rapport Ventes Quantité: {debut:yyyy-MM-dd} à {fin:yyyy-MM-dd}, Page {page}");

                var rapport = await _posService.GetRapportVentesQuantiteAsync(debut, fin, page);

                if (!downloadPDF)
                {
                    return Ok(rapport);
                }

                try
                {
                    var pdfBytes = _rapportsPdfService.GenerateRapportVentesQuantitePDF(rapport);

                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        _logger.LogError("PDF généré avec 0 octets");
                        return StatusCode(500, new { success = false, message = "Erreur génération PDF" });
                    }

                    var nomFichier = $"Rapport_Ventes_Quantite_{debut:yyyyMMdd}_{fin:yyyyMMdd}_p{page}.pdf";
                    _logger.LogInformation($"✅ PDF généré: {nomFichier} ({pdfBytes.Length} octets)");

                    return File(pdfBytes, "application/pdf", nomFichier);
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError(pdfEx, "Erreur génération PDF Quantité");
                    return StatusCode(500, new { success = false, message = "Erreur génération PDF: " + pdfEx.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRapportVentesQuantiteAsync");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        /// <summary>
        /// Rapport Stock (JSON pour grid, PDF optionnel)
        /// </summary>
        [HttpGet("rapports/stock")]
        [Authorize(Roles = "Owner,Manager,SuperAdmin")]
        public async Task<IActionResult> GetRapportStockAsync(
      [FromQuery] int page = 1,
      [FromQuery] bool downloadPDF = false)
        {
            try
            {
                _logger.LogInformation($"📊 Rapport Stock, Page {page}");

                var rapport = await _posService.GetRapportStockAsync(page);

                if (!downloadPDF)
                {
                    return Ok(rapport);
                }

                try
                {
                    var pdfBytes = _rapportsPdfService.GenerateRapportStockPDF(rapport);

                    if (pdfBytes == null || pdfBytes.Length == 0)
                    {
                        _logger.LogError("PDF généré avec 0 octets");
                        return StatusCode(500, new { success = false, message = "Erreur génération PDF" });
                    }

                    var nomFichier = $"Rapport_Stock_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    _logger.LogInformation($"✅ PDF généré: {nomFichier} ({pdfBytes.Length} octets)");

                    return File(pdfBytes, "application/pdf", nomFichier);
                }
                catch (Exception pdfEx)
                {
                    _logger.LogError(pdfEx, "Erreur génération PDF Stock");
                    return StatusCode(500, new { success = false, message = "Erreur génération PDF: " + pdfEx.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRapportStockAsync");
                return StatusCode(500, new { success = false, message = "Erreur serveur" });
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // Méthodes privées pour générer les PDFs
        // ════════════════════════════════════════════════════════════════════════════

        private async Task<byte[]> GenerateRapportVentesPDFAsync(RapportVentesDto rapport)
        {
            // À implémenter avec PdfSharp
            await Task.Delay(100);
            return new byte[0];
        }

        private async Task<byte[]> GenerateRapportVentesQuantitePDFAsync(RapportVentesQuantiteDto rapport)
        {
            // À implémenter
            await Task.Delay(100);
            return new byte[0];
        }

        private async Task<byte[]> GenerateRapportStockPDFAsync(RapportStockDto rapport)
        {
            // À implémenter
            await Task.Delay(100);
            return new byte[0];
        }
    }
}