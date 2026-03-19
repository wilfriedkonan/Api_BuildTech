using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Organisation
{
    public class OrganisationService : DatabaseService
    {
        public OrganisationService(
            string connectionString,
            ILogger<OrganisationService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<OrganisationListResponse> GetAllAsync()
        {
            var result = new OrganisationListResponse { Success = true };

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Identifiant, Designation, Etat, EstActif, CreatedDate
                    FROM ORGANISATION
                    WHERE ISNULL(Etat, 'Actif') != 'Supprimer'
                    ORDER BY CreatedDate DESC, Designation", conn);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var organisation = new OrganisationDto
                    {
                        Id = reader.GetGuid(0),
                        Identifiant = reader.GetString(1),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        EstActif = ReadNullableBool(reader, "EstActif"),
                        CreatedDate = reader.GetDateTime(5)
                    };

                    result.Organisations.Add(organisation);

                    if (organisation.EstActif == true)
                        result.TotalActives++;
                }

                result.Total = result.Organisations.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération organisations");
                result.Success = false;
            }

            return result;
        }

        public async Task<OrganisationListResponse> GetActivesAsync()
        {
            var result = new OrganisationListResponse { Success = true };

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Identifiant, Designation, Etat, EstActif, CreatedDate
                    FROM ORGANISATION
                    WHERE EstActif = 1 
                      AND ISNULL(Etat, 'Actif') = 'Actif'
                    ORDER BY Designation", conn);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var organisation = new OrganisationDto
                    {
                        Id = reader.GetGuid(0),
                        Identifiant = reader.GetString(1),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        EstActif = ReadNullableBool(reader, "EstActif"),
                        CreatedDate = reader.GetDateTime(5)
                    };

                    result.Organisations.Add(organisation);
                }

                result.Total = result.Organisations.Count;
                result.TotalActives = result.Total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération organisations actives");
                result.Success = false;
            }

            return result;
        }

        public async Task<OrganisationDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Identifiant, Designation, Etat, EstActif, CreatedDate
                    FROM ORGANISATION
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", id);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new OrganisationDto
                    {
                        Id = reader.GetGuid(0),
                        Identifiant = reader.GetString(1),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        EstActif = ReadNullableBool(reader, "EstActif"),
                        CreatedDate = reader.GetDateTime(5)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération organisation {id}");
            }

            return null;
        }

        public async Task<OrganisationDto?> GetByIdentifiantAsync(string identifiant)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT Id, Identifiant, Designation, Etat, EstActif, CreatedDate
                    FROM ORGANISATION
                    WHERE Identifiant = @Identifiant", conn);

                cmd.Parameters.AddWithValue("@Identifiant", identifiant);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new OrganisationDto
                    {
                        Id = reader.GetGuid(0),
                        Identifiant = reader.GetString(1),
                        Designation = ReadNullableString(reader, "Designation"),
                        Etat = ReadNullableString(reader, "Etat"),
                        EstActif = ReadNullableBool(reader, "EstActif"),
                        CreatedDate = reader.GetDateTime(5)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération organisation {identifiant}");
            }

            return null;
        }

        public async Task<OrganisationDto?> CreateAsync(CreateOrganisationRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();
                var createdDate = DateTime.Now;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO ORGANISATION (
                        Id, Identifiant, Designation, Etat, EstActif, CreatedDate
                    )
                    VALUES (
                        @Id, @Identifiant, @Designation, @Etat, @EstActif, @CreatedDate
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Identifiant", request.Identifiant);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Etat", request.Etat);
                cmd.Parameters.AddWithValue("@EstActif", request.EstActif);
                cmd.Parameters.AddWithValue("@CreatedDate", createdDate);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Organisation créée: {newId} - {request.Identifiant}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création organisation");
                return null;
            }
        }

        public async Task<OrganisationDto?> UpdateAsync(Guid id, UpdateOrganisationRequest request)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                    UPDATE ORGANISATION
                    SET Designation = COALESCE(@Designation, Designation),
                        Etat = COALESCE(@Etat, Etat),
                        EstActif = COALESCE(@EstActif, EstActif)
                    WHERE Id = @Id";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Designation", request.Designation);
                AddParameter(cmd, "@Etat", request.Etat);
                AddParameter(cmd, "@EstActif", request.EstActif);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                    return null;

                _logger.LogInformation($"Organisation mise à jour: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour organisation {id}");
                return null;
            }
        }
    }
}