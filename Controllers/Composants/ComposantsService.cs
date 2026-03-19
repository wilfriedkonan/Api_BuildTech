using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Composants
{
    public class ComposantsService : DatabaseService
    {
        public ComposantsService(
            string connectionString,
            ILogger<ComposantsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<ComposantListResponse> GetByArticleAsync(Guid idArticle)
        {
            var result = new ComposantListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.Designation, c.Etat, c.Ordre, c.IdArticle, 
                           c.idCatheComposant, c.IdEntreprise,
                           a.Designation AS NomArticle,
                           cat.Designation AS NomCategorie
                    FROM COMPOSANTS c
                    LEFT JOIN ARTICLES a ON c.IdArticle = a.Id
                    LEFT JOIN CATHEGORIE_COMPOSENTS cat ON c.idCatheComposant = cat.Id
                    WHERE c.IdArticle = @IdArticle 
                    AND ISNULL(c.Etat, 'Actif') = 'Actif' {whereClause}
                    ORDER BY ISNULL(c.Ordre, 999), c.Designation", conn);

                cmd.Parameters.AddWithValue("@IdArticle", idArticle);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Composants.Add(new ComposantDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdCatheComposant = ReadNullableGuid(reader, "idCatheComposant"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        NomArticle = ReadNullableString(reader, "NomArticle"),
                        NomCategorie = ReadNullableString(reader, "NomCategorie")
                    });
                }

                result.Total = result.Composants.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération composants article {idArticle}");
                result.Success = false;
            }

            return result;
        }

        public async Task<ComposantListResponse> GetAllAsync()
        {
            var result = new ComposantListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.Designation, c.Etat, c.Ordre, c.IdArticle, 
                           c.idCatheComposant, c.IdEntreprise,
                           a.Designation AS NomArticle,
                           cat.Designation AS NomCategorie
                    FROM COMPOSANTS c
                    LEFT JOIN ARTICLES a ON c.IdArticle = a.Id
                    LEFT JOIN CATHEGORIE_COMPOSENTS cat ON c.idCatheComposant = cat.Id
                    WHERE ISNULL(c.Etat, 'Actif') = 'Actif' {whereClause}
                    ORDER BY a.Designation, ISNULL(c.Ordre, 999)", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Composants.Add(new ComposantDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdCatheComposant = ReadNullableGuid(reader, "idCatheComposant"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        NomArticle = ReadNullableString(reader, "NomArticle"),
                        NomCategorie = ReadNullableString(reader, "NomCategorie")
                    });
                }

                result.Total = result.Composants.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération composants");
                result.Success = false;
            }

            return result;
        }

        public async Task<ComposantDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.Designation, c.Etat, c.Ordre, c.IdArticle, 
                           c.idCatheComposant, c.IdEntreprise,
                           a.Designation AS NomArticle,
                           cat.Designation AS NomCategorie
                    FROM COMPOSANTS c
                    LEFT JOIN ARTICLES a ON c.IdArticle = a.Id
                    LEFT JOIN CATHEGORIE_COMPOSENTS cat ON c.idCatheComposant = cat.Id
                    WHERE c.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ComposantDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdCatheComposant = ReadNullableGuid(reader, "idCatheComposant"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        NomArticle = ReadNullableString(reader, "NomArticle"),
                        NomCategorie = ReadNullableString(reader, "NomCategorie")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération composant {id}");
            }

            return null;
        }

        public async Task<ComposantDto?> CreateAsync(CreateComposantRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO COMPOSANTS (
                        Id, Designation, Etat, Ordre, IdArticle, idCatheComposant, IdEntreprise
                    )
                    VALUES (
                        @Id, @Designation, 'Actif', @Ordre, @IdArticle, @IdCatheComposant, @IdEntreprise
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                AddParameter(cmd, "@Ordre", request.Ordre);
                cmd.Parameters.AddWithValue("@IdArticle", request.IdArticle);
                cmd.Parameters.AddWithValue("@IdCatheComposant", request.IdCatheComposant);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Composant créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création composant");
                return null;
            }
        }

        public async Task<ComposantDto?> UpdateAsync(Guid id, UpdateComposantRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE COMPOSANTS
                    SET Designation = COALESCE(@Designation, Designation),
                        Ordre = COALESCE(@Ordre, Ordre),
                        idCatheComposant = COALESCE(@IdCatheComposant, idCatheComposant),
                        Etat = COALESCE(@Etat, Etat)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Ordre", request.Ordre);
                AddParameter(cmd, "@IdCatheComposant", request.IdCatheComposant);
                AddParameter(cmd, "@Etat", request.Etat);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour composant {id}");
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
                    UPDATE COMPOSANTS
                    SET Etat = 'Inactif'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Composant désactivé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation composant {id}");
                return false;
            }
        }
    }
}