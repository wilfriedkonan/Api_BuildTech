namespace Api_BuildTech.Controllers.MouvementStock
{
    /// <summary>
    /// DTO Mouvement de Stock
    /// </summary>
    public class MouvementStockDto
    {
        public Guid Id { get; set; }
        public DateTime? DateTransaction { get; set; }
        public string? TypeMouvement { get; set; }
        public decimal? Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public decimal? Montant { get; set; }
        public string? Reference { get; set; }
        public string? Commentaire { get; set; }
        public string? Motif { get; set; }
        public Guid? IdArticle { get; set; }
        public Guid? IdMatierePremiere { get; set; }
        public Guid? IdStock { get; set; }
        public Guid? IdAutresMag { get; set; }
        public Guid? IdEntreprise { get; set; }
        public string? ReferenceParentMvt { get; set; }
        public bool? EstSupprimer { get; set; }

        // Relations
        public string? NomArticle { get; set; }
        public string? NomMatiere { get; set; }
    }

    /// <summary>
    /// Requête de création de mouvement
    /// </summary>
    public class CreateMouvementStockRequest
    {
        public DateTime? DateTransaction { get; set; }
        public string TypeMouvement { get; set; } = string.Empty; // "Entree" ou "Sortie"
        public decimal Quantite { get; set; }
        public decimal PrixUnitaire { get; set; }
        public string? Reference { get; set; }
        public string? Commentaire { get; set; }
        public string? Motif { get; set; }
        
        public Guid? IdArticle { get; set; }
        public Guid? IdStock { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    /// <summary>
    /// Requête de mise à jour de mouvement
    /// </summary>
    public class UpdateMouvementStockRequest
    {
        public decimal? Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public string? Commentaire { get; set; }
        public string? Motif { get; set; }
        
    }

    /// <summary>
    /// Métadonnées de pagination
    /// </summary>
    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public bool HasPrevious { get; set; }
        public bool HasNext { get; set; }
    }

    /// <summary>
    /// Réponse liste mouvements avec pagination
    /// </summary>
    public class MouvementStockListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal TotalEntrees { get; set; }
        public decimal TotalSorties { get; set; }
        public decimal ValeurEntrees { get; set; }
        public decimal ValeurSorties { get; set; }
        public decimal SoldeQuantite { get; set; }
        public decimal SoldeValeur { get; set; }
        public List<MouvementStockDto> Mouvements { get; set; } = new();

        // ✅ AJOUTÉ : Pagination
        public PaginationMetadata? Pagination { get; set; }
    }
}