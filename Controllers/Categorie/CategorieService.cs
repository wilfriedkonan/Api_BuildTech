using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Categorie
{
    public class CategorieService : DatabaseService
    {
        public CategorieService(
            string connectionString,
            ILogger<CategorieService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<CathegorieListResponse> GetAllAsync()
        {
            var result = new CathegorieListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id,  c.Code, c.Designation, c.Couleur, c.IdEntreprise, c.IdDomaine,
                           c.Etat, c.Ordre, c.EstRestaurant, c.EstEmporte, c.Statut
                    FROM CATHEGORIE c
                    WHERE ISNULL(c.Etat, 'Actif') != 'Supprimer' {whereClause}
                    ORDER BY c.Ordre, c.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var categorie = new CathegorieDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Code = ReadNullableString(reader, "Code"),
                        Couleur = ReadNullableString(reader, "Couleur"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdDomaine = ReadNullableGuid(reader, "IdDomaine"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        EstRestaurant = ReadNullableBool(reader, "EstRestaurant"),
                        EstEmporte = ReadNullableBool(reader, "EstEmporte"),
                        Statut = ReadNullableBool(reader, "Statut")

                    };

                    result.Categories.Add(categorie);
                }

                result.Total = result.Categories.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories");
                result.Success = false;
            }

            return result;
        }

        public async Task<CathegorieListResponse> GetRestaurantAsync()
        {
            var result = new CathegorieListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id,  c.Code, c.Designation, c.Couleur, c.IdEntreprise, c.IdDomaine,
                           c.Etat, c.Ordre, c.EstRestaurant, c.EstEmporte, c.Statut
                    FROM CATHEGORIE c
                    WHERE c.EstRestaurant = 1 
                      AND ISNULL(c.Etat, 'Actif') = 'Actif' 
                      {whereClause}
                    ORDER BY c.Ordre, c.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var categorie = new CathegorieDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Code = ReadNullableString(reader, "Code"),
                        Couleur = ReadNullableString(reader, "Couleur"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdDomaine = ReadNullableGuid(reader, "IdDomaine"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        EstRestaurant = ReadNullableBool(reader, "EstRestaurant"),
                        EstEmporte = ReadNullableBool(reader, "EstEmporte"),
                        Statut = ReadNullableBool(reader, "Statut")

                    };

                    result.Categories.Add(categorie);
                }

                result.Total = result.Categories.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories restaurant");
                result.Success = false;
            }

            return result;
        }

        public async Task<CathegorieListResponse> GetEmporteAsync()
        {
            var result = new CathegorieListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id,  c.Code, c.Designation, c.Couleur, c.IdEntreprise, c.IdDomaine,
                           c.Etat, c.Ordre, c.EstRestaurant, c.EstEmporte,  c.Statut
                    FROM CATHEGORIE c
                    WHERE c.EstEmporte = 1 
                      AND ISNULL(c.Etat, 'Actif') = 'Actif' 
                      {whereClause}
                    ORDER BY c.Ordre, c.Designation", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var categorie = new CathegorieDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Code = ReadNullableString(reader, "Code"),
                        Couleur = ReadNullableString(reader, "Couleur"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdDomaine = ReadNullableGuid(reader, "IdDomaine"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        EstRestaurant = ReadNullableBool(reader, "EstRestaurant"),
                        EstEmporte = ReadNullableBool(reader, "EstEmporte"),
                        Statut = ReadNullableBool(reader, "Statut")

                    };

                    result.Categories.Add(categorie);
                }

                result.Total = result.Categories.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération catégories à emporter");
                result.Success = false;
            }

            return result;
        }

        public async Task<CathegorieDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id,  c.Code, c.Designation, c.Couleur, c.IdEntreprise, c.IdDomaine,
                           c.Etat, c.Ordre, c.EstRestaurant, c.EstEmporte,  c.Statut
                    FROM CATHEGORIE c
                    WHERE c.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new CathegorieDto
                    {
                        Id = reader.GetGuid(0),
                        Designation = ReadNullableString(reader, "Designation"),
                        Code = ReadNullableString(reader, "Code"),
                        Couleur = ReadNullableString(reader, "Couleur"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        IdDomaine = ReadNullableGuid(reader, "IdDomaine"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ordre = ReadNullableInt(reader, "Ordre"),
                        EstRestaurant = ReadNullableBool(reader, "EstRestaurant"),
                        EstEmporte = ReadNullableBool(reader, "EstEmporte"),
                        Statut = ReadNullableBool(reader, "Statut")

                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération catégorie {id}");
            }

            return null;
        }

        public async Task<CathegorieDto?> CreateAsync(CreateCathegorieRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO CATHEGORIE (
                        Id, Code, Designation, Couleur, IdEntreprise, IdDomaine, Etat,
                        Ordre, EstRestaurant, EstEmporte,Statut
                    )
                    VALUES (
                        @Id, @Code,@Designation,@Couleur, @IdEntreprise, @IdDomaine, @Etat,
                        @Ordre, @EstRestaurant, @EstEmporte, @Statut
                    )", conn);

                
                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Code", request.Code);
                cmd.Parameters.AddWithValue("@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@Couleur", request.Couleur);
                AddParameter(cmd, "@IdDomaine", request.IdDomaine);
                AddParameter(cmd, "@Etat", request.Etat);
                AddParameter(cmd, "@Ordre", request.Ordre);
                cmd.Parameters.AddWithValue("@EstRestaurant", request.EstRestaurant);
                cmd.Parameters.AddWithValue("@EstEmporte", request.EstEmporte);
                cmd.Parameters.AddWithValue("@Statut", request.Statut);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Catégorie créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création catégorie");
                return null;
            }
        }

        public async Task<CathegorieDto?> UpdateAsync(Guid id, UpdateCathegorieRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE CATHEGORIE
                    SET Code = COALESCE(@Code, Code),
                        Designation = COALESCE(@Designation, Designation),
                        Couleur = COALESCE(@Couleur, Couleur),
                        IdDomaine = COALESCE(@IdDomaine, IdDomaine),
                        Etat = COALESCE(@Etat, Etat),
                        Ordre = COALESCE(@Ordre, Ordre),
                        EstRestaurant = COALESCE(@EstRestaurant, EstRestaurant),
                        EstEmporte = COALESCE(@EstEmporte, EstEmporte),
                        Statut = COALESCE(@Statut, Statut)

                    WHERE Id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Code", request.Code);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Couleur", request.Couleur);
                AddParameter(cmd, "@IdDomaine", request.IdDomaine);
                AddParameter(cmd, "@Etat", request.Etat);
                AddParameter(cmd, "@Ordre", request.Ordre);
                AddParameter(cmd, "@EstRestaurant", request.EstRestaurant);
                AddParameter(cmd, "@EstEmporte", request.EstEmporte);
                AddParameter(cmd, "@Statut", request.Statut);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour catégorie {id}");
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
                    UPDATE CATHEGORIE
                    SET Etat = 'Supprimer'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Catégorie supprimée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression catégorie {id}");
                return false;
            }
        }
    }
}