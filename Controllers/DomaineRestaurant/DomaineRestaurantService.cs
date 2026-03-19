using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.DomaineRestaurant
{
    public class DomaineRestaurantService : DatabaseService
    {
        public DomaineRestaurantService(
            string connectionString,
            ILogger<DomaineRestaurantService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<DomaineRestaurantListResponse> GetAllAsync()
        {
            var result = new DomaineRestaurantListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, CheminImprimente, Ordre, Etat, IdEntreprise, identifient
                    FROM DOMAINE_RESTAURANT
                    WHERE ISNULL(Etat, 'Actif') = 'Actif' {whereClause}
                    ORDER BY ISNULL(Ordre, 999), Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Domaines.Add(new DomaineRestaurantDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        CheminImprimente = ReadNullableString(reader, "CheminImprimente"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        Etat = ReadNullableString(reader, "Etat"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifient = reader.GetInt32(6)
                    });
                }

                result.Total = result.Domaines.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération domaines restaurant");
                result.Success = false;
            }

            return result;
        }

        public async Task<DomaineRestaurantDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, CheminImprimente, Ordre, Etat, IdEntreprise, identifient
                    FROM DOMAINE_RESTAURANT
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new DomaineRestaurantDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        CheminImprimente = ReadNullableString(reader, "CheminImprimente"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        Etat = ReadNullableString(reader, "Etat"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifient = reader.GetInt32(6)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération domaine restaurant {id}");
            }

            return null;
        }

        public async Task<DomaineRestaurantDto?> CreateAsync(CreateDomaineRestaurantRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO DOMAINE_RESTAURANT (
                        Id, Designation, CheminImprimente, Ordre, Etat, IdEntreprise
                    )
                    VALUES (
                        @Id, @Designation, @CheminImprimente, @Ordre, 'Actif', @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                AddParameter(cmd, "@CheminImprimente", request.CheminImprimente);
                AddParameter(cmd, "@Ordre", request.Ordre);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Domaine restaurant créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création domaine restaurant");
                return null;
            }
        }

        public async Task<DomaineRestaurantDto?> UpdateAsync(Guid id, UpdateDomaineRestaurantRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE DOMAINE_RESTAURANT
                    SET Designation = COALESCE(@Designation, Designation),
                        CheminImprimente = COALESCE(@CheminImprimente, CheminImprimente),
                        Ordre = COALESCE(@Ordre, Ordre),
                        Etat = COALESCE(@Etat, Etat)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@CheminImprimente", request.CheminImprimente);
                AddParameter(cmd, "@Ordre", request.Ordre);
                AddParameter(cmd, "@Etat", request.Etat);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour domaine restaurant {id}");
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
                    UPDATE DOMAINE_RESTAURANT
                    SET Etat = 'Inactif'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Domaine restaurant désactivé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation domaine restaurant {id}");
                return false;
            }
        }
    }
}