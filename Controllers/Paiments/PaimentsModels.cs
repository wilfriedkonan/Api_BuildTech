namespace Api_BuildTech.Controllers.Paiments
{
    public class PaiementDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public decimal? Montant { get; set; }
        public DateTime? Date { get; set; }
        public Guid? IdClient { get; set; }
        public Guid? IdTypePaiement { get; set; }
        public Guid? IdEntreprise { get; set; }
        public int Identifiant { get; set; }

        // Relations
        public string? NomClient { get; set; }
        public string? TypePaiement { get; set; }
    }

    public class CreatePaiementRequest
    {
        public string Designation { get; set; } = string.Empty;
        public decimal Montant { get; set; }
        public DateTime Date { get; set; }
        public Guid? IdClient { get; set; }
        public Guid IdTypePaiement { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdatePaiementRequest
    {
        public string? Designation { get; set; }
        public decimal? Montant { get; set; }
        public DateTime? Date { get; set; }
        public Guid? IdClient { get; set; }
        public Guid? IdTypePaiement { get; set; }
    }

    public class PaiementListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal TotalMontant { get; set; }
        public List<PaiementDto> Paiements { get; set; } = new();
    }
}