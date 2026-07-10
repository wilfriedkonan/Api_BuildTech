// ════════════════════════════════════════════════════════════════════════════
// Services/RapportsPdfService.cs - ALIGNEMENT DES GRILLES CORRIGÉ
// ════════════════════════════════════════════════════════════════════════════

using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp;
using Microsoft.Extensions.Logging;

namespace Api_BuildTech.Services
{
    /// <summary>
    /// Service pour générer les rapports en PDF
    /// Version finale avec alignement des grilles corrigé
    /// </summary>
    public class RapportsPdfService
    {
        private readonly ILogger<RapportsPdfService> _logger;

        // Configuration PDF
        private const float MARGIN_LEFT = 40;
        private const float MARGIN_RIGHT = 40;
        private const float MARGIN_TOP = 40;
        private const float MARGIN_BOTTOM = 40;
        private const float PAGE_WIDTH = 595; // A4
        private const float PAGE_HEIGHT = 842; // A4
        private const float CONTENT_WIDTH = PAGE_WIDTH - MARGIN_LEFT - MARGIN_RIGHT;

        // Couleurs
        private static readonly XColor COLOR_PRIMARY = XColor.FromArgb(0, 0, 0);
        private static readonly XColor COLOR_LIGHT_GRAY = XColor.FromArgb(240, 240, 240);
        private static readonly XColor COLOR_BORDER = XColor.FromArgb(200, 200, 200);
        private static readonly XColor COLOR_LIGHT = XColor.FromArgb(245, 245, 245);

        // Polices
        private XFont _fontTitrePrincipal = new XFont("Arial", 18, XFontStyleEx.Bold);
        private readonly XFont _fontTitreRapport = new("Arial", 14, XFontStyleEx.Bold);
        private readonly XFont _fontSousTitre = new("Arial", 10, XFontStyleEx.Regular);

        private readonly XFont _fontTableHeader = new("Arial", 10, XFontStyleEx.Bold);
        private readonly XFont _fontTableRow = new("Arial", 9, XFontStyleEx.Regular);

        private readonly XFont _fontLabel = new("Arial", 9, XFontStyleEx.Bold);
        private readonly XFont _fontValue = new("Arial", 9, XFontStyleEx.Regular);

        private readonly XFont _fontFooter = new("Arial", 8, XFontStyleEx.Italic);

        public RapportsPdfService(ILogger<RapportsPdfService> logger)
        {
            _logger = logger;
        }


