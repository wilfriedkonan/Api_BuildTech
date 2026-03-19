namespace Api_BuildTech.Controllers.Organisation
{
    public class OrganisationDto
    {
        public Guid Id { get; set; }
        public string Identifiant { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public string? Etat { get; set; }
        public bool? EstActif { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateOrganisationRequest
    {
        public string Identifiant { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public string? Etat { get; set; } = "Actif";
        public bool EstActif { get; set; } = true;
    }

    public class UpdateOrganisationRequest
    {
        public string? Designation { get; set; }
        public string? Etat { get; set; }
        public bool? EstActif { get; set; }
    }

    public class OrganisationListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int TotalActives { get; set; }
        public List<OrganisationDto> Organisations { get; set; } = new();
    }
}