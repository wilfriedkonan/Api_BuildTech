namespace Api_BuildTech.Controllers.UniteMesures
{
    public class UniteMesureDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public Guid? IdEntreprise { get; set; }
        public bool? EstSupprimer { get; set; }
        public int Identifiant { get; set; }
    }

    public class CreateUniteMesureRequest
    {
        public string Designation { get; set; } = string.Empty;
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateUniteMesureRequest
    {
        public string? Designation { get; set; }
    }

    public class UniteMesureListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<UniteMesureDto> UniteMesures { get; set; } = new();
    }
}