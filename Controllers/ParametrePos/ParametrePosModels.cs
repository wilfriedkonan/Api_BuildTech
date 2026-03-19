namespace Api_BuildTech.Controllers.ParametrePos
{
    public class ParametrePosDto
    {
        public Guid Id { get; set; }
        public string? DesignationEntreprise { get; set; }
        public bool? EstPosFullScreen { get; set; }
        public bool? EstPosAvecCalculeMonnaie { get; set; }
        public Guid? IdEntreprise { get; set; }
    }

    public class CreateParametrePosRequest
    {
        public string? DesignationEntreprise { get; set; }
        public bool EstPosFullScreen { get; set; }
        public bool EstPosAvecCalculeMonnaie { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateParametrePosRequest
    {
        public string? DesignationEntreprise { get; set; }
        public bool? EstPosFullScreen { get; set; }
        public bool? EstPosAvecCalculeMonnaie { get; set; }
    }

    public class ParametrePosResponse
    {
        public bool Success { get; set; }
        public ParametrePosDto? Data { get; set; }
        public string? Message { get; set; }
    }
}