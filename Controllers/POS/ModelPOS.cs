public class FactureDto
{
    public Guid Id { get; set; }
    public string? NumeroFacture { get; set; }
    public string? Designation { get; set; }
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }
    public string? Message { get; set; }

    // Montants
    public decimal Montant { get; set; }
    public decimal Sous_total { get; set; }
    public decimal Total_final { get; set; }
    public decimal MontantVerser { get; set; }
    public decimal MonnaieRemis { get; set; }
    public decimal RestApayer { get; set; }
    public decimal Remise { get; set; }
    public decimal Remise_globale { get; set; }
    public decimal ValeurRemise_globale { get; set; }
    public decimal ValeurTVA { get; set; }
    public decimal TVA { get; set; }
    public decimal BeneficeSurFact { get; set; }

    // Références
    public Guid? IdTable { get; set; }
    public string? DesignationTable { get; set; }
    public Guid? IdPayement { get; set; }
    public string? DesignationPayement { get; set; }
    public Guid? idUtilisateur { get; set; }
    public string? NomUtilisateur { get; set; }
    public Guid? IdClient { get; set; }
    public string? nomClient { get; set; }
    public Guid IdEntreprise { get; set; }
    public Guid? IdSession { get; set; }

    // États
    public string? Etat { get; set; }
    public string? Statut { get; set; }
    public bool EstAnnuler { get; set; }
    public bool EstSupprimer { get; set; }
    public bool EstEnattente { get; set; }
    public bool estestCloturer { get; set; }

    // Caisse
    public string? Caisse { get; set; }
    public string? Serveur { get; set; }
    public bool Solder { get; set; }

    // Détails
    public List<DetailTransactionDto> Details { get; set; } = new();
}

// ========================================
// DÉTAIL TRANSACTION DTO
// ========================================

public class DetailTransactionDto
{
    public Guid Id { get; set; }
    public string? Designation { get; set; }
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHT { get; set; }
    public decimal PrixUnitaireTTC { get; set; }
    public decimal PrixVente { get; set; }
    public decimal PrixTotal { get; set; }
    public decimal sousTotal { get; set; }
    public decimal TauxTVA { get; set; }
    public decimal MontantTVA { get; set; }
    public decimal valeurRemise { get; set; }
    public decimal PrixAchatUnitaire { get; set; }

    // Spécifications
    public string? Specificite { get; set; }
    public string? DetailComposent { get; set; }
    public string? DetailComposant { get; set; }
    public string? Specification { get; set; }
    public string? domaineAricle { get; set; }

    // Références
    public Guid IdArticle { get; set; }
    public Guid IdFacture { get; set; }
    public Guid? IdServeur { get; set; }
    public Guid? IdCuisinier { get; set; }
    public Guid? IdUser { get; set; }
    public string? DesignationAgent { get; set; }
    public Guid IdEntreprise { get; set; }

    // États
    public string? Etat { get; set; }
    public bool EstExecuter { get; set; }
    public bool estSuite { get; set; }
    public bool estDetaileComd { get; set; }
    public bool estSupprimer { get; set; }
    public bool EstModifier { get; set; }
    public bool EstAvarie { get; set; }
    public bool AutorisationModif { get; set; }

    // Domaines
    public Guid? idDomaine { get; set; }
    public Guid? idTypeService { get; set; }
    public DateTime DateCreation { get; set; }
    public DateTime? DateModification { get; set; }
    public Guid? IdUserModification { get; set; }
}

// ========================================
// REQUÊTES POS (RENOMMÉES POUR ÉVITER COLLISION)
// ========================================

public class CreatePosFactureRequest
{
    public string NumeroFacture { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Message { get; set; }
    public Guid? IdTable { get; set; }
    public Guid? IdClient { get; set; }
    public Guid? idUtilisateur { get; set; }
    public Guid IdEntreprise { get; set; }
    public Guid? IdSession { get; set; }
    public string? Caisse { get; set; }
    public string? Serveur { get; set; }
}

public class AddPosDetailRequest
{
    public Guid IdArticle { get; set; }
    public string? Designation { get; set; }
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHT { get; set; }
    public decimal PrixVente { get; set; }
    public decimal TauxTVA { get; set; }
    public decimal valeurRemise { get; set; } = 0;
    public string? Specificite { get; set; }
    public string? DetailComposent { get; set; }
    public Guid? IdServeur { get; set; }
    public Guid? IdCuisinier { get; set; }
}

/// <summary>
/// Requête INTELLIGENTE pour mettre en attente POS
/// 
/// 📌 CAS 1 : Créer facture + Ajouter articles + Mettre en attente en UN seul endpoint
///    - IdFacture = null/Guid.Empty
///    - NumeroFacture requis
///    - Articles requis (List de détails)
///    → Crée facture + ajoute articles + Met en attente (EstEnattente=1)
///
/// 📌 CAS 2 : Facture existe → Mettre en attente uniquement
///    - IdFacture fourni
///    - Articles = null/vide (ignorés)
///    → Vérifie facture existe + Met en attente
/// </summary>
public class PutOnHoldSmartRequest
{
    // ========================================
    // REQUIS POUR DÉTECTION DE CAS
    // ========================================

