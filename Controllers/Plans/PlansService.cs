using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Plans
{
    public class PlansService : DatabaseService
    {
        public PlansService(
            string connectionString,
            ILogger<PlansService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        /// <summary>
        /// Récupère tous les plans
        /// </summary>
        public async Task<PlanListResponse> GetAllAsync()
        {
            var result = new PlanListResponse { Success = true };

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Name, Description, Price, MaxUsers, 
                           MaxInvoicesPerMonth, MaxStorageMB, IsActive
                    FROM PLANS
                    ORDER BY Price ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var plan = MapToPlanDto(reader);
                    result.Plans.Add(plan);

                    if (plan.IsActive)
                        result.TotalActifs++;
                }

                result.Total = result.Plans.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans");
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Récupère uniquement les plans actifs
        /// </summary>
        public async Task<PlanListResponse> GetActifsAsync()
        {
            var result = new PlanListResponse { Success = true };

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Name, Description, Price, MaxUsers, 
                           MaxInvoicesPerMonth, MaxStorageMB, IsActive
                    FROM PLANS
                    WHERE IsActive = 1
                    ORDER BY Price ASC", conn);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var plan = MapToPlanDto(reader);
                    result.Plans.Add(plan);
                }

                result.Total = result.Plans.Count;
                result.TotalActifs = result.Total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans actifs");
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Récupère un plan par ID
        /// </summary>
        public async Task<PlanDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Name, Description, Price, MaxUsers, 
                           MaxInvoicesPerMonth, MaxStorageMB, IsActive
                    FROM PLANS
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", id);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapToPlanDto(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération plan {id}");
            }

            return null;
        }

        /// <summary>
        /// Récupère un plan par nom
        /// </summary>
        public async Task<PlanDto?> GetByNameAsync(string name)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Name, Description, Price, MaxUsers, 
                           MaxInvoicesPerMonth, MaxStorageMB, IsActive
                    FROM PLANS
                    WHERE Name = @Name", conn);

                cmd.Parameters.AddWithValue("@Name", name);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapToPlanDto(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération plan {name}");
            }

            return null;
        }

        /// <summary>
        /// Récupère les plans avec leurs caractéristiques enrichies
        /// </summary>
        public async Task<PlanFeaturesResponse> GetPlansWithFeaturesAsync()
        {
            var result = new PlanFeaturesResponse { Success = true };

            try
            {
                var plansResponse = await GetActifsAsync();

                if (!plansResponse.Success)
                {
                    result.Success = false;
                    return result;
                }

                foreach (var plan in plansResponse.Plans)
                {
                    var planWithFeatures = new PlanWithFeaturesDto
                    {
                        Id = plan.Id,
                        Name = plan.Name,
                        Description = plan.Description,
                        Price = plan.Price,
                        MaxUsers = plan.MaxUsers,
                        MaxInvoicesPerMonth = plan.MaxInvoicesPerMonth,
                        MaxStorageMB = plan.MaxStorageMB,
                        IsActive = plan.IsActive,
                        Features = BuildFeaturesList(plan),
                        Limitations = BuildLimitationsList(plan),
                        RecommendedFor = GetRecommendation(plan),
                        IsPopular = IsPopularPlan(plan),
                        Badge = GetPlanBadge(plan)
                    };

                    result.Plans.Add(planWithFeatures);
                }

                result.Total = result.Plans.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération plans avec features");
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Génère une comparaison détaillée des plans
        /// </summary>
        public async Task<PlanComparisonDto> GetComparisonAsync()
        {
            var comparison = new PlanComparisonDto();

            try
            {
                var plansResponse = await GetActifsAsync();

                if (!plansResponse.Success)
                    return comparison;

                comparison.Plans = plansResponse.Plans;

                // Construction des features de comparaison
                comparison.Features = new List<ComparisonFeature>
                {
                    CreateComparisonFeature("Utilisateurs", "Capacité",
                        comparison.Plans, p => p.FormattedUsers),

                    CreateComparisonFeature("Factures mensuelles", "Capacité",
                        comparison.Plans, p => p.FormattedInvoices),

                    CreateComparisonFeature("Stockage", "Capacité",
                        comparison.Plans, p => p.FormattedStorage),

                    CreateComparisonFeature("Support", "Service",
                        comparison.Plans, p => GetSupportLevel(p)),

                    CreateComparisonFeature("Gestion tables", "Fonctionnalité",
                        comparison.Plans, p => "✓"),

                    CreateComparisonFeature("Point de vente (POS)", "Fonctionnalité",
                        comparison.Plans, p => "✓"),

                    CreateComparisonFeature("Gestion stock", "Fonctionnalité",
                        comparison.Plans, p => "✓"),

                    CreateComparisonFeature("Rapports avancés", "Fonctionnalité",
                        comparison.Plans, p => p.Price >= 50 ? "✓" : "✗"),

                    CreateComparisonFeature("API Access", "Fonctionnalité",
                        comparison.Plans, p => p.Price >= 100 ? "✓" : "✗"),

                    CreateComparisonFeature("Installation locale", "Service",
                        comparison.Plans, p => p.Price >= 100 ? "✓" : "✗")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur génération comparaison plans");
            }

            return comparison;
        }

        // ========================================
        // MÉTHODES PRIVÉES
        // ========================================

        private PlanDto MapToPlanDto(SqlDataReader reader)
        {
            return new PlanDto
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Description = ReadNullableString(reader, "Description"),
                Price = reader.GetDecimal(3),
                MaxUsers = reader.GetInt32(4),
                MaxInvoicesPerMonth = reader.GetInt32(5),
                MaxStorageMB = reader.GetInt32(6),
                IsActive = reader.GetBoolean(7)
            };
        }

        private List<string> BuildFeaturesList(PlanDto plan)
        {
            var features = new List<string>
            {
                $"{plan.FormattedUsers}",
                $"{plan.FormattedInvoices}",
                $"Stockage: {plan.FormattedStorage}",
                "Gestion complète des tables",
                "Point de vente (POS)",
                "Gestion des stocks",
                "Gestion clients et fournisseurs"
            };

            if (plan.Price >= 50)
            {
                features.Add("Rapports et statistiques avancés");
                features.Add("Export de données");
            }

            if (plan.Price >= 100)
            {
                features.Add("Accès API complet");
                features.Add("Installation locale disponible");
                features.Add("Support prioritaire");
                features.Add("Synchronisation multi-sites");
            }

            return features;
        }

        private List<string> BuildLimitationsList(PlanDto plan)
        {
            var limitations = new List<string>();

            if (plan.MaxUsers != -1)
                limitations.Add($"Limité à {plan.MaxUsers} utilisateur(s)");

            if (plan.MaxInvoicesPerMonth != -1)
                limitations.Add($"Limité à {plan.MaxInvoicesPerMonth} factures/mois");

            if (plan.MaxStorageMB != -1)
                limitations.Add($"Limité à {plan.MaxStorageMB} MB de stockage");

            if (plan.Price < 50)
            {
                limitations.Add("Rapports basiques uniquement");
                limitations.Add("Pas d'accès API");
            }

            if (plan.Price < 100)
            {
                limitations.Add("Pas d'installation locale");
                limitations.Add("Support standard");
            }

            return limitations;
        }

        private string GetRecommendation(PlanDto plan)
        {
            if (plan.Price < 30)
                return "Idéal pour petits restaurants et food trucks";
            else if (plan.Price < 80)
                return "Parfait pour restaurants moyens avec plusieurs serveurs";
            else
                return "Optimal pour chaînes de restaurants et grandes entreprises";
        }

        private bool IsPopularPlan(PlanDto plan)
        {
            // Le plan du milieu est généralement le plus populaire
            return plan.Price >= 40 && plan.Price <= 80;
        }

        private string? GetPlanBadge(PlanDto plan)
        {
            if (plan.Price < 30)
                return "DÉMARRAGE";
            else if (IsPopularPlan(plan))
                return "PLUS POPULAIRE";
            else if (plan.Price >= 100)
                return "PROFESSIONNEL";

            return null;
        }

        private string GetSupportLevel(PlanDto plan)
        {
            if (plan.Price < 30)
                return "Email";
            else if (plan.Price < 100)
                return "Email + Chat";
            else
                return "24/7 Prioritaire";
        }

        private ComparisonFeature CreateComparisonFeature(
            string name,
            string category,
            List<PlanDto> plans,
            Func<PlanDto, string> valueExtractor)
        {
            var feature = new ComparisonFeature
            {
                Name = name,
                Category = category
            };

            foreach (var plan in plans)
            {
                feature.PlanValues[plan.Id] = valueExtractor(plan);
            }

            return feature;
        }
    }
}