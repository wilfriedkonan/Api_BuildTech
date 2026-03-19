namespace Api_BuildTech.Controllers.Categorie
{
    public class CathegorieDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? Code { get; set; }
        public string? Couleur { get; set; }
        public Guid? IdEntreprise { get; set; }
        public Guid? IdDomaine { get; set; }
        public string? Etat { get; set; }

        public int? Ordre { get; set; }
        public bool? EstRestaurant { get; set; }
        public bool? EstEmporte { get; set; }
        public bool? Statut { get; set; }


    }

    public class CreateCathegorieRequest
    {
        public string Designation { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Couleur { get; set; }
        public Guid IdEntreprise { get; set; }
        public Guid? IdDomaine { get; set; }
        public string? Etat { get; set; } = "Actif";
        public int? Ordre { get; set; }
        public bool EstRestaurant { get; set; } = true;
        public bool EstEmporte { get; set; } = false;
        public bool Statut { get; set; } = true;


    }

    public class UpdateCathegorieRequest
    {
        public string? Designation { get; set; }
        public string? Code { get; set; }
        public string? Couleur { get; set; }
        public Guid? IdDomaine { get; set; }
        public string? Etat { get; set; }
        public int? Ordre { get; set; }
        public bool? EstRestaurant { get; set; }
        public bool? EstEmporte { get; set; }
        public bool? Statut { get; set; }

    }

    public class CathegorieListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<CathegorieDto> Categories { get; set; } = new();
    }
}