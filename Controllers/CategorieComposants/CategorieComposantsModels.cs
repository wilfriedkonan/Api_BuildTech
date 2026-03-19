namespace Api_BuildTech.Controllers.CategorieComposants
{
    public class CategorieComposantDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? Etat { get; set; }
        public int? Ordre { get; set; }
        public Guid? IdEntreprise { get; set; }
    }

    public class CreateCategorieComposantRequest
    {
        public string Designation { get; set; } = string.Empty;
        public int? Ordre { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateCategorieComposantRequest
    {
        public string? Designation { get; set; }
        public int? Ordre { get; set; }
        public string? Etat { get; set; }
    }

    public class CategorieComposantListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<CategorieComposantDto> Categories { get; set; } = new();
    }
}