namespace Api_BuildTech.Controllers.Sessions
{
    public class SessionDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public string? Duree { get; set; }
        public bool? EstCloturee { get; set; }
        public Guid? IdEntreprise { get; set; }
        public int Identifiant { get; set; }
    }

    public class CreateSessionRequest
    {
        public string Designation { get; set; } = string.Empty;
        public DateTime DateDebut { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateSessionRequest
    {
        public string? Designation { get; set; }
        public DateTime? DateFin { get; set; }
        public bool? EstCloturee { get; set; }
    }

    public class ClotureSessionRequest
    {
        public DateTime DateFin { get; set; }
    }

    public class SessionListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<SessionDto> Sessions { get; set; } = new();
    }
}