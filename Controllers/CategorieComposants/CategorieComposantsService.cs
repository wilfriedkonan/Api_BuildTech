using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.CategorieComposants
{
    public class CategorieComposantsService : DatabaseService
    {
        public CategorieComposantsService(
            string connectionString,
            ILogger<CategorieComposantsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<CategorieComposantListResponse> GetAllAsync()
        {
            var result = new CategorieComposantListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, Etat, Ordre, IdEntreprise
                    FROM CATHEGORIE_COMPOSENTS
                    WHERE ISNULL(Etat, 'Actif') = 'Actif' {whereClause}
                    ORDER BY ISNULL(Ordre, 999), Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Categories.Add(new CategorieComposantDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    });
                }

                result.Total = result.Categories.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories composants");
                result.Success = false;
            }

            return result;
        }

        public async Task<CategorieComposantDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, Etat, Ordre, IdEntreprise
                    FROM CATHEGORIE_COMPOSENTS
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new CategorieComposantDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération catégorie composant {id}");
            }

            return null;
        }

        public async Task<CategorieComposantDto?> CreateAsync(CreateCategorieComposantRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO CATHEGORIE_COMPOSENTS (Id, Designation, Etat, Ordre, IdEntreprise)
                    VALUES (@Id, @Designation, 'Actif', @Ordre, @IdEntreprise)", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                AddParameter(cmd, "@Ordre", request.Ordre);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Catégorie composant créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création catégorie composant");
                return null;
            }
        }

        public async Task<CategorieComposantDto?> UpdateAsync(Guid id, UpdateCategorieComposantRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE CATHEGORIE_COMPOSENTS
                    SET Designation = COALESCE(@Designation, Designation),
                        Ordre = COALESCE(@Ordre, Ordre),
                        Etat = COALESCE(@Etat, Etat)
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Ordre", request.Ordre);
                AddParameter(cmd, "@Etat", request.Etat);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour catégorie composant {id}");
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
                    UPDATE CATHEGORIE_COMPOSENTS
                    SET Etat = 'Inactif'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Catégorie composant désactivée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation catégorie composant {id}");
                return false;
            }
        }
    }
}