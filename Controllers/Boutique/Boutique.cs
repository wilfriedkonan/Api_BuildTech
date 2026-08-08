namespace Api_BuildTech.Controllers.Boutique
{
    public class BoutiqueDto
    {
        public Guid IdBoutique { get; set; }
        public Guid IdEntreprise { get; set; }
        public Guid IdTypeBoutique { get; set; }
        public string NomBoutique { get; set; } = "";
        public string SlugBoutique { get; set; } = "";
        public string CodeQR { get; set; } = "";
        public string? EmailBoutique { get; set; }
        public string? TelephoneBoutique { get; set; }
        public string? AdresseBoutique { get; set; }
        public string? LogoBoutique { get; set; }
        public string? DescriptionBoutique { get; set; }
        public string? URLDomainCustom { get; set; }
        public string TypeBoutique { get; set; } = "";
        public string? CodeType { get; set; }
        public string? IconeType { get; set; }
        public bool EstActif { get; set; }
        public bool PermitCommandeEnLigne { get; set; }
        public bool PermitAccesQR { get; set; }
        public bool PermitLivraison { get; set; }
        public bool PermitRetrait { get; set; }
        public int NombreArticles { get; set; }
        public int NombreCategories { get; set; }
        public DateTime? DateCreation { get; set; }
        public DateTime? DateMiseAJour { get; set; }
    }

    public class TypeBoutiqueDto
    {
        public Guid IdTypeBoutique { get; set; }
        public string NomType { get; set; } = "";
        public string CodeType { get; set; } = "";
        public string? Description { get; set; }
        public string? IconeType { get; set; }
        public bool PermitCommandeParDefaut { get; set; }
        public bool PermitLivraisonParDefaut { get; set; }
        public bool PermitRetraitParDefaut { get; set; }
        public bool PermitReservationParDefaut { get; set; }
    }

    public class CreateBoutiqueRequest
    {
        public Guid IdEntreprise { get; set; }
        public string TypeBoutique { get; set; } = ""; // 'Restaurant' ou 'Magasin'
        public string NomBoutique { get; set; } = "";
        public string? EmailBoutique { get; set; }
        public string? TelephoneBoutique { get; set; }
        public string? AdresseBoutique { get; set; }
        public string? LogoBoutique { get; set; }
        public string? DescriptionBoutique { get; set; }
    }

    public class UpdateBoutiqueRequest
    {
        public string? NomBoutique { get; set; }
        public string? EmailBoutique { get; set; }
        public string? TelephoneBoutique { get; set; }
        public string? AdresseBoutique { get; set; }
        public string? LogoBoutique { get; set; }
        public string? DescriptionBoutique { get; set; }
        public bool? PermitCommandeEnLigne { get; set; }
        public bool? PermitLivraison { get; set; }
        public bool? PermitRetrait { get; set; }
    }

    public class BoutiqueListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public PaginationMetadata? Pagination { get; set; }
        public List<BoutiqueDto> Boutiques { get; set; } = new();
    }

    /// <summary>
    /// ⚠️ Si PaginationMetadata existe déjà globalement (ex: Api_BuildTech.Services),
    /// supprimer cette classe et ajouter le bon using à la place.
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

    // ════════════════════════════════════════════════════════════════════════════
    // DTOs Catalogue Public
    // ════════════════════════════════════════════════════════════════════════════

    public class CatalogueDto
    {
        public Guid IdBoutique { get; set; }
        public string NomBoutique { get; set; } = "";
        public string? LogoBoutique { get; set; }
        public string? DescriptionBoutique { get; set; }
        public string TypeBoutique { get; set; } = ""; // Pour adapter le design côté front
        public string? CodeType { get; set; }
        public string? IconeType { get; set; }
        public string Mode { get; set; } = "shop"; // "view" (QR/lecture seule) ou "shop" (commande)
        public bool PermitCommande { get; set; }
        public List<CataloguePublicCategoryDto> Categories { get; set; } = new();
    }

    public class CataloguePublicCategoryDto
    {
        public Guid IdCategorie { get; set; }
        public string NomCategorie { get; set; } = "";
        public string? Description { get; set; }
        public string? ImageURL { get; set; }
        public List<CataloguePublicArticleDto> Articles { get; set; } = new();
    }

    public class CataloguePublicArticleDto
    {
        public Guid IdArticle { get; set; }
        public string NomArticle { get; set; } = "";
        public string? DescriptionArticle { get; set; }
        public decimal PrixVente { get; set; }
        public decimal? PrixPromo { get; set; }
        public bool EstPromo { get; set; }
        public string? ImageURL { get; set; }
        public bool EstEnStock { get; set; }
    }
}