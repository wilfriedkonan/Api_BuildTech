using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Boutique
{
    /// <summary>
    /// Service CRUD pour BOUTIQUE (admin/privé) - Multi-tenant via DatabaseService
    /// </summary>
    public class BoutiqueService : DatabaseService
    {
        public BoutiqueService(
            string connectionString,
            ILogger<BoutiqueService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // GET ALL AVEC PAGINATION
        // ========================================

        public async Task<BoutiqueListResponse> GetAllAsync(int page = 1, int pageSize = 20)
        {
            var result = new BoutiqueListResponse { Success = true };

            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("b");

                // ÉTAPE 1: COMPTER LE TOTAL
                int totalRecords;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM BOUTIQUE b
                    WHERE b.EstActif = 1 {whereClause}", conn))
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

                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                int offset = (page - 1) * pageSize;

                // ÉTAPE 2: RÉCUPÉRER DONNÉES PAGINÉES
                using (var cmd = new SqlCommand($@"
                    SELECT 
                        b.IdBoutique, b.IdEntreprise, b.IdTypeBoutique, b.NomBoutique,
                        b.SlugBoutique, b.CodeQR, b.EmailBoutique, b.TelephoneBoutique,
                        b.AdresseBoutique, b.LogoBoutique, b.DescriptionBoutique,
                        b.URLDomainCustom, b.TypeBoutique, b.EstActif,
                        b.PermitCommandeEnLigne, b.PermitAccesQR, b.PermitLivraison, b.PermitRetrait,
                        b.NombreArticles, b.NombreCategories, b.DateCreation, b.DateMiseAJour,
                        tb.CodeType, tb.IconeType
                    FROM BOUTIQUE b
                    INNER JOIN TYPE_BOUTIQUE tb ON b.IdTypeBoutique = tb.IdTypeBoutique
                    WHERE b.EstActif = 1 {whereClause}
                    ORDER BY b.NomBoutique
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        result.Boutiques.Add(MapBoutiqueFromReader(reader));
                    }
                }

                result.Total = totalRecords;
                result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération boutiques");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // SEARCH AVEC PAGINATION
        // ========================================

        public async Task<BoutiqueListResponse> SearchAsync(
            string? nom,
            string? typeBoutique,
            int page = 1,
            int pageSize = 20)
        {
            var result = new BoutiqueListResponse { Success = true };

            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("b");

                var filters = "";
                if (!string.IsNullOrEmpty(nom))
                    filters += " AND b.NomBoutique LIKE @Nom";
                if (!string.IsNullOrEmpty(typeBoutique))
                    filters += " AND b.TypeBoutique = @TypeBoutique";

                int totalRecords;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM BOUTIQUE b
                    WHERE b.EstActif = 1 {whereClause} {filters}", conn))
                {
                    AddEntrepriseParameter(cmdCount);
                    if (!string.IsNullOrEmpty(nom))
                        cmdCount.Parameters.AddWithValue("@Nom", $"%{nom}%");
                    if (!string.IsNullOrEmpty(typeBoutique))
                        cmdCount.Parameters.AddWithValue("@TypeBoutique", typeBoutique);

                    totalRecords = (int)await cmdCount.ExecuteScalarAsync();
                }

                if (totalRecords == 0)
                {
                    result.Total = 0;
                    result.Pagination = CreateEmptyPagination(page, pageSize);
                    return result;
                }

                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                int offset = (page - 1) * pageSize;

                using (var cmd = new SqlCommand($@"
                    SELECT 
                        b.IdBoutique, b.IdEntreprise, b.IdTypeBoutique, b.NomBoutique,
                        b.SlugBoutique, b.CodeQR, b.EmailBoutique, b.TelephoneBoutique,
                        b.AdresseBoutique, b.LogoBoutique, b.DescriptionBoutique,
                        b.URLDomainCustom, b.TypeBoutique, b.EstActif,
                        b.PermitCommandeEnLigne, b.PermitAccesQR, b.PermitLivraison, b.PermitRetrait,
                        b.NombreArticles, b.NombreCategories, b.DateCreation, b.DateMiseAJour,
                        tb.CodeType, tb.IconeType
                    FROM BOUTIQUE b
                    INNER JOIN TYPE_BOUTIQUE tb ON b.IdTypeBoutique = tb.IdTypeBoutique
                    WHERE b.EstActif = 1 {whereClause} {filters}
                    ORDER BY b.NomBoutique
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    if (!string.IsNullOrEmpty(nom))
                        cmd.Parameters.AddWithValue("@Nom", $"%{nom}%");
                    if (!string.IsNullOrEmpty(typeBoutique))
                        cmd.Parameters.AddWithValue("@TypeBoutique", typeBoutique);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        result.Boutiques.Add(MapBoutiqueFromReader(reader));
                    }
                }

                result.Total = totalRecords;
                result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche boutiques");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET BY ID
        // ========================================

        public async Task<BoutiqueDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("b");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        b.IdBoutique, b.IdEntreprise, b.IdTypeBoutique, b.NomBoutique,
                        b.SlugBoutique, b.CodeQR, b.EmailBoutique, b.TelephoneBoutique,
                        b.AdresseBoutique, b.LogoBoutique, b.DescriptionBoutique,
                        b.URLDomainCustom, b.TypeBoutique, b.EstActif,
                        b.PermitCommandeEnLigne, b.PermitAccesQR, b.PermitLivraison, b.PermitRetrait,
                        b.NombreArticles, b.NombreCategories, b.DateCreation, b.DateMiseAJour,
                        tb.CodeType, tb.IconeType
                    FROM BOUTIQUE b
                    INNER JOIN TYPE_BOUTIQUE tb ON b.IdTypeBoutique = tb.IdTypeBoutique
                    WHERE b.IdBoutique = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapBoutiqueFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération boutique {id}");
            }

            return null;
        }

        // ========================================
        // CREATE (génération Slug + CodeQR en C#)
        // ========================================

        public async Task<BoutiqueDto?> CreateAsync(CreateBoutiqueRequest request)
        {
            try
            {
                if (request.TypeBoutique != "Restaurant" && request.TypeBoutique != "Magasin")
                {
                    _logger.LogWarning($"TypeBoutique invalide: {request.TypeBoutique}");
                    return null;
                }

                var newId = Guid.NewGuid();
                var currentUserId = GetUserIdFromContext();

                using var conn = await GetConnectionAsync();

                // Récupérer IdTypeBoutique depuis TypeBoutique (nom)
                Guid idTypeBoutique;
                using (var cmdType = new SqlCommand(
                    "SELECT IdTypeBoutique FROM TYPE_BOUTIQUE WHERE NomType = @NomType AND EstActif = 1", conn))
                {
                    cmdType.Parameters.AddWithValue("@NomType", request.TypeBoutique);
                    var typeResult = await cmdType.ExecuteScalarAsync();
                    if (typeResult == null)
                    {
                        _logger.LogWarning($"TypeBoutique introuvable en base: {request.TypeBoutique}");
                        return null;
                    }
                    idTypeBoutique = (Guid)typeResult;
                }

                // Génération Slug unique
                var slug = await GenerateUniqueSlugAsync(conn, request.NomBoutique);

                // Génération CodeQR unique (préfixe selon type)
                var codeQR = await GenerateUniqueCodeQRAsync(conn, request.TypeBoutique);

                using (var cmd = new SqlCommand(@"
                    INSERT INTO BOUTIQUE (
                        IdBoutique, IdEntreprise, IdTypeBoutique, NomBoutique, SlugBoutique, CodeQR,
                        EmailBoutique, TelephoneBoutique, AdresseBoutique,
                        LogoBoutique, DescriptionBoutique, TypeBoutique,
                        EstActif, PermitCommandeEnLigne, PermitAccesQR, PermitLivraison, PermitRetrait,
                        DateCreation
                    )
                    VALUES (
                        @IdBoutique, @IdEntreprise, @IdTypeBoutique, @NomBoutique, @SlugBoutique, @CodeQR,
                        @EmailBoutique, @TelephoneBoutique, @AdresseBoutique,
                        @LogoBoutique, @DescriptionBoutique, @TypeBoutique,
                        1, 1, 1, 1, 1,
                        @DateCreation
                    )", conn))
                {
                    cmd.Parameters.AddWithValue("@IdBoutique", newId);
                    cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                    cmd.Parameters.AddWithValue("@IdTypeBoutique", idTypeBoutique);
                    cmd.Parameters.AddWithValue("@NomBoutique", request.NomBoutique);
                    cmd.Parameters.AddWithValue("@SlugBoutique", slug);
                    cmd.Parameters.AddWithValue("@CodeQR", codeQR);
                    AddParameter(cmd, "@EmailBoutique", request.EmailBoutique);
                    AddParameter(cmd, "@TelephoneBoutique", request.TelephoneBoutique);
                    AddParameter(cmd, "@AdresseBoutique", request.AdresseBoutique);
                    AddParameter(cmd, "@LogoBoutique", request.LogoBoutique);
                    AddParameter(cmd, "@DescriptionBoutique", request.DescriptionBoutique);
                    cmd.Parameters.AddWithValue("@TypeBoutique", request.TypeBoutique);
                    cmd.Parameters.AddWithValue("@DateCreation", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation($"✅ Boutique créée: {newId} ({request.TypeBoutique}) par utilisateur {currentUserId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création boutique");
                return null;
            }
        }

        // ========================================
        // UPDATE (champs éditables uniquement - Slug/CodeQR/Type non modifiables)
        // ========================================

        public async Task<BoutiqueDto?> UpdateAsync(Guid id, UpdateBoutiqueRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("");
                var currentUserId = GetUserIdFromContext();

                using var cmd = new SqlCommand($@"
                    UPDATE BOUTIQUE
                    SET NomBoutique = COALESCE(@NomBoutique, NomBoutique),
                        EmailBoutique = COALESCE(@EmailBoutique, EmailBoutique),
                        TelephoneBoutique = COALESCE(@TelephoneBoutique, TelephoneBoutique),
                        AdresseBoutique = COALESCE(@AdresseBoutique, AdresseBoutique),
                        LogoBoutique = COALESCE(@LogoBoutique, LogoBoutique),
                        DescriptionBoutique = COALESCE(@DescriptionBoutique, DescriptionBoutique),
                        PermitCommandeEnLigne = COALESCE(@PermitCommandeEnLigne, PermitCommandeEnLigne),
                        PermitLivraison = COALESCE(@PermitLivraison, PermitLivraison),
                        PermitRetrait = COALESCE(@PermitRetrait, PermitRetrait),
                        DateMiseAJour = @DateMiseAJour
                    WHERE IdBoutique = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@NomBoutique", request.NomBoutique);
                AddParameter(cmd, "@EmailBoutique", request.EmailBoutique);
                AddParameter(cmd, "@TelephoneBoutique", request.TelephoneBoutique);
                AddParameter(cmd, "@AdresseBoutique", request.AdresseBoutique);
                AddParameter(cmd, "@LogoBoutique", request.LogoBoutique);
                AddParameter(cmd, "@DescriptionBoutique", request.DescriptionBoutique);
                AddParameter(cmd, "@PermitCommandeEnLigne", request.PermitCommandeEnLigne);
                AddParameter(cmd, "@PermitLivraison", request.PermitLivraison);
                AddParameter(cmd, "@PermitRetrait", request.PermitRetrait);
                cmd.Parameters.AddWithValue("@DateMiseAJour", DateTime.Now);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Boutique mise à jour: {id} par utilisateur {currentUserId}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour boutique {id}");
                return null;
            }
        }

        // ========================================
        // DELETE (SOFT - EstActif = 0, pas de colonne Etat sur BOUTIQUE)
        // ========================================

        public async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("");
                var currentUserId = GetUserIdFromContext();

                using var cmd = new SqlCommand($@"
                    UPDATE BOUTIQUE
                    SET EstActif = 0,
                        DateMiseAJour = @DateMiseAJour
                    WHERE IdBoutique = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@DateMiseAJour", DateTime.Now);
                AddEntrepriseParameter(cmd);

                var rows = await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Boutique désactivée: {id} par utilisateur {currentUserId}");

                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression boutique {id}");
                return false;
            }
        }

        // ========================================
        // TYPES DE BOUTIQUE (lookup, pas de filtrage multi-tenant - table globale)
        // ========================================

        public async Task<List<TypeBoutiqueDto>> GetTypesAsync()
        {
            var types = new List<TypeBoutiqueDto>();

            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    SELECT 
                        IdTypeBoutique, NomType, CodeType, Description, IconeType,
                        PermitCommandeParDefaut, PermitLivraisonParDefaut,
                        PermitRetraitParDefaut, PermitReservationParDefaut
                    FROM TYPE_BOUTIQUE
                    WHERE EstActif = 1
                    ORDER BY NomType", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    types.Add(new TypeBoutiqueDto
                    {
                        IdTypeBoutique = reader.GetGuid(reader.GetOrdinal("IdTypeBoutique")),
                        NomType = reader.GetString(reader.GetOrdinal("NomType")),
                        CodeType = reader.GetString(reader.GetOrdinal("CodeType")),
                        Description = ReadNullableString(reader, "Description"),
                        IconeType = ReadNullableString(reader, "IconeType"),
                        PermitCommandeParDefaut = reader.GetBoolean(reader.GetOrdinal("PermitCommandeParDefaut")),
                        PermitLivraisonParDefaut = reader.GetBoolean(reader.GetOrdinal("PermitLivraisonParDefaut")),
                        PermitRetraitParDefaut = reader.GetBoolean(reader.GetOrdinal("PermitRetraitParDefaut")),
                        PermitReservationParDefaut = reader.GetBoolean(reader.GetOrdinal("PermitReservationParDefaut"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types de boutique");
            }

            return types;
        }

        // ========================================
        // HELPERS PRIVÉS: Génération Slug / CodeQR
        // ========================================

        private async Task<string> GenerateUniqueSlugAsync(SqlConnection conn, string nomBoutique)
        {
            var baseSlug = nomBoutique
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "-");

            // Retirer accents/caractères spéciaux basiques
            baseSlug = System.Text.RegularExpressions.Regex.Replace(baseSlug, "[^a-z0-9-]", "");

            var slug = $"{baseSlug}-{DateTime.Now.Year}";
            var counter = 1;

            while (await SlugExistsAsync(conn, slug))
            {
                slug = $"{baseSlug}-{DateTime.Now.Year}-{counter}";
                counter++;
            }

            return slug;
        }

        private async Task<bool> SlugExistsAsync(SqlConnection conn, string slug)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM BOUTIQUE WHERE SlugBoutique = @Slug", conn);
            cmd.Parameters.AddWithValue("@Slug", slug);
            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        private async Task<string> GenerateUniqueCodeQRAsync(SqlConnection conn, string typeBoutique)
        {
            var prefix = typeBoutique switch
            {
                "Restaurant" => "RES",
                "Magasin" => "MAG",
                _ => "BOT"
            };

            string codeQR;
            do
            {
                var random = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
                codeQR = $"{prefix}{random}";
            }
            while (await CodeQRExistsAsync(conn, codeQR));

            return codeQR;
        }

        private async Task<bool> CodeQRExistsAsync(SqlConnection conn, string codeQR)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM BOUTIQUE WHERE CodeQR = @CodeQR", conn);
            cmd.Parameters.AddWithValue("@CodeQR", codeQR);
            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        // ========================================
        // HELPER: MAP FROM READER
        // ========================================

        private BoutiqueDto MapBoutiqueFromReader(SqlDataReader reader)
        {
            return new BoutiqueDto
            {
                IdBoutique = reader.GetGuid(reader.GetOrdinal("IdBoutique")),
                IdEntreprise = reader.GetGuid(reader.GetOrdinal("IdEntreprise")),
                IdTypeBoutique = reader.GetGuid(reader.GetOrdinal("IdTypeBoutique")),
                NomBoutique = reader.GetString(reader.GetOrdinal("NomBoutique")),
                SlugBoutique = reader.GetString(reader.GetOrdinal("SlugBoutique")),
                CodeQR = reader.GetString(reader.GetOrdinal("CodeQR")),
                EmailBoutique = ReadNullableString(reader, "EmailBoutique"),
                TelephoneBoutique = ReadNullableString(reader, "TelephoneBoutique"),
                AdresseBoutique = ReadNullableString(reader, "AdresseBoutique"),
                LogoBoutique = ReadNullableString(reader, "LogoBoutique"),
                DescriptionBoutique = ReadNullableString(reader, "DescriptionBoutique"),
                URLDomainCustom = ReadNullableString(reader, "URLDomainCustom"),
                TypeBoutique = reader.GetString(reader.GetOrdinal("TypeBoutique")),
                CodeType = ReadNullableString(reader, "CodeType"),
                IconeType = ReadNullableString(reader, "IconeType"),
                EstActif = reader.GetBoolean(reader.GetOrdinal("EstActif")),
                PermitCommandeEnLigne = reader.GetBoolean(reader.GetOrdinal("PermitCommandeEnLigne")),
                PermitAccesQR = reader.GetBoolean(reader.GetOrdinal("PermitAccesQR")),
                PermitLivraison = reader.GetBoolean(reader.GetOrdinal("PermitLivraison")),
                PermitRetrait = reader.GetBoolean(reader.GetOrdinal("PermitRetrait")),
                NombreArticles = ReadNullableInt(reader, "NombreArticles") ?? 0,
                NombreCategories = ReadNullableInt(reader, "NombreCategories") ?? 0,
                DateCreation = ReadNullableDateTime(reader, "DateCreation"),
                DateMiseAJour = ReadNullableDateTime(reader, "DateMiseAJour")
            };
        }

        // ========================================
        // HELPERS PAGINATION
        // ========================================

        private PaginationMetadata CreatePagination(int currentPage, int pageSize, int totalPages, int totalRecords)
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
