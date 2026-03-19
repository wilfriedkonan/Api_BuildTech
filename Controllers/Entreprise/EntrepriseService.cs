using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Entreprise
{
    public class EntrepriseService : DatabaseService
    {
        public EntrepriseService(
            string connectionString,
            ILogger<EntrepriseService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        /// <summary>
        /// Récupère toutes les entreprises avec filtrage manuel
        /// SuperAdmin voit tout, autres voient uniquement leur entreprise
        /// </summary>
        public async Task<EntrepriseListResponse> GetAllAsync()
        {
            var result = new EntrepriseListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();

                // ✅ FILTRAGE MANUEL
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, Localisation, Contact, Email, Pays, Ville, Commune,
                           NRC, Autorisation, CodeEntreprise, IsActive, SubscriptionStatus,
                           SubscriptionEndsAt, CreatedAt, UpdatedAt
                    FROM ENTREPRISE
                    WHERE IsActive = 1 {whereClause}
                    ORDER BY CreatedAt DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Entreprises.Add(MapToDto(reader));
                }

                result.Total = result.Entreprises.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération entreprises");
                result.Success = false;
            }

            return result;
        }

        public async Task<EntrepriseDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // ✅ FILTRAGE MANUEL
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    SELECT Id, Designation, Localisation, Contact, Email, Pays, Ville, Commune,
                           NRC, Autorisation, CodeEntreprise, IsActive, SubscriptionStatus,
                           SubscriptionEndsAt, CreatedAt, UpdatedAt
                    FROM ENTREPRISE
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapToDto(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération entreprise {id}");
            }

            return null;
        }

        public async Task<EntrepriseDto?> CreateAsync(CreateEntrepriseRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO ENTREPRISE (
                        Id, Designation, Email, Contact, Localisation, Pays, Ville, Commune,
                        CodeEntreprise, IsActive, SubscriptionStatus, CreatedAt, Etat, Autorisation
                    )
                    VALUES (
                        @Id, @Designation, @Email, @Contact, @Localisation, @Pays, @Ville, @Commune,
                        @CodeEntreprise, 0, 'Active', GETUTCDATE(), NULL, 0
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Email", request.Email);
                AddParameter(cmd, "@Contact", request.Contact);
                AddParameter(cmd, "@Localisation", request.Localisation);
                AddParameter(cmd, "@Pays", request.Pays);
                AddParameter(cmd, "@Ville", request.Ville);
                AddParameter(cmd, "@Commune", request.Commune);
                AddParameter(cmd, "@CodeEntreprise", request.CodeEntreprise ?? GenerateCode());

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Entreprise créée: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création entreprise");
                return null;
            }
        }

        public async Task<EntrepriseDto?> UpdateAsync(Guid id, UpdateEntrepriseRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // ✅ FILTRAGE MANUEL pour sécurité
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE ENTREPRISE
                    SET Designation = COALESCE(@Designation, Designation),
                        Email = COALESCE(@Email, Email),
                        Contact = COALESCE(@Contact, Contact),
                        Localisation = COALESCE(@Localisation, Localisation),
                        Pays = COALESCE(@Pays, Pays),
                        Ville = COALESCE(@Ville, Ville),
                        Commune = COALESCE(@Commune, Commune),
                        IsActive = COALESCE(@IsActive, IsActive),
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Email", request.Email);
                AddParameter(cmd, "@Contact", request.Contact);
                AddParameter(cmd, "@Localisation", request.Localisation);
                AddParameter(cmd, "@Pays", request.Pays);
                AddParameter(cmd, "@Ville", request.Ville);
                AddParameter(cmd, "@Commune", request.Commune);
                AddParameter(cmd, "@IsActive", request.IsActive);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Entreprise mise à jour: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour entreprise {id}");
                return null;
            }
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // ✅ FILTRAGE MANUEL
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE ENTREPRISE
                    SET IsActive = 0, UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Entreprise désactivée: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation entreprise {id}");
                return false;
            }
        }

        private EntrepriseDto MapToDto(SqlDataReader reader)
        {
            return new EntrepriseDto
            {
                Id = reader.GetGuid(0),
                Designation = ReadNullableString(reader, "Designation"),
                Localisation = ReadNullableString(reader, "Localisation"),
                Contact = ReadNullableString(reader, "Contact"),
                Email = ReadNullableString(reader, "Email"),
                Pays = ReadNullableString(reader, "Pays"),
                Ville = ReadNullableString(reader, "Ville"),
                Commune = ReadNullableString(reader, "Commune"),
                NRC = ReadNullableString(reader, "NRC"),
                Autorisation = reader.GetBoolean(reader.GetOrdinal("Autorisation")),
                CodeEntreprise = ReadNullableString(reader, "CodeEntreprise"),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                SubscriptionStatus = ReadNullableString(reader, "SubscriptionStatus"),
                SubscriptionEndsAt = ReadNullableDateTime(reader, "SubscriptionEndsAt"),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = ReadNullableDateTime(reader, "UpdatedAt")
            };
        }

        private string GenerateCode()
        {
            return $"ENT{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}