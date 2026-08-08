namespace Api_BuildTech.Controllers.Ecommerce
{
    public class CommandeDto
    {
        public Guid Id { get; set; }
        public Guid IdEntreprise { get; set; }
        public Guid IdBoutique { get; set; }
        public string? NumeroCommande { get; set; }
        public string ClientWhatsApp { get; set; } = "";
        public string? LivraisonTexte { get; set; }
        public double? LivraisonLat { get; set; }
        public double? LivraisonLng { get; set; }
        public decimal SousTotal { get; set; }
        public decimal Total { get; set; }
        public string Etat { get; set; } = "EnAttente";
        public bool NotificationBoutiqueEnvoyee { get; set; }
        public bool NotificationClientEnvoyee { get; set; }
        public Guid? IdFacture { get; set; }
        public DateTime DateCommande { get; set; }
        public DateTime? DateMiseAJour { get; set; }
    }

    public class DetailCommandeDto
    {
        public Guid Id { get; set; }
        public Guid IdArticle { get; set; }
        public string NomArticle { get; set; } = "";
        public decimal PrixUnitaire { get; set; }
        public decimal Quantite { get; set; }
        public decimal SousTotalLigne { get; set; }
    }

    public class CommandeDetailDto
    {
        public CommandeDto Commande { get; set; } = new();
        public List<DetailCommandeDto> Lignes { get; set; } = new();
    }

    public class CommandeListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public PaginationMetadata? Pagination { get; set; }
        public List<CommandeDto> Commandes { get; set; } = new();
    }

    // ⚠️ Si PaginationMetadata existe déjà globalement, supprimer cette classe
    // et ajouter le using correspondant à la place.
    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public bool HasPrevious { get; set; }
        public bool HasNext { get; set; }
    }
}
