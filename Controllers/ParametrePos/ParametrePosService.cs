using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.ParametrePos
{
    public class ParametrePosService : DatabaseService
    {
        public ParametrePosService(
            string connectionString,
            ILogger<ParametrePosService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<ParametrePosDto?> GetByEntrepriseAsync()
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, DesignationEntreprise, EstPosFullScreen, 
                           EstPosAvecCalculeMonnaie, IdEntreprise
                    FROM PARAMETRE_POS
                    WHERE 1=1 {whereClause}", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ParametrePosDto
                    {
                        Id = reader.GetGuid(0),
                        DesignationEntreprise = ReadNullableString(reader, "DesignationEntreprise"),
                        EstPosFullScreen = ReadNullableBool(reader, "EstPosFullScreen"),
                        EstPosAvecCalculeMonnaie = ReadNullableBool(reader, "EstPosAvecCalculeMonnaie"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération paramètres POS");
            }

            return null;
        }

        public async Task<ParametrePosDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, DesignationEntreprise, EstPosFullScreen, 
                           EstPosAvecCalculeMonnaie, IdEntreprise
                    FROM PARAMETRE_POS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ParametrePosDto
                    {
                        Id = reader.GetGuid(0),
                        DesignationEntreprise = ReadNullableString(reader, "DesignationEntreprise"),
                        EstPosFullScreen = ReadNullableBool(reader, "EstPosFullScreen"),
                        EstPosAvecCalculeMonnaie = ReadNullableBool(reader, "EstPosAvecCalculeMonnaie"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération paramètre POS {id}");
            }

            return null;
        }

        public async Task<ParametrePosDto?> CreateAsync(CreateParametrePosRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO PARAMETRE_POS (
                        Id, DesignationEntreprise, EstPosFullScreen, 
                        EstPosAvecCalculeMonnaie, IdEntreprise
                    )
                    VALUES (@Id, @DesignationEntreprise, @EstPosFullScreen, 
                            @EstPosAvecCalculeMonnaie, @IdEntreprise)", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                AddParameter(cmd, "@DesignationEntreprise", request.DesignationEntreprise);
                cmd.Parameters.AddWithValue("@EstPosFullScreen", request.EstPosFullScreen);
                cmd.Parameters.AddWithValue("@EstPosAvecCalculeMonnaie", request.EstPosAvecCalculeMonnaie);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Paramètre POS créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création paramètre POS");
                return null;
            }
        }

        public async Task<ParametrePosDto?> UpdateAsync(Guid id, UpdateParametrePosRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE PARAMETRE_POS
                    SET DesignationEntreprise = COALESCE(@DesignationEntreprise, DesignationEntreprise),
                        EstPosFullScreen = COALESCE(@EstPosFullScreen, EstPosFullScreen),
                        EstPosAvecCalculeMonnaie = COALESCE(@EstPosAvecCalculeMonnaie, EstPosAvecCalculeMonnaie)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@DesignationEntreprise", request.DesignationEntreprise);
                AddParameter(cmd, "@EstPosFullScreen", request.EstPosFullScreen);
                AddParameter(cmd, "@EstPosAvecCalculeMonnaie", request.EstPosAvecCalculeMonnaie);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour paramètre POS {id}");
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
                    DELETE FROM PARAMETRE_POS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Paramètre POS supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression paramètre POS {id}");
                return false;
            }
        }
    }
}