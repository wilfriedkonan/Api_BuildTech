using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Serveur
{
    public class ServeurService : DatabaseService
    {
        public ServeurService(
            string connectionString,
            ILogger<ServeurService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<ServeurListResponse> GetAllAsync()
        {
            var result = new ServeurListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, EstSupprimer, IdEntreprise, identifiant
                    FROM SERVEUR
                    WHERE ISNULL(EstSupprimer, 0) = 0 {whereClause}
                    ORDER BY Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Serveurs.Add(new ServeurDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(4)
                    });
                }

                result.Total = result.Serveurs.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération serveurs");
                result.Success = false;
            }

            return result;
        }

        public async Task<ServeurDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, EstSupprimer, IdEntreprise, identifiant
                    FROM SERVEUR
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ServeurDto
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
                _logger.LogError(ex, $"Erreur récupération serveur {id}");
            }

            return null;
        }

        public async Task<ServeurDto?> CreateAsync(CreateServeurRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO SERVEUR (Id, Designation, EstSupprimer, IdEntreprise)
                    VALUES (@Id, @Designation, 0, @IdEntreprise)", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Serveur créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création serveur");
                return null;
            }
        }

        public async Task<ServeurDto?> UpdateAsync(Guid id, UpdateServeurRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE SERVEUR
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
                _logger.LogError(ex, $"Erreur mise à jour serveur {id}");
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
                    UPDATE SERVEUR
                    SET EstSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Serveur supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression serveur {id}");
                return false;
            }
        }
    }
}