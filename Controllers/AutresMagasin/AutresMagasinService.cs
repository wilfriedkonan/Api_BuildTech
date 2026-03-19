using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.AutresMagasin
{
    public class AutresMagasinService : DatabaseService
    {
        public AutresMagasinService(
            string connectionString,
            ILogger<AutresMagasinService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<AutresMagasinListResponse> GetAllAsync()
        {
            var result = new AutresMagasinListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT a.Id, a.Designation, a.Etat, 
                           a.IdEntreprise, 
                    FROM AUTRES_MAGASIN a
                    WHERE ISNULL(a.Etat != Supprimer, 0) = 0 {whereClause}
                    ORDER BY a.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var magasin = new AutresMagasinDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Lieu"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };

                    result.Magasins.Add(magasin);

                }

                result.Total = result.Magasins.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération autres magasins");
                result.Success = false;
            }

            return result;
        }

        public async Task<AutresMagasinDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("a");

                using var cmd = new SqlCommand($@"
                    SELECT a.Id, a.Designation, a.quantite, a.quantiteInitial, 
                           a.PrixUnitaire, a.Montant, a.Lieu, a.idUnite, 
                           a.EstSupprimer, a.IdEntreprise, u.Designation AS Unite
                    FROM AUTRES_MAGASIN a
                    LEFT JOIN UNITE_MESURES u ON a.idUnite = u.Id
                    WHERE a.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new AutresMagasinDto
                    {

                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Lieu"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération autre magasin {id}");
            }

            return null;
        }

        public async Task<AutresMagasinDto?> CreateAsync(CreateAutresMagasinRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO AUTRES_MAGASIN (
                        Id, Designation, Etat, IdEntreprise
                    )
                    VALUES (
                        @Id, @Designation, @Etat,@IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);

                AddParameter(cmd, "@Etat", request.Etat);

                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Autre magasin créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création autre magasin");
                return null;
            }
        }

        public async Task<AutresMagasinDto?> UpdateAsync(Guid id, UpdateAutresMagasinRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE AUTRES_MAGASIN
                    SET Designation = COALESCE(@Designation, Designation),
                        Etat = COALESCE(@Lieu, Lieu),
                    WHERE Id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Lieu", request.Etat);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour autre magasin {id}");
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
                    UPDATE AUTRES_MAGASIN
                    SET EstSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Autre magasin supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression autre magasin {id}");
                return false;
            }
        }
    }
}