    /// <summary>
    /// ID de la facture existante (optionnel)
    /// - Si null/Guid.Empty → Cas 1 (créer facture)
    /// - Si fourni → Cas 2 (mettre en attente facture existante)
    /// </summary>
    public Guid? IdFacture { get; set; }

    /// <summary>
    /// Motif de mise en attente
    /// </summary>
    public string? Motif { get; set; }

    // ========================================
    // CAS 1 UNIQUEMENT : CRÉATION FACTURE
    // ========================================

    /// <summary>
    /// [CAS 1 REQUIS] Numéro de facture
    /// Requis si IdFacture est null/Guid.Empty
    /// </summary>
    public string? NumeroFacture { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Désignation facture
    /// </summary>
    public string? Designation { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Message/Notes
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Table
    /// </summary>
    public Guid? IdTable { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Client
    /// </summary>
    public Guid? IdClient { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Utilisateur
    /// Sinon utilisé depuis JWT context
    /// </summary>
    public Guid? idUtilisateur { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Entreprise
    /// Sinon utilisé depuis JWT context
    /// </summary>
    public Guid? IdEntreprise { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Session
    /// </summary>
    public Guid? IdSession { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Caisse
    /// </summary>
    public string? Caisse { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Serveur
    /// </summary>
    public string? Serveur { get; set; }

    // ========================================
    // CAS 1 REQUIS : ARTICLES
    // ========================================

    /// <summary>
    /// [CAS 1 REQUIS] Liste des articles à ajouter à la facture
    /// 
    /// ⚠️ REQUIS si IdFacture est null/Guid.Empty
    /// ⚠️ IGNORÉ si IdFacture est fourni (facture existe déjà)
    /// </summary>
    public List<AddPosDetailRequest> Articles { get; set; } = new();
}

public class PutPosOnHoldRequest
{
    public Guid IdFacture { get; set; }
    public string? Motif { get; set; }
}

public class CancelPosFactureRequest
{
    public Guid IdFacture { get; set; }
    public string? Motif { get; set; }
}

/// <summary>
/// Requête INTELLIGENTE pour confirmer le paiement POS
/// 
/// 📌 CAS 1 : Créer facture + Ajouter articles + Payer en un seul endpoint
///    - IdFacture = null/Guid.Empty
///    - NumeroFacture requis
///    - Articles requis (List de détails)
///    - Idpayement + MontantVerser
///    → Crée facture + ajoute articles + crée mouvements stock
///
/// 📌 CAS 2 : Facture existe → Payer uniquement
///    - IdFacture fourni
///    - Articles = null/vide (ignorés)
///    - IdPayement + MontantVerser
///    → Vérifie facture existe + confirme paiement + crée mouvements stock
/// </summary>
public class ConfirmPosPaymentRequest
{
    // ========================================
    // CAS 1 & 2 : PAIEMENT (REQUIS)
    // ========================================

    /// <summary>
    /// ID de la facture existante (optionnel)
    /// - Si null/Guid.Empty → Cas 1 (créer facture)
    /// - Si fourni → Cas 2 (payer facture existante)
    /// </summary>
    public Guid? IdFacture { get; set; }

    /// <summary>
    /// Type de paiement (REQUIS)
    /// </summary>
    public Guid IdPayement { get; set; }

    /// <summary>
    /// Montant versé (REQUIS)
    /// </summary>
    public decimal MontantVerser { get; set; }

    /// <summary>
    /// Monnaie remise (optionnel)
    /// </summary>
    public decimal MonnaieRemis { get; set; }

    /// <summary>
    /// Remise globale (optionnel)
    /// </summary>
    public decimal Remise { get; set; } = 0;

    // ========================================
    // CAS 1 UNIQUEMENT : CRÉATION FACTURE
    // ========================================

    /// <summary>
    /// [CAS 1 REQUIS] Numéro de facture
    /// Requis si IdFacture est null/Guid.Empty
    /// </summary>
    public string? NumeroFacture { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Désignation facture
    /// </summary>
    public string? Designation { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Message/Notes
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Table
    /// </summary>
    public Guid? IdTable { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Client
    /// </summary>
    public Guid? IdClient { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Utilisateur
    /// Sinon utilisé depuis JWT context
    /// </summary>
    public Guid? idUtilisateur { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Entreprise
    /// Sinon utilisé depuis JWT context
    /// </summary>
    public Guid? IdEntreprise { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] ID Session
    /// </summary>
    public Guid? IdSession { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Caisse
    /// </summary>
    public string? Caisse { get; set; }

    /// <summary>
    /// [CAS 1 OPTIONNEL] Serveur
    /// </summary>
    public string? Serveur { get; set; }

    // ========================================
    // CAS 1 REQUIS : ARTICLES
    // ========================================

    /// <summary>
    /// [CAS 1 REQUIS] Liste des articles à ajouter à la facture
    /// 
    /// ⚠️ REQUIS si IdFacture est null/Guid.Empty
    /// ⚠️ IGNORÉ si IdFacture est fourni (facture existe déjà)
    /// 
    /// Chaque détail contient :
    /// - IdArticle
    /// - Quantite
    /// - PrixUnitaireHT
    /// - TauxTVA
    /// - etc.
    /// </summary>
    public List<AddPosDetailRequest> Articles { get; set; } = new();
}

// ========================================
// TYPES DE PAIEMENT
// ========================================

public class TypePaiementDto
{
    public Guid Id { get; set; }
    public string? Designation { get; set; }
    public bool EstDefaut { get; set; }
    public bool estSupprimer { get; set; }
    public Guid IdEntreprise { get; set; }
}

// ========================================
// MOUVEMENTS STOCK
// ========================================

public class MouvementStockDto
{
    public Guid Id { get; set; }
    public Guid IdArticle { get; set; }
    public string? Reference { get; set; }
    public DateTime DateTransaction { get; set; }
    public string? TypeMouvement { get; set; }
    public decimal Quantite { get; set; }
    public decimal QuantiteAvant { get; set; }
    public decimal QuantiteApres { get; set; }
    public decimal PrixUnitaire { get; set; }
    public decimal PrixTotal { get; set; }
    public string? Utilisateur { get; set; }
    public Guid IdEntreprise { get; set; }
    public string? Motif { get; set; }
}

// ========================================
// RÉPONSES
// ========================================

public class FactureCompleteResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public FactureDto Facture { get; set; } = new();
    public List<DetailTransactionDto> Details { get; set; } = new();
}

public class FacturesEnAttenteResponse
{
    public bool Success { get; set; }
    public int Total { get; set; }
    public List<FactureDto> Factures { get; set; } = new();
    public List<DetailTransactionDto> Details { get; set; } = new();
}

public class FactureSummaryDto
{
    public Guid Id { get; set; }
    public string? NumeroFacture { get; set; }
    public DateTime DateCreation { get; set; }
    public decimal Total_final { get; set; }
    public decimal RestApayer { get; set; }
    public string? Etat { get; set; }
    public bool EstEnattente { get; set; }
    public string? nomClient { get; set; }
    public int NombreLignes { get; set; }
}

/// <summary>
/// Réponse intelligente du paiement POS
/// Indique quel cas a été traité
/// </summary>
/// <summary>
/// Réponse intelligente de mise en attente POS
/// Indique quel cas a été traité
/// </summary>
public class PosOnHoldResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }

    /// <summary>
    /// "Created" = Facture créée + articles ajoutés + mise en attente (CAS 1)
    /// "OnHold" = Facture mise en attente (CAS 2)
    /// </summary>
    public string? OnHoldMode { get; set; }

    /// <summary>
    /// La facture après traitement
    /// </summary>
    public FactureDto Facture { get; set; } = new();

    /// <summary>
    /// Nombre d'articles ajoutés (CAS 1 uniquement)
    /// </summary>
    public int ArticlesAdded { get; set; } = 0;
}

public class PosPaymentResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }

    /// <summary>
    /// "Created" = Facture créée + articles ajoutés + paiement confirmé (CAS 1)
    /// "Paid" = Facture payée (CAS 2)
    /// </summary>
    public string? PaymentMode { get; set; }

    /// <summary>
    /// La facture après traitement
    /// </summary>
    public FactureDto Facture { get; set; } = new();

    /// <summary>
    /// Nombre d'articles ajoutés (CAS 1 uniquement)
    /// </summary>
    public int ArticlesAdded { get; set; } = 0;

    /// <summary>
    /// Nombre de mouvements stock créés
    /// </summary>
    public int StockMovementsCreated { get; set; } = 0;
}

// ════════════════════════════════════════════════════════════════════════════
// AJOUTER À PosModels.cs - REMPLACER LES 3 RÉPONSES PRÉCÉDENTES
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Statistiques du jour : Ventes + CA + Panier moyen en UN SEUL endpoint
/// </summary>
public class StatistiquesJourResponse
{
    /// <summary>
    /// Nombre de factures payées aujourd'hui
    /// </summary>
    public int NombreVentes { get; set; }

    /// <summary>
    /// Chiffre d'affaires du jour (TTC)
    /// </summary>
    public decimal ChiffreAffaires { get; set; }

    /// <summary>
    /// Sous-total (HT)
    /// </summary>
    public decimal SousTotal { get; set; }

    /// <summary>
    /// Total TVA collectée
    /// </summary>
    public decimal TotalTVA { get; set; }

    /// <summary>
    /// Panier moyen (CA / Nombre de ventes)
    /// </summary>
    public decimal PanierMoyen { get; set; }

    /// <summary>
    /// Total des remises du jour
    /// </summary>
    public decimal TotalRemises { get; set; }

    /// <summary>
    /// Monnaie (XOF)
    /// </summary>
    public string Monnaie { get; set; } = "XOF";

    /// <summary>
    /// Date de la statistique
    /// </summary>
    public DateTime DateStatistique { get; set; }

    /// <summary>
    /// Messages de résumé
    /// </summary>
    public List<string> Messages { get; set; } = new();

}
// ════════════════════════════════════════════════════════════════════════════
// AJOUTER À PosModels.cs
// DTOs pour les rapports PDF avec pagination
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request pour générer un rapport
/// </summary>
public class RapportRequest
{
    /// <summary>
    /// Type de rapport: "ventes", "ventes-quantite", "stock"
    /// </summary>
    public string TypeRapport { get; set; }

    /// <summary>
    /// Date début (optionnel, default: début du mois)
    /// </summary>
    public DateTime? DateDebut { get; set; }

    /// <summary>
    /// Date fin (optionnel, default: aujourd'hui)
    /// </summary>
    public DateTime? DateFin { get; set; }

    /// <summary>
    /// Numéro de page (default: 1)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Nombre de lignes par page (default: 10)
    /// </summary>
    public int LignesParPage { get; set; } = 10;

    /// <summary>
    /// Exporter en PDF (true) ou JSON (false)
    /// </summary>
    public bool ExporterPDF { get; set; } = true;
}

/// <summary>
/// Ligne du rapport Ventes (Dates)
/// </summary>
public class RapportVentesLigneDto
{
    public DateTime Date { get; set; }
    public string NumeroFacture { get; set; }
    public string Designation { get; set; }
    public decimal MontantTotal { get; set; }
}

/// <summary>
/// Rapport Ventes paginé
/// </summary>
public class RapportVentesDto
{
    public string Titre { get; set; } = "RAPPORT DE VENTES";
    public string NomEntreprise { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public int PageActuelle { get; set; }
    public int TotalPages { get; set; }
    public int TotalLignes { get; set; }
    public decimal SousTotal { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalGeneral { get; set; }
    public string Devise { get; set; } = "FCFA";
    public List<RapportVentesLigneDto> Lignes { get; set; } = new();
}

/// <summary>
/// Ligne du rapport Ventes Quantité+Valeur
/// </summary>
public class RapportVentesQuantiteLigneDto
{
    public string Designation { get; set; }
    public decimal Quantite { get; set; }
    public decimal MontantTotal { get; set; }
}

/// <summary>
/// Rapport Ventes Quantité+Valeur paginé
/// </summary>
public class RapportVentesQuantiteDto
{
    public string Titre { get; set; } = "RAPPORT VENTES - QUANTITÉ ET VALEUR";
    public string NomEntreprise { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public int PageActuelle { get; set; }
    public int TotalPages { get; set; }
    public int TotalLignes { get; set; }
    public decimal QuantiteTotalVendue { get; set; }
    public decimal MontantTotalVendu { get; set; }
    public string Devise { get; set; } = "FCFA";
    public List<RapportVentesQuantiteLigneDto> Lignes { get; set; } = new();
}

/// <summary>
/// Ligne du rapport Stock
/// </summary>
public class RapportStockLigneDto
{
    public string Designation { get; set; }
    public string CodeArticle { get; set; }
    public decimal StockActuel { get; set; }
}

/// <summary>
/// Rapport Stock paginé
/// </summary>
public class RapportStockDto
{
    public string Titre { get; set; } = "RAPPORT DE STOCK";
    public string NomEntreprise { get; set; }
    public DateTime DateRapport { get; set; }
    public int PageActuelle { get; set; }
    public int TotalPages { get; set; }
    public int TotalLignes { get; set; }
    public decimal StockTotalArticles { get; set; }
    public List<RapportStockLigneDto> Lignes { get; set; } = new();
}

/// <summary>
/// Réponse rapport PDF/JSON
/// </summary>
public class RapportResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public byte[] ContenuPDF { get; set; }
    public string NomFichier { get; set; }
    public string ContentType { get; set; }
    public object Donnees { get; set; }
}