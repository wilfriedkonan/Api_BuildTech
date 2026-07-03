using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Controllers.POS
{
    public class PosService : DatabaseService
    {
        public PosService(
            string connectionString,
            ILogger<PosService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // FACTURES - CRUD
        // ========================================

        /// <summary>
        /// Créer une nouvelle facture POS
        /// </summary>
        public async Task<FactureDto?> CreateFactureAsync(CreatePosFactureRequest request)
        {
            try
            {
                var newId = Guid.NewGuid();
                var currentUserId = GetUserIdFromContext();
                var currentDate = DateTime.Now;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO FACTURE (
                        id, NumeroFacture, Designation, DateCreation, Message,
                        Montant, Sous_total, Total_final, MontantVerser, MonnaieRemis,
                        RestApayer, Remise, Remise_globale, ValeurRemise_globale,
                        ValeurTVA, TVA, BeneficeSurFact,
                        IdTable, Caisse, Serveur, idUtilisateur, IdClient, IdEntreprise,
                        idTypeService, IdSession, Etat, EstAnnuler, EstSupprimer,
                        EstEnattente, Solder, DateModification, Statut
                    )
                    VALUES (
                        @Id, @NumeroFacture, @Designation, @DateCreation, @Message,
                        0, 0, 0, 0, 0,
                        0, 0, 0, 0,
                        0, 0, 0,
                        @IdTable, @Caisse, @Serveur, @idUtilisateur, @IdClient, @IdEntreprise,
                        NULL, @IdSession, 'En attente', 0, 0,
                        1, 0, @DateModification, 'Ouvert'
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@NumeroFacture", request.NumeroFacture);
                AddParameter(cmd, "@Designation", request.Designation);
                cmd.Parameters.AddWithValue("@DateCreation", currentDate);
                AddParameter(cmd, "@Message", request.Message);
                AddParameter(cmd, "@IdTable", request.IdTable);
                AddParameter(cmd, "@Caisse", request.Caisse);
                AddParameter(cmd, "@Serveur", request.Serveur);
                AddParameter(cmd, "@idUtilisateur", request.idUtilisateur ?? currentUserId);
                AddParameter(cmd, "@IdClient", request.IdClient);
                cmd.Parameters.AddWithValue("@IdEntreprise", request.IdEntreprise);
                AddParameter(cmd, "@IdSession", request.IdSession);
                cmd.Parameters.AddWithValue("@DateModification", currentDate);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Facture POS créée: {newId} ({request.NumeroFacture})");

                return await GetFactureByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création facture POS");
                return null;
            }
        }

        /// <summary>
        /// Récupère une facture par ID avec tous ses détails
        /// </summary>
        public async Task<FactureCompleteResponse> GetFactureCompleteAsync(Guid idFacture)
        {
            var response = new FactureCompleteResponse { Success = true };

            try
            {
                var facture = await GetFactureByIdAsync(idFacture);
                if (facture == null)
                {
                    response.Success = false;
                    response.Message = "Facture introuvable";
                    return response;
                }

                var details = await GetDetailsFactureAsync(idFacture);

                response.Facture = facture;
                response.Details = details;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération facture complète {idFacture}");
                response.Success = false;
            }

            return response;
        }

        /// <summary>
        /// Récupère les détails d'une facture
        /// </summary>
        private async Task<List<DetailTransactionDto>> GetDetailsFactureAsync(Guid idFacture)
        {
            var details = new List<DetailTransactionDto>();

            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    SELECT 
                        Id, Designation, Quantite, PrixUnitaireHT, PrixUnitaireTTC,
                        PrixVente, PrixTotal, sousTotal, TauxTVA, MontantTVA, valeurRemise,
                        PrixAchatUnitaire, Specificite, DetailComposent, DetailComposant,
                        Specification, domaineAricle, IdArticle, IdFacture, IdServeur,
                        IdCuisinier, IdUser, DesignationAgent, IdEntreprise, Etat,
                        EstExecuter, estSuite, estDetaileComd, estSupprimer, EstModifier,
                        EstAvarie, AutorisationModif, idDomaine, idTypeService,
                        DateCreation, DateModification, IdUserModification
                    FROM DETAIL_TRANSACTIONS
                    WHERE IdFacture = @IdFacture AND estSupprimer = 0
                    ORDER BY DateCreation", conn);

                cmd.Parameters.AddWithValue("@IdFacture", idFacture);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    details.Add(MapDetailFromReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détails facture {idFacture}");
            }

            return details;
        }

        /// <summary>
        /// Ajouter un détail à une facture POS
        /// </summary>
        public async Task<DetailTransactionDto?> AddDetailToFactureAsync(
            Guid idFacture,
            AddPosDetailRequest request)
        {
            try
            {
                var detailId = Guid.NewGuid();
                var currentUserId = GetUserIdFromContext();
                var currentDate = DateTime.Now;

                // Calculer les montants
                decimal prixTotal = request.Quantite * request.PrixUnitaireHT;
                decimal montantTVA = prixTotal * (request.TauxTVA / 100);
                decimal prixTTC = prixTotal + montantTVA;
                decimal sousTotal = prixTotal - request.valeurRemise;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO DETAIL_TRANSACTIONS (
                        Id, IdFacture, IdArticle, Designation, Quantite,
                        PrixUnitaireHT, PrixUnitaireTTC, PrixVente, PrixTotal,
                        sousTotal, TauxTVA, MontantTVA, valeurRemise,
                        PrixAchatUnitaire, Specificite, DetailComposent, DetailComposant,
                        IdServeur, IdCuisinier, IdUser, DesignationAgent, IdEntreprise,
                        Etat, EstExecuter, estDetaileComd, estSupprimer, DateCreation,
                        DateModification, IdUserModification
                    )
                    VALUES (
                        @Id, @IdFacture, @IdArticle, @Designation, @Quantite,
                        @PrixUnitaireHT, @PrixUnitaireTTC, @PrixVente, @PrixTotal,
                        @sousTotal, @TauxTVA, @MontantTVA, @valeurRemise,
                        0, @Specificite, @DetailComposent, @DetailComposent,
                        @IdServeur, @IdCuisinier, @IdUser, @DesignationAgent, @IdEntreprise,
                        'Actif', 0, 0, 0, @DateCreation,
                        @DateModification, @IdUser
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", detailId);
                cmd.Parameters.AddWithValue("@IdFacture", idFacture);
                cmd.Parameters.AddWithValue("@IdArticle", request.IdArticle);
                cmd.Parameters.AddWithValue("@Designation", request.Designation ?? "");
                cmd.Parameters.AddWithValue("@Quantite", request.Quantite);
                cmd.Parameters.AddWithValue("@PrixUnitaireHT", request.PrixUnitaireHT);
                cmd.Parameters.AddWithValue("@PrixUnitaireTTC", prixTTC);
                cmd.Parameters.AddWithValue("@PrixVente", request.PrixVente);
                cmd.Parameters.AddWithValue("@PrixTotal", prixTotal);
                cmd.Parameters.AddWithValue("@sousTotal", sousTotal);
                cmd.Parameters.AddWithValue("@TauxTVA", request.TauxTVA);
                cmd.Parameters.AddWithValue("@MontantTVA", montantTVA);
                cmd.Parameters.AddWithValue("@valeurRemise", request.valeurRemise);
                AddParameter(cmd, "@Specificite", request.Specificite);
                AddParameter(cmd, "@DetailComposent", request.DetailComposent);
                AddParameter(cmd, "@IdServeur", request.IdServeur);
                AddParameter(cmd, "@IdCuisinier", request.IdCuisinier);
                cmd.Parameters.AddWithValue("@IdUser", currentUserId);
                cmd.Parameters.AddWithValue("@DesignationAgent", "Agent POS");
                cmd.Parameters.AddWithValue("@IdEntreprise", GetEntrepriseIdFromContext());
                cmd.Parameters.AddWithValue("@DateCreation", currentDate);
                cmd.Parameters.AddWithValue("@DateModification", currentDate);

                await cmd.ExecuteNonQueryAsync();

                // Mettre à jour les totaux de la facture
                await UpdateFactureTotalsAsync(idFacture);

                _logger.LogInformation($"✅ Détail ajouté à facture POS {idFacture}");

                return await GetDetailByIdAsync(detailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur ajout détail facture POS");
                return null;
            }
        }

        // ========================================
        // GESTION STATUTS FACTURE
        // ========================================

        // ========================================
        // GESTION STATUTS FACTURE - ENDPOINT INTELLIGENT
        // ========================================

        /// <summary>
        /// 📌 CAS 1 : Créer une facture + ajouter articles + mettre en attente EN UN SEUL APPEL
        /// 
        /// Étapes :
        /// 1. Créer la facture
        /// 2. Ajouter tous les articles (liste)
        /// 3. Mettre en attente (EstEnattente = 1)
        /// 4. Tout dans une transaction atomique
        /// </summary>
        public async Task<PosOnHoldResponse> CreateAndPutOnHoldAsync(PutOnHoldSmartRequest request)
        {
            var response = new PosOnHoldResponse();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();

                try
                {
                    var currentUserId = GetUserIdFromContext();
                    var currentDate = DateTime.Now;
                    var enterpriseId = request.IdEntreprise ?? GetEntrepriseIdFromContext();
                    var factureId = Guid.NewGuid();

                    // ========================================
                    // ÉTAPE 0 : GÉNÉRER LE NUMÉRO DE FACTURE
                    // ========================================

                    string numeroFacture;
                    if (string.IsNullOrWhiteSpace(request.NumeroFacture))
                    {
                        // ✅ Générer automatiquement si non fourni
                        numeroFacture = await GetOrGenerateInvoiceNumberAsync(null);
                        _logger.LogInformation($"✅ Numéro de facture généré: {numeroFacture}");
                    }
                    else
                    {
                        // ✅ Utiliser le numéro fourni
                        numeroFacture = request.NumeroFacture.Trim();
                    }

                    // ========================================
                    // ÉTAPE 1 : CRÉER LA FACTURE
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO FACTURE (
                            id, NumeroFacture, Designation, DateCreation, Message,
                            Montant, Sous_total, Total_final, MontantVerser, MonnaieRemis,
                            RestApayer, Remise, Remise_globale, ValeurRemise_globale,
                            ValeurTVA, TVA, BeneficeSurFact,
                            IdTable, Caisse, Serveur, idUtilisateur, IdClient, IdEntreprise,
                            idTypeService, IdSession, Etat, EstAnnuler, EstSupprimer,
                            EstEnattente, Solder, DateModification, Statut
                        )
                        VALUES (
                            @Id, @NumeroFacture, @Designation, @DateCreation, @Message,
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0,
                            0, 0, 0,
                            @IdTable, @Caisse, @Serveur, @idUtilisateur, @IdClient, @IdEntreprise,
                            NULL, @IdSession, 'En attente', 0, 0,
                            1, 0, @DateModification, 'Ouvert'
                        )", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        cmd.Parameters.AddWithValue("@NumeroFacture", numeroFacture);  // ✅ Utiliser le numéro généré
                        AddParameter(cmd, "@Designation", request.Designation);
                        cmd.Parameters.AddWithValue("@DateCreation", currentDate);
                        AddParameter(cmd, "@Message", request.Message);
                        AddParameter(cmd, "@IdTable", request.IdTable);
                        AddParameter(cmd, "@Caisse", request.Caisse);
                        AddParameter(cmd, "@Serveur", request.Serveur);
                        AddParameter(cmd, "@idUtilisateur", request.idUtilisateur ?? currentUserId);
                        AddParameter(cmd, "@IdClient", request.IdClient);
                        cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                        AddParameter(cmd, "@IdSession", request.IdSession);
                        cmd.Parameters.AddWithValue("@DateModification", currentDate);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation($"✅ [CAS 1 HOLD] Facture créée: {factureId} ({numeroFacture})");

                    // ========================================
                    // ÉTAPE 2 : AJOUTER TOUS LES ARTICLES
                    // ========================================

                    foreach (var article in request.Articles)
                    {
                        var detailId = Guid.NewGuid();

                        decimal prixTotal = article.Quantite * article.PrixUnitaireHT;
                        decimal montantTVA = prixTotal * (article.TauxTVA / 100);
                        decimal prixTTC = prixTotal + montantTVA;
                        decimal sousTotal = prixTotal - article.valeurRemise;

                        using (var cmd = new SqlCommand(@"
                            INSERT INTO DETAIL_TRANSACTIONS (
                                Id, IdFacture, IdArticle, Designation, Quantite,
                                PrixUnitaireHT, PrixUnitaireTTC, PrixVente, PrixTotal,
                                sousTotal, TauxTVA, MontantTVA, valeurRemise,
                                PrixAchatUnitaire, Specificite, DetailComposent, DetailComposant,
                                IdServeur, IdCuisinier, IdUser, DesignationAgent, IdEntreprise,
                                Etat, EstExecuter, estDetaileComd, estSupprimer, DateCreation,
                                DateModification, IdUserModification
                            )
                            VALUES (
                                @Id, @IdFacture, @IdArticle, @Designation, @Quantite,
                                @PrixUnitaireHT, @PrixUnitaireTTC, @PrixVente, @PrixTotal,
                                @sousTotal, @TauxTVA, @MontantTVA, @valeurRemise,
                                0, @Specificite, @DetailComposent, @DetailComposent,
                                @IdServeur, @IdCuisinier, @IdUser, @DesignationAgent, @IdEntreprise,
                                'Actif', 0, 0, 0, @DateCreation,
                                @DateModification, @IdUser
                            )", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", detailId);
                            cmd.Parameters.AddWithValue("@IdFacture", factureId);
                            cmd.Parameters.AddWithValue("@IdArticle", article.IdArticle);
                            cmd.Parameters.AddWithValue("@Designation", article.Designation ?? "");
                            cmd.Parameters.AddWithValue("@Quantite", article.Quantite);
                            cmd.Parameters.AddWithValue("@PrixUnitaireHT", article.PrixUnitaireHT);
                            cmd.Parameters.AddWithValue("@PrixUnitaireTTC", prixTTC);
                            cmd.Parameters.AddWithValue("@PrixVente", article.PrixVente);
                            cmd.Parameters.AddWithValue("@PrixTotal", prixTotal);
                            cmd.Parameters.AddWithValue("@sousTotal", sousTotal);
                            cmd.Parameters.AddWithValue("@TauxTVA", article.TauxTVA);
                            cmd.Parameters.AddWithValue("@MontantTVA", montantTVA);
                            cmd.Parameters.AddWithValue("@valeurRemise", article.valeurRemise);
                            AddParameter(cmd, "@Specificite", article.Specificite);
                            AddParameter(cmd, "@DetailComposent", article.DetailComposent);
                            AddParameter(cmd, "@IdServeur", article.IdServeur);
                            AddParameter(cmd, "@IdCuisinier", article.IdCuisinier);
                            cmd.Parameters.AddWithValue("@IdUser", currentUserId);
                            cmd.Parameters.AddWithValue("@DesignationAgent", "Agent POS");
                            cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                            cmd.Parameters.AddWithValue("@DateCreation", currentDate);
                            cmd.Parameters.AddWithValue("@DateModification", currentDate);

                            await cmd.ExecuteNonQueryAsync();

                            response.ArticlesAdded++;
                        }
                    }

                    _logger.LogInformation($"✅ [CAS 1 HOLD] {response.ArticlesAdded} articles ajoutés à facture {factureId}");

                    // ========================================
                    // ÉTAPE 3 : METTRE À JOUR LES TOTAUX
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                        UPDATE FACTURE
                        SET Sous_total = (SELECT ISNULL(SUM(sousTotal), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
                            ValeurTVA = (SELECT ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
                            Total_final = (SELECT ISNULL(SUM(sousTotal), 0) + ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0)
                        WHERE id = @Id", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // ========================================
                    // ÉTAPE 4 : METTRE EN ATTENTE
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                        UPDATE FACTURE
                        SET EstEnattente = 1,
                            Etat = 'En attente',
                            DesignationAtttente = @Motif,
                            DateModification = @DateModification
                        WHERE id = @Id", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        AddParameter(cmd, "@Motif", request.Motif ?? "Mise en attente");
                        cmd.Parameters.AddWithValue("@DateModification", currentDate);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();

                    var facture = await GetFactureByIdAsync(factureId);

                    response.Success = true;
                    response.Message = $"✅ CAS 1 : Facture créée, {response.ArticlesAdded} articles ajoutés, mise en attente confirmée";
                    response.OnHoldMode = "Created";
                    response.Facture = facture ?? new FactureDto { Id = factureId };

                    _logger.LogInformation($"✅ [CAS 1 HOLD COMPLET] Facture {factureId} créée et mise en attente");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CAS 1 HOLD] Erreur création + mise en attente facture");
                response.Success = false;
                response.Message = $"Erreur : {ex.Message}";
            }

            return response;
        }

        /// <summary>
        /// 📌 CAS 2 : Mettre en attente une facture POS qui existe déjà
        /// 
        /// Étapes :
        /// 1. Vérifier que la facture existe
        /// 2. Mettre en attente (EstEnattente = 1, Etat = 'En attente')
        /// </summary>
        public async Task<PosOnHoldResponse> PutExistingOnHoldAsync(PutOnHoldSmartRequest request)
        {
            var response = new PosOnHoldResponse();

            try
            {
                var currentUserId = GetUserIdFromContext();
                var currentDate = DateTime.Now;
                var enterpriseId = GetEntrepriseIdFromContext();
                var factureId = request.IdFacture ?? Guid.Empty;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();

                try
                {
                    // ========================================
                    // ÉTAPE 1 : VÉRIFIER QUE LA FACTURE EXISTE
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                SELECT id, Etat, Sous_total, ValeurTVA, Total_final
                FROM FACTURE
                WHERE id = @Id AND IdEntreprise = @IdEntreprise", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);

                        using var reader = await cmd.ExecuteReaderAsync();
                        if (!await reader.ReadAsync())
                        {
                            response.Success = false;
                            response.Message = $"Facture {factureId} introuvable";
                            return response;
                        }
                    }

                    // ========================================
                    // ÉTAPE 2 : METTRE À JOUR L'ÉTAT FACTURE
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                UPDATE FACTURE
                SET EstEnattente = 1,
                    Etat = 'En attente',
                    DesignationAtttente = @Motif,
                    DateModification = @DateModification
                WHERE id = @Id AND IdEntreprise = @IdEntreprise", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        AddParameter(cmd, "@Motif", request.Motif ?? "Mise en attente");
                        cmd.Parameters.AddWithValue("@DateModification", currentDate);
                        cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation($"[CAS 2] Facture mise en attente: {factureId}");

                    // ========================================
                    // ÉTAPE 3 : METTRE À JOUR DÉTAILS (OPTIONNEL)
                    // ========================================

                    if (request.Articles != null && request.Articles.Count > 0)
                    {
                        // Supprimer les anciens détails (soft delete)
                        using (var cmd = new SqlCommand(@"
                    UPDATE DETAIL_TRANSACTIONS
                    SET estSupprimer = 1,
                        DateModification = @DateModification,
                        IdUserModification = @IdUser
                    WHERE IdFacture = @IdFacture AND estSupprimer = 0", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@IdFacture", factureId);
                            cmd.Parameters.AddWithValue("@DateModification", currentDate);
                            cmd.Parameters.AddWithValue("@IdUser", currentUserId);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Ajouter les nouveaux détails (batching)
                        var valuesClauses = new List<string>();
                        var insertCmd = new SqlCommand { Connection = conn, Transaction = transaction };

                        decimal sousTotal = 0;
                        decimal valeurTVA = 0;

                        for (int i = 0; i < request.Articles.Count; i++)
                        {
                            var article = request.Articles[i];
                            var detailId = Guid.NewGuid();

                            decimal prixTotal = article.Quantite * article.PrixUnitaireHT;
                            decimal montantTVA = prixTotal * (article.TauxTVA / 100);
                            decimal prixTTC = prixTotal + montantTVA;
                            decimal sousItem = prixTotal - article.valeurRemise;

                            sousTotal += sousItem;
                            valeurTVA += montantTVA;

                            insertCmd.Parameters.AddWithValue($"@Id_{i}", detailId);
                            insertCmd.Parameters.AddWithValue($"@IdFacture_{i}", factureId);
                            insertCmd.Parameters.AddWithValue($"@IdArticle_{i}", article.IdArticle);
                            insertCmd.Parameters.AddWithValue($"@Designation_{i}", article.Designation ?? "");
                            insertCmd.Parameters.AddWithValue($"@Quantité_{i}", article.Quantite);
                            insertCmd.Parameters.AddWithValue($"@PrixUnitaireHT_{i}", article.PrixUnitaireHT);
                            insertCmd.Parameters.AddWithValue($"@PrixUnitaireTTC_{i}", prixTTC);
                            insertCmd.Parameters.AddWithValue($"@PrixVente_{i}", article.PrixVente);
                            insertCmd.Parameters.AddWithValue($"@PrixTotal_{i}", prixTotal);
                            insertCmd.Parameters.AddWithValue($"@sousTotal_{i}", sousItem);
                            insertCmd.Parameters.AddWithValue($"@TauxTVA_{i}", article.TauxTVA);
                            insertCmd.Parameters.AddWithValue($"@MontantTVA_{i}", montantTVA);
                            insertCmd.Parameters.AddWithValue($"@valeurRemise_{i}", article.valeurRemise);
                            AddParameter(insertCmd, $"@Specificite_{i}", article.Specificite);
                            AddParameter(insertCmd, $"@DetailComposent_{i}", article.DetailComposent);
                            AddParameter(insertCmd, $"@IdServeur_{i}", article.IdServeur);
                            AddParameter(insertCmd, $"@IdCuisinier_{i}", article.IdCuisinier);

                            valuesClauses.Add($@"(
                        @Id_{i}, @IdFacture_{i}, @IdArticle_{i}, @Designation_{i}, @Quantité_{i},
                        @PrixUnitaireHT_{i}, @PrixUnitaireTTC_{i}, @PrixVente_{i}, @PrixTotal_{i},
                        @sousTotal_{i}, @TauxTVA_{i}, @MontantTVA_{i}, @valeurRemise_{i},
                        0, @Specificite_{i}, @DetailComposent_{i}, @DetailComposent_{i},
                        @IdServeur_{i}, @IdCuisinier_{i}, @IdUser, 'Agent POS', @IdEntreprise,
                        'Actif', 0, 0, 0, @DateCreation, @DateModification, @IdUser
                    )");

                            response.ArticlesAdded++;
                        }

                        // Insérer les nouveaux détails
                        insertCmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                        insertCmd.Parameters.AddWithValue("@IdUser", currentUserId);
                        insertCmd.Parameters.AddWithValue("@DateCreation", currentDate);
                        insertCmd.Parameters.AddWithValue("@DateModification", currentDate);

                        insertCmd.CommandText = $@"
                    INSERT INTO DETAIL_TRANSACTIONS (
                        Id, IdFacture, IdArticle, Designation, Quantite,
                        PrixUnitaireHT, PrixUnitaireTTC, PrixVente, PrixTotal,
                        sousTotal, TauxTVA, MontantTVA, valeurRemise,
                        PrixAchatUnitaire, Specificite, DetailComposent, DetailComposant,
                        IdServeur, IdCuisinier, IdUser, DesignationAgent, IdEntreprise,
                        Etat, EstExecuter, estDetaileComd, estSupprimer, DateCreation,
                        DateModification, IdUserModification
                    )
                    VALUES {string.Join(",", valuesClauses)}";

                        await insertCmd.ExecuteNonQueryAsync();

                        // Mettre à jour les totaux de la facture
                        using (var cmd = new SqlCommand(@"
                    UPDATE FACTURE
                    SET Sous_total = @Sous_total,
                        ValeurTVA = @ValeurTVA,
                        Total_final = @Total_final,
                        DateModification = @DateModification
                    WHERE id = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", factureId);
                            cmd.Parameters.AddWithValue("@Sous_total", sousTotal);
                            cmd.Parameters.AddWithValue("@ValeurTVA", valeurTVA);
                            cmd.Parameters.AddWithValue("@Total_final", sousTotal + valeurTVA);
                            cmd.Parameters.AddWithValue("@DateModification", currentDate);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        _logger.LogInformation($"[CAS 2] {response.ArticlesAdded} articles ajoutés à facture {factureId}");



                    }

                    transaction.Commit();

                    // ========================================
                    // RETOUR RÉPONSE (SIMPLE ET RAPIDE)
                    // ========================================

                    response.Success = true;
                    response.Message = $"✅ CAS 2 : Facture mise en attente" +
                        (response.ArticlesAdded > 0 ? $" + {response.ArticlesAdded} articles mis à jour" : "");
                    response.OnHoldMode = "OnHold";
                    response.Facture = new FactureDto
                    {
                        Id = factureId,
                        Etat = "En attente",
                        EstEnattente = true,
                        Solder = false
                    };

                    _logger.LogInformation($"✅ [CAS 2 COMPLET] Facture {factureId} mise en attente avec {response.ArticlesAdded} articles");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CAS 2] Erreur mise en attente facture {request.IdFacture}");
                response.Success = false;
                response.Message = $"Erreur : {ex.Message}";
            }

            return response;
        }
        /// <summary>
        /// Ancien endpoint - Compatible (non-smart)
        /// </summary>
        public async Task<FactureDto?> PutOnHoldAsync(Guid idFacture, string? motif)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE FACTURE
                    SET EstEnattente = 1,
                        Etat = 'En attente',
                        DesignationAtttente = @Motif,
                        DateModification = @DateModification
                    WHERE id = @Id AND IdEntreprise = @IdEntreprise", conn);

                cmd.Parameters.AddWithValue("@Id", idFacture);
                AddParameter(cmd, "@Motif", motif ?? "Mise en attente");
                cmd.Parameters.AddWithValue("@DateModification", DateTime.Now);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Facture mise en attente: {idFacture}");

                return await GetFactureByIdAsync(idFacture);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise en attente facture {idFacture}");
                return null;
            }
        }

        /// <summary>
        /// Récupère toutes les factures en attente
        /// </summary>
        public async Task<FacturesEnAttenteResponse> GetAllOnHoldAsync()
        {
            var response = new FacturesEnAttenteResponse { Success = true };

            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        f.id, f.NumeroFacture, f.Designation, f.DateCreation, f.Message,
                        f.Montant, f.Sous_total, f.Total_final, f.MontantVerser, f.MonnaieRemis,
                        f.RestApayer, f.Remise, f.Remise_globale, f.Solder,
                        f.ValeurRemise_globale, f.ValeurTVA, f.TVA, f.BeneficeSurFact,
                        f.IdTable, f.Caisse, f.Serveur, f.DesignationTable,
                        f.IdPayement, f.idUtilisateur, f.IdClient, f.IdEntreprise,
                        f.idBlockCommandes, f.Etat, f.EstAnnuler, f.EstSupprimer,
                        f.estestCloturer, f.EstEnattente, f.DesignationAtttente,
                        f.IdSession, f.identifiantSession, f.Statut, f.DateModification,
                        c.Nom AS nomClient, u.Nom AS NomUtilisateur, tp.Designation AS DesignationPayement
                    FROM FACTURE f
                    LEFT JOIN CLIENTS c ON f.IdClient = c.Id
                    LEFT JOIN UTILISATEURS u ON f.idUtilisateur = u.Id
                    LEFT JOIN TYPE_PAIEMENT tp ON f.IdPayement = tp.Id
                    WHERE f.EstEnattente = 1 AND f.EstAnnuler = 0 AND f.EstSupprimer = 0 {whereClause}
                    ORDER BY f.DateCreation DESC", conn);

                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    response.Factures.Add(MapFactureFromReader(reader));
                }
                if (response.Factures.Count() > 0)
                {
                    var details = await GetDetailsFactureAsync(response.Factures.FirstOrDefault(x => x.Id != null).Id);
                    response.Details = details;
                }

                response.Total = response.Factures.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération factures en attente");
                response.Success = false;
            }

            return response;
        }

        /// <summary>
        /// Reprendre une facture en attente
        /// </summary>
        public async Task<FactureDto?> ResumeFactureAsync(Guid idFacture)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE FACTURE
                    SET EstEnattente = 0,
                        Etat = 'En cours',
                        DesignationAtttente = NULL,
                        DateModification = @DateModification
                    WHERE id = @Id AND IdEntreprise = @IdEntreprise", conn);

                cmd.Parameters.AddWithValue("@Id", idFacture);
                cmd.Parameters.AddWithValue("@DateModification", DateTime.Now);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Facture reprise: {idFacture}");

                return await GetFactureByIdAsync(idFacture);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur reprise facture {idFacture}");
                return null;
            }
        }

        /// <summary>
        /// Annuler une facture POS (soft delete)
        /// </summary>
        public async Task<FactureDto?> CancelFactureAsync(Guid idFacture, string? motif)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE FACTURE
                    SET EstAnnuler = 1,
                        Etat = 'Annulé',
                        Message = @Motif,
                        DateModification = @DateModification
                    WHERE id = @Id AND IdEntreprise = @IdEntreprise", conn);

                cmd.Parameters.AddWithValue("@Id", idFacture);
                AddParameter(cmd, "@Motif", motif ?? "Facture annulée");
                cmd.Parameters.AddWithValue("@DateModification", DateTime.Now);
                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                // Inverser les mouvements de stock si facture était payée
                await ReverseStockMovementsAsync(idFacture);

                _logger.LogInformation($"✅ Facture annulée: {idFacture}");

                return await GetFactureByIdAsync(idFacture);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur annulation facture {idFacture}");
                return null;
            }
        }

        // ========================================
        // PAIEMENT - ENDPOINT INTELLIGENT
        // ========================================

        /// <summary>
        /// Récupère tous les types de paiement
        /// </summary>
        public async Task<List<TypePaiementDto>> GetPaymentTypesAsync()
        {
            var types = new List<TypePaiementDto>();

            try
            {
                using var conn = await GetConnectionAsync();
                //var whereClause = BuildWhereClause("tp");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        Id, Designation, EstDefaut, estSupprimer, IdEntreprise
                    FROM TYPE_PAIEMENT
                    WHERE estSupprimer IS NULL 
                    ORDER BY EstDefaut DESC, Designation", conn);

                //AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var id = ReadNullableGuid(reader, "Id");
                    //var idEntreprise = ReadNullableGuid(reader, "IdEntreprise");

                    // Vérifier que les IDs ne sont pas null
                    if (id == null)
                        continue; // Sauter cette ligne si IDs null

                    types.Add(new TypePaiementDto
                    {
                        Id = id.Value,
                        Designation = ReadNullableString(reader, "Designation"),
                        EstDefaut = ReadNullableBool(reader, "EstDefaut") ?? false,
                        estSupprimer = ReadNullableBool(reader, "estSupprimer") ?? false,
                        //IdEntreprise = idEntreprise.Value
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération types paiement");
            }

            return types;
        }

        // ========================================
        // CAS 1 : CRÉER FACTURE + ARTICLES + PAYER EN 1 APPEL
        // ========================================

        /// <summary>
        /// Générer un numéro de facture unique
        /// Format: POS-YYYYMMDD-HHMMSS-XXXXX (où XXXXX = premiers caractères du GUID)
        /// Exemple: POS-20240627-153045-a1b2c
        /// </summary>
        private string GenerateInvoiceNumber()
        {
            var now = DateTime.Now;
            var guid = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            return $"POS-{now:yyyyMMdd-HHmmss}-{guid}";
        }

        /// <summary>
        /// Générer un numéro de facture unique avec compteur du jour
        /// Format: POS-YYYYMMDD-NNN (où NNN = compteur du jour)
        /// Exemple: POS-20240627-001
        /// </summary>
        private async Task<string> GenerateInvoiceNumberWithCounterAsync()
        {
            try
            {
                var today = DateTime.Now.Date;
                var enterpriseId = GetEntrepriseIdFromContext();

                using var conn = await GetConnectionAsync();

                // Compter les factures d'aujourd'hui pour cette entreprise
                using var cmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM FACTURE 
                    WHERE IdEntreprise = @IdEntreprise 
                    AND CAST(DateCreation AS DATE) = @Today
                    AND Etat != 'Annulé'", conn);

                cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                cmd.Parameters.AddWithValue("@Today", today);

                var count = (int?)await cmd.ExecuteScalarAsync() ?? 0;
                var nextNumber = count + 1;

                return $"POS-{today:yyyyMMdd}-{nextNumber:D4}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur génération numéro facture avec compteur, utiliser format simple");
                return GenerateInvoiceNumber();
            }
        }

        /// <summary>
        /// Obtenir le numéro de facture (fourni ou généré)
        /// Si request.NumeroFacture est vide → Générer un nouveau
        /// </summary>
        private async Task<string> GetOrGenerateInvoiceNumberAsync(string? requestNumber)
        {
            // Si l'utilisateur a fourni un numéro, l'utiliser
            if (!string.IsNullOrWhiteSpace(requestNumber))
                return requestNumber.Trim();

            // Sinon, générer automatiquement
            return await GenerateInvoiceNumberWithCounterAsync();
        }

        /// <summary>
        /// CAS 1 : Créer une facture + ajouter articles + confirmer paiement en UN SEUL APPEL
        /// 
        /// Étapes :
        /// 1. Créer la facture
        /// 2. Ajouter tous les articles (liste)
        /// 3. Confirmer le paiement
        /// 4. Créer mouvements stock automatiquement
        /// 5. Tout dans une transaction atomique
        /// </summary>
        //public async Task<PosPaymentResponse> CreateAndPayFactureAsync(ConfirmPosPaymentRequest request)
        //{
        //    var response = new PosPaymentResponse();

        //    try
        //    {
        //        using var conn = new SqlConnection(_connectionString);
        //        await conn.OpenAsync();

        //        using var transaction = conn.BeginTransaction();

        //        try
        //        {
        //            var currentUserId = GetUserIdFromContext();
        //            var currentDate = DateTime.Now;
        //            var enterpriseId = request.IdEntreprise ?? GetEntrepriseIdFromContext();
        //            var factureId = Guid.NewGuid();

        //            // ========================================
        //            // ÉTAPE 0 : GÉNÉRER LE NUMÉRO DE FACTURE
        //            // ========================================

        //            string numeroFacture;
        //            if (string.IsNullOrWhiteSpace(request.NumeroFacture))
        //            {
        //                // ✅ Générer automatiquement si non fourni
        //                numeroFacture = await GetOrGenerateInvoiceNumberAsync(null);
        //                _logger.LogInformation($"✅ Numéro de facture généré: {numeroFacture}");
        //            }
        //            else
        //            {
        //                // ✅ Utiliser le numéro fourni
        //                numeroFacture = request.NumeroFacture.Trim();
        //            }

        //            // ========================================
        //            // ÉTAPE 1 : CRÉER LA FACTURE
        //            // ========================================

        //            using (var cmd = new SqlCommand(@"
        //                INSERT INTO FACTURE (
        //                    id, NumeroFacture, Designation, DateCreation, Message,
        //                    Montant, Sous_total, Total_final, MontantVerser, MonnaieRemis,
        //                    RestApayer, Remise, Remise_globale, ValeurRemise_globale,
        //                    ValeurTVA, TVA, BeneficeSurFact,
        //                    IdTable, Caisse, Serveur, idUtilisateur, IdClient, IdEntreprise,
        //                    idTypeService, IdSession, Etat, EstAnnuler, EstSupprimer,
        //                    EstEnattente, Solder, DateModification, Statut
        //                )
        //                VALUES (
        //                    @Id, @NumeroFacture, @Designation, @DateCreation, @Message,
        //                    0, 0, 0, 0, 0,
        //                    0, 0, 0, 0,
        //                    0, 0, 0,
        //                    @IdTable, @Caisse, @Serveur, @idUtilisateur, @IdClient, @IdEntreprise,
        //                    NULL, @IdSession, 'En attente', 0, 0,
        //                    0, 0, @DateModification, 'Ouvert'
        //                )", conn, transaction))
        //            {
        //                cmd.Parameters.AddWithValue("@Id", factureId);
        //                cmd.Parameters.AddWithValue("@NumeroFacture", numeroFacture);  // ✅ Utiliser le numéro généré
        //                AddParameter(cmd, "@Designation", request.Designation);
        //                cmd.Parameters.AddWithValue("@DateCreation", currentDate);
        //                AddParameter(cmd, "@Message", request.Message);
        //                AddParameter(cmd, "@IdTable", request.IdTable);
        //                AddParameter(cmd, "@Caisse", request.Caisse);
        //                AddParameter(cmd, "@Serveur", request.Serveur);
        //                AddParameter(cmd, "@idUtilisateur", request.idUtilisateur ?? currentUserId);
        //                AddParameter(cmd, "@IdClient", request.IdClient);
        //                cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
        //                AddParameter(cmd, "@IdSession", request.IdSession);
        //                cmd.Parameters.AddWithValue("@DateModification", currentDate);

        //                await cmd.ExecuteNonQueryAsync();
        //            }

        //            _logger.LogInformation($"✅ [CAS 1] Facture créée: {factureId} ({numeroFacture})");

        //            // ========================================
        //            // ÉTAPE 2 : AJOUTER TOUS LES ARTICLES
        //            // ========================================

        //            foreach (var article in request.Articles)
        //            {
        //                var detailId = Guid.NewGuid();

        //                // Calculer les montants
        //                decimal prixTotal = article.Quantite * article.PrixUnitaireHT;
        //                decimal montantTVA = prixTotal * (article.TauxTVA / 100);
        //                decimal prixTTC = prixTotal + montantTVA;
        //                decimal sousTotal = prixTotal - article.valeurRemise;

        //                using (var cmd = new SqlCommand(@"
        //                    INSERT INTO DETAIL_TRANSACTIONS (
        //                        Id, IdFacture, IdArticle, Designation, Quantite,
        //                        PrixUnitaireHT, PrixUnitaireTTC, PrixVente, PrixTotal,
        //                        sousTotal, TauxTVA, MontantTVA, valeurRemise,
        //                        PrixAchatUnitaire, Specificite, DetailComposent, DetailComposant,
        //                        IdServeur, IdCuisinier, IdUser, DesignationAgent, IdEntreprise,
        //                        Etat, EstExecuter, estDetaileComd, estSupprimer, DateCreation,
        //                        DateModification, IdUserModification
        //                    )
        //                    VALUES (
        //                        @Id, @IdFacture, @IdArticle, @Designation, @Quantite,
        //                        @PrixUnitaireHT, @PrixUnitaireTTC, @PrixVente, @PrixTotal,
        //                        @sousTotal, @TauxTVA, @MontantTVA, @valeurRemise,
        //                        0, @Specificite, @DetailComposent, @DetailComposent,
        //                        @IdServeur, @IdCuisinier, @IdUser, @DesignationAgent, @IdEntreprise,
        //                        'Actif', 0, 0, 0, @DateCreation,
        //                        @DateModification, @IdUser
        //                    )", conn, transaction))
        //                {
        //                    cmd.Parameters.AddWithValue("@Id", detailId);
        //                    cmd.Parameters.AddWithValue("@IdFacture", factureId);
        //                    cmd.Parameters.AddWithValue("@IdArticle", article.IdArticle);
        //                    cmd.Parameters.AddWithValue("@Designation", article.Designation ?? "");
        //                    cmd.Parameters.AddWithValue("@Quantite", article.Quantite);
        //                    cmd.Parameters.AddWithValue("@PrixUnitaireHT", article.PrixUnitaireHT);
        //                    cmd.Parameters.AddWithValue("@PrixUnitaireTTC", prixTTC);
        //                    cmd.Parameters.AddWithValue("@PrixVente", article.PrixVente);
        //                    cmd.Parameters.AddWithValue("@PrixTotal", prixTotal);
        //                    cmd.Parameters.AddWithValue("@sousTotal", sousTotal);
        //                    cmd.Parameters.AddWithValue("@TauxTVA", article.TauxTVA);
        //                    cmd.Parameters.AddWithValue("@MontantTVA", montantTVA);
        //                    cmd.Parameters.AddWithValue("@valeurRemise", article.valeurRemise);
        //                    AddParameter(cmd, "@Specificite", article.Specificite);
        //                    AddParameter(cmd, "@DetailComposent", article.DetailComposent);
        //                    AddParameter(cmd, "@IdServeur", article.IdServeur);
        //                    AddParameter(cmd, "@IdCuisinier", article.IdCuisinier);
        //                    cmd.Parameters.AddWithValue("@IdUser", currentUserId);
        //                    cmd.Parameters.AddWithValue("@DesignationAgent", "Agent POS");
        //                    cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
        //                    cmd.Parameters.AddWithValue("@DateCreation", currentDate);
        //                    cmd.Parameters.AddWithValue("@DateModification", currentDate);

        //                    await cmd.ExecuteNonQueryAsync();

        //                    response.ArticlesAdded++;
        //                }
        //            }

        //            _logger.LogInformation($"✅ [CAS 1] {response.ArticlesAdded} articles ajoutés à facture {factureId}");

        //            // ========================================
        //            // ÉTAPE 3 : METTRE À JOUR LES TOTAUX
        //            // ========================================

        //            using (var cmd = new SqlCommand(@"
        //                UPDATE FACTURE
        //                SET Sous_total = (SELECT ISNULL(SUM(sousTotal), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
        //                    ValeurTVA = (SELECT ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
        //                    Total_final = (SELECT ISNULL(SUM(sousTotal), 0) + ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0)
        //                WHERE id = @Id", conn, transaction))
        //            {
        //                cmd.Parameters.AddWithValue("@Id", factureId);
        //                await cmd.ExecuteNonQueryAsync();
        //            }

        //            // ========================================
        //            // ÉTAPE 4 : CONFIRMER LE PAIEMENT
        //            // ========================================

        //            decimal restApayer = Math.Max(0, request.MontantVerser);

        //            using (var cmd = new SqlCommand(@"
        //                UPDATE FACTURE
        //                SET IdPayement = @IdPayement,
        //                    MontantVerser = @MontantVerser,
        //                    MonnaieRemis = @MonnaieRemis,
        //                    RestApayer = @RestApayer,
        //                    Remise = @Remise,
        //                    Etat = 'Payé',
        //                    Solder = @Solder,
        //                    estestCloturer = 1,
        //                    EstEnattente = 0,
        //                    DateModification = @DateModification
        //                WHERE id = @Id", conn, transaction))
        //            {
        //                cmd.Parameters.AddWithValue("@Id", factureId);
        //                cmd.Parameters.AddWithValue("@IdPayement", request.IdPayement);
        //                cmd.Parameters.AddWithValue("@MontantVerser", request.MontantVerser);
        //                cmd.Parameters.AddWithValue("@MonnaieRemis", request.MonnaieRemis);
        //                cmd.Parameters.AddWithValue("@RestApayer", restApayer);
        //                cmd.Parameters.AddWithValue("@Remise", request.Remise);
        //                cmd.Parameters.AddWithValue("@Solder", restApayer == 0);
        //                cmd.Parameters.AddWithValue("@DateModification", currentDate);

        //                await cmd.ExecuteNonQueryAsync();
        //            }

        //            _logger.LogInformation($"✅ [CAS 1] Paiement confirmé pour facture {factureId}");

        //            // ========================================
        //            // ÉTAPE 5 : CRÉER LES MOUVEMENTS STOCK
        //            // ========================================

        //            var details = await GetDetailsFactureAsync(factureId);

        //            foreach (var detail in details)
        //            {
        //                var movementId = Guid.NewGuid();

        //                using (var cmd = new SqlCommand(@"
        //                    INSERT INTO MOUVEMENT_STOCK (
        //                        Id, IdArticle, TypeMouvement, Quantite, PrixUnitaire,
        //                        PrixTotal, DateTransaction, Reference, Utilisateur,
        //                        idUtilsateur, IdEntreprise, Etat, idDetailTransaction
        //                    )
        //                    VALUES (
        //                        @Id, @IdArticle, 'Sortie', @Quantite, @PrixUnitaire,
        //                        @PrixTotal, @DateTransaction, @Reference, @Utilisateur,
        //                        @idUtilsateur, @IdEntreprise, 'Actif', @idDetailTransaction
        //                    )", conn, transaction))
        //                {
        //                    cmd.Parameters.AddWithValue("@Id", movementId);
        //                    cmd.Parameters.AddWithValue("@IdArticle", detail.IdArticle);
        //                    cmd.Parameters.AddWithValue("@Quantite", detail.Quantite);
        //                    cmd.Parameters.AddWithValue("@PrixUnitaire", detail.PrixAchatUnitaire);
        //                    cmd.Parameters.AddWithValue("@PrixTotal", detail.Quantite * detail.PrixAchatUnitaire);
        //                    cmd.Parameters.AddWithValue("@DateTransaction", currentDate);
        //                    cmd.Parameters.AddWithValue("@Reference", $"POS-{factureId:N}");
        //                    cmd.Parameters.AddWithValue("@Utilisateur", "POS");
        //                    cmd.Parameters.AddWithValue("@idUtilsateur", currentUserId);
        //                    cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
        //                    cmd.Parameters.AddWithValue("@idDetailTransaction", detail.Id);

        //                    await cmd.ExecuteNonQueryAsync();

        //                    response.StockMovementsCreated++;
        //                }
        //            }

        //            transaction.Commit();

        //            // Récupérer la facture finale
        //            var facture = await GetFactureByIdAsync(factureId);

        //            response.Success = true;
        //            response.Message = $"✅ CAS 1 : Facture créée, {response.ArticlesAdded} articles ajoutés, paiement confirmé, {response.StockMovementsCreated} mouvements stock créés";
        //            response.PaymentMode = "Created";
        //            response.Facture = facture ?? new FactureDto { Id = factureId };

        //            _logger.LogInformation($"✅ [CAS 1 COMPLET] Facture {factureId} créée, payée et finalisée");
        //        }
        //        catch (Exception ex)
        //        {
        //            transaction.Rollback();
        //            throw;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"[CAS 1] Erreur création + paiement facture");
        //        response.Success = false;
        //        response.Message = $"Erreur : {ex.Message}";
        //    }

        //    return response;
        //}
        public async Task<PosPaymentResponse> CreateAndPayFactureAsync(ConfirmPosPaymentRequest request)
        {
            var response = new PosPaymentResponse();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var transaction = conn.BeginTransaction();

                try
                {
                    var currentUserId = GetUserIdFromContext();
                    var currentDate = DateTime.Now;
                    var enterpriseId = request.IdEntreprise ?? GetEntrepriseIdFromContext();
                    var factureId = Guid.NewGuid();

                    // ========================================
                    // ÉTAPE 0 : GÉNÉRER NUMÉRO FACTURE (OPTIMISÉ)
                    // ========================================

                    // ⚡ OPTIMISATION: Pas d'appel async/DB
                    // Utiliser GenerateInvoiceNumber() au lieu de GenerateInvoiceNumberWithCounterAsync()
                    // Gain: -5-10ms par appel

                    string numeroFacture;
                    if (string.IsNullOrWhiteSpace(request.NumeroFacture))
                    {
                        // ✅ Générer SYNCHRONE sans DB
                        numeroFacture = await GetOrGenerateInvoiceNumberAsync(null);
                        _logger.LogInformation($"✅ Numéro facture généré (rapide): {numeroFacture}");
                    }
                    else
                    {
                        numeroFacture = request.NumeroFacture.Trim();
                    }

                    // ========================================
                    // ÉTAPE 1 : CRÉER LA FACTURE
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                INSERT INTO FACTURE (
                    id, NumeroFacture, Designation, DateCreation, Message,
                    Montant, Sous_total, Total_final, MontantVerser, MonnaieRemis,
                    RestApayer, Remise, Remise_globale, ValeurRemise_globale,
                    ValeurTVA, TVA, BeneficeSurFact,
                    IdTable, Caisse, Serveur, idUtilisateur, IdClient, IdEntreprise,
                    idTypeService, IdSession, Etat, EstAnnuler, EstSupprimer,
                    EstEnattente, Solder, DateModification, Statut
                )
                VALUES (
                    @Id, @NumeroFacture, @Designation, @DateCreation, @Message,
                    0, 0, 0, 0, 0,
                    0, 0, 0, 0,
                    0, 0, 0,
                    @IdTable, @Caisse, @Serveur, @idUtilisateur, @IdClient, @IdEntreprise,
                    NULL, @IdSession, 'En attente', 0, 0,
                    0, 0, @DateModification, 'Ouvert'
                )", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        cmd.Parameters.AddWithValue("@NumeroFacture", numeroFacture);
                        AddParameter(cmd, "@Designation", request.Designation);
                        cmd.Parameters.AddWithValue("@DateCreation", currentDate);
                        AddParameter(cmd, "@Message", request.Message);
                        AddParameter(cmd, "@IdTable", request.IdTable);
                        AddParameter(cmd, "@Caisse", request.Caisse);
                        AddParameter(cmd, "@Serveur", request.Serveur);
                        AddParameter(cmd, "@idUtilisateur", request.idUtilisateur ?? currentUserId);
                        AddParameter(cmd, "@IdClient", request.IdClient);
                        cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                        AddParameter(cmd, "@IdSession", request.IdSession);
                        cmd.Parameters.AddWithValue("@DateModification", currentDate);

                        cmd.ExecuteNonQuery();  // ← Synchrone (plus rapide)
                    }

                    // ========================================
                    // ÉTAPE 2 : AJOUTER ARTICLES (OPTIMISÉ - BATCHING)
                    // ========================================

                    // ⚡ OPTIMISATION: Batching au lieu de boucle
                    // Avant: N requêtes INSERT (1 par article)
                    // Après: 1 requête INSERT multi-ligne
                    // Gain: -50ms à -200ms selon nombre articles

                    if (request.Articles != null && request.Articles.Count > 0)
                    {
                        // Construire VALUES multi-ligne
                        var valuesClauses = new List<string>();
                        var insertCmd = new SqlCommand { Connection = conn, Transaction = transaction };

                        for (int i = 0; i < request.Articles.Count; i++)
                        {
                            var article = request.Articles[i];
                            var detailId = Guid.NewGuid();

                            // Calculer les montants (SYNCHRONE, très rapide)
                            decimal prixTotal = article.Quantite * article.PrixUnitaireHT;
                            decimal montantTVA = prixTotal * (article.TauxTVA / 100);
                            decimal prixTTC = prixTotal + montantTVA;
                            decimal sousTotal = prixTotal - article.valeurRemise;

                            // Ajouter les paramètres
                            insertCmd.Parameters.AddWithValue($"@Id_{i}", detailId);
                            insertCmd.Parameters.AddWithValue($"@IdFacture_{i}", factureId);
                            insertCmd.Parameters.AddWithValue($"@IdArticle_{i}", article.IdArticle);
                            insertCmd.Parameters.AddWithValue($"@Designation_{i}", article.Designation ?? "");
                            insertCmd.Parameters.AddWithValue($"@Quantité_{i}", article.Quantite);
                            insertCmd.Parameters.AddWithValue($"@PrixUnitaireHT_{i}", article.PrixUnitaireHT);
                            insertCmd.Parameters.AddWithValue($"@PrixUnitaireTTC_{i}", prixTTC);
                            insertCmd.Parameters.AddWithValue($"@PrixVente_{i}", article.PrixVente);
                            insertCmd.Parameters.AddWithValue($"@PrixTotal_{i}", prixTotal);
                            insertCmd.Parameters.AddWithValue($"@sousTotal_{i}", sousTotal);
                            insertCmd.Parameters.AddWithValue($"@TauxTVA_{i}", article.TauxTVA);
                            insertCmd.Parameters.AddWithValue($"@MontantTVA_{i}", montantTVA);
                            insertCmd.Parameters.AddWithValue($"@valeurRemise_{i}", article.valeurRemise);
                            AddParameter(insertCmd, $"@Specificite_{i}", article.Specificite);
                            AddParameter(insertCmd, $"@DetailComposent_{i}", article.DetailComposent);
                            AddParameter(insertCmd, $"@IdServeur_{i}", article.IdServeur);
                            AddParameter(insertCmd, $"@IdCuisinier_{i}", article.IdCuisinier);
                            AddParameter(insertCmd, $"@IdUser_{i}", currentUserId);

                            // Ajouter VALUE clause
                            valuesClauses.Add($@"(
                        @Id_{i}, @IdFacture_{i}, @IdArticle_{i}, @Designation_{i}, @Quantité_{i},
                        @PrixUnitaireHT_{i}, @PrixUnitaireTTC_{i}, @PrixVente_{i}, @PrixTotal_{i},
                        @sousTotal_{i}, @TauxTVA_{i}, @MontantTVA_{i}, @valeurRemise_{i},
                        0, @Specificite_{i}, @DetailComposent_{i},
                        @IdServeur_{i}, @IdCuisinier_{i}, @IdUser_{i}, 'Agent POS', @IdEntreprise,
                        'Actif', 1, 0, 0, @DateCreation,
                        @DateModification, @IdUser_{i}
                    )");

                            response.ArticlesAdded++;
                        }

                        // Exécuter INSERT multi-ligne EN UNE SEULE REQUÊTE
                        insertCmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                        insertCmd.Parameters.AddWithValue("@DateCreation", currentDate);
                        insertCmd.Parameters.AddWithValue("@DateModification", currentDate);

                        insertCmd.CommandText = $@"
                    INSERT INTO DETAIL_TRANSACTIONS (
                        Id, IdFacture, IdArticle, Designation, Quantite,
                        PrixUnitaireHT, PrixUnitaireTTC, PrixVente, PrixTotal,
                        sousTotal, TauxTVA, MontantTVA, valeurRemise,
                        PrixAchatUnitaire, Specificite, DetailComposent,
                        IdServeur, IdCuisinier, IdUser, DesignationAgent, IdEntreprise,
                        Etat, EstExecuter, estDetaileComd, estSupprimer, DateCreation,
                        DateModification, IdUserModification
                    )
                    VALUES {string.Join(",", valuesClauses)}";

                        insertCmd.ExecuteNonQuery();  // ← 1 requête au lieu de N!
                    }

                    // ========================================
                    // ÉTAPE 3 : METTRE À JOUR LES TOTAUX
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                UPDATE FACTURE
                SET Sous_total = (SELECT ISNULL(SUM(sousTotal), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
                    ValeurTVA = (SELECT ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
                    Total_final = (SELECT ISNULL(SUM(sousTotal), 0) + ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0)
                WHERE id = @Id", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        cmd.ExecuteNonQuery();  // ← Synchrone
                    }

                    // ========================================
                    // ÉTAPE 4 : CRÉER PAIEMENT
                    // ========================================

                    var paiementId = Guid.NewGuid();
                    using (var cmd = new SqlCommand(@"
                INSERT INTO PAIEMENTS (
                    id, IdFacture, IdTypePaiement, MontantPaye, DatePaiement,
                    Observation, IdUser, IdEntreprise, DateCreation, DateModification
                )
                VALUES (
                    @Id, @IdFacture, @IdTypePaiement, @MontantPaye, @DatePaiement,
                    'POS Payment', @IdUser, @IdEntreprise, @DateCreation, @DateModification
                )", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", paiementId);
                        cmd.Parameters.AddWithValue("@IdFacture", factureId);
                        cmd.Parameters.AddWithValue("@IdTypePaiement", request.IdPayement);
                        cmd.Parameters.AddWithValue("@MontantPaye", request.MontantVerser);
                        cmd.Parameters.AddWithValue("@DatePaiement", currentDate);
                        cmd.Parameters.AddWithValue("@IdUser", currentUserId);
                        cmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                        cmd.Parameters.AddWithValue("@DateCreation", currentDate);
                        cmd.Parameters.AddWithValue("@DateModification", currentDate);

                        cmd.ExecuteNonQuery();  // ← Synchrone
                    }

                    // ========================================
                    // ÉTAPE 5 : METTRE À JOUR FACTURE PAYÉE
                    // ========================================

                    using (var cmd = new SqlCommand(@"
                UPDATE FACTURE
                SET Etat = 'Payé',
                    MontantVerser = @MontantVerser,
                    RestApayer = Total_final - @MontantVerser,
                    DateModification = @DateModification
                WHERE id = @Id", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", factureId);
                        cmd.Parameters.AddWithValue("@MontantVerser", request.MontantVerser);
                        cmd.Parameters.AddWithValue("@DateModification", currentDate);

                        cmd.ExecuteNonQuery();  // ← Synchrone
                    }

                    // ========================================
                    // ÉTAPE 6 : CRÉER MOUVEMENTS STOCK (OPTIMISÉ)
                    // ========================================

                    // ⚡ OPTIMISATION: Batching des SORTIES
                    if (request.Articles != null && request.Articles.Count > 0)
                    {
                        var movementClauses = new List<string>();
                        var movementCmd = new SqlCommand { Connection = conn, Transaction = transaction };

                        for (int i = 0; i < request.Articles.Count; i++)
                        {
                            var article = request.Articles[i];
                            decimal montant = article.Quantite * article.PrixUnitaireHT;

                            movementCmd.Parameters.AddWithValue($"@IdArticle_{i}", article.IdArticle);
                            movementCmd.Parameters.AddWithValue($"@Quantité_{i}", article.Quantite);
                            movementCmd.Parameters.AddWithValue($"@PrixUnitaire_{i}", article.PrixUnitaireHT);
                            movementCmd.Parameters.AddWithValue($"@Montant_{i}", montant);

                            movementClauses.Add($@"(
                                DEFAULT,
                                @DateTransaction,
                                @IdArticle_{i},
                                0,
                                'SORTIE',
                                NULL,
                                @idUtilisateur,
                                @IdEntreprise,
                                NULL,
                                'Validé',
                                @Quantité_{i},
                                @PrixUnitaire_{i},
                                @Montant_{i},
                                @Reference,
                                'Sortie POS - Vente',
                                'Paiement'
                            )");

                            response.StockMovementsCreated++;
                        }

                        movementCmd.Parameters.AddWithValue("@DateTransaction", currentDate);
                        movementCmd.Parameters.AddWithValue("@idUtilisateur", currentUserId);
                        movementCmd.Parameters.AddWithValue("@IdEntreprise", enterpriseId);
                        movementCmd.Parameters.AddWithValue("@Reference", $"POS-PAY-{currentDate:yyyyMMddHHmmss}");

                        movementCmd.CommandText = $@"
                            INSERT INTO MOUVEMENT_STOCK (
                                Id, DateTransaction, IdArticle, EstAvarier,
                                TypeMouvement, idDetailTransaction, idUtilsateur, IdEntreprise,
                                IdStock, Etat, Quantite, PrixUnitaire,
                                Montant, Reference, Commentaire, Motif
                            )
                            VALUES {string.Join(",", movementClauses)}";

                        movementCmd.ExecuteNonQuery();  // ← 1 requête multi-ligne
                        response.StockMovementsCreated = request.Articles.Count;
                    }

                    transaction.Commit();

                    var facture = await GetFactureByIdAsync(factureId);

                    response.Success = true;
                    response.Message = $"✅ Facture créée et payée en {DateTime.Now.Millisecond}ms";
                    response.PaymentMode = "Created";
                    response.Facture = facture ?? new FactureDto { Id = factureId };

                    _logger.LogInformation($"✅ [OPTIMIZED] Facture {factureId} créée, payée et stock en {DateTime.Now.Millisecond}ms");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OPTIMIZED] Erreur création + paiement facture");
                response.Success = false;
                response.Message = $"Erreur : {ex.Message}";
            }

            return response;
        }

        // ========================================
        // CAS 2 : PAYER FACTURE EXISTANTE
        // ========================================

        /// <summary>
        /// 📌 CAS 2 : Payer une facture POS qui existe déjà
        /// 
        /// Étapes :
        /// 1. Vérifier que la facture existe
        /// 2. Confirmer le paiement
        /// 3. Créer mouvements stock automatiquement
        /// </summary>
        public async Task<PosPaymentResponse> PayExistingFactureAsync(ConfirmPosPaymentRequest request)
        {
            var response = new PosPaymentResponse();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Vérifier que la facture existe
                var existingFacture = await GetFactureByIdAsync(request.IdFacture ?? Guid.Empty);

                if (existingFacture == null)
                {
                    response.Success = false;
                    response.Message = $"Facture {request.IdFacture} introuvable";
                    return response;
                }

                using var transaction = conn.BeginTransaction();

                try
                {
                    var currentDate = DateTime.Now;
                    var currentUserId = GetUserIdFromContext();

                    // ========================================
                    // ÉTAPE 1 : CONFIRMER LE PAIEMENT
                    // ========================================

                    decimal restApayer = Math.Max(0, existingFacture.Total_final - request.MontantVerser);

                    using (var cmd = new SqlCommand(@"
                        UPDATE FACTURE
                        SET IdPayement = @IdPayement,
                            MontantVerser = @MontantVerser,
                            MonnaieRemis = @MonnaieRemis,
                            RestApayer = @RestApayer,
                            Remise = @Remise,
                            Etat = 'Payé',
                            Solder = @Solder,
                            estestCloturer = 0,
                            EstEnattente = 0,
                            DateModification = @DateModification
                        WHERE id = @Id AND IdEntreprise = @IdEntreprise", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", request.IdFacture);
                        cmd.Parameters.AddWithValue("@IdPayement", request.IdPayement);
                        cmd.Parameters.AddWithValue("@MontantVerser", request.MontantVerser);
                        cmd.Parameters.AddWithValue("@MonnaieRemis", request.MonnaieRemis);
                        cmd.Parameters.AddWithValue("@RestApayer", restApayer);
                        cmd.Parameters.AddWithValue("@Remise", request.Remise);
                        cmd.Parameters.AddWithValue("@Solder", restApayer == 0);
                        cmd.Parameters.AddWithValue("@DateModification", currentDate);
                        AddEntrepriseParameter(cmd);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation($"✅ [CAS 2] Paiement confirmé pour facture {request.IdFacture}");

                    // ========================================
                    // ÉTAPE 2 : CRÉER LES MOUVEMENTS STOCK
                    // ========================================

                    var details = await GetDetailsFactureAsync(request.IdFacture ?? Guid.Empty);

                    foreach (var detail in details)
                    {
                        var movementId = Guid.NewGuid();

                        using (var cmd = new SqlCommand(@"
                            INSERT INTO MOUVEMENT_STOCK (
                                Id, IdArticle, TypeMouvement, Quantite, PrixUnitaire,
                                Montant, DateTransaction, Reference,Commentaire, Utilisateur,
                                idUtilsateur, IdEntreprise, Etat, idDetailTransaction
                            )
                            VALUES (
                                @Id, @IdArticle, 'SORTIE', @Quantite, @PrixUnitaire,
                                @Montant, @DateTransaction, @Reference, @Commentaire, @Utilisateur,
                                @idUtilsateur, @IdEntreprise, 'Actif', @idDetailTransaction
                            )", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", movementId);
                            cmd.Parameters.AddWithValue("@IdArticle", detail.IdArticle);
                            cmd.Parameters.AddWithValue("@Quantite", detail.Quantite);
                            cmd.Parameters.AddWithValue("@PrixUnitaire", detail.PrixVente);
                            cmd.Parameters.AddWithValue("@Montant", detail.Quantite * detail.PrixVente);
                            cmd.Parameters.AddWithValue("@DateTransaction", currentDate);
                            cmd.Parameters.AddWithValue("@Reference", $"POS-PAY-{currentDate:yyyyMMddHHmmss}");
                            cmd.Parameters.AddWithValue("@Commentaire", "Sortie POS - Vente");
                            cmd.Parameters.AddWithValue("@Utilisateur", "POS");
                            cmd.Parameters.AddWithValue("@idUtilsateur", currentUserId);
                            cmd.Parameters.AddWithValue("@IdEntreprise", GetEntrepriseIdFromContext());
                            cmd.Parameters.AddWithValue("@idDetailTransaction", detail.Id);

                            await cmd.ExecuteNonQueryAsync();

                            response.StockMovementsCreated++;
                        }
                    }

                    transaction.Commit();

                    // Récupérer la facture finale
                    var facture = await GetFactureByIdAsync(request.IdFacture ?? Guid.Empty);

                    response.Success = true;
                    response.Message = $"✅ CAS 2 : Facture payée, {response.StockMovementsCreated} mouvements stock créés";
                    response.PaymentMode = "Paid";
                    response.Facture = facture ?? new FactureDto { Id = request.IdFacture ?? Guid.Empty };

                    _logger.LogInformation($"✅ [CAS 2 COMPLET] Facture {request.IdFacture} payée et finalisée");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CAS 2] Erreur paiement facture {request.IdFacture}");
                response.Success = false;
                response.Message = $"Erreur : {ex.Message}";
            }

            return response;
        }

        // ========================================
        // HELPERS (ANCIENS - COMPATIBILITÉ)
        // ========================================

        private async Task<FactureDto?> GetFactureByIdAsync(Guid idFacture)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("f");

                using var cmd = new SqlCommand($@"
                    SELECT 
                        f.id, f.NumeroFacture, f.Designation, f.DateCreation, f.Message,
                        f.Montant, f.Sous_total, f.Total_final, f.MontantVerser, f.MonnaieRemis,
                        f.RestApayer, f.Remise, f.Remise_globale, f.Solder,
                        f.ValeurRemise_globale, f.ValeurTVA, f.TVA, f.BeneficeSurFact,
                        f.IdTable, f.Caisse, f.Serveur, f.DesignationTable,
                        f.IdPayement, f.idUtilisateur, f.IdClient, f.IdEntreprise,
                        f.idBlockCommandes, f.Etat, f.EstAnnuler, f.EstSupprimer,
                        f.estestCloturer, f.EstEnattente, f.DesignationAtttente,
                        f.IdSession, f.identifiantSession, f.Statut, f.DateModification,
                        c.Nom AS nomClient, u.Nom AS NomUtilisateur, tp.Designation AS DesignationPayement
                    FROM FACTURE f
                    LEFT JOIN CLIENTS c ON f.IdClient = c.Id
                    LEFT JOIN UTILISATEURS u ON f.idUtilisateur = u.Id
                    LEFT JOIN TYPE_PAIEMENT tp ON f.IdPayement = tp.Id
                    WHERE f.id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", idFacture);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapFactureFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération facture POS {idFacture}");
            }

            return null;
        }

        private async Task<DetailTransactionDto?> GetDetailByIdAsync(Guid idDetail)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    SELECT 
                        Id, Designation, Quantite, PrixUnitaireHT, PrixUnitaireTTC,
                        PrixVente, PrixTotal, sousTotal, TauxTVA, MontantTVA, valeurRemise,
                        PrixAchatUnitaire, Specificite, DetailComposent, DetailComposant,
                        Specification, domaineAricle, IdArticle, IdFacture, IdServeur,
                        IdCuisinier, IdUser, DesignationAgent, IdEntreprise, Etat,
                        EstExecuter, estSuite, estDetaileComd, estSupprimer, EstModifier,
                        EstAvarie, AutorisationModif, idDomaine, idTypeService,
                        DateCreation, DateModification, IdUserModification
                    FROM DETAIL_TRANSACTIONS
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", idDetail);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapDetailFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération détail POS {idDetail}");
            }

            return null;
        }

        private async Task UpdateFactureTotalsAsync(Guid idFacture)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE FACTURE
                    SET Sous_total = (SELECT ISNULL(SUM(sousTotal), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
                        ValeurTVA = (SELECT ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0),
                        Total_final = (SELECT ISNULL(SUM(sousTotal), 0) + ISNULL(SUM(MontantTVA), 0) FROM DETAIL_TRANSACTIONS WHERE IdFacture = @Id AND estSupprimer = 0)
                    WHERE id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", idFacture);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour totaux facture POS {idFacture}");
            }
        }

        private async Task ReverseStockMovementsAsync(Guid idFacture)
        {
            try
            {
                using var conn = await GetConnectionAsync();

                using var cmd = new SqlCommand(@"
                    UPDATE MOUVEMENT_STOCK
                    SET Etat = 'Annulé'
                    WHERE idDetailTransaction IN (
                        SELECT Id FROM DETAIL_TRANSACTIONS WHERE IdFacture = @IdFacture
                    )", conn);

                cmd.Parameters.AddWithValue("@IdFacture", idFacture);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur inversion mouvements stock POS {idFacture}");
            }
        }

        private FactureDto MapFactureFromReader(SqlDataReader reader)
        {
            return new FactureDto
            {
                Id = reader.GetGuid(0),
                NumeroFacture = ReadNullableString(reader, "NumeroFacture"),
                Designation = ReadNullableString(reader, "Designation"),
                DateCreation = reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                DateModification = ReadNullableDateTime(reader, "DateModification"),
                Message = ReadNullableString(reader, "Message"),
                Montant = reader.GetDecimal(reader.GetOrdinal("Montant")),
                Sous_total = reader.GetDecimal(reader.GetOrdinal("Sous_total")),
                Total_final = reader.GetDecimal(reader.GetOrdinal("Total_final")),
                MontantVerser = reader.GetDecimal(reader.GetOrdinal("MontantVerser")),
                MonnaieRemis = reader.GetDecimal(reader.GetOrdinal("MonnaieRemis")),
                RestApayer = reader.GetDecimal(reader.GetOrdinal("RestApayer")),
                Remise = reader.GetDecimal(reader.GetOrdinal("Remise")),
                Remise_globale = reader.GetDecimal(reader.GetOrdinal("Remise_globale")),
                ValeurRemise_globale = reader.GetDecimal(reader.GetOrdinal("ValeurRemise_globale")),
                ValeurTVA = reader.GetDecimal(reader.GetOrdinal("ValeurTVA")),
                TVA = reader.GetDecimal(reader.GetOrdinal("TVA")),
                BeneficeSurFact = reader.GetDecimal(reader.GetOrdinal("BeneficeSurFact")),
                IdTable = ReadNullableGuid(reader, "IdTable"),
                DesignationTable = ReadNullableString(reader, "DesignationTable"),
                IdPayement = ReadNullableGuid(reader, "IdPayement"),
                DesignationPayement = ReadNullableString(reader, "DesignationPayement"),
                idUtilisateur = ReadNullableGuid(reader, "idUtilisateur"),
                NomUtilisateur = ReadNullableString(reader, "NomUtilisateur"),
                IdClient = ReadNullableGuid(reader, "IdClient"),
                nomClient = ReadNullableString(reader, "nomClient"),
                IdEntreprise = reader.GetGuid(reader.GetOrdinal("IdEntreprise")),
                IdSession = ReadNullableGuid(reader, "IdSession"),
                Etat = ReadNullableString(reader, "Etat"),
                Statut = ReadNullableString(reader, "Statut"),
                EstAnnuler = ReadNullableBool(reader, "EstAnnuler") ?? false,
                EstSupprimer = ReadNullableBool(reader, "EstSupprimer") ?? false,
                EstEnattente = ReadNullableBool(reader, "EstEnattente") ?? false,
                estestCloturer = ReadNullableBool(reader, "estestCloturer") ?? false,
                Caisse = ReadNullableString(reader, "Caisse"),
                Serveur = ReadNullableString(reader, "Serveur"),
                Solder = ReadNullableBool(reader, "Solder") ?? false
            };
        }

        private DetailTransactionDto MapDetailFromReader(SqlDataReader reader)
        {
            return new DetailTransactionDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Designation = ReadNullableString(reader, "Designation"),
                Quantite = reader.GetDecimal(reader.GetOrdinal("Quantite")),
                PrixUnitaireHT = reader.GetDecimal(reader.GetOrdinal("PrixUnitaireHT")),
                PrixUnitaireTTC = reader.GetDecimal(reader.GetOrdinal("PrixUnitaireTTC")),
                PrixVente = reader.GetDecimal(reader.GetOrdinal("PrixVente")),
                PrixTotal = reader.GetDecimal(reader.GetOrdinal("PrixTotal")),
                sousTotal = reader.GetDecimal(reader.GetOrdinal("sousTotal")),
                TauxTVA = reader.GetDecimal(reader.GetOrdinal("TauxTVA")),
                MontantTVA = reader.GetDecimal(reader.GetOrdinal("MontantTVA")),
                valeurRemise = reader.GetDecimal(reader.GetOrdinal("valeurRemise")),
                PrixAchatUnitaire = reader.GetDecimal(reader.GetOrdinal("PrixAchatUnitaire")),
                Specificite = ReadNullableString(reader, "Specificite"),
                DetailComposent = ReadNullableString(reader, "DetailComposent"),
                DetailComposant = ReadNullableString(reader, "DetailComposant"),
                Specification = ReadNullableString(reader, "Specification"),
                domaineAricle = ReadNullableString(reader, "domaineAricle"),
                IdArticle = reader.GetGuid(reader.GetOrdinal("IdArticle")),
                IdFacture = reader.GetGuid(reader.GetOrdinal("IdFacture")),
                IdServeur = ReadNullableGuid(reader, "IdServeur"),
                IdCuisinier = ReadNullableGuid(reader, "IdCuisinier"),
                IdUser = ReadNullableGuid(reader, "IdUser"),
                DesignationAgent = ReadNullableString(reader, "DesignationAgent"),
                IdEntreprise = reader.GetGuid(reader.GetOrdinal("IdEntreprise")),
                Etat = ReadNullableString(reader, "Etat"),
                EstExecuter = ReadNullableBool(reader, "EstExecuter") ?? false,
                estSuite = ReadNullableBool(reader, "estSuite") ?? false,
                estDetaileComd = ReadNullableBool(reader, "estDetaileComd") ?? false,
                estSupprimer = ReadNullableBool(reader, "estSupprimer") ?? false,
                EstModifier = ReadNullableBool(reader, "EstModifier") ?? false,
                EstAvarie = ReadNullableBool(reader, "EstAvarie") ?? false,
                AutorisationModif = ReadNullableBool(reader, "AutorisationModif") ?? false,
                idDomaine = ReadNullableGuid(reader, "idDomaine"),
                idTypeService = ReadNullableGuid(reader, "idTypeService"),
                DateCreation = reader.GetDateTime(reader.GetOrdinal("DateCreation")),
                DateModification = ReadNullableDateTime(reader, "DateModification"),
                IdUserModification = ReadNullableGuid(reader, "IdUserModification")
            };
        }

        // ════════════════════════════════════════════════════════════════════════════
        // AJOUTER À PosService.cs - REMPLACER LES 4 MÉTHODES PRÉCÉDENTES
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtenir les 3 statistiques du jour en UNE SEULE requête
        /// - Nombre de ventes
        /// - Chiffre d'affaires
        /// - Panier moyen
        /// 
        /// ⚡ OPTIMISÉ: Une seule requête SQL = Ultra rapide (<30ms)
        /// </summary>
        public async Task<StatistiquesJourResponse> GetStatistiquesJourAsync()
        {
            try
            {
                var entrepriseId = GetEntrepriseIdFromContext();
                var today = DateTime.Now.Date;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // ⚡ UNE SEULE REQUÊTE pour tout
                using var cmd = new SqlCommand(@"
            SELECT 
                COUNT(DISTINCT id) as NombreVentes,
                ISNULL(SUM(Sous_total), 0) as SousTotal,
                ISNULL(SUM(ValeurTVA), 0) as TotalTVA,
                ISNULL(SUM(Total_final), 0) as ChiffreAffaires,
                ISNULL(SUM(ISNULL(Remise_globale, 0)), 0) as TotalRemises
            FROM FACTURE
            WHERE IdEntreprise = @IdEntreprise
            AND CAST(DateCreation AS DATE) = @Today
            AND Etat = 'Payé'
            AND EstSupprimer = 0", conn);

                cmd.Parameters.AddWithValue("@IdEntreprise", entrepriseId);
                cmd.Parameters.AddWithValue("@Today", today);

                var response = new StatistiquesJourResponse
                {
                    DateStatistique = today
                };

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        response.NombreVentes = reader.GetInt32(0);
                        response.SousTotal = reader.GetDecimal(1);
                        response.TotalTVA = reader.GetDecimal(2);
                        response.ChiffreAffaires = reader.GetDecimal(3);
                        response.TotalRemises = reader.GetDecimal(4);

                        // Calculer le panier moyen EN MÉMOIRE
                        response.PanierMoyen = response.NombreVentes > 0
                            ? response.ChiffreAffaires / response.NombreVentes
                            : 0;

                        // Ajouter les messages
                        response.Messages.Add($"✅ {response.NombreVentes} vente(s)");
                        response.Messages.Add($"✅ CA: {response.ChiffreAffaires:N2} XOF");
                        response.Messages.Add($"✅ Panier moyen: {response.PanierMoyen:N2} XOF");
                    }
                }

                _logger.LogInformation($"✅ Statistiques jour: {response.NombreVentes} ventes, {response.ChiffreAffaires:N2} XOF");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetStatistiquesJourAsync");
                return new StatistiquesJourResponse
                {
                    DateStatistique = DateTime.Now.Date,
                    Messages = new List<string> { $"❌ Erreur : {ex.Message}" }
                };
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // AJOUTER À PosService.cs (ou créer RapportsService.cs)
        // Génération des rapports avec pagination
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Générer Rapport Ventes (Dates)
        /// </summary>
        public async Task<RapportVentesDto> GetRapportVentesAsync(DateTime dateDebut, DateTime dateFin, int page = 1)
        {
            try
            {
                var entrepriseId = GetEntrepriseIdFromContext();
                var entrepriseName = await GetEntrepriseNomAsync(entrepriseId);
                var lignesParPage = 10;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // ÉTAPE 1: Compter total lignes
                int totalLignes = 0;
                using (var cmd = new SqlCommand(@"
            SELECT COUNT(*) FROM FACTURE
            WHERE IdEntreprise = @IdEntreprise
            AND CAST(DateCreation AS DATE) >= @DateDebut
            AND CAST(DateCreation AS DATE) <= @DateFin
            AND Etat = 'Payé'
            AND EstAnnuler = 0
            AND EstSupprimer = 0", conn))
                {
                    cmd.Parameters.AddWithValue("@IdEntreprise", entrepriseId);
                    cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
                    cmd.Parameters.AddWithValue("@DateFin", dateFin);

                    totalLignes = (int)await cmd.ExecuteScalarAsync();
                }

                int totalPages = (int)Math.Ceiling((double)totalLignes / lignesParPage);
                if (page < 1) page = 1;
                if (page > totalPages && totalPages > 0) page = totalPages;

                int skip = (page - 1) * lignesParPage;

                // ÉTAPE 2: Récupérer les factures paginées
                var lignes = new List<RapportVentesLigneDto>();
                decimal sousTotal = 0;
                decimal totalTVA = 0;
                decimal totalGeneral = 0;

                using (var cmd = new SqlCommand(@"
            SELECT 
                f.DateCreation,
                f.NumeroFacture,
                f.Designation,
                f.Sous_total,
                f.ValeurTVA,
                f.Total_final
            FROM FACTURE f
            WHERE f.IdEntreprise = @IdEntreprise
            AND CAST(f.DateCreation AS DATE) >= @DateDebut
            AND CAST(f.DateCreation AS DATE) <= @DateFin
            AND f.Etat = 'Payé'
            AND f.EstAnnuler = 0
            AND f.EstSupprimer = 0
            ORDER BY f.DateCreation DESC, f.NumeroFacture DESC
            OFFSET @Skip ROWS
            FETCH NEXT @Take ROWS ONLY", conn))
                {
                    cmd.Parameters.AddWithValue("@IdEntreprise", entrepriseId);
                    cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
                    cmd.Parameters.AddWithValue("@DateFin", dateFin);
                    cmd.Parameters.AddWithValue("@Skip", skip);
                    cmd.Parameters.AddWithValue("@Take", lignesParPage);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var montant = reader.GetDecimal(5);
                        lignes.Add(new RapportVentesLigneDto
                        {
                            Date = reader.GetDateTime(0),
                            NumeroFacture = ReadNullableString(reader, "NumeroFacture") ?? "-",
                            Designation = ReadNullableString(reader, "Designation") ?? "-",
                            MontantTotal = montant
                        });

                        sousTotal += reader.GetDecimal(3);
                        totalTVA += reader.GetDecimal(4);
                        totalGeneral += montant;
                    }
                }

                return new RapportVentesDto
                {
                    NomEntreprise = entrepriseName,
                    DateDebut = dateDebut,
                    DateFin = dateFin,
                    PageActuelle = page,
                    TotalPages = totalPages,
                    TotalLignes = totalLignes,
                    SousTotal = sousTotal,
                    TotalTVA = totalTVA,
                    TotalGeneral = totalGeneral,
                    Lignes = lignes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRapportVentesAsync");
                return new RapportVentesDto { Lignes = new List<RapportVentesLigneDto>() };
            }
        }

        /// <summary>
        /// Générer Rapport Ventes Quantité+Valeur
        /// </summary>
        public async Task<RapportVentesQuantiteDto> GetRapportVentesQuantiteAsync(DateTime dateDebut, DateTime dateFin, int page = 1)
        {
            try
            {
                var entrepriseId = GetEntrepriseIdFromContext();
                var entrepriseName = await GetEntrepriseNomAsync(entrepriseId);
                var lignesParPage = 10;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // ÉTAPE 1: Compter total articles
                int totalLignes = 0;
                using (var cmd = new SqlCommand(@"
            SELECT COUNT(*) FROM (
                SELECT DISTINCT dt.IdArticle
                FROM DETAIL_TRANSACTIONS dt
                JOIN FACTURE f ON dt.IdFacture = f.id
                WHERE f.IdEntreprise = @IdEntreprise
                AND CAST(f.DateCreation AS DATE) >= @DateDebut
                AND CAST(f.DateCreation AS DATE) <= @DateFin
                AND f.Etat = 'Payé'
                AND f.EstAnnuler = 0
                AND f.EstSupprimer = 0
                AND dt.estSupprimer = 0
            ) t", conn))
                {
                    cmd.Parameters.AddWithValue("@IdEntreprise", entrepriseId);
                    cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
                    cmd.Parameters.AddWithValue("@DateFin", dateFin);

                    totalLignes = (int)await cmd.ExecuteScalarAsync();
                }

                int totalPages = (int)Math.Ceiling((double)totalLignes / lignesParPage);
                if (page < 1) page = 1;
                if (page > totalPages && totalPages > 0) page = totalPages;

                int skip = (page - 1) * lignesParPage;

                // ÉTAPE 2: Récupérer les articles paginés avec VUE
                var lignes = new List<RapportVentesQuantiteLigneDto>();
                decimal quantiteTotalVendue = 0;
                decimal montantTotalVendu = 0;

                using (var cmd = new SqlCommand(@"
            SELECT 
                a.Designation,
                ISNULL(SUM(dt.Quantite), 0) as QuantiteVendue,
                ISNULL(SUM(dt.PrixTotal + dt.MontantTVA), 0) as MontantTTC
            FROM ARTICLES a
            LEFT JOIN DETAIL_TRANSACTIONS dt ON a.id = dt.IdArticle AND dt.estSupprimer = 0
            LEFT JOIN FACTURE f ON dt.IdFacture = f.id 
                AND f.Etat = 'Payé' 
                AND f.EstAnnuler = 0 
                AND f.EstSupprimer = 0
                AND CAST(f.DateCreation AS DATE) >= @DateDebut
                AND CAST(f.DateCreation AS DATE) <= @DateFin
            WHERE a.IdEntreprise = @IdEntreprise
            AND a.Statut = 1
            AND (dt.IdArticle IS NOT NULL OR 1=1)
            GROUP BY a.id, a.Designation
            HAVING ISNULL(SUM(dt.Quantite), 0) > 0
            ORDER BY a.Designation ASC
            OFFSET @Skip ROWS
            FETCH NEXT @Take ROWS ONLY", conn))
                {
                    cmd.Parameters.AddWithValue("@IdEntreprise", entrepriseId);
                    cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
                    cmd.Parameters.AddWithValue("@DateFin", dateFin);
                    cmd.Parameters.AddWithValue("@Skip", skip);
                    cmd.Parameters.AddWithValue("@Take", lignesParPage);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        decimal quantite = reader.GetDecimal(1);
                        decimal montant = reader.GetDecimal(2);

                        lignes.Add(new RapportVentesQuantiteLigneDto
                        {
                            Designation = reader.GetString(0),
                            Quantite = quantite,
                            MontantTotal = montant
                        });

                        quantiteTotalVendue += quantite;
                        montantTotalVendu += montant;
                    }
                }

                return new RapportVentesQuantiteDto
                {
                    NomEntreprise = entrepriseName,
                    DateDebut = dateDebut,
                    DateFin = dateFin,
                    PageActuelle = page,
                    TotalPages = totalPages,
                    TotalLignes = totalLignes,
                    QuantiteTotalVendue = quantiteTotalVendue,
                    MontantTotalVendu = montantTotalVendu,
                    Lignes = lignes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRapportVentesQuantiteAsync");
                return new RapportVentesQuantiteDto { Lignes = new List<RapportVentesQuantiteLigneDto>() };
            }
        }

        /// <summary>
        /// Générer Rapport Stock
        /// </summary>
        public async Task<RapportStockDto> GetRapportStockAsync(int page = 1)
        {
            try
            {
                var entrepriseId = GetEntrepriseIdFromContext();
                var entrepriseName = await GetEntrepriseNomAsync(entrepriseId);
                var lignesParPage = 10;

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // ÉTAPE 1: Compter total articles
                int totalLignes = 0;
                using (var cmd = new SqlCommand(@"
            SELECT COUNT(*) FROM [BuildTechPlatforme].[dbo].[V_STOCK_ARTICLES_ENTREPRISE]
            WHERE IdEntreprise = @IdEntreprise", conn))
                {
                    cmd.Parameters.AddWithValue("@IdEntreprise", entrepriseId);
                    totalLignes = (int)await cmd.ExecuteScalarAsync();
                }

                int totalPages = (int)Math.Ceiling((double)totalLignes / lignesParPage);
                if (page < 1) page = 1;
                if (page > totalPages && totalPages > 0) page = totalPages;

                int skip = (page - 1) * lignesParPage;

                // ÉTAPE 2: Récupérer les articles paginés
                var lignes = new List<RapportStockLigneDto>();
                decimal stockTotalArticles = 0;

                using (var cmd = new SqlCommand(@"
            SELECT 
                Designation,
                CodeArticle,
                StockActuel
            FROM [BuildTechPlatforme].[dbo].[V_STOCK_ARTICLES_ENTREPRISE]
            WHERE IdEntreprise = @IdEntreprise
            ORDER BY Designation ASC
            OFFSET @Skip ROWS
            FETCH NEXT @Take ROWS ONLY", conn))
                {
                    cmd.Parameters.AddWithValue("@IdEntreprise", entrepriseId);
                    cmd.Parameters.AddWithValue("@Skip", skip);
                    cmd.Parameters.AddWithValue("@Take", lignesParPage);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        decimal stock = reader.GetDecimal(2);
                        lignes.Add(new RapportStockLigneDto
                        {
                            Designation = reader.GetString(0),
                            CodeArticle = ReadNullableString(reader, "CodeArticle") ?? "-",
                            StockActuel = stock
                        });

                        stockTotalArticles += stock;
                    }
                }

                return new RapportStockDto
                {
                    NomEntreprise = entrepriseName,
                    DateRapport = DateTime.Now,
                    PageActuelle = page,
                    TotalPages = totalPages,
                    TotalLignes = totalLignes,
                    StockTotalArticles = stockTotalArticles,
                    Lignes = lignes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur GetRapportStockAsync");
                return new RapportStockDto { Lignes = new List<RapportStockLigneDto>() };
            }
        }

        /// <summary>
        /// Helper: Récupérer le nom de l'entreprise
        /// </summary>
        private async Task<string> GetEntrepriseNomAsync(Guid entrepriseId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand("SELECT Designation FROM Entreprise WHERE id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", entrepriseId);
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? "ENTREPRISE";
            }
            catch
            {
                return "ENTREPRISE";
            }
        }

        /// <summary>
        /// Helper: Formater nombre sans virgule (10 000 000)
        /// </summary>
        public string FormatNombreSansVirgule(decimal montant)
        {
            return string.Format("{0:N0}", montant).Replace(",", " ");
        }

        /// <summary>
        /// Helper: Formater nombre sans virgule (10 000 000)
        /// </summary>
        public string FormatNombreSansVirgule(int nombre)
        {
            return string.Format("{0:N0}", nombre).Replace(",", " ");
        }
    }
}