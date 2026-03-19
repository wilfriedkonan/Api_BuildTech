using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.UniteMesures
{
    public class UniteMesuresService : DatabaseService
    {
        public UniteMesuresService(
            string connectionString,
            ILogger<UniteMesuresService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<UniteMesureListResponse> GetAllAsync()
        {
            var result = new UniteMesureListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, EstSupprimer, IdEntreprise, Identifiant
                    FROM UNITE_MESURES
                    WHERE ISNULL(EstSupprimer, 0) = 0 {whereClause}
                    ORDER BY Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.UniteMesures.Add(new UniteMesureDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(4)
                    });
                }

                result.Total = result.UniteMesures.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération unités mesure");
                result.Success = false;
            }

            return result;
        }

        public async Task<UniteMesureDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, EstSupprimer, IdEntreprise, Identifiant
                    FROM UNITE_MESURES
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new UniteMesureDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(4)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération unité mesure {id}");
            }

            return null;
        }

        public async Task<UniteMesureDto?> CreateAsync(CreateUniteMesureRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO UNITE_MESURES (Id, Designation, EstSupprimer, IdEntreprise)
                    VALUES (@Id, @Designation, 0, @IdEntreprise)", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Unité mesure créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création unité mesure");
                return null;
            }
        }

        public async Task<UniteMesureDto?> UpdateAsync(Guid id, UpdateUniteMesureRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE UNITE_MESURES
                    SET Designation = COALESCE(@Designation, Designation)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour unité mesure {id}");
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
                    UPDATE UNITE_MESURES
                    SET EstSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Unité mesure supprimée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression unité mesure {id}");
                return false;
            }
        }
    }
}