using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Users
{
    public class UsersService : DatabaseService
    {
        public UsersService(
            string connectionString,
            ILogger<UsersService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<UserListResponse> GetAllAsync()
        {
            var result = new UserListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();

                // ✅ FILTRAGE MANUEL
                var whereClause = BuildWhereClause("u");

                using var cmd = new SqlCommand($@"
                    SELECT u.Id, u.Email, u.Nom, u.Prenom, u.IdEntreprise, u.IdRole,
                           u.IsSuperAdmin, u.IsActive, u.LastLoginAt, u.CreatedAt,
                           e.Designation AS NomEntreprise, r.Name AS RoleName
                    FROM UTILISATEURS u
                    LEFT JOIN ENTREPRISE e ON u.IdEntreprise = e.Id
                    LEFT JOIN ROLES r ON u.IdRole = r.Id
                    WHERE u.IsActive = 1 {whereClause}
                    ORDER BY u.CreatedAt DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Users.Add(MapToDto(reader));
                }

                result.Total = result.Users.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération users");
                result.Success = false;
            }

            return result;
        }

        public async Task<UserDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // ✅ FILTRAGE MANUEL
                var whereClause = BuildWhereClause("u");

                using var cmd = new SqlCommand($@"
                    SELECT u.Id, u.Email, u.Nom, u.Prenom, u.IdEntreprise, u.IdRole,
                           u.IsSuperAdmin, u.IsActive, u.LastLoginAt, u.CreatedAt,
                           e.Designation AS NomEntreprise, r.Name AS RoleName
                    FROM UTILISATEURS u
                    LEFT JOIN ENTREPRISE e ON u.IdEntreprise = e.Id
                    LEFT JOIN ROLES r ON u.IdRole = r.Id
                    WHERE u.Id = @Id {whereClause}", conn);

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
                _logger.LogError(ex, $"Erreur récupération user {id}");
            }

            return null;
        }

        public async Task<UserDto?> CreateAsync(CreateUserRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // TODO: Hasher le password avec BCrypt
                var hashedPassword = request.Password;

                using var cmd = new SqlCommand(@"
                    INSERT INTO UTILISATEURS (
                        Id, Email, MotDePasse, Nom, Prenom, IdEntreprise, IdRole,
                        IsSuperAdmin, IsActive, CreatedAt
                    )
                    VALUES (
                        @Id, @Email, @MotDePasse, @Nom, @Prenom, @IdEntreprise, @IdRole,
                        0, 1, GETUTCDATE()
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Email", request.Email);
                cmd.Parameters.AddWithValue("@MotDePasse", hashedPassword);
                AddParameter(cmd, "@Nom", request.Nom);
                AddParameter(cmd, "@Prenom", request.Prenom);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                AddParameter(cmd, "@IdRole", request.IdRole);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"User créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création user");
                return null;
            }
        }

        public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                // ✅ FILTRAGE MANUEL
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE UTILISATEURS
                    SET Email = COALESCE(@Email, Email),
                        Nom = COALESCE(@Nom, Nom),
                        Prenom = COALESCE(@Prenom, Prenom),
                        IdRole = COALESCE(@IdRole, IdRole),
                        IsActive = COALESCE(@IsActive, IsActive),
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Email", request.Email);
                AddParameter(cmd, "@Nom", request.Nom);
                AddParameter(cmd, "@Prenom", request.Prenom);
                AddParameter(cmd, "@IdRole", request.IdRole);
                AddParameter(cmd, "@IsActive", request.IsActive);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"User mis à jour: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour user {id}");
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
                    UPDATE UTILISATEURS
                    SET IsActive = 0, UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"User désactivé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur désactivation user {id}");
                return false;
            }
        }

        private UserDto MapToDto(SqlDataReader reader)
        {
            return new UserDto
            {
                Id = reader.GetGuid(0),
                Email = reader.GetString(1),
                Nom = ReadNullableString(reader, "Nom"),
                Prenom = ReadNullableString(reader, "Prenom"),
                IdEntreprise = reader.GetGuid(4),
                IdRole = ReadNullableGuid(reader, "IdRole"),
                IsSuperAdmin = reader.GetBoolean(6),
                IsActive = reader.GetBoolean(7),
                LastLoginAt = ReadNullableDateTime(reader, "LastLoginAt"),
                CreatedAt = reader.GetDateTime(9),
                NomEntreprise = ReadNullableString(reader, "NomEntreprise"),
                RoleName = ReadNullableString(reader, "RoleName")
            };
        }
    }
}