public class ActivityDataDto
{
    public SalesStatsDto Sales { get; set; }
    public RevenueStatsDto Revenue { get; set; }
    public CustomersStatsDto Customers { get; set; }
    public InventoryStatsDto Inventory { get; set; }
}

/// <summary>
/// Statistiques des ventes
/// </summary>
public class SalesStatsDto
{
    public int Today { get; set; }  // Nombre de ventes aujourd'hui
    public int ThisWeek { get; set; }  // Ventes cette semaine
    public int ThisMonth { get; set; }  // Ventes ce mois
    public decimal Growth { get; set; }  // % croissance par rapport à période précédente
}

/// <summary>
/// Statistiques des revenus
/// </summary>
public class RevenueStatsDto
{
    public decimal Today { get; set; }  // Revenus aujourd'hui
    public decimal ThisWeek { get; set; }  // Revenus cette semaine
    public decimal ThisMonth { get; set; }  // Revenus ce mois
    public decimal Growth { get; set; }  // % croissance
}

/// <summary>
/// Statistiques des clients
/// Note: Un "client" = une facture unique (selon les exigences)
/// </summary>
public class CustomersStatsDto
{
    public int Total { get; set; }  // Total de clients/factures uniques
    public int New { get; set; }  // Nouveaux clients ce mois
    public int Returning { get; set; }  // Clients récurrents ce mois
    public decimal Growth { get; set; }  // % croissance
}

/// <summary>
/// Statistiques d'inventaire
/// </summary>
public class InventoryStatsDto
{
    public int TotalProducts { get; set; }  // Nombre total de produits
    public int LowStock { get; set; }  // Articles en stock faible (< 10)
    public int OutOfStock { get; set; }  // Articles en rupture (= 0)
}

/// <summary>
/// Données pour le graphique de ventes
/// </summary>
public class SalesChartDataDto
{
    public string Date { get; set; }  // Format: "DD/MM"
    public int Sales { get; set; }  // Nombre de ventes
    public decimal Revenue { get; set; }  // Montant revenus
}

/// <summary>
/// Réponse Dashboard complète
/// </summary>
public class DashboardResponseDto
{
    public ActivityDataDto ActivityData { get; set; }
    public List<SalesChartDataDto> ChartData { get; set; }
}

/// <summary>
/// Données brutes pour les statistiques (depuis SQL)
/// </summary>
public class RawStatisticsData
{
    public int SalesToday { get; set; }
    public int SalesThisWeek { get; set; }
    public int SalesThisMonth { get; set; }
    public int SalesLastMonth { get; set; }

    public decimal RevenuesToday { get; set; }
    public decimal RevenuesThisWeek { get; set; }
    public decimal RevenuesThisMonth { get; set; }
    public decimal RevenuesLastMonth { get; set; }

    public int TotalClients { get; set; }
    public int NewClientsThisMonth { get; set; }
    public int ReturningClientsThisMonth { get; set; }
    public int ClientsLastMonth { get; set; }

    public int TotalProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int OutOfStockProducts { get; set; }
}
