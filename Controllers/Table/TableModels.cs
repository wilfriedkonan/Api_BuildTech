namespace Api_BuildTech.Controllers.Table
{
    public class TableDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public bool? Disponible { get; set; }
        public string? Etat { get; set; }
        public Guid? IdEntreprise { get; set; }
        public Guid? IdUtilisateur { get; set; }
        public string? Statue { get; set; }
        public int? Ordre { get; set; }
        public int? Identifient { get; set; }
        public Guid? IdFacture { get; set; }
        public bool? EstEncourEdition { get; set; }
        public string? ServeurAffecte { get; set; }
    }

    public class CreateTableRequest
    {
        public string Designation { get; set; } = string.Empty;
        public bool Disponible { get; set; } = true;
        public string? Etat { get; set; } = "Actif";
        public Guid IdEntreprise { get; set; }
        public string? Statue { get; set; } = "Libre";
        public int? Ordre { get; set; }
    }

    public class UpdateTableRequest
    {
        public string? Designation { get; set; }
        public bool? Disponible { get; set; }
        public string? Etat { get; set; }
        public string? Statue { get; set; }
        public int? Ordre { get; set; }
        public string? ServeurAffecte { get; set; }
    }

    public class AffecterServeurRequest
    {
        public Guid IdUtilisateur { get; set; }
        public string ServeurAffecte { get; set; } = string.Empty;
    }

    public class TableListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<TableDto> Tables { get; set; } = new();
    }
}