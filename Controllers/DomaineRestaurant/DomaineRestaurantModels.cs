namespace Api_BuildTech.Controllers.DomaineRestaurant
{
    public class DomaineRestaurantDto
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? CheminImprimente { get; set; }
        public int? Ordre { get; set; }
        public string? Etat { get; set; }
        public Guid? IdEntreprise { get; set; }
        public int Identifient { get; set; }
    }

    public class CreateDomaineRestaurantRequest
    {
        public string Designation { get; set; } = string.Empty;
        public string? CheminImprimente { get; set; }
        public int? Ordre { get; set; }
        public Guid IdEntreprise { get; set; }
    }

    public class UpdateDomaineRestaurantRequest
    {
        public string? Designation { get; set; }
        public string? CheminImprimente { get; set; }
        public int? Ordre { get; set; }
        public string? Etat { get; set; }
    }

    public class DomaineRestaurantListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<DomaineRestaurantDto> Domaines { get; set; } = new();
    }
}