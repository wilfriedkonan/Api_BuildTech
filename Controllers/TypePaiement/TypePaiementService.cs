using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.TypePaiement
{
    public class TypePaiementService : DatabaseService
    {
        public TypePaiementService(
            string connectionString,
            ILogger<TypePaiementService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<TypePaiementListResponse> GetAllAsync()
        {
            var result = new TypePaiementListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, estSupprimer, IdEntreprise, identifient
                    FROM TYPE_PAIEMENT
                    WHERE ISNULL(estSupprimer, 0) = 0 
                    ORDER BY Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.TypePaiements.Add(new TypePaiementDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        EstSupprimer = ReadNullableBool(reader, "estSupprimer"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifient = reader.GetInt32(4)
                    });
                }

                result.Total = result.TypePaiements.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types paiement");
                result.Success = false;
            }

            return result;
        }

        public async Task<TypePaiementDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, estSupprimer, IdEntreprise, identifient
                    FROM TYPE_PAIEMENT
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new TypePaiementDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        EstSupprimer = ReadNullableBool(reader, "estSupprimer"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifient = reader.GetInt32(4)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération type paiement {id}");
            }

            return null;
        }

        public async Task<TypePaiementDto?> CreateAsync(CreateTypePaiementRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO TYPE_PAIEMENT (Id, Designation, estSupprimer, IdEntreprise)
                    VALUES (@Id, @Designation, 0, @IdEntreprise)", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Type paiement créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création type paiement");
                return null;
            }
        }

        public async Task<TypePaiementDto?> UpdateAsync(Guid id, UpdateTypePaiementRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE TYPE_PAIEMENT
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
                _logger.LogError(ex, $"Erreur mise à jour type paiement {id}");
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
                    UPDATE TYPE_PAIEMENT
                    SET estSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Type paiement supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression type paiement {id}");
                return false;
            }
        }
    }
}