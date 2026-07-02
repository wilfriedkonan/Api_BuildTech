using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Statistics
{
    public class StatisticsService : DatabaseService
    {
        public StatisticsService(
            string connectionString,
            ILogger<StatisticsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        /// <summary>
        /// Obtenir les données du dashboard (activité + graphique)
        /// </summary>
        public async Task<DashboardResponseDto> GetDashboardDataAsync(string period = "7days")
        {
            Guid idEntreprise = GetEntrepriseIdFromContext();
            try
            {
                _logger.LogInformation($"📊 Chargement dashboard pour entreprise {idEntreprise}, période: {period}");

                // Récupérer les données brutes
                var rawData = await GetRawStatisticsAsync(idEntreprise);

                // Calculer les statistiques d'activité
                var activityData = CalculateActivityData(rawData);

                // Récupérer les données du graphique
                var chartData = await GetChartDataAsync(idEntreprise, period);

                var response = new DashboardResponseDto
                {
                    ActivityData = activityData,
                    ChartData = chartData
                };

                _logger.LogInformation($"✅ Dashboard chargé avec succès");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement du dashboard");
                throw;
            }
        }

        /// <summary>
        /// Récupérer les données brutes depuis la base de données
        /// </summary>
        private async Task<RawStatisticsData> GetRawStatisticsAsync(Guid idEntreprise)
        {
            try
            {
                var today = DateTime.Now.Date;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1); // Lundi
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var startOfLastMonth = startOfMonth.AddMonths(-1);
                var endOfLastMonth = startOfMonth.AddDays(-1);

                const string query = @"
                    SELECT 
                        -- Ventes
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) = @Today) AS SalesToday,
                        
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfWeek AND CAST(DateCreation AS DATE) <= @Today) AS SalesThisWeek,
                        
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfMonth) AS SalesThisMonth,
                        
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfLastMonth AND CAST(DateCreation AS DATE) <= @EndOfLastMonth) AS SalesLastMonth,
                        
                        -- Revenus
                        (SELECT ISNULL(SUM(Total_final), 0) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) = @Today) AS RevenuesToday,
                        
                        (SELECT ISNULL(SUM(Total_final), 0) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfWeek AND CAST(DateCreation AS DATE) <= @Today) AS RevenuesThisWeek,
                        
                        (SELECT ISNULL(SUM(Total_final), 0) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfMonth) AS RevenuesThisMonth,
                        
                        (SELECT ISNULL(SUM(Total_final), 0) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfLastMonth AND CAST(DateCreation AS DATE) <= @EndOfLastMonth) AS RevenuesLastMonth,
                        
                        -- Clients (factures uniques)
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0) AS TotalClients,
                        
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfMonth) AS NewClientsThisMonth,
                        
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) < @StartOfMonth) AS ReturningClientsThisMonth,
                        
                        (SELECT COUNT(DISTINCT id) FROM FACTURE 
                            WHERE IdEntreprise = @IdEntreprise AND Etat = 'Payé' AND EstAnnuler = 0 
                            AND CAST(DateCreation AS DATE) >= @StartOfLastMonth AND CAST(DateCreation AS DATE) <= @EndOfLastMonth) AS ClientsLastMonth,
                        
                        -- Stock
                        (SELECT COUNT(*) FROM ARTICLES WHERE IdEntreprise = @IdEntreprise) AS TotalProducts,
                        (SELECT COUNT(*) FROM STOCK WHERE IdEntreprise = @IdEntreprise AND Quanté < 10 AND Quanté > 0) AS LowStockProducts,
                        (SELECT COUNT(*) FROM STOCK WHERE IdEntreprise = @IdEntreprise AND Quanté = 0) AS OutOfStockProducts
                ";

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        // Paramètres
                        command.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
                        command.Parameters.AddWithValue("@Today", today);
                        command.Parameters.AddWithValue("@StartOfWeek", startOfWeek);
                        command.Parameters.AddWithValue("@StartOfMonth", startOfMonth);
                        command.Parameters.AddWithValue("@StartOfLastMonth", startOfLastMonth);
                        command.Parameters.AddWithValue("@EndOfLastMonth", endOfLastMonth);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new RawStatisticsData
                                {
                                    SalesToday = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                    SalesThisWeek = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                    SalesThisMonth = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                    SalesLastMonth = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                    RevenuesToday = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                                    RevenuesThisWeek = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                                    RevenuesThisMonth = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                                    RevenuesLastMonth = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                    TotalClients = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                                    NewClientsThisMonth = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                    ReturningClientsThisMonth = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                                    ClientsLastMonth = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                                    TotalProducts = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                                    LowStockProducts = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                                    OutOfStockProducts = reader.IsDBNull(14) ? 0 : reader.GetInt32(14)
                                };
                            }
                        }
                    }
                }

                throw new Exception("Impossible de récupérer les statistiques");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRawStatisticsAsync");
                throw;
            }
        }

        /// <summary>
        /// Calculer les données d'activité
        /// </summary>
        private ActivityDataDto CalculateActivityData(RawStatisticsData raw)
        {
            var salesGrowth = raw.SalesLastMonth > 0
                ? ((raw.SalesThisMonth - raw.SalesLastMonth) / (decimal)raw.SalesLastMonth) * 100
                : 0;

            var revenueGrowth = raw.RevenuesLastMonth > 0
                ? ((raw.RevenuesThisMonth - raw.RevenuesLastMonth) / raw.RevenuesLastMonth) * 100
                : 0;

            var clientsGrowth = raw.ClientsLastMonth > 0
                ? ((raw.TotalClients - raw.ClientsLastMonth) / (decimal)raw.ClientsLastMonth) * 100
                : 0;

            return new ActivityDataDto
            {
                Sales = new SalesStatsDto
                {
                    Today = raw.SalesToday,
                    ThisWeek = raw.SalesThisWeek,
                    ThisMonth = raw.SalesThisMonth,
                    Growth = Math.Round(salesGrowth, 2)
                },
                Revenue = new RevenueStatsDto
                {
                    Today = raw.RevenuesToday,
                    ThisWeek = raw.RevenuesThisWeek,
                    ThisMonth = raw.RevenuesThisMonth,
                    Growth = Math.Round(revenueGrowth, 2)
                },
                Customers = new CustomersStatsDto
                {
                    Total = raw.TotalClients,
                    New = raw.NewClientsThisMonth,
                    Returning = raw.ReturningClientsThisMonth,
                    Growth = Math.Round(clientsGrowth, 2)
                },
                Inventory = new InventoryStatsDto
                {
                    TotalProducts = raw.TotalProducts,
                    LowStock = raw.LowStockProducts,
                    OutOfStock = raw.OutOfStockProducts
                }
            };
        }

        /// <summary>
        /// Obtenir les données du graphique de ventes
        /// </summary>
        private async Task<List<SalesChartDataDto>> GetChartDataAsync(Guid idEntreprise, string period)
        {
            try
            {
                var result = new List<SalesChartDataDto>();
                var today = DateTime.Now.Date;
                int daysToShow = period switch
                {
                    "7days" => 7,
                    "30days" => 30,
                    "90days" => 90,
                    "1year" => 365,
                    _ => 7
                };

                var startDate = today.AddDays(-daysToShow);

                const string query = @"
                    SELECT 
                        CAST(DateCreation AS DATE) as SaleDate,
                        COUNT(DISTINCT id) as SalesCount,
                        ISNULL(SUM(Total_final), 0) as TotalRevenue
                    FROM FACTURE
                    WHERE IdEntreprise = @IdEntreprise 
                        AND Etat = 'Payé' 
                        AND EstAnnuler = 0
                        AND CAST(DateCreation AS DATE) >= @StartDate
                    GROUP BY CAST(DateCreation AS DATE)
                    ORDER BY CAST(DateCreation AS DATE)
                ";

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
                        command.Parameters.AddWithValue("@StartDate", startDate);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var saleDate = reader.GetDateTime(0);
                                result.Add(new SalesChartDataDto
                                {
                                    Date = saleDate.ToString("dd/MM"),
                                    Sales = reader.GetInt32(1),
                                    Revenue = reader.GetDecimal(2)
                                });
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetChartDataAsync");
                throw;
            }
        }
    }
}
