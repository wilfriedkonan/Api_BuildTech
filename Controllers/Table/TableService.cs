using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Table
{
    public class TableService : DatabaseService
    {
        public TableService(
            string connectionString,
            ILogger<TableService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<TableListResponse> GetAllAsync()
        {
            var result = new TableListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("t");

                using var cmd = new SqlCommand($@"
                    SELECT t.id, t.Designation, t.Diponible, t.Etat, 
                           t.IdEntreprise, t.idUtilisateur, t.Statue, 
                           t.ordre, t.identifient, t.idFacture, 
                           t.EstEncourEdition, t.ServeurAffecte
                    FROM [TABLE] t
                    WHERE ISNULL(t.Etat, 'Actif') != 'Supprimer' {whereClause}
                    ORDER BY t.ordre, t.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var table = new TableDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Disponible = ReadNullableBool(reader, "Diponible"),
                        Etat = ReadNullableString(reader, "Etat"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdUtilisateur = ReadNullableGuid(reader, "idUtilisateur"),
                        Statue = ReadNullableString(reader, "Statue"),
                        Ordre = ReadNullableInt(reader, "ordre"),
                        Identifient = ReadNullableInt(reader, "identifient"),
                        IdFacture = ReadNullableGuid(reader, "idFacture"),
                        EstEncourEdition = ReadNullableBool(reader, "EstEncourEdition"),
                        ServeurAffecte = ReadNullableString(reader, "ServeurAffecte")
                    };

                    result.Tables.Add(table);
                }

                result.Total = result.Tables.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération tables");
                result.Success = false;
            }

            return result;
        }

        public async Task<TableListResponse> GetDisponiblesAsync()
        {
            var result = new TableListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("t");

                using var cmd = new SqlCommand($@"
                    SELECT t.id, t.Designation, t.Diponible, t.Etat, 
                           t.IdEntreprise, t.idUtilisateur, t.Statue, 
                           t.ordre, t.identifient, t.idFacture, 
                           t.EstEncourEdition, t.ServeurAffecte
                    FROM [TABLE] t
                    WHERE t.Diponible = 1 
                      AND ISNULL(t.Etat, 'Actif') = 'Actif'
                      AND (t.Statue = 'Libre' OR t.Statue IS NULL)
                      {whereClause}
                    ORDER BY t.ordre, t.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var table = new TableDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Disponible = ReadNullableBool(reader, "Diponible"),
                        Etat = ReadNullableString(reader, "Etat"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdUtilisateur = ReadNullableGuid(reader, "idUtilisateur"),
                        Statue = ReadNullableString(reader, "Statue"),
                        Ordre = ReadNullableInt(reader, "ordre"),
                        Identifient = ReadNullableInt(reader, "identifient"),
                        IdFacture = ReadNullableGuid(reader, "idFacture"),
                        EstEncourEdition = ReadNullableBool(reader, "EstEncourEdition"),
                        ServeurAffecte = ReadNullableString(reader, "ServeurAffecte")
                    };

                    result.Tables.Add(table);
                }

                result.Total = result.Tables.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération tables disponibles");
                result.Success = false;
            }

            return result;
        }

        public async Task<TableDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("t");

                using var cmd = new SqlCommand($@"
                    SELECT t.id, t.Designation, t.Diponible, t.Etat, 
                           t.IdEntreprise, t.idUtilisateur, t.Statue, 
                           t.ordre, t.identifient, t.idFacture, 
                           t.EstEncourEdition, t.ServeurAffecte
                    FROM [TABLE] t
                    WHERE t.id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new TableDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Disponible = ReadNullableBool(reader, "Diponible"),
                        Etat = ReadNullableString(reader, "Etat"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdUtilisateur = ReadNullableGuid(reader, "idUtilisateur"),
                        Statue = ReadNullableString(reader, "Statue"),
                        Ordre = ReadNullableInt(reader, "ordre"),
                        Identifient = ReadNullableInt(reader, "identifient"),
                        IdFacture = ReadNullableGuid(reader, "idFacture"),
                        EstEncourEdition = ReadNullableBool(reader, "EstEncourEdition"),
                        ServeurAffecte = ReadNullableString(reader, "ServeurAffecte")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération table {id}");
            }

            return null;
        }

        public async Task<TableDto?> CreateAsync(CreateTableRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO [TABLE] (
                        id, Designation, Diponible, Etat, IdEntreprise, 
                        Statue, ordre
                    )
                    VALUES (
                        @Id, @Designation, @Disponible, @Etat, @IdEntreprise,
                        @Statue, @Ordre
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@Disponible", request.Disponible);
                AddParameter(cmd, "@Etat", request.Etat);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                AddParameter(cmd, "@Statue", request.Statue);
                AddParameter(cmd, "@Ordre", request.Ordre);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Table créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création table");
                return null;
            }
        }

        public async Task<TableDto?> UpdateAsync(Guid id, UpdateTableRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE [TABLE]
                    SET Designation = COALESCE(@Designation, Designation),
                        Diponible = COALESCE(@Disponible, Diponible),
                        Etat = COALESCE(@Etat, Etat),
                        Statue = COALESCE(@Statue, Statue),
                        ordre = COALESCE(@Ordre, ordre),
                        ServeurAffecte = COALESCE(@ServeurAffecte, ServeurAffecte)
                    WHERE id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Disponible", request.Disponible);
                AddParameter(cmd, "@Etat", request.Etat);
                AddParameter(cmd, "@Statue", request.Statue);
                AddParameter(cmd, "@Ordre", request.Ordre);
                AddParameter(cmd, "@ServeurAffecte", request.ServeurAffecte);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour table {id}");
                return null;
            }
        }

        public async Task<TableDto?> AffecterServeurAsync(Guid id, AffecterServeurRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE [TABLE]
                    SET idUtilisateur = @IdUtilisateur,
                        ServeurAffecte = @ServeurAffecte,
                        Statue = 'Occupee'
                    WHERE id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@IdUtilisateur", request.IdUtilisateur);
                cmd.Parameters.AddWithValue("@ServeurAffecte", request.ServeurAffecte);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur affectation serveur table {id}");
                return null;
            }
        }

        public async Task<TableDto?> LibererTableAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE [TABLE]
                    SET idUtilisateur = NULL,
                        ServeurAffecte = NULL,
                        Statue = 'Libre',
                        idFacture = NULL,
                        EstEncourEdition = 0
                    WHERE id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur libération table {id}");
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
                    UPDATE [TABLE]
                    SET Etat = 'Supprimer'
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Table supprimée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression table {id}");
                return false;
            }
        }
    }
}