using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.Ecommerce
{
    public class Ecommerceservice : DatabaseService
    {
        public Ecommerceservice(
            string connectionString,
            ILogger<Ecommerceservice> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // LISTE DES COMMANDES (filtrable par boutique + état, paginée)
        // "Commandes en attente" côté POS = GetAsync(idBoutique, etat: "EnAttente")
        // ========================================

        public async Task<CommandeListResponse> GetAsync(
            Guid? idBoutique,
            string? etat,
            int page = 1,
            int pageSize = 20)
        {
            var result = new CommandeListResponse { Success = true };

            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                var filters = "";
                if (idBoutique.HasValue)
                    filters += " AND c.IdBoutique = @IdBoutique";
                if (!string.IsNullOrEmpty(etat))
                    filters += " AND c.Etat = @Etat";

                int totalRecords;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM COMMANDE_ECOMMERCE c
                    WHERE 1 = 1 {whereClause} {filters}", conn))
                {
                    AddEntrepriseParameter(cmdCount);
                    if (idBoutique.HasValue) cmdCount.Parameters.AddWithValue("@IdBoutique", idBoutique.Value);
                    if (!string.IsNullOrEmpty(etat)) cmdCount.Parameters.AddWithValue("@Etat", etat);

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
                        c.Id, c.IdEntreprise, c.IdBoutique, c.NumeroCommande, c.ClientWhatsApp,
                        c.LivraisonTexte, c.LivraisonLat, c.LivraisonLng,
                        c.SousTotal, c.Total, c.Etat,
                        c.NotificationBoutiqueEnvoyee, c.NotificationClientEnvoyee,
                        c.IdFacture, c.DateCommande, c.DateMiseAJour
                    FROM COMMANDE_ECOMMERCE c
                    WHERE 1 = 1 {whereClause} {filters}
                    ORDER BY c.DateCommande DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    if (idBoutique.HasValue) cmd.Parameters.AddWithValue("@IdBoutique", idBoutique.Value);
                    if (!string.IsNullOrEmpty(etat)) cmd.Parameters.AddWithValue("@Etat", etat);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        result.Commandes.Add(MapCommandeFromReader(reader));
                    }
                }

                result.Total = totalRecords;
                result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération commandes e-commerce");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // DÉTAIL D'UNE COMMANDE (avec lignes)
        // ========================================

        public async Task<CommandeDetailDto?> GetByIdAsync(Guid idCommande)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("c");

                CommandeDto? entete = null;

                using (var cmd = new SqlCommand($@"
                    SELECT 
                        c.Id, c.IdEntreprise, c.IdBoutique, c.NumeroCommande, c.ClientWhatsApp,
                        c.LivraisonTexte, c.LivraisonLat, c.LivraisonLng,
                        c.SousTotal, c.Total, c.Etat,
                        c.NotificationBoutiqueEnvoyee, c.NotificationClientEnvoyee,
                        c.IdFacture, c.DateCommande, c.DateMiseAJour
                    FROM COMMANDE_ECOMMERCE c
                    WHERE c.Id = @Id {whereClause}", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idCommande);
                    AddEntrepriseParameter(cmd);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                        entete = MapCommandeFromReader(reader);
                }

                if (entete == null) return null;

                var lignes = new List<DetailCommandeDto>();
                using (var cmd = new SqlCommand(@"
                        SELECT Id, IdArticle, Designation, PrixUnitaire, Quantite, PrixTotal
                        FROM DETAIL_TRANSACTIONS
                        WHERE IdCommandeEcommerce = @IdCommande
                          AND ISNULL(estSupprimer, 0) = 0", conn))
                {
                    cmd.Parameters.AddWithValue("@IdCommande", idCommande);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        lignes.Add(new DetailCommandeDto
                        {
                            Id = reader.GetGuid(0),
                            IdArticle = reader.GetGuid(1),
                            NomArticle = reader.GetString(2),
                            PrixUnitaire = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                            Quantite = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4), // decimal, plus int (voir DTO ci-dessous)
                            SousTotalLigne = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
                        });
                    }
                }

                return new CommandeDetailDto { Commande = entete, Lignes = lignes };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération commande {idCommande}");
                return null;
            }
        }

        // ========================================
        // CHANGEMENT D'ÉTAT (action POS: Confirmer / En préparation / Prête / Livrée / Annuler)
        // ========================================

        private static readonly string[] EtatsValides =
            { "EnAttente", "Confirmee", "EnPreparation", "Prete","EnLivraison" , "Livree", "Annulee", "Terminee" };

        public async Task<bool> UpdateEtatAsync(Guid idCommande, string nouvelEtat)
        {
            if (!EtatsValides.Contains(nouvelEtat))
            {
                _logger.LogWarning($"État invalide '{nouvelEtat}' pour commande {idCommande}");
                return false;
            }

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("");

                using var cmd = new SqlCommand($@"
                    UPDATE COMMANDE_ECOMMERCE
                    SET Etat = @Etat, DateMiseAJour = @DateMiseAJour
                    WHERE Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", idCommande);
                cmd.Parameters.AddWithValue("@Etat", nouvelEtat);
                cmd.Parameters.AddWithValue("@DateMiseAJour", DateTime.Now);
                AddEntrepriseParameter(cmd);

                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows > 0)
                    _logger.LogInformation($"✅ Commande {idCommande} → {nouvelEtat}");

                // 🔌 TODO (plus tard): notifier le groupe SignalR "boutique:{IdBoutique}"
                // du changement d'état ici, une fois le Hub temps réel mis en place.

                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur changement état commande {idCommande}");
                return false;
            }
        }

        // ========================================
        // HELPERS
        // ========================================

        private CommandeDto MapCommandeFromReader(SqlDataReader reader)
        {
            return new CommandeDto
            {
                Id = reader.GetGuid(0),
                IdEntreprise = reader.GetGuid(1),
                IdBoutique = reader.GetGuid(2),
                NumeroCommande = ReadNullableString(reader, "NumeroCommande"),
                ClientWhatsApp = reader.GetString(4),
                LivraisonTexte = ReadNullableString(reader, "LivraisonTexte"),
                LivraisonLat = ReadNullableDouble(reader, "LivraisonLat"),
                LivraisonLng = ReadNullableDouble(reader, "LivraisonLng"),
                SousTotal = reader.GetDecimal(8),
                Total = reader.GetDecimal(9),
                Etat = reader.GetString(10),
                NotificationBoutiqueEnvoyee = reader.GetBoolean(11),
                NotificationClientEnvoyee = reader.GetBoolean(12),
                IdFacture = ReadNullableGuid(reader, "IdFacture"),
                DateCommande = reader.GetDateTime(14),
                DateMiseAJour = ReadNullableDateTime(reader, "DateMiseAJour")
            };
        }

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