        /// <summary>
        /// Générer PDF Rapport Ventes
        /// </summary>
        public byte[] GenerateRapportVentesPDF(RapportVentesDto rapport)
        {
            try
            {
                using var document = new PdfDocument();
                var page = document.AddPage();
                page.Size = PageSize.A4;

                using var gfx = XGraphics.FromPdfPage(page);
                float yPosition = MARGIN_TOP;

                // En-tête
                gfx.DrawString(rapport.NomEntreprise.ToUpper(), _fontTitrePrincipal, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 18;

                gfx.DrawString("RAPPORT DE VENTES", _fontTitreRapport, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 16;

                gfx.DrawString($"Du {rapport.DateDebut:dd/MM/yyyy} au {rapport.DateFin:dd/MM/yyyy}",
                    _fontSousTitre, new XSolidBrush(COLOR_BORDER), MARGIN_LEFT, yPosition);
                yPosition += 20;

                // Résumé totaux
                yPosition = DrawTotalsSection(gfx,
                    ("Sous-total HT", FormatNombre(rapport.SousTotal)),
                    ("TVA collectée", FormatNombre(rapport.TotalTVA)),
                    ("TOTAL GÉNÉRAL", FormatNombre(rapport.TotalGeneral)),
                    yPosition);

                yPosition += 15;

                // Titre table
                gfx.DrawString("Détail des factures", _fontTableHeader, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 12;  // ← REMONTÉ: était 15

                // En-têtes table
                var headers = new[] { "DATE", "N°FACTURE", "MONTANT" };
                var columnWidths = new[] { 80f, 300f, 115f };
                yPosition = DrawTableHeader(gfx, headers, columnWidths, yPosition);

                // Lignes table
                foreach (var ligne in rapport.Lignes)
                {
                    // Vérifier si nouvelle page nécessaire
                    if (yPosition > PAGE_HEIGHT - MARGIN_BOTTOM - 30)
                    {
                        page = document.AddPage();
                        page.Size = PageSize.A4;
                        yPosition = MARGIN_TOP;
                    }

                    var values = new[]
                    {
                        ligne.Date.ToString("dd/MM/yyyy"),
                        string.IsNullOrEmpty(ligne.Designation) ? ligne.NumeroFacture : ligne.Designation,
                        FormatNombre(ligne.MontantTotal) + " FCFA"
                    };

                    yPosition = DrawTableRow(gfx, values, columnWidths, yPosition);
                }

                // Pied de page
                DrawFooter(gfx, $"Page {document.PageCount} - {DateTime.Now:dd/MM/yyyy HH:mm}", page);

                // Convertir en bytes
                using var stream = new MemoryStream();
                document.Save(stream, false);
                var result = stream.ToArray();

                _logger.LogInformation($"✅ PDF Ventes généré: {result.Length} octets");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur génération PDF Ventes");
                throw;
            }
        }

        /// <summary>
        /// Générer PDF Rapport Ventes Quantité+Valeur
        /// </summary>
        public byte[] GenerateRapportVentesQuantitePDF(RapportVentesQuantiteDto rapport)
        {
            try
            {
                using var document = new PdfDocument();
                var page = document.AddPage();
                page.Size = PageSize.A4;

                using var gfx = XGraphics.FromPdfPage(page);
                float yPosition = MARGIN_TOP;

                // En-tête
                gfx.DrawString(rapport.NomEntreprise.ToUpper(), _fontTitrePrincipal, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 18;

                gfx.DrawString("RAPPORT VENTES - QUANTITÉ ET VALEUR", _fontTitreRapport, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 16;

                gfx.DrawString($"Du {rapport.DateDebut:dd/MM/yyyy} au {rapport.DateFin:dd/MM/yyyy}",
                    _fontSousTitre, new XSolidBrush(COLOR_BORDER), MARGIN_LEFT, yPosition);
                yPosition += 20;

                // Résumé totaux
                yPosition = DrawTotalsSection(gfx,
                    ("Quantité vendue", FormatNombre(rapport.QuantiteTotalVendue) + " unités"),
                    ("Montant total", FormatNombre(rapport.MontantTotalVendu) + " FCFA"),
                    ("Panier moyen", rapport.QuantiteTotalVendue > 0
                        ? FormatNombre(rapport.MontantTotalVendu / rapport.QuantiteTotalVendue) + " FCFA"
                        : "0 FCFA"),
                    yPosition);

                yPosition += 15;

                // Titre table
                gfx.DrawString("Détail par article", _fontTableHeader, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 12;  // ← REMONTÉ: était 15

                // En-têtes table
                var headers = new[] { "DESIGNATION", "QUANTITÉ", "MONTANT TOTAL" };
                var columnWidths = new[] { 300f, 80f, 115f };
                yPosition = DrawTableHeader(gfx, headers, columnWidths, yPosition);

                // Lignes table
                foreach (var ligne in rapport.Lignes)
                {
                    if (yPosition > PAGE_HEIGHT - MARGIN_BOTTOM - 30)
                    {
                        page = document.AddPage();
                        page.Size = PageSize.A4;
                        yPosition = MARGIN_TOP;
                    }

                    var values = new[]
                    {
                        ligne.Designation,
                        FormatNombre(ligne.Quantite),
                        FormatNombre(ligne.MontantTotal) + " FCFA"
                    };

                    yPosition = DrawTableRow(gfx, values, columnWidths, yPosition);
                }

                // Pied de page
                DrawFooter(gfx, $"Page {document.PageCount} - {DateTime.Now:dd/MM/yyyy HH:mm}", page);

                using var stream = new MemoryStream();
                document.Save(stream, false);
                var result = stream.ToArray();

                _logger.LogInformation($"✅ PDF Quantité généré: {result.Length} octets");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur génération PDF Ventes Quantité");
                throw;
            }
        }

        /// <summary>
        /// Générer PDF Rapport Stock
        /// </summary>
        public byte[] GenerateRapportStockPDF(RapportStockDto rapport)
        {
            try
            {
                using var document = new PdfDocument();
                var page = document.AddPage();
                page.Size = PageSize.A4;

                using var gfx = XGraphics.FromPdfPage(page);
                float yPosition = MARGIN_TOP;

                // En-tête
                gfx.DrawString(rapport.NomEntreprise.ToUpper(), _fontTitrePrincipal, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 18;

                gfx.DrawString("RAPPORT DE STOCK", _fontTitreRapport, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 16;

                gfx.DrawString($"Généré le {rapport.DateRapport:dd/MM/yyyy}",
                    _fontSousTitre, new XSolidBrush(COLOR_BORDER), MARGIN_LEFT, yPosition);
                yPosition += 20;

                // Résumé totaux
                yPosition = DrawTotalsSection(gfx,
                    ("Articles en stock", rapport.TotalLignes.ToString()),
                    ("Stock total", FormatNombre(rapport.StockTotalArticles) + " unités"),
                    ("Date du rapport", rapport.DateRapport.ToString("dd/MM/yyyy")),
                    yPosition);

                yPosition += 15;

                // Titre table
                gfx.DrawString("Détail du stock", _fontTableHeader, XBrushes.Black, MARGIN_LEFT, yPosition);
                yPosition += 12;  // ← REMONTÉ: était 15

                // En-têtes table
                var headers = new[] { "DESIGNATION", "CODE", "STOCK ACTUEL" };
                var columnWidths = new[] { 300f, 100f, 95f };
                yPosition = DrawTableHeader(gfx, headers, columnWidths, yPosition);

                // Lignes table
                foreach (var ligne in rapport.Lignes)
                {
                    if (yPosition > PAGE_HEIGHT - MARGIN_BOTTOM - 30)
                    {
                        page = document.AddPage();
                        page.Size = PageSize.A4;
                        yPosition = MARGIN_TOP;
                    }

                    var values = new[]
                    {
                        ligne.Designation,
                        ligne.CodeArticle,
                        FormatNombre(ligne.StockActuel)
                    };

                    yPosition = DrawTableRow(gfx, values, columnWidths, yPosition);
                }

                // Pied de page
                DrawFooter(gfx, $"Page {document.PageCount} - {DateTime.Now:dd/MM/yyyy HH:mm}", page);

                using var stream = new MemoryStream();
                document.Save(stream, false);
                var result = stream.ToArray();

                _logger.LogInformation($"✅ PDF Stock généré: {result.Length} octets");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur génération PDF Stock");
                throw;
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // MÉTHODES PRIVÉES DE DESSIN
        // ════════════════════════════════════════════════════════════════════════════

        private float DrawTotalsSection(XGraphics gfx, (string label, string value) total1,
            (string label, string value) total2, (string label, string value) total3, float yPos)
        {
            var boxWidth = (CONTENT_WIDTH - 10) / 3;
            var boxHeight = 40;

            // Box 1
            DrawBox(gfx, MARGIN_LEFT, yPos, boxWidth, boxHeight);
            gfx.DrawString(total1.label, _fontLabel, new XSolidBrush(COLOR_BORDER), MARGIN_LEFT + 5, yPos + 8);
            gfx.DrawString(total1.value, _fontValue, XBrushes.Black, MARGIN_LEFT + 5, yPos + 18);

            // Box 2
            DrawBox(gfx, MARGIN_LEFT + boxWidth + 5, yPos, boxWidth, boxHeight);
            gfx.DrawString(total2.label, _fontLabel, new XSolidBrush(COLOR_BORDER), MARGIN_LEFT + boxWidth + 10, yPos + 8);
            gfx.DrawString(total2.value, _fontValue, XBrushes.Black, MARGIN_LEFT + boxWidth + 10, yPos + 18);

            // Box 3
            DrawBox(gfx, MARGIN_LEFT + (boxWidth + 5) * 2, yPos, boxWidth, boxHeight);
            gfx.DrawString(total3.label, _fontLabel, new XSolidBrush(COLOR_BORDER), MARGIN_LEFT + (boxWidth + 5) * 2 + 5, yPos + 8);
            gfx.DrawString(total3.value, _fontValue, XBrushes.Black, MARGIN_LEFT + (boxWidth + 5) * 2 + 5, yPos + 18);

            return yPos + boxHeight + 5;
        }

        private float DrawTableHeader(XGraphics gfx, string[] headers, float[] columnWidths, float yPos)
        {
            float xPos = MARGIN_LEFT;

            // Background
            gfx.DrawRectangle(new XSolidBrush(COLOR_LIGHT_GRAY),
                MARGIN_LEFT, yPos, CONTENT_WIDTH, 18);  // ← RÉDUIT: était 20

            // Headers - ALIGNEMENT VERTICAL AMÉLIORÉ
            for (int i = 0; i < headers.Length; i++)
            {
                gfx.DrawString(headers[i], _fontTableHeader, XBrushes.Black, xPos + 3, yPos + 13);  // ← AJUSTÉ: était yPos + 4
                xPos += columnWidths[i];
            }

            // Border
            gfx.DrawRectangle(XPens.Gray, MARGIN_LEFT, yPos, CONTENT_WIDTH, 18);  // ← RÉDUIT: était 20

            return yPos + 20;  // ← REMONTÉ: était 22
        }

        private float DrawTableRow(XGraphics gfx, string[] values, float[] columnWidths, float yPos)
        {
            float xPos = MARGIN_LEFT;
            const float ROW_HEIGHT = 15;  // ← RÉDUIT: était 16

            // Background alternée
            if ((int)(yPos / ROW_HEIGHT) % 2 == 0)
            {
                gfx.DrawRectangle(new XSolidBrush(COLOR_LIGHT),
                    MARGIN_LEFT, yPos, CONTENT_WIDTH, ROW_HEIGHT);
            }

            // Valeurs - ALIGNEMENT VERTICAL AMÉLIORÉ
            for (int i = 0; i < values.Length; i++)
            {
                var text = values[i] ?? "";
                // Limiter à 50 caractères pour éviter le débordement
                if (text.Length > 50)
                    text = text.Substring(0, 47) + "...";

                gfx.DrawString(text, _fontTableRow, XBrushes.Black, xPos + 3, yPos + 12);  // ← AJUSTÉ: était yPos + 3
                xPos += columnWidths[i];
            }

            // Border
            gfx.DrawRectangle(XPens.LightGray, MARGIN_LEFT, yPos, CONTENT_WIDTH, ROW_HEIGHT);

            return yPos + ROW_HEIGHT;
        }

        private void DrawBox(XGraphics gfx, float x, float y, float width, float height)
        {
            gfx.DrawRectangle(new XSolidBrush(COLOR_LIGHT), x, y, width, height);
            gfx.DrawRectangle(new XPen(COLOR_BORDER), x, y, width, height);
        }

        private void DrawFooter(XGraphics gfx, string text, PdfPage page)
        {
            gfx.DrawString(text, _fontFooter, XBrushes.Gray, MARGIN_LEFT, page.Height - MARGIN_BOTTOM + 10);
        }

        private string FormatNombre(decimal nombre)
        {
            return string.Format("{0:N0}", nombre).Replace(",", " ");
        }

        private string FormatNombre(int nombre)
        {
            return string.Format("{0:N0}", nombre).Replace(",", " ");
        }
    }
}