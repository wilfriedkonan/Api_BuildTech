namespace Api_BuildTech.Controllers.Livreur
{
    public class LivreurDto
    {
        public Guid Id { get; set; }
        public string? Designatin { get; set; } // Note: Typo dans la BDD
        public string? Contact { get; set; }
        public string? NCni { get; set; }
        public string? Etat { get; set; }
        public DateTime? DateCraation { get; set; } // Note: Typo dans la BDD
        public bool? EstDiponible { get; set; }
        public bool? EstEnAttente { get; set; }
        public Guid? IdEntreprise { get; set; }
    }

    public class CreateLivreurRequest
    {
        public string Designatin { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string? NCni { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateLivreurRequest
    {
        public string? Designatin { get; set; }
        public string? Contact { get; set; }
        public string? NCni { get; set; }
        public bool? EstDiponible { get; set; }
        public bool? EstEnAttente { get; set; }
        public string? Etat { get; set; }
    }

    public class LivreurListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int TotalDisponibles { get; set; }
        public List<LivreurDto> Livreurs { get; set; } = new();
    }
}