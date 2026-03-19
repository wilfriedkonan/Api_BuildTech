namespace Api_BuildTech.Controllers.Clients
{
    public class ClientDto
    {
        public Guid Id { get; set; }
        public string? Nom { get; set; }
        public string? Prenoms { get; set; }
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public DateTime? DateNaissance { get; set; }
        public string? LieuNaissance { get; set; }
        public string? Addresse { get; set; }
        public bool? EstAbonner { get; set; }
        public string? NumeroCNI { get; set; }
        public string? ImageDoctument { get; set; }
        public Guid? IdEntreprise { get; set; }
        public string? Etat { get; set; }
        public string? Ident { get; set; }
    }

    public class CreateClientRequest
    {
        public string Nom { get; set; } = string.Empty;
        public string? Prenoms { get; set; }
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public DateTime? DateNaissance { get; set; }
        public string? LieuNaissance { get; set; }
        public string? Addresse { get; set; }
        public bool EstAbonner { get; set; } = false;
        public string? NumeroCNI { get; set; }
        public Guid IdEntreprise { get; set; }
        public string? Etat { get; set; } = "Actif";
    }

    public class UpdateClientRequest
    {
        public string? Nom { get; set; }
        public string? Prenoms { get; set; }
        public string? Telephone { get; set; }
        public string? Email { get; set; }
        public DateTime? DateNaissance { get; set; }
        public string? LieuNaissance { get; set; }
        public string? Addresse { get; set; }
        public string? NumeroCNI { get; set; }
        public string? Etat { get; set; }
    }

    public class UpdateAbonnementRequest
    {
        public bool EstAbonner { get; set; }
    }

    public class ClientListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int TotalAbonnes { get; set; }
        public List<ClientDto> Clients { get; set; } = new();
    }
}