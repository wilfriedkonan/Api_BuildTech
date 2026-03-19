namespace Api_BuildTech.Controllers.Serveur
{
    public class ServeurDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public bool? EstSupprimer { get; set; }
        public Guid? IdEntreprise { get; set; }
        public int Identifiant { get; set; }
    }

    public class CreateServeurRequest
    {
        public string Designation { get; set; } = string.Empty;
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateServeurRequest
    {
        public string? Designation { get; set; }
    }

    public class ServeurListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<ServeurDto> Serveurs { get; set; } = new();
    }
}