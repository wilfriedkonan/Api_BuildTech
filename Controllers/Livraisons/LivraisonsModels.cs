namespace Api_BuildTech.Controllers.Livraisons
{
    public class LivraisonDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? DesignationLivreur { get; set; }
        public decimal? Prix { get; set; }
        public string? Satut { get; set; } // Note: Typo dans la BDD
        public int? TotalCommande { get; set; }
        public decimal? PrixTotal { get; set; }
        public Guid? IdLivreur { get; set; }
        public DateTime? Date { get; set; }
        public string? Etat { get; set; }
        public bool? EstEnCours { get; set; }
        public bool? EstTerminer { get; set; }
        public string? DesignationBloc { get; set; }
        public Guid? IdBlockCommande { get; set; }
        public Guid? IdFrais { get; set; }
        public Guid? IdEntreprise { get; set; }
    }

    public class CreateLivraisonRequest
    {
        public string Designation { get; set; } = string.Empty;
        public decimal Prix { get; set; }
        public Guid IdLivreur { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateLivraisonRequest
    {
        public string? Designation { get; set; }
        public decimal? Prix { get; set; }
        public string? Satut { get; set; }
        public bool? EstEnCours { get; set; }
        public bool? EstTerminer { get; set; }
    }

    public class TerminerLivraisonRequest
    {
        public decimal? PrixTotal { get; set; }
    }

    public class LivraisonListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal TotalPrix { get; set; }
        public List<LivraisonDto> Livraisons { get; set; } = new();
    }
}