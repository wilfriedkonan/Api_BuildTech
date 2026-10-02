using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Roles
{
    public class RolesService : DatabaseService
    {
        public RolesService(
            string connectionString,
            ILogger<RolesService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        /// <summary>
        /// Liste des rôles attribuables, à l'exclusion de "Owner"
        /// (le rôle Owner n'est jamais assignable manuellement via cet endpoint).
        /// </summary>
        public async Task<List<RoleDto>> GetAllExceptOwnerAsync()
        {
            var roles = new List<RoleDto>();

            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Name, Description
                    FROM ROLES
                    WHERE Name <> 'Owner'
                    ORDER BY Name", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    roles.Add(new RoleDto
                    {
                        Id = reader.GetGuid(0),
                        Name = reader.GetString(1),
                        Description = ReadNullableString(reader, "Description")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération rôles");
            }

            return roles;
        }
    }
}