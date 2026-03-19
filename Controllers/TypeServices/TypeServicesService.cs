using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.TypeServices
{
    public class TypeServicesService : DatabaseService
    {
        public TypeServicesService(
            string connectionString,
            ILogger<TypeServicesService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<TypeServiceListResponse> GetAllAsync()
        {
            var result = new TypeServiceListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT id, designation, estSupprimer, IdEntreprise, identifient
                    FROM TYPE_SERVICES
                    WHERE ISNULL(estSupprimer, 0) = 0 {whereClause}
                    ORDER BY designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.TypeServices.Add(new TypeServiceDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "designation"),
                        EstSupprimer = ReadNullableBool(reader, "estSupprimer"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifient = reader.GetInt32(4)
                    });
                }

                result.Total = result.TypeServices.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types services");
                result.Success = false;
            }

            return result;
        }

        public async Task<TypeServiceDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT id, designation, estSupprimer, IdEntreprise, identifient
                    FROM TYPE_SERVICES
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new TypeServiceDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "designation"),
                        EstSupprimer = ReadNullableBool(reader, "estSupprimer"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifient = reader.GetInt32(4)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération type service {id}");
            }

            return null;
        }

        public async Task<TypeServiceDto?> CreateAsync(CreateTypeServiceRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO TYPE_SERVICES (id, designation, estSupprimer, IdEntreprise)
                    VALUES (@Id, @Designation, 0, @IdEntreprise)", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Type service créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création type service");
                return null;
            }
        }

        public async Task<TypeServiceDto?> UpdateAsync(Guid id, UpdateTypeServiceRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE TYPE_SERVICES
                    SET designation = COALESCE(@Designation, designation)
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour type service {id}");
                return null;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE TYPE_SERVICES
                    SET estSupprimer = 1
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Type service supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression type service {id}");
                return false;
            }
        }
    }
}