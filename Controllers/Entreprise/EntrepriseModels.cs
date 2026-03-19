namespace Api_BuildTech.Controllers.Entreprise
{
    public class EntrepriseDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? Identifiant { get; set; }
        public string? Localisation { get; set; }
        public string? Contact { get; set; }
        public string? Email { get; set; }
        public string? Pays { get; set; }
        public string? Ville { get; set; }
        public string? Commune { get; set; }
        public string? NRC { get; set; }
        public bool Autorisation { get; set; }
        public string? CodeEntreprise { get; set; }
        public bool IsActive { get; set; }
        public string? SubscriptionStatus { get; set; }
        public DateTime? SubscriptionEndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? IdOrganisation { get; set; }
    }

    public class CreateEntrepriseRequest
    {
        public string Designation { get; set; } = string.Empty;
        public string Identifiant { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Contact { get; set; }
        public string? Localisation { get; set; }
        public string? Pays { get; set; }
        public string? Ville { get; set; }
        public string? Commune { get; set; }
        public string? CodeEntreprise { get; set; }
        public Guid IdOrganisation { get; set; } = Guid.Empty;

    }

    public class UpdateEntrepriseRequest
    {
        public string? Designation { get; set; }
        public string? Email { get; set; }
        public string? Contact { get; set; }
        public string? Localisation { get; set; }
        public string? Pays { get; set; }
        public string? Ville { get; set; }
        public string? Commune { get; set; }
        public bool? IsActive { get; set; }
    }

    public class EntrepriseListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<EntrepriseDto> Entreprises { get; set; } = new();
    }
}