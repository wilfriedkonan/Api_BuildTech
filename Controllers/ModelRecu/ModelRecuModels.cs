namespace Api_BuildTech.Controllers.ModelRecu
{
    public class ModelRecuDto
    {
        public Guid Id { get; set; }
        public string? Entete1 { get; set; }
        public string? Entete2 { get; set; }
        public string? Localisation { get; set; }
        public string? Tel { get; set; }
        public string? TypeActivite { get; set; }
        public string? Message { get; set; }
        public string? Etat { get; set; }
        public Guid? IdEntreprise { get; set; }
    }

    public class CreateModelRecuRequest
    {
        public string? Entete1 { get; set; }
        public string? Entete2 { get; set; }
        public string? Localisation { get; set; }
        public string? Tel { get; set; }
        public string? TypeActivite { get; set; }
        public string? Message { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateModelRecuRequest
    {
        public string? Entete1 { get; set; }
        public string? Entete2 { get; set; }
        public string? Localisation { get; set; }
        public string? Tel { get; set; }
        public string? TypeActivite { get; set; }
        public string? Message { get; set; }
        public string? Etat { get; set; }
    }

    public class ModelRecuResponse
    {
        public bool Success { get; set; }
        public ModelRecuDto? Data { get; set; }
        public string? Message { get; set; }
    }
}