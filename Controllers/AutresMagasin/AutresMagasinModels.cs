namespace Api_BuildTech.Controllers.AutresMagasin
{
    public class AutresMagasinDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? Etat { get; set; }
        public Guid? IdEntreprise { get; set; }
    }

    public class CreateAutresMagasinRequest
    {
        public string Designation { get; set; } = string.Empty;
        public string? Etat { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateAutresMagasinRequest
    {
        public string? Designation { get; set; }
        public string? Etat { get; set; }
        public Guid? IdUnite { get; set; }
    }

    public class AutresMagasinListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<AutresMagasinDto> Magasins { get; set; } = new();
    }
}