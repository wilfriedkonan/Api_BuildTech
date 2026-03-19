namespace Api_BuildTech.Controllers.TypeServices
{
    public class TypeServiceDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public Guid? IdEntreprise { get; set; }
        public bool? EstSupprimer { get; set; }
        public int Identifient { get; set; }
    }

    public class CreateTypeServiceRequest
    {
        public string Designation { get; set; } = string.Empty;
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateTypeServiceRequest
    {
        public string? Designation { get; set; }
    }

    public class TypeServiceListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<TypeServiceDto> TypeServices { get; set; } = new();
    }
}