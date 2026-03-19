namespace Api_BuildTech.Controllers.MatierePremiere
{
    public class MatierePremiereDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public decimal? Quantite { get; set; }
        public decimal? QuantiteInitial { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public decimal? Montant { get; set; }
        public Guid? IdUnite { get; set; }
        public Guid? IdEntreprise { get; set; }
        public bool? EstSupprimer { get; set; }

        // Relations
        public string? Unite { get; set; }
    }

    public class CreateMatierePremiereRequest
    {
        public string Designation { get; set; } = string.Empty;
        public decimal Quantite { get; set; }
        public decimal PrixUnitaire { get; set; }
        public Guid IdUnite { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateMatierePremiereRequest
    {
        public string? Designation { get; set; }
        public decimal? Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public Guid? IdUnite { get; set; }
    }

    public class MatierePremiereListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal ValeurTotale { get; set; }
        public List<MatierePremiereDto> Matieres { get; set; } = new();
    }
}