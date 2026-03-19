using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Sessions
{
    public class SessionsService : DatabaseService
    {
        public SessionsService(
            string connectionString,
            ILogger<SessionsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<SessionListResponse> GetAllAsync(bool? cloturees = null)
        {
            var result = new SessionListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();
                var clotureFilter = cloturees.HasValue ?
                    $"AND ISNULL(EstCloturee, 0) = {(cloturees.Value ? 1 : 0)}" : "";

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, dateDeubut, dateFin, durée, 
                           EstCloturee, IdEntreprise, identifiant
                    FROM SESSIONS
                    WHERE 1=1 {whereClause} {clotureFilter}
                    ORDER BY dateDeubut DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Sessions.Add(new SessionDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        DateDebut = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        DateFin = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        Duree = ReadNullableString(reader, "durée"),
                        EstCloturee = ReadNullableBool(reader, "EstCloturee"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(7)
                    });
                }

                result.Total = result.Sessions.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération sessions");
                result.Success = false;
            }

            return result;
        }

        public async Task<SessionDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, dateDeubut, dateFin, durée, 
                           EstCloturee, IdEntreprise, identifiant
                    FROM SESSIONS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new SessionDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        DateDebut = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        DateFin = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        Duree = ReadNullableString(reader, "durée"),
                        EstCloturee = ReadNullableBool(reader, "EstCloturee"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(7)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération session {id}");
            }

            return null;
        }

        public async Task<SessionDto?> GetSessionOuverteAsync()
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT TOP 1 Id, Designation, dateDeubut, dateFin, durée, 
                           EstCloturee, IdEntreprise, identifiant
                    FROM SESSIONS
                    WHERE ISNULL(EstCloturee, 0) = 0 {whereClause}
                    ORDER BY dateDeubut DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new SessionDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        DateDebut = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        DateFin = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        Duree = ReadNullableString(reader, "durée"),
                        EstCloturee = ReadNullableBool(reader, "EstCloturee"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Identifiant = reader.GetInt32(7)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération session ouverte");
            }

            return null;
        }

        public async Task<SessionDto?> CreateAsync(CreateSessionRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO SESSIONS (
                        Id, Designation, dateDeubut, EstCloturee, IdEntreprise
                    )
                    VALUES (
                        @Id, @Designation, @DateDebut, 0, @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@DateDebut", request.DateDebut);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Session créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création session");
                return null;
            }
        }

        public async Task<SessionDto?> CloturerAsync(Guid id, ClotureSessionRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                // Calculer la durée
                using var cmdGet = new SqlCommand($@"
                    SELECT dateDeubut FROM SESSIONS WHERE Id = @Id {whereClause}", conn);
                cmdGet.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmdGet);

                var dateDebut = (DateTime?)await cmdGet.ExecuteScalarAsync();
                if (dateDebut == null)
                {
                    _logger.LogWarning($"Session {id} introuvable");
                    return null;
                }

                var duree = request.DateFin - dateDebut.Value;
                var dureeStr = $"{(int)duree.TotalHours}h {duree.Minutes}m";

                using var cmd = new SqlCommand($@"
                    UPDATE SESSIONS
                    SET dateFin = @DateFin,
                        durée = @Duree,
                        EstCloturee = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@DateFin", request.DateFin);
                cmd.Parameters.AddWithValue("@Duree", dureeStr);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Session clôturée: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur clôture session {id}");
                return null;
            }
        }

        public async Task<SessionDto?> UpdateAsync(Guid id, UpdateSessionRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE SESSIONS
                    SET Designation = COALESCE(@Designation, Designation),
                        dateFin = COALESCE(@DateFin, dateFin),
                        EstCloturee = COALESCE(@EstCloturee, EstCloturee)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@DateFin", request.DateFin);
                AddParameter(cmd, "@EstCloturee", request.EstCloturee);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour session {id}");
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
                    DELETE FROM SESSIONS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Session supprimée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression session {id}");
                return false;
            }
        }
    }
}