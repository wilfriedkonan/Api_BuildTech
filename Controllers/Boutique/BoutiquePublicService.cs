using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Boutique
{
    /// <summary>
    /// Service PUBLIC pour BOUTIQUE (QR code / slug / catalogue).
    /// ⚠️ N'appelle JAMAIS BuildWhereClause()/AddEntrepriseParameter() ici :
    /// ces méthodes bloquent l'accès (AND 1=0) quand il n'y a pas de JWT,
    /// or les endpoints publics sont accessibles sans authentification.
    /// La sécurité vient plutôt de EstActif = 1 + résolution par CodeQR/Slug uniquement.
    /// </summary>
    public class BoutiquePublicService : DatabaseService
    {
        public BoutiquePublicService(
            string connectionString,
            ILogger<BoutiquePublicService> logger)
            : base(connectionString, logger, httpContextAccessor: null)
        {
        }

        // ========================================
        // RÉSOLUTION BOUTIQUE (QR / Slug / Type+Slug)
        // ========================================

        public async Task<BoutiqueDto?> GetByQRCodeAsync(string codeQR)
        {
            return await GetBoutiqueByFilterAsync("b.CodeQR = @Value", codeQR);
        }

        public async Task<BoutiqueDto?> GetBySlugAsync(string slug)
        {
            return await GetBoutiqueByFilterAsync("b.SlugBoutique = @Value", slug);
        }

        public async Task<BoutiqueDto?> GetByTypeAndSlugAsync(string typeBoutique, string slug)
        {
            return await GetBoutiqueByFilterAsync(
                "b.TypeBoutique = @Type AND b.SlugBoutique = @Value", slug, typeBoutique);
        }

        private async Task<BoutiqueDto?> GetBoutiqueByFilterAsync(string filterSql, string value, string? typeValue = null)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand($@"
                    SELECT TOP 1
                        b.IdBoutique, b.IdEntreprise, b.IdTypeBoutique, b.NomBoutique,
                        b.SlugBoutique, b.CodeQR, b.EmailBoutique, b.TelephoneBoutique,
                        b.AdresseBoutique, b.LogoBoutique, b.DescriptionBoutique,
                        b.URLDomainCustom, b.TypeBoutique, b.EstActif,
                        b.PermitCommandeEnLigne, b.PermitAccesQR, b.PermitLivraison, b.PermitRetrait,
                        b.NombreArticles, b.NombreCategories, b.DateCreation, b.DateMiseAJour,
                        tb.CodeType, tb.IconeType
                    FROM BOUTIQUE b
                    INNER JOIN TYPE_BOUTIQUE tb ON b.IdTypeBoutique = tb.IdTypeBoutique
                    WHERE {filterSql} AND b.EstActif = 1", conn);

                cmd.Parameters.AddWithValue("@Value", value);
                if (typeValue != null)
                    cmd.Parameters.AddWithValue("@Type", typeValue);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapBoutiqueFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur résolution boutique publique ({filterSql})");
            }

            return null;
        }

        // ========================================
        // CATALOGUE (CATHEGORIE + ARTICLES réels)
        // ========================================

        public async Task<CatalogueDto?> GetCatalogueAsync(Guid idBoutique, string mode = "shop")
        {
            try
            {
                var boutique = await GetBoutiqueByIdInternalAsync(idBoutique);
                if (boutique == null)
                    return null;

                // Colonnes réelles: Id (PK), Designation (nom), IdCathegorie (FK),
                // Etat (nvarchar 'Actif'), EstVisibleCatalogue (bit), PrixPromo (float)
                const string query = @"
                    SELECT 
                        c.Id, c.Designation, c.Description, c.ImageURL, c.Ordre,
                        a.Id, a.Designation, a.Description,
                        a.PrixVente, a.PrixPromo, a.EstPromo, a.ImageURL, a.EstEnStock
                    FROM CATHEGORIE c
                    LEFT JOIN ARTICLES a 
                        ON c.Id = a.IdCathegorie
                        AND a.Etat = 'Actif' AND a.EstVisibleCatalogue = 1
                    WHERE c.IdBoutique = @IdBoutique
                      AND c.Etat = 'Actif'
                      AND c.EstVisibleCatalogue = 1
                    ORDER BY c.Ordre, c.Designation, a.Designation";

                var categories = new Dictionary<Guid, CataloguePublicCategoryDto>();

                using var conn = await GetConnectionAsync();
                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@IdBoutique", idBoutique);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var categoryId = reader.GetGuid(0);

                    if (!categories.ContainsKey(categoryId))
                    {
                        categories[categoryId] = new CataloguePublicCategoryDto
                        {
                            IdCategorie = categoryId,
                            NomCategorie = reader.GetString(1),
                            // Description est char(128) → padding d'espaces à trimmer
                            Description = reader.IsDBNull(2) ? null : reader.GetString(2).TrimEnd(),
                            ImageURL = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Articles = new List<CataloguePublicArticleDto>()
                        };
                    }

                    if (!reader.IsDBNull(5)) // Id (article)
                    {
                        categories[categoryId].Articles.Add(new CataloguePublicArticleDto
                        {
                            IdArticle = reader.GetGuid(5),
                            NomArticle = reader.GetString(6),
                            DescriptionArticle = reader.IsDBNull(7) ? null : reader.GetString(7),
                            PrixVente = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                            // ⚠️ PrixPromo est FLOAT en DB → GetDouble() puis cast (GetDecimal plante sinon)
                            PrixPromo = reader.IsDBNull(9) ? null : (decimal?)Convert.ToDecimal(reader.GetDouble(9)),
                            EstPromo = !reader.IsDBNull(10) && reader.GetBoolean(10),
                            ImageURL = reader.IsDBNull(11) ? null : reader.GetString(11),
                            EstEnStock = !reader.IsDBNull(12) && reader.GetBoolean(12)
                        });
                    }
                }

                return new CatalogueDto
                {
                    IdBoutique = boutique.IdBoutique,
                    NomBoutique = boutique.NomBoutique,
                    LogoBoutique = boutique.LogoBoutique,
                    DescriptionBoutique = boutique.DescriptionBoutique,
                    TypeBoutique = boutique.TypeBoutique,
                    CodeType = boutique.CodeType,
                    IconeType = boutique.IconeType,
                    Mode = mode,
                    PermitCommande = mode == "shop" && boutique.PermitCommandeEnLigne,
                    Categories = categories.Values.ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération catalogue boutique {idBoutique}");
                return null;
            }
        }

        public async Task<BoutiqueDto?> GetBoutiqueByIdInternalAsync(Guid idBoutique)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    SELECT TOP 1
                        b.IdBoutique, b.IdEntreprise, b.IdTypeBoutique, b.NomBoutique,
                        b.SlugBoutique, b.CodeQR, b.EmailBoutique, b.TelephoneBoutique,
                        b.AdresseBoutique, b.LogoBoutique, b.DescriptionBoutique,
                        b.URLDomainCustom, b.TypeBoutique, b.EstActif,
                        b.PermitCommandeEnLigne, b.PermitAccesQR, b.PermitLivraison, b.PermitRetrait,
                        b.NombreArticles, b.NombreCategories, b.DateCreation, b.DateMiseAJour,
                        tb.CodeType, tb.IconeType
                    FROM BOUTIQUE b
                    INNER JOIN TYPE_BOUTIQUE tb ON b.IdTypeBoutique = tb.IdTypeBoutique
                    WHERE b.IdBoutique = @Id AND b.EstActif = 1", conn);

                cmd.Parameters.AddWithValue("@Id", idBoutique);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapBoutiqueFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération boutique {idBoutique}");
            }

            return null;
        }

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
    }
}
