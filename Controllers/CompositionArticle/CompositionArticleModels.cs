namespace Api_BuildTech.Controllers.CompositionArticle
{
    public class CompositionArticleDto
    {
        public Guid Id { get; set; }
        public decimal? Quantite { get; set; }
        public Guid? IdMatierePremiere { get; set; }
        public Guid? IdArticle { get; set; }
        public Guid? IdEntreprise { get; set; }
        public bool? EstSupprimer { get; set; }

        // Relations
        public string? NomMatiere { get; set; }
        public string? NomArticle { get; set; }
    }

    public class CreateCompositionArticleRequest
    {
        public decimal Quantite { get; set; }
        public Guid IdMatierePremiere { get; set; }
        public Guid IdArticle { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateCompositionArticleRequest
    {
        public decimal? Quantite { get; set; }
    }

    public class CompositionArticleListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<CompositionArticleDto> Compositions { get; set; } = new();
    }
}