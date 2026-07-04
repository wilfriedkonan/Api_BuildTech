namespace Api_BuildTech.Controllers.Plans
{
    public class PlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int MaxUsers { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public int MaxStorageMB { get; set; }
        public bool IsActive { get; set; }

        // Propriétés calculées pour l'affichage
        public bool HasUnlimitedUsers => MaxUsers == -1;
        public bool HasUnlimitedInvoices => MaxInvoicesPerMonth == -1;
        public bool HasUnlimitedStorage => MaxStorageMB == -1;
        public string FormattedPrice
        {
            get
            {
                var cultureInfo = new System.Globalization.CultureInfo("fr-CI");
                return Price.ToString("C", cultureInfo);
            }
        }
        public string FormattedStorage => MaxStorageMB == -1 ? "Illimité" : $"{MaxStorageMB} MB";
        public string FormattedInvoices => MaxInvoicesPerMonth == -1 ? "Illimité" : $"{MaxInvoicesPerMonth}/mois";
        public string FormattedUsers => MaxUsers == -1 ? "Illimité" : $"{MaxUsers} utilisateurs";
    }

    public class PlanWithFeaturesDto : PlanDto
    {
        public List<string> Features { get; set; } = new();
        public List<string> Limitations { get; set; } = new();
        public string? RecommendedFor { get; set; }
        public bool IsPopular { get; set; }
        public string? Badge { get; set; }
    }

    public class PlanComparisonDto
    {
        public List<PlanDto> Plans { get; set; } = new();
        public List<ComparisonFeature> Features { get; set; } = new();
    }

    public class ComparisonFeature
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Dictionary<Guid, string> PlanValues { get; set; } = new();
    }

    public class PlanListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int TotalActifs { get; set; }
        public List<PlanDto> Plans { get; set; } = new();
    }

    public class PlanFeaturesResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public List<PlanWithFeaturesDto> Plans { get; set; } = new();
    }

}