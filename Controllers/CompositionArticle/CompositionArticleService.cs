using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.CompositionArticle
{
    public class CompositionArticleService : DatabaseService
    {
        public CompositionArticleService(
            string connectionString,
            ILogger<CompositionArticleService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<CompositionArticleListResponse> GetByArticleAsync(Guid idArticle)
        {
            var result = new CompositionArticleListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.quantite, c.IdMatierePremiere, c.IdArticle, 
                           c.IdEntreprise, c.EstSupprimer,
                           m.Designation AS NomMatiere,
                           a.Designation AS NomArticle
                    FROM COMPOSITION_ARTICLE c
                    LEFT JOIN MATIERE_PREMIERE m ON c.IdMatierePremiere = m.Id
                    LEFT JOIN ARTICLES a ON c.IdArticle = a.Id
                    WHERE c.IdArticle = @IdArticle 
                    AND ISNULL(c.EstSupprimer, 0) = 0 {whereClause}", conn);

                cmd.Parameters.AddWithValue("@IdArticle", idArticle);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Compositions.Add(new CompositionArticleDto
                    {
                        Id = reader.GetGuid(0),
                        Quantite = reader.GetDecimal("quantite"),
                        IdMatierePremiere = ReadNullableGuid(reader, "IdMatierePremiere"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                        NomMatiere = ReadNullableString(reader, "NomMatiere"),
                        NomArticle = ReadNullableString(reader, "NomArticle")
                    });
                }

                result.Total = result.Compositions.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération compositions article {idArticle}");
                result.Success = false;
            }

            return result;
        }

        public async Task<CompositionArticleDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.quantite, c.IdMatierePremiere, c.IdArticle, 
                           c.IdEntreprise, c.EstSupprimer,
                           m.Designation AS NomMatiere,
                           a.Designation AS NomArticle
                    FROM COMPOSITION_ARTICLE c
                    LEFT JOIN MATIERE_PREMIERE m ON c.IdMatierePremiere = m.Id
                    LEFT JOIN ARTICLES a ON c.IdArticle = a.Id
                    WHERE c.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new CompositionArticleDto
                    {
                        Id = reader.GetGuid(0),
                        Quantite = reader.GetDecimal("quantite"),
                        IdMatierePremiere = ReadNullableGuid(reader, "IdMatierePremiere"),
                        IdArticle = ReadNullableGuid(reader, "IdArticle"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        EstSupprimer = ReadNullableBool(reader, "EstSupprimer"),
                        NomMatiere = ReadNullableString(reader, "NomMatiere"),
                        NomArticle = ReadNullableString(reader, "NomArticle")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération composition article {id}");
            }

            return null;
        }

        public async Task<CompositionArticleDto?> CreateAsync(CreateCompositionArticleRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO COMPOSITION_ARTICLE (
                        Id, quantite, IdMatierePremiere, IdArticle, IdEntreprise, EstSupprimer
                    )
                    VALUES (
                        @Id, @Quantite, @IdMatierePremiere, @IdArticle, @IdEntreprise, 0
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Quantite", request.Quantite);
                cmd.Parameters.AddWithValue("@IdMatierePremiere", request.IdMatierePremiere);
                cmd.Parameters.AddWithValue("@IdArticle", request.IdArticle);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Composition article créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création composition article");
                return null;
            }
        }

        public async Task<CompositionArticleDto?> UpdateAsync(Guid id, UpdateCompositionArticleRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE COMPOSITION_ARTICLE
                    SET quantite = COALESCE(@Quantite, quantite)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Quantite", request.Quantite);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour composition article {id}");
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
                    UPDATE COMPOSITION_ARTICLE
                    SET EstSupprimer = 1
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Composition article supprimée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression composition article {id}");
                return false;
            }
        }
    }
}