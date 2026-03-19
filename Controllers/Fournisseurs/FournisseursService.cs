using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Fournisseurs
{
    public class FournisseursService : DatabaseService
    {
        public FournisseursService(
            string connectionString,
            ILogger<FournisseursService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // GET ALL AVEC PAGINATION
        // ========================================

        /// <summary>
        /// Récupère tous les fournisseurs avec pagination
        /// </summary>
        public async Task<FournisseurListResponse> GetAllAsync(int page = 1, int pageSize = 20)
        {
            var result = new FournisseurListResponse { Success = true };

            try
            {
                // Validation pagination
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                // ========================================
                // ÉTAPE 1 : COMPTER LE TOTAL
                // ========================================
                int totalRecords = 0;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM FOURNISSEURS f
                    WHERE ISNULL(f.Etat, 'Actif') != 'Supprimer' {whereClause}", conn))
                {
                    AddEntrepriseParameter(cmdCount);
                    totalRecords = (int)await cmdCount.ExecuteScalarAsync();
                }

                if (totalRecords == 0)
                {
                    result.Total = 0;
                    result.Pagination = CreateEmptyPagination(page, pageSize);
                    return result;
                }

                // Calculer pagination
                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                int offset = (page - 1) * pageSize;

                // ========================================
                // ÉTAPE 2 : RÉCUPÉRER DONNÉES PAGINÉES
                // ========================================
                using (var cmd = new SqlCommand($@"
                    SELECT 
                        f.Id, f.Code, f.Nom, f.Specialite, f.Contact, 
                        f.Email, f.NRC, f.IdEntreprise, f.Etat, f.Adresse, f.Statut,
                        f.DateCreate, f.idCreateUser, f.DateLastUpdate, f.idLastUpdateUser,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM FOURNISSEURS f
                    LEFT JOIN UTILISATEURS u1 ON f.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON f.idLastUpdateUser = u2.Id
                    WHERE ISNULL(f.Etat, 'Actif') != 'Supprimer' {whereClause}
                    ORDER BY f.Nom
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var fournisseur = MapFournisseurFromReader(reader);
                        result.Fournisseurs.Add(fournisseur);

                        // Compteurs par état
                        if (fournisseur.Etat == "Actif")
                            result.TotalActifs++;
                        else
                            result.TotalInactifs++;
                    }
                }

                result.Total = totalRecords;
                result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);

                _logger.LogInformation($"✅ Fournisseurs récupérés: Page {page}/{totalPages}, {result.Fournisseurs.Count} fournisseurs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération fournisseurs");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // SEARCH AVEC PAGINATION
        // ========================================

        /// <summary>
        /// Recherche de fournisseurs avec pagination
        /// </summary>
        public async Task<FournisseurListResponse> SearchAsync(
            string? nom,
            string? contact,
            int page = 1,
            int pageSize = 20)
        {
            var result = new FournisseurListResponse { Success = true };

            try
            {
                // Validation pagination
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                // Construire conditions de recherche
                var searchConditions = "";
                if (!string.IsNullOrEmpty(nom))
                    searchConditions += "AND f.Nom LIKE @Nom ";

                if (!string.IsNullOrEmpty(contact))
                    searchConditions += "AND f.Contact LIKE @Contact ";

                // Compter total
                int totalRecords = 0;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM FOURNISSEURS f
                    WHERE ISNULL(f.Etat, 'Actif') = 'Actif' 
                      {whereClause} 
                      {searchConditions}", conn))
                {
                    AddEntrepriseParameter(cmdCount);
                    if (!string.IsNullOrEmpty(nom))
                        cmdCount.Parameters.AddWithValue("@Nom", $"%{nom}%");
                    if (!string.IsNullOrEmpty(contact))
                        cmdCount.Parameters.AddWithValue("@Contact", $"%{contact}%");

                    totalRecords = (int)await cmdCount.ExecuteScalarAsync();
                }

                if (totalRecords == 0)
                {
                    result.Pagination = CreateEmptyPagination(page, pageSize);
                    return result;
                }

                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                int offset = (page - 1) * pageSize;

                // Récupérer données
                using (var cmd = new SqlCommand($@"
                    SELECT 
                        f.Id, f.Code, f.Nom, f.Specialite, f.Contact, 
                        f.Email, f.NRC, f.IdEntreprise, f.Etat, f.Adresse, f.Statut,
                        f.DateCreate, f.idCreateUser, f.DateLastUpdate, f.idLastUpdateUser,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM FOURNISSEURS f
                    LEFT JOIN UTILISATEURS u1 ON f.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON f.idLastUpdateUser = u2.Id
                    WHERE ISNULL(f.Etat, 'Actif') = 'Actif' 
                      {whereClause} 
                      {searchConditions}
                    ORDER BY f.Nom
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    if (!string.IsNullOrEmpty(nom))
                        cmd.Parameters.AddWithValue("@Nom", $"%{nom}%");
                    if (!string.IsNullOrEmpty(contact))
                        cmd.Parameters.AddWithValue("@Contact", $"%{contact}%");
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        result.Fournisseurs.Add(MapFournisseurFromReader(reader));
                    }
                }

                result.Total = totalRecords;
                result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);

                _logger.LogInformation($"✅ Recherche fournisseurs: {result.Fournisseurs.Count} résultats");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche fournisseurs");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET BY ID
        // ========================================

        public async Task<FournisseurDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        f.Id, f.Code, f.Nom, f.Specialite, f.Contact, 
                        f.Email, f.NRC, f.IdEntreprise, f.Etat, f.Adresse, f.Statut,
                        f.DateCreate, f.idCreateUser, f.DateLastUpdate, f.idLastUpdateUser,
                        u1.Nom + ' ' + ISNULL(u1.Prenom, '') AS NomCreateUser,
                        u2.Nom + ' ' + ISNULL(u2.Prenom, '') AS NomLastUpdateUser
                    FROM FOURNISSEURS f
                    LEFT JOIN UTILISATEURS u1 ON f.idCreateUser = u1.Id
                    LEFT JOIN UTILISATEURS u2 ON f.idLastUpdateUser = u2.Id
                    WHERE f.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapFournisseurFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération fournisseur {id}");
            }

            return null;
        }

        // ========================================
        // CREATE WITH TRACKING
        // ========================================

        public async Task<FournisseurDto?> CreateAsync(CreateFournisseurRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();
                var currentUserId = GetUserIdFromContext();
                var currentDate = DateTime.Now;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO FOURNISSEURS (
                        Id, Code, Nom, Specialite, Contact, Email, NRC,
                        IdEntreprise, Etat, Adresse, Statut,
                        DateCreate, idCreateUser, DateLastUpdate, idLastUpdateUser
                    )
                    VALUES (
                        @Id, @Code, @Nom, @Specialite, @Contact, @Email, @NRC,
                        @IdEntreprise, @Etat, @Adresse, @Statut,
                        @DateCreate, @idCreateUser, @DateLastUpdate, @idLastUpdateUser
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                AddParameter(cmd, "@Code", request.Code);
                cmd.Parameters.AddWithValue("@Nom", request.Nom);
                AddParameter(cmd, "@Specialite", request.Specialite);
                AddParameter(cmd, "@Contact", request.Contact);
                AddParameter(cmd, "@Email", request.Email);
                AddParameter(cmd, "@NRC", request.NRC);

                cmd.Parameters.AddWithValue("@Etat", "Actif");
                AddParameter(cmd, "@Adresse", request.Adresse);
                cmd.Parameters.AddWithValue("@Statut", request.Statut);

                // ✅ Tracking utilisateur
                cmd.Parameters.AddWithValue("@DateCreate", currentDate);
                cmd.Parameters.AddWithValue("@idCreateUser", currentUserId);
                cmd.Parameters.AddWithValue("@DateLastUpdate", currentDate);
                cmd.Parameters.AddWithValue("@idLastUpdateUser", currentUserId);

                //Entreprise 
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Fournisseur créé: {newId} par utilisateur {currentUserId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création fournisseur");
                return null;
            }
        }

        // ========================================
        // UPDATE WITH TRACKING
        // ========================================

        public async Task<FournisseurDto?> UpdateAsync(Guid id, UpdateFournisseurRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();
                var currentUserId = GetUserIdFromContext();
                var currentDate = DateTime.Now;

                using var cmd = new SqlCommand($@"
                    UPDATE FOURNISSEURS
                    SET Code = COALESCE(@Code, Code),
                        Nom = COALESCE(@Nom, Nom),
                        Specialite = COALESCE(@Specialite, Specialite),
                        Contact = COALESCE(@Contact, Contact),
                        Email = COALESCE(@Email, Email),
                        NRC = COALESCE(@NRC, NRC),
                        Etat = COALESCE(@Etat, Etat),
                        Adresse = COALESCE(@Adresse, Adresse),
                        Statut = COALESCE(@Statut, Statut),
                        DateLastUpdate = @DateLastUpdate,
                        idLastUpdateUser = @idLastUpdateUser
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Code", request.Code);
                AddParameter(cmd, "@Nom", request.Nom);
                AddParameter(cmd, "@Specialite", request.Specialite);
                AddParameter(cmd, "@Contact", request.Contact);
                AddParameter(cmd, "@Email", request.Email);
                AddParameter(cmd, "@NRC", request.NRC);
                AddParameter(cmd, "@Etat", request.Etat);
                AddParameter(cmd, "@Adresse", request.Adresse);
                AddParameter(cmd, "@Statut", request.Statut);

                // ✅ Tracking utilisateur
                cmd.Parameters.AddWithValue("@DateLastUpdate", currentDate);
                cmd.Parameters.AddWithValue("@idLastUpdateUser", currentUserId);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Fournisseur mis à jour: {id} par utilisateur {currentUserId}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour fournisseur {id}");
                return null;
            }
        }

        // ========================================
        // DELETE (SOFT)
        // ========================================

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();
                var currentUserId = GetUserIdFromContext();

                using var cmd = new SqlCommand($@"
                    UPDATE FOURNISSEURS
                    SET Etat = 'Supprimer',
                        DateLastUpdate = @DateLastUpdate,
                        idLastUpdateUser = @idLastUpdateUser
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@DateLastUpdate", DateTime.Now);
                cmd.Parameters.AddWithValue("@idLastUpdateUser", currentUserId);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Fournisseur supprimé: {id} par utilisateur {currentUserId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression fournisseur {id}");
                return false;
            }
        }

        // ========================================
        // HELPER : MAP FOURNISSEUR FROM READER
        // ========================================

        private FournisseurDto MapFournisseurFromReader(SqlDataReader reader)
        {
            return new FournisseurDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Code = ReadNullableString(reader, "Code"),
                Nom = ReadNullableString(reader, "Nom"),
                Specialite = ReadNullableString(reader, "Specialite"),
                Contact = ReadNullableString(reader, "Contact"),
                Email = ReadNullableString(reader, "Email"),
                NRC = ReadNullableString(reader, "NRC"),
                IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                Etat = ReadNullableString(reader, "Etat"),
                Adresse = ReadNullableString(reader, "Adresse"),
                Statut = ReadNullableBool(reader, "Statut"),

                // Tracking
                DateCreate = ReadNullableDateTime(reader, "DateCreate"),
                idCreateUser = ReadNullableGuid(reader, "idCreateUser"),
                NomCreateUser = ReadNullableString(reader, "NomCreateUser"),
                DateLastUpdate = ReadNullableDateTime(reader, "DateLastUpdate"),
                idLastUpdateUser = ReadNullableGuid(reader, "idLastUpdateUser"),
                NomLastUpdateUser = ReadNullableString(reader, "NomLastUpdateUser")
            };
        }

        // ========================================
        // MÉTHODES HELPER PAGINATION
        // ========================================

        private PaginationMetadata CreatePagination(
            int currentPage,
            int pageSize,
            int totalPages,
            int totalRecords)
        {
            return new PaginationMetadata
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalRecords = totalRecords,
                HasPrevious = currentPage > 1,
                HasNext = currentPage < totalPages
            };
        }

        private PaginationMetadata CreateEmptyPagination(int page, int pageSize)
        {
            return new PaginationMetadata
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = 0,
                TotalRecords = 0,
                HasPrevious = false,
                HasNext = false
            };
        }
    }
}