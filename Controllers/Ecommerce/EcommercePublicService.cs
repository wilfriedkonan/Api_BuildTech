using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using static Api_BuildTech.Controllers.Boutique.BoutiquePublicController;

namespace Api_BuildTech.Controllers.Ecommerce
{
    public class EcommercePublicService : DatabaseService
    {
        public EcommercePublicService(
            string connectionString,
            ILogger<EcommercePublicService> logger)
            : base(connectionString, logger, httpContextAccessor: null)
        {
        }

        /// <summary>
        /// Crée l'entête + les lignes de la commande. Retourne l'Id créé, ou null en cas d'échec.
        /// </summary>
        public async Task<Guid?> CreerCommandeAsync(
            Guid idBoutique,
            Guid idEntreprise,
            string clientWhatsApp,
            string? livraisonTexte,
            double? livraisonLat,
            double? livraisonLng,
            decimal total,
            List<LigneCommandeRequest> lignes)
        {
            using var conn = await GetConnectionAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                var idCommande = Guid.NewGuid();
                var numeroCommande = await GenererNumeroCommandeAsync(conn, transaction, idBoutique);
                var sousTotal = lignes.Sum(l => l.PrixUnitaire * l.Quantite);

                using (var cmd = new SqlCommand(@"
                    INSERT INTO COMMANDE_ECOMMERCE (
                        Id, IdEntreprise, IdBoutique, NumeroCommande, ClientWhatsApp,
                        LivraisonTexte, LivraisonLat, LivraisonLng,
                        SousTotal, Total, Etat, DateCommande
                    )
                    VALUES (
                        @Id, @IdEntreprise, @IdBoutique, @NumeroCommande, @ClientWhatsApp,
                        @LivraisonTexte, @LivraisonLat, @LivraisonLng,
                        @SousTotal, @Total, 'EnAttente', @DateCommande
                    )", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", idCommande);
                    cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
                    cmd.Parameters.AddWithValue("@IdBoutique", idBoutique);
                    cmd.Parameters.AddWithValue("@NumeroCommande", numeroCommande);
                    cmd.Parameters.AddWithValue("@ClientWhatsApp", clientWhatsApp);
                    AddParameter(cmd, "@LivraisonTexte", livraisonTexte);
                    AddParameter(cmd, "@LivraisonLat", livraisonLat);
                    AddParameter(cmd, "@LivraisonLng", livraisonLng);
                    cmd.Parameters.AddWithValue("@SousTotal", sousTotal);
                    cmd.Parameters.AddWithValue("@Total", total);
                    cmd.Parameters.AddWithValue("@DateCommande", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }

                foreach (var ligne in lignes)
                {
                    using var cmdLigne = new SqlCommand(@"
                INSERT INTO DETAIL_TRANSACTIONS (
                    Id, IdCommandeEcommerce, IdArticle, IdEntreprise,
                    Designation, PrixUnitaire, PrixVente, Quantite, PrixTotal, sousTotal,
                    Etat, estSupprimer, EstExecuter, domaineAricle, Date, DateCreation
                )
                VALUES (
                    @Id, @IdCommandeEcommerce, @IdArticle, @IdEntreprise,
                    @Designation, @PrixUnitaire, @PrixVente, @Quantite, @PrixTotal, @SousTotal,
                    'Actif', 0, 0, 'ECOMMERCE', @Date, @DateCreation
                        )", conn, transaction);

                    var sousTotalLigne = ligne.PrixUnitaire * ligne.Quantite;

                    cmdLigne.Parameters.AddWithValue("@Id", Guid.NewGuid());
                    cmdLigne.Parameters.AddWithValue("@IdCommandeEcommerce", idCommande);
                    cmdLigne.Parameters.AddWithValue("@IdArticle", ligne.IdArticle);
                    cmdLigne.Parameters.AddWithValue("@IdEntreprise", idEntreprise);
                    cmdLigne.Parameters.AddWithValue("@Designation", ligne.NomArticle);
                    cmdLigne.Parameters.AddWithValue("@PrixUnitaire", ligne.PrixUnitaire);
                    cmdLigne.Parameters.AddWithValue("@PrixVente", ligne.PrixUnitaire);
                    cmdLigne.Parameters.AddWithValue("@Quantite", (decimal)ligne.Quantite); // Quantite est decimal(15,2) dans DETAIL_TRANSACTIONS
                    cmdLigne.Parameters.AddWithValue("@PrixTotal", sousTotalLigne);
                    cmdLigne.Parameters.AddWithValue("@SousTotal", sousTotalLigne);
                    cmdLigne.Parameters.AddWithValue("@Date", DateTime.Today);
                    cmdLigne.Parameters.AddWithValue("@DateCreation", DateTime.Now);

                    await cmdLigne.ExecuteNonQueryAsync();
                }
                transaction.Commit();

                _logger.LogInformation($"✅ Commande {numeroCommande} créée ({idCommande}) pour boutique {idBoutique}");

                return idCommande;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, $"❌ Erreur création commande pour boutique {idBoutique}");
                return null;
            }
        }

        /// <summary>
        /// Met à jour les flags de notification WhatsApp après tentative d'envoi.
        /// </summary>
        public async Task MettreAJourFlagsNotificationAsync(
            Guid idCommande, bool notifBoutiqueEnvoyee, bool notifClientEnvoyee)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE COMMANDE_ECOMMERCE
                    SET NotificationBoutiqueEnvoyee = @NotifBoutique,
                        NotificationClientEnvoyee = @NotifClient,
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", idCommande);
                cmd.Parameters.AddWithValue("@NotifBoutique", notifBoutiqueEnvoyee);
                cmd.Parameters.AddWithValue("@NotifClient", notifClientEnvoyee);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour flags notification commande {idCommande}");
            }
        }

        /// <summary>
        /// Génère un numéro lisible "CMD-000042" (séquentiel par boutique, non garanti
        /// sans collision sous forte concurrence mais suffisant pour l'affichage POS).
        /// </summary>
        private async Task<string> GenererNumeroCommandeAsync(SqlConnection conn, SqlTransaction transaction, Guid idBoutique)
        {
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM COMMANDE_ECOMMERCE WHERE IdBoutique = @IdBoutique",
                conn, transaction);
            cmd.Parameters.AddWithValue("@IdBoutique", idBoutique);

            var count = (int)await cmd.ExecuteScalarAsync();
            return $"CMD-{(count + 1):D6}";
        }
    }
}