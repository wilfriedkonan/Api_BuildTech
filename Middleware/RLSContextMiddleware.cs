using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace Api_BuildTech.Middleware
{
    /// <summary>
    /// Middleware qui définit automatiquement le SESSION_CONTEXT SQL Server
    /// pour activer le Row-Level Security (RLS) sur toutes les requêtes
    /// </summary>
    public class RLSContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _connectionString;
        private readonly ILogger<RLSContextMiddleware> _logger;

        public RLSContextMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<RLSContextMiddleware> logger)
        {
            _next = next;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' non trouvée");
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Récupérer IdEntreprise et IsSuperAdmin du JWT token
            var entrepriseIdClaim = context.User.FindFirst("entreprise_id")?.Value;
            var isSuperAdmin = context.User.IsInRole("SuperAdmin");

            if (!string.IsNullOrEmpty(entrepriseIdClaim) && Guid.TryParse(entrepriseIdClaim, out Guid idEntreprise))
            {
                try
                {
                    // Définir SESSION_CONTEXT pour cette requête
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "EXEC usp_SetEntrepriseContext @IdEntreprise, @IsSuperAdmin";
                            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
                            cmd.Parameters.AddWithValue("@IsSuperAdmin", isSuperAdmin);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Stocker dans HttpContext.Items pour utilisation dans les controllers
                    context.Items["IdEntreprise"] = idEntreprise;
                    context.Items["IsSuperAdmin"] = isSuperAdmin;

                    _logger.LogDebug($"SESSION_CONTEXT défini: Entreprise={idEntreprise}, SuperAdmin={isSuperAdmin}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erreur lors de la définition du SESSION_CONTEXT pour {idEntreprise}");
                    // Continuer quand même, les requêtes échoueront si RLS bloque
                }
            }
            else
            {
                // Pas de token JWT ou entreprise_id manquant
                // Les endpoints publics (login, health) doivent fonctionner sans
                _logger.LogDebug("SESSION_CONTEXT non défini : pas de JWT ou entreprise_id manquant");
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension method pour faciliter l'enregistrement du middleware
    /// </summary>
    public static class RLSContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseRLSContext(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RLSContextMiddleware>();
        }
    }
}