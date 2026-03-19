using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Livreur
{
    public class LivreurService : DatabaseService
    {
        public LivreurService(
            string connectionString,
            ILogger<LivreurService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<LivreurListResponse> GetAllAsync(bool? disponiblesOnly = null)
        {
            var result = new LivreurListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();
                var disponibleFilter = disponiblesOnly.HasValue && disponiblesOnly.Value ?
                    "AND ISNULL(EstDiponible, 0) = 1" : "";

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designatin, Contact, NCni, Etat, DateCraation, 
                           EstDiponible, EstEnAttente, idEntreprise
                    FROM LIVREUR
                    WHERE ISNULL(Etat, 'Actif') = 'Actif' {whereClause} {disponibleFilter}
                    ORDER BY Designatin", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var livreur = new LivreurDto
                    {
                        Id = reader.GetGuid(0),
                        Designatin = ReadNullableString(reader, "Designatin"),
                        Contact = ReadNullableString(reader, "Contact"),
                        NCni = ReadNullableString(reader, "NCni"),
                        Etat = ReadNullableString(reader, "Etat"),
                        DateCraation = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        EstDiponible = ReadNullableBool(reader, "EstDiponible"),
                        EstEnAttente = ReadNullableBool(reader, "EstEnAttente"),
                        IdEntreprise = ReadNullableGuid(reader, "idEntreprise")
                    };

                    result.Livreurs.Add(livreur);
                    if (livreur.EstDiponible == true)
                        result.TotalDisponibles++;
                }

                result.Total = result.Livreurs.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération livreurs");
                result.Success = false;
            }

            return result;
        }

        public async Task<LivreurDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designatin, Contact, NCni, Etat, DateCraation, 
                           EstDiponible, EstEnAttente, idEntreprise
                    FROM LIVREUR
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new LivreurDto
                    {
                        Id = reader.GetGuid(0),
                        Designatin = ReadNullableString(reader, "Designatin"),
                        Contact = ReadNullableString(reader, "Contact"),
                        NCni = ReadNullableString(reader, "NCni"),
                        Etat = ReadNullableString(reader, "Etat"),
                        DateCraation = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                        EstDiponible = ReadNullableBool(reader, "EstDiponible"),
                        EstEnAttente = ReadNullableBool(reader, "EstEnAttente"),
                        IdEntreprise = ReadNullableGuid(reader, "idEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération livreur {id}");
            }

            return null;
        }

        public async Task<LivreurDto?> CreateAsync(CreateLivreurRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO LIVREUR (
                        Id, Designatin, Contact, NCni, Etat, DateCraation, 
                        EstDiponible, EstEnAttente, idEntreprise
                    )
                    VALUES (
                        @Id, @Designatin, @Contact, @NCni, 'Actif', GETDATE(), 
                        1, 0, @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designatin", request.Designatin);
                cmd.Parameters.AddWithValue("@Contact", request.Contact);
                AddParameter(cmd, "@NCni", request.NCni);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Livreur créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création livreur");
                return null;
            }
        }

        public async Task<LivreurDto?> UpdateAsync(Guid id, UpdateLivreurRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE LIVREUR
                    SET Designatin = COALESCE(@Designatin, Designatin),
                        Contact = COALESCE(@Contact, Contact),
                        NCni = COALESCE(@NCni, NCni),
                        EstDiponible = COALESCE(@EstDiponible, EstDiponible),
                        EstEnAttente = COALESCE(@EstEnAttente, EstEnAttente),
                        Etat = COALESCE(@Etat, Etat)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designatin", request.Designatin);
                AddParameter(cmd, "@Contact", request.Contact);
                AddParameter(cmd, "@NCni", request.NCni);
                AddParameter(cmd, "@EstDiponible", request.EstDiponible);
                AddParameter(cmd, "@EstEnAttente", request.EstEnAttente);
                AddParameter(cmd, "@Etat", request.Etat);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour livreur {id}");
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
                    UPDATE LIVREUR
                    SET Etat = 'Inactif'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Livreur désactivé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation livreur {id}");
                return false;
            }
        }
    }
}