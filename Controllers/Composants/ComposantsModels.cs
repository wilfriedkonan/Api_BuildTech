namespace Api_BuildTech.Controllers.Composants
{
    public class ComposantDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? Etat { get; set; }
        public int? Ordre { get; set; }
        public Guid? IdArticle { get; set; }
        public Guid? IdCatheComposant { get; set; }
        public Guid? IdEntreprise { get; set; }

        // Relations
        public string? NomArticle { get; set; }
        public string? NomCategorie { get; set; }
    }

    public class CreateComposantRequest
    {
        public string Designation { get; set; } = string.Empty;
        public int? Ordre { get; set; }
        public Guid IdArticle { get; set; }
        public Guid IdCatheComposant { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateComposantRequest
    {
        public string? Designation { get; set; }
        public int? Ordre { get; set; }
        public Guid? IdCatheComposant { get; set; }
        public string? Etat { get; set; }
    }

    public class ComposantListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<ComposantDto> Composants { get; set; } = new();
    }
}