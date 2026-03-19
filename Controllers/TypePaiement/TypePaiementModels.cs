namespace Api_BuildTech.Controllers.TypePaiement
{
    public class TypePaiementDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public Guid? IdEntreprise { get; set; }
        public bool? EstSupprimer { get; set; }
        public int Identifient { get; set; }
    }

    public class CreateTypePaiementRequest
    {
        public string Designation { get; set; } = string.Empty;
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateTypePaiementRequest
    {
        public string? Designation { get; set; }
    }

    public class TypePaiementListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<TypePaiementDto> TypePaiements { get; set; } = new();
    }
}