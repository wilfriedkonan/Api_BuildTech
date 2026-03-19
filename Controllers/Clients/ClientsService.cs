using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.Clients
{
    public class ClientsService : DatabaseService
    {
        public ClientsService(
            string connectionString,
            ILogger<ClientsService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        public async Task<ClientListResponse> GetAllAsync()
        {
            var result = new ClientListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.Nom, c.Prenoms, c.Telephone, c.Email,
                           c.DateNaissance, c.LieuNaissance, c.Addresse,
                           c.EstAbonner, c.NumeroCNI, c.IdEntreprise,
                           c.Etat, c.ident
                    FROM CLIENTS c
                    WHERE ISNULL(c.Etat, 'Actif') != 'Supprimer' {whereClause}
                    ORDER BY c.Nom, c.Prenoms", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var client = new ClientDto
                    {
                        Id = reader.GetGuid(0),
                        Nom = ReadNullableString(reader, "Nom"),
                        Prenoms = ReadNullableString(reader, "Prenoms"),
                        Telephone = ReadNullableString(reader, "Telephone"),
                        Email = ReadNullableString(reader, "Email"),
                        DateNaissance = reader.GetDateTime("DateNaissance"),
                        LieuNaissance = ReadNullableString(reader, "LieuNaissance"),
                        Addresse = ReadNullableString(reader, "Addresse"),
                        EstAbonner = ReadNullableBool(reader, "EstAbonner"),
                        NumeroCNI = ReadNullableString(reader, "NumeroCNI"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ident = ReadNullableString(reader, "ident")
                    };

                    result.Clients.Add(client);

                    if (client.EstAbonner == true)
                        result.TotalAbonnes++;
                }

                result.Total = result.Clients.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération clients");
                result.Success = false;
            }

            return result;
        }

        public async Task<ClientListResponse> GetAbonnesAsync()
        {
            var result = new ClientListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.Nom, c.Prenoms, c.Telephone, c.Email,
                           c.DateNaissance, c.LieuNaissance, c.Addresse,
                           c.EstAbonner, c.NumeroCNI, c.IdEntreprise,
                           c.Etat, c.ident
                    FROM CLIENTS c
                    WHERE c.EstAbonner = 1 
                      AND ISNULL(c.Etat, 'Actif') = 'Actif' 
                      {whereClause}
                    ORDER BY c.Nom, c.Prenoms", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var client = new ClientDto
                    {
                        Id = reader.GetGuid(0),
                        Nom = ReadNullableString(reader, "Nom"),
                        Prenoms = ReadNullableString(reader, "Prenoms"),
                        Telephone = ReadNullableString(reader, "Telephone"),
                        Email = ReadNullableString(reader, "Email"),
                        DateNaissance = reader.GetDateTime("DateNaissance"),
                        LieuNaissance = ReadNullableString(reader, "LieuNaissance"),
                        Addresse = ReadNullableString(reader, "Addresse"),
                        EstAbonner = ReadNullableBool(reader, "EstAbonner"),
                        NumeroCNI = ReadNullableString(reader, "NumeroCNI"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ident = ReadNullableString(reader, "ident")
                    };

                    result.Clients.Add(client);
                }

                result.Total = result.Clients.Count;
                result.TotalAbonnes = result.Total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération clients abonnés");
                result.Success = false;
            }

            return result;
        }

        public async Task<ClientListResponse> SearchAsync(string? nom, string? telephone)
        {
            var result = new ClientListResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                var searchConditions = "";
                if (!string.IsNullOrEmpty(nom))
                    searchConditions += "AND (c.Nom LIKE @Nom OR c.Prenoms LIKE @Nom) ";

                if (!string.IsNullOrEmpty(telephone))
                    searchConditions += "AND c.Telephone LIKE @Telephone ";

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.Nom, c.Prenoms, c.Telephone, c.Email,
                           c.DateNaissance, c.LieuNaissance, c.Addresse,
                           c.EstAbonner, c.NumeroCNI, c.IdEntreprise,
                           c.Etat, c.ident
                    FROM CLIENTS c
                    WHERE ISNULL(c.Etat, 'Actif') = 'Actif' 
                      {whereClause} 
                      {searchConditions}
                    ORDER BY c.Nom, c.Prenoms", conn);

                AddEntrepriseParameter(cmd);

                if (!string.IsNullOrEmpty(nom))
                    cmd.Parameters.AddWithValue("@Nom", $"%{nom}%");

                if (!string.IsNullOrEmpty(telephone))
                    cmd.Parameters.AddWithValue("@Telephone", $"%{telephone}%");

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var client = new ClientDto
                    {
                        Id = reader.GetGuid(0),
                        Nom = ReadNullableString(reader, "Nom"),
                        Prenoms = ReadNullableString(reader, "Prenoms"),
                        Telephone = ReadNullableString(reader, "Telephone"),
                        Email = ReadNullableString(reader, "Email"),
                        DateNaissance = reader.GetDateTime("DateNaissance"),
                        LieuNaissance = ReadNullableString(reader, "LieuNaissance"),
                        Addresse = ReadNullableString(reader, "Addresse"),
                        EstAbonner = ReadNullableBool(reader, "EstAbonner"),
                        NumeroCNI = ReadNullableString(reader, "NumeroCNI"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ident = ReadNullableString(reader, "ident")
                    };

                    result.Clients.Add(client);

                    if (client.EstAbonner == true)
                        result.TotalAbonnes++;
                }

                result.Total = result.Clients.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche clients");
                result.Success = false;
            }

            return result;
        }

        public async Task<ClientDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                using var cmd = new SqlCommand($@"
                    SELECT c.Id, c.Nom, c.Prenoms, c.Telephone, c.Email,
                           c.DateNaissance, c.LieuNaissance, c.Addresse,
                           c.EstAbonner, c.NumeroCNI, c.ImageDoctument,
                           c.IdEntreprise, c.Etat, c.ident
                    FROM CLIENTS c
                    WHERE c.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new ClientDto
                    {
                        Id = reader.GetGuid(0),
                        Nom = ReadNullableString(reader, "Nom"),
                        Prenoms = ReadNullableString(reader, "Prenoms"),
                        Telephone = ReadNullableString(reader, "Telephone"),
                        Email = ReadNullableString(reader, "Email"),
                        DateNaissance = reader.GetDateTime("DateNaissance"),
                        LieuNaissance = ReadNullableString(reader, "LieuNaissance"),
                        Addresse = ReadNullableString(reader, "Addresse"),
                        EstAbonner = ReadNullableBool(reader, "EstAbonner"),
                        NumeroCNI = ReadNullableString(reader, "NumeroCNI"),
                        ImageDoctument = reader.GetString("ImageDoctument"),
                        IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                        Etat = ReadNullableString(reader, "Etat"),
                        Ident = ReadNullableString(reader, "ident")
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération client {id}");
            }

            return null;
        }

        public async Task<ClientDto?> CreateAsync(CreateClientRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO CLIENTS (
                        Id, Nom, Prenoms, Telephone, Email, DateNaissance,
                        LieuNaissance, Addresse, EstAbonner, NumeroCNI,
                        IdEntreprise, Etat
                    )
                    VALUES (
                        @Id, @Nom, @Prenoms, @Telephone, @Email, @DateNaissance,
                        @LieuNaissance, @Addresse, @EstAbonner, @NumeroCNI,
                        @IdEntreprise, @Etat
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@Nom", request.Nom);
                AddParameter(cmd, "@Prenoms", request.Prenoms);
                AddParameter(cmd, "@Telephone", request.Telephone);
                AddParameter(cmd, "@Email", request.Email);
                AddParameter(cmd, "@DateNaissance", request.DateNaissance);
                AddParameter(cmd, "@LieuNaissance", request.LieuNaissance);
                AddParameter(cmd, "@Addresse", request.Addresse);
                cmd.Parameters.AddWithValue("@EstAbonner", request.EstAbonner);
                AddParameter(cmd, "@NumeroCNI", request.NumeroCNI);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                AddParameter(cmd, "@Etat", request.Etat);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Client créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création client");
                return null;
            }
        }

        public async Task<ClientDto?> UpdateAsync(Guid id, UpdateClientRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE CLIENTS
                    SET Nom = COALESCE(@Nom, Nom),
                        Prenoms = COALESCE(@Prenoms, Prenoms),
                        Telephone = COALESCE(@Telephone, Telephone),
                        Email = COALESCE(@Email, Email),
                        DateNaissance = COALESCE(@DateNaissance, DateNaissance),
                        LieuNaissance = COALESCE(@LieuNaissance, LieuNaissance),
                        Addresse = COALESCE(@Addresse, Addresse),
                        NumeroCNI = COALESCE(@NumeroCNI, NumeroCNI),
                        Etat = COALESCE(@Etat, Etat)
                    WHERE Id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Nom", request.Nom);
                AddParameter(cmd, "@Prenoms", request.Prenoms);
                AddParameter(cmd, "@Telephone", request.Telephone);
                AddParameter(cmd, "@Email", request.Email);
                AddParameter(cmd, "@DateNaissance", request.DateNaissance);
                AddParameter(cmd, "@LieuNaissance", request.LieuNaissance);
                AddParameter(cmd, "@Addresse", request.Addresse);
                AddParameter(cmd, "@NumeroCNI", request.NumeroCNI);
                AddParameter(cmd, "@Etat", request.Etat);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour client {id}");
                return null;
            }
        }

        public async Task<ClientDto?> UpdateAbonnementAsync(Guid id, UpdateAbonnementRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                string sql = $@"
                    UPDATE CLIENTS
                    SET EstAbonner = @EstAbonner
                    WHERE Id = @Id {whereClause}";

                using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@EstAbonner", request.EstAbonner);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Abonnement client mis à jour: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour abonnement client {id}");
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
                    UPDATE CLIENTS
                    SET Etat = 'Supprimer'
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Client supprimé: {id}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression client {id}");
                return false;
            }
        }
    }
}