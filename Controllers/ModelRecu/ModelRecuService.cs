using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.ModelRecu
{
    public class ModelRecuService : DatabaseService
    {
        public ModelRecuService(
            string connectionString,
            ILogger<ModelRecuService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<ModelRecuDto?> GetByEntrepriseAsync()
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT id, entete1, entete2, localisation, tel, 
                           TypeActivite, Message, etat, IdEntreprise
                    FROM MODEL_RECU
                    WHERE 1=1 {whereClause}", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ModelRecuDto
                    {
                        Id = reader.GetGuid(0),
                        Entete1 = ReadNullableString(reader, "entete1"),
                        Entete2 = ReadNullableString(reader, "entete2"),
                        Localisation = ReadNullableString(reader, "localisation"),
                        Tel = ReadNullableString(reader, "tel"),
                        TypeActivite = ReadNullableString(reader, "TypeActivite"),
                        Message = ReadNullableString(reader, "Message"),
                        Etat = ReadNullableString(reader, "etat"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération modèle reçu");
            }

            return null;
        }

        public async Task<ModelRecuDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT id, entete1, entete2, localisation, tel, 
                           TypeActivite, Message, etat, IdEntreprise
                    FROM MODEL_RECU
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ModelRecuDto
                    {
                        Id = reader.GetGuid(0),
                        Entete1 = ReadNullableString(reader, "entete1"),
                        Entete2 = ReadNullableString(reader, "entete2"),
                        Localisation = ReadNullableString(reader, "localisation"),
                        Tel = ReadNullableString(reader, "tel"),
                        TypeActivite = ReadNullableString(reader, "TypeActivite"),
                        Message = ReadNullableString(reader, "Message"),
                        Etat = ReadNullableString(reader, "etat"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération modèle reçu {id}");
            }

            return null;
        }

        public async Task<ModelRecuDto?> CreateAsync(CreateModelRecuRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO MODEL_RECU (
                        id, entete1, entete2, localisation, tel, 
                        TypeActivite, Message, etat, IdEntreprise
                    )
                    VALUES (
                        @Id, @Entete1, @Entete2, @Localisation, @Tel, 
                        @TypeActivite, @Message, 'Actif', @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                AddParameter(cmd, "@Entete1", request.Entete1);
                AddParameter(cmd, "@Entete2", request.Entete2);
                AddParameter(cmd, "@Localisation", request.Localisation);
                AddParameter(cmd, "@Tel", request.Tel);
                AddParameter(cmd, "@TypeActivite", request.TypeActivite);
                AddParameter(cmd, "@Message", request.Message);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Modèle reçu créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création modèle reçu");
                return null;
            }
        }

        public async Task<ModelRecuDto?> UpdateAsync(Guid id, UpdateModelRecuRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE MODEL_RECU
                    SET entete1 = COALESCE(@Entete1, entete1),
                        entete2 = COALESCE(@Entete2, entete2),
                        localisation = COALESCE(@Localisation, localisation),
                        tel = COALESCE(@Tel, tel),
                        TypeActivite = COALESCE(@TypeActivite, TypeActivite),
                        Message = COALESCE(@Message, Message),
                        etat = COALESCE(@Etat, etat)
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Entete1", request.Entete1);
                AddParameter(cmd, "@Entete2", request.Entete2);
                AddParameter(cmd, "@Localisation", request.Localisation);
                AddParameter(cmd, "@Tel", request.Tel);
                AddParameter(cmd, "@TypeActivite", request.TypeActivite);
                AddParameter(cmd, "@Message", request.Message);
                AddParameter(cmd, "@Etat", request.Etat);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour modèle reçu {id}");
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
                    UPDATE MODEL_RECU
                    SET etat = 'Inactif'
                    WHERE id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Modèle reçu désactivé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation modèle reçu {id}");
                return false;
            }
        }
    }
}