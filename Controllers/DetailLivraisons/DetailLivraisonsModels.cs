namespace Api_BuildTech.Controllers.DetailLivraisons
{
    public class DetailLivraisonDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public DateTime? Date { get; set; }
        public string? Lieu { get; set; }
        public decimal FraisLivraison { get; set; }
        public string? Etat { get; set; }
        public decimal? MotantFacture { get; set; } // Note: Typo dans la BDD
        public decimal? TotalFacture { get; set; }
        public string? Satue { get; set; } // Note: Typo dans la BDD
        public bool? EstAnnuler { get; set; }
        public string? Justificatif { get; set; }
        public Guid? IdLivraison { get; set; }
        public Guid? IdFacture { get; set; }
        public Guid? IdEntreprise { get; set; }
    }

    public class CreateDetailLivraisonRequest
    {
        public string Designation { get; set; } = string.Empty;
        public string Lieu { get; set; } = string.Empty;
        public decimal FraisLivraison { get; set; }
        public decimal MotantFacture { get; set; }
        public Guid IdLivraison { get; set; }
        public Guid IdFacture { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateDetailLivraisonRequest
    {
        public string? Designation { get; set; }
        public string? Lieu { get; set; }
        public decimal? FraisLivraison { get; set; }
        public string? Satue { get; set; }
        public bool? EstAnnuler { get; set; }
        public string? Justificatif { get; set; }
    }

    public class DetailLivraisonListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal TotalFrais { get; set; }
        public List<DetailLivraisonDto> Details { get; set; } = new();
    }
}