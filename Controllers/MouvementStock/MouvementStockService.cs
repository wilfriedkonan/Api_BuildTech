using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Api_BuildTech.Controllers.MouvementStock
{
    public class MouvementStockService : DatabaseService
    {
        // Etat d'un mouvement supprime (suppression logique : la table n'a PAS de colonne EstSupprimer)
        private const string EtatSupprime = "Supprimer";

        // Types de mouvement (calcul des totaux entrees / sorties)
        // Valeurs ecrites par l'API : "Entree" et "Sortie". La lecture reste tolerante : casse, espaces de tete, accent
        // (LIKE 'Entr%' evite le "é"), anciens libelles ("Entrée stock", "Sortie Vente") et majuscules (ENTREE, VENTE...).
        private const string CondEntree = "(LTRIM(m.TypeMouvement) LIKE 'Entr%' OR m.TypeMouvement IN ('RETOUR', 'AJUSTEMENT_POSITIF'))";
        private const string CondSortie = "(LTRIM(m.TypeMouvement) LIKE 'Sortie%' OR m.TypeMouvement IN ('VENTE', 'PERTE', 'AJUSTEMENT_NEGATIF'))";

        private const string TypeEntreeStock = "Entree";
        private const string TypeSortieVente = "Sortie";

        // Exclut les mouvements supprimes logiquement
        private const string FiltreNonSupprime = "AND (m.Etat IS NULL OR m.Etat NOT IN ('Supprimer', 'Annulé'))";

        // Colonnes lues (liste + detail) - noms exacts de dbo.MOUVEMENT_STOCK (idUtilsateur : faute d'origine)
        private const string Colonnes = @"
        m.Id, m.DateTransaction, m.TypeMouvement, m.Quantite, m.PrixUnitaire, m.Montant, m.PrixTotal,
        m.Reference, m.Commentaire, m.Motif, m.Etat,
        m.QuantiteAvant, m.QuantiteApres, m.QuanteStockage,
        m.EstAvarier, m.EstGater, m.EstMouvementMatierePremiere,
        m.IdArticle, m.IdMatierePremiere, m.IdStock, m.IdAutresMag, m.IdEntreprise,
        m.idDetailTransaction, m.idUtilsateur, m.ReferenceParentMvt,
        m.IdMachine, m.IdentifientMachine, m.Utilisateur,
        a.Designation AS NomArticle";

        public MouvementStockService(
            string connectionString,
            ILogger<MouvementStockService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(connectionString, logger, httpContextAccessor)
        {
        }

        // ========================================
        // GET ALL AVEC PAGINATION
        // ========================================

        /// <summary>
        /// Récupère tous les mouvements de stock avec pagination
        /// </summary>
        /// <param name="dateDebut">Date début filtre (optionnel)</param>
        /// <param name="dateFin">Date fin filtre (optionnel)</param>
        /// <param name="page">Numéro de page (1-based)</param>
        /// <param name="pageSize">Nombre d'éléments par page</param>
        /// <param name="typeMouvement">Filtre optionnel : "Entree", "Sortie" (toutes variantes) ou mots contenus dans le type</param>
        public async Task<MouvementStockListResponse> GetAllAsync(
            DateTime? dateDebut = null,
            DateTime? dateFin = null,
            int page = 1,
            int pageSize = 20,
            string? typeMouvement = null)
        {
            var result = new MouvementStockListResponse { Success = true };

            try
            {
                // Validation pagination
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("m") + " " + FiltreNonSupprime;

                // Une date de fin sans heure (00:00) signifie "jusqu'a la fin de cette journee"
                if (dateFin.HasValue && dateFin.Value.TimeOfDay == TimeSpan.Zero)
                    dateFin = dateFin.Value.AddDays(1);

                // Construire filtre de dates
                var dateFilter = "";
                if (dateDebut.HasValue && dateFin.HasValue)
                {
                    dateFilter = "AND m.DateTransaction >= @DateDebut AND m.DateTransaction < @DateFin";
                }
                else if (dateDebut.HasValue)
                {
                    dateFilter = "AND m.DateTransaction >= @DateDebut";
                }
                else if (dateFin.HasValue)
                {
                    dateFilter = "AND m.DateTransaction < @DateFin";
                }

                // Filtre sur le type de mouvement (applique au total, a la page ET aux totaux)
                var typeParams = new List<KeyValuePair<string, string>>();
                dateFilter += " " + BuildTypeFilter(typeMouvement, typeParams);

                // ========================================
                // ÉTAPE 1 : COMPTER LE TOTAL
                // ========================================
                int totalRecords = 0;
                using (var cmdCount = new SqlCommand($@"
                    SELECT COUNT(*)
                    FROM MOUVEMENT_STOCK m
                    WHERE 1 = 1 {whereClause} {dateFilter}", conn))
                {
                    AddEntrepriseParameter(cmdCount);
                    AddTypeParameters(cmdCount, typeParams);
                    if (dateDebut.HasValue)
                        cmdCount.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                    if (dateFin.HasValue)
                        cmdCount.Parameters.AddWithValue("@DateFin", dateFin.Value);

                    totalRecords = (int)await cmdCount.ExecuteScalarAsync();
                }

                // Si aucun résultat
                if (totalRecords == 0)
                {
                    result.Total = 0;
                    result.Pagination = CreateEmptyPagination(page, pageSize);
                    return result;
                }

                // Calculer métadonnées pagination
                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                int offset = (page - 1) * pageSize;

                // ========================================
                // ÉTAPE 2 : RÉCUPÉRER DONNÉES PAGINÉES
                // ========================================
                using (var cmd = new SqlCommand($@"
                    SELECT {Colonnes}

                    FROM MOUVEMENT_STOCK m
                    LEFT JOIN ARTICLES a ON m.IdArticle = a.Id
                    WHERE 1 = 1 {whereClause} {dateFilter}
                    ORDER BY m.DateTransaction DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY", conn))
                {
                    AddEntrepriseParameter(cmd);
                    AddTypeParameters(cmd, typeParams);
                    if (dateDebut.HasValue)
                        cmd.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                    if (dateFin.HasValue)
                        cmd.Parameters.AddWithValue("@DateFin", dateFin.Value);
                    cmd.Parameters.AddWithValue("@Offset", offset);
                    cmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var mouvement = MapMouvement(reader);

                        result.Mouvements.Add(mouvement);
                    }
                }

                // ========================================
                // ÉTAPE 3 : CALCULER TOTAUX ET SOLDES
                // ========================================
                using (var cmdTotaux = new SqlCommand($@"
                    SELECT
                        SUM(CASE WHEN {CondEntree} THEN ISNULL(m.Quantite, 0) ELSE 0 END) AS TotalEntrees,
                        SUM(CASE WHEN {CondSortie} THEN ISNULL(m.Quantite, 0) ELSE 0 END) AS TotalSorties,
                        SUM(CASE WHEN {CondEntree} THEN COALESCE(m.Montant, m.Quantite * m.PrixUnitaire, 0) ELSE 0 END) AS ValeurEntrees,
                        SUM(CASE WHEN {CondSortie} THEN COALESCE(m.Montant, m.Quantite * m.PrixUnitaire, 0) ELSE 0 END) AS ValeurSorties
                    FROM MOUVEMENT_STOCK m
                    WHERE 1 = 1 {whereClause} {dateFilter}", conn))
                {
                    AddEntrepriseParameter(cmdTotaux);
                    AddTypeParameters(cmdTotaux, typeParams);
                    if (dateDebut.HasValue)
                        cmdTotaux.Parameters.AddWithValue("@DateDebut", dateDebut.Value);
                    if (dateFin.HasValue)
                        cmdTotaux.Parameters.AddWithValue("@DateFin", dateFin.Value);

                    using var readerTotaux = await cmdTotaux.ExecuteReaderAsync();
                    if (await readerTotaux.ReadAsync())
                    {
                        result.TotalEntrees = readerTotaux.IsDBNull(0) ? 0 : readerTotaux.GetDecimal(0);
                        result.TotalSorties = readerTotaux.IsDBNull(1) ? 0 : readerTotaux.GetDecimal(1);
                        result.ValeurEntrees = readerTotaux.IsDBNull(2) ? 0 : readerTotaux.GetDecimal(2);
                        result.ValeurSorties = readerTotaux.IsDBNull(3) ? 0 : readerTotaux.GetDecimal(3);

                        // Calculer soldes
                        result.SoldeQuantite = result.TotalEntrees - result.TotalSorties;
                        result.SoldeValeur = result.ValeurEntrees - result.ValeurSorties;
                    }
                }

                // ========================================
                // ÉTAPE 4 : MÉTADONNÉES PAGINATION
                // ========================================
                result.Total = totalRecords;
                result.Pagination = CreatePagination(page, pageSize, totalPages, totalRecords);

                _logger.LogInformation($"✅ Mouvements stock récupérés: Page {page}/{totalPages}, {result.Mouvements.Count} mouvements");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération mouvements stock");
                result.Success = false;
            }

            return result;
        }

        // ========================================
        // GET BY ID
        // ========================================

        public async Task<MouvementStockDto?> GetByIdAsync(Guid id)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause("m") + " " + FiltreNonSupprime;

                using var cmd = new SqlCommand($@"
                    SELECT {Colonnes}

                    FROM MOUVEMENT_STOCK m
                    LEFT JOIN ARTICLES a ON m.IdArticle = a.Id
                    WHERE m.Id = @Id {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return MapMouvement(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récupération mouvement stock {id}");
            }

            return null;
        }

        // ========================================
        // CREATE
        // ========================================

        public async Task<MouvementStockDto?> CreateAsync(CreateMouvementStockRequest request)
        {
            try
            {
                var typeMvt = NormaliserType(request.TypeMouvement);
                if (typeMvt == null)
                {
                    _logger.LogWarning($"Type de mouvement invalide : '{request.TypeMouvement}'");
                    return null;
                }

                var newId = Guid.NewGuid();
                var montant = request.Quantite * request.PrixUnitaire;
                var currentUserId = GetUserIdFromContext();

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    INSERT INTO MOUVEMENT_STOCK (
                        Id, DateTransaction, TypeMouvement, Quantite, PrixUnitaire, 
                        Montant, PrixTotal, Reference, Commentaire, Motif, IdArticle,  
                        IdStock, IdEntreprise, idUtilsateur
                    )
                    VALUES (
                        @Id, @DateTransaction, @TypeMouvement, @Quantite, @PrixUnitaire, 
                        @Montant, @Montant, @Reference, @Commentaire, @Motif, @IdArticle, 
                        @IdStock, @IdEntreprise, @idUtilsateur
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", newId);
                cmd.Parameters.AddWithValue("@DateTransaction", request.DateTransaction ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@TypeMouvement", typeMvt);
                cmd.Parameters.AddWithValue("@Quantite", request.Quantite);
                cmd.Parameters.AddWithValue("@PrixUnitaire", request.PrixUnitaire);
                cmd.Parameters.AddWithValue("@Montant", montant);
                // ✅ Tracking utilisateur
                cmd.Parameters.AddWithValue("@idUtilsateur", currentUserId);

                AddParameter(cmd, "@Reference", request.Reference);
                AddParameter(cmd, "@Commentaire", request.Commentaire);
                AddParameter(cmd, "@Motif", request.Motif);
                AddParameter(cmd, "@IdArticle", request.IdArticle);

                AddParameter(cmd, "@IdStock", request.IdStock);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Mouvement stock créé: {newId}");

                return await GetByIdAsync(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création mouvement stock");
                return null;
            }
        }

        // ========================================
        // UPDATE
        // ========================================

        public async Task<MouvementStockDto?> UpdateAsync(Guid id, UpdateMouvementStockRequest request)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var whereClause = BuildWhereClause();

                using var cmd = new SqlCommand($@"
                    UPDATE MOUVEMENT_STOCK
                    SET Quantite = COALESCE(@Quantite, Quantite),
                        PrixUnitaire = COALESCE(@PrixUnitaire, PrixUnitaire),
                        Montant = COALESCE(@Quantite, Quantite) * COALESCE(@PrixUnitaire, PrixUnitaire),
                        PrixTotal = COALESCE(@Quantite, Quantite) * COALESCE(@PrixUnitaire, PrixUnitaire),
                        Commentaire = COALESCE(@Commentaire, Commentaire),
                        Motif = COALESCE(@Motif, Motif)
                    WHERE Id = @Id AND (Etat IS NULL OR Etat NOT IN ('Supprimer', 'Annulé')) {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddParameter(cmd, "@Quantite", request.Quantite);
                AddParameter(cmd, "@PrixUnitaire", request.PrixUnitaire);
                AddParameter(cmd, "@Commentaire", request.Commentaire);
                AddParameter(cmd, "@Motif", request.Motif);

                AddEntrepriseParameter(cmd);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"✅ Mouvement stock mis à jour: {id}");

                return await GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur mise à jour mouvement stock {id}");
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

                using var cmd = new SqlCommand($@"
                    UPDATE MOUVEMENT_STOCK
                    SET Etat = '{EtatSupprime}'
                    WHERE Id = @Id AND (Etat IS NULL OR Etat <> '{EtatSupprime}') {whereClause}", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                AddEntrepriseParameter(cmd);

                var lignes = await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation($"Mouvement stock supprime: {id} ({lignes} ligne(s))");

                return lignes > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression mouvement stock {id}");
                return false;
            }
        }

        /// <summary>
        /// Construit le filtre TypeMouvement. Une demande qui designe une entree ("Entree", "Entrée stock", "retour"...)
        /// ou une sortie ("Sortie", "Sortie Vente", "vente"...) retient TOUTES les variantes enregistrees en base.
        /// Sinon : chaque mot saisi doit etre contenu dans le type (LIKE %mot%, tous les mots requis).
        /// </summary>
        private static string BuildTypeFilter(string? typeMouvement, List<KeyValuePair<string, string>> parametres)
        {
            if (string.IsNullOrWhiteSpace(typeMouvement)) return "";

            var normalise = NormaliserType(typeMouvement);
            if (normalise == TypeEntreeStock) return "AND " + CondEntree;
            if (normalise == TypeSortieVente) return "AND " + CondSortie;

            var mots = typeMouvement
                .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(5)
                .ToList();
            if (mots.Count == 0) return "";

            var conditions = new List<string>();
            for (var i = 0; i < mots.Count; i++)
            {
                var nom = $"@TypeMvt{i}";
                // echappe les jokers LIKE saisis par l'utilisateur
                var motEchappe = mots[i].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
                parametres.Add(new KeyValuePair<string, string>(nom, "%" + motEchappe + "%"));
                conditions.Add($"m.TypeMouvement LIKE {nom} ESCAPE '\\'");
            }
            return "AND " + string.Join(" AND ", conditions);
        }

        private static void AddTypeParameters(SqlCommand cmd, List<KeyValuePair<string, string>> parametres)
        {
            foreach (var p in parametres)
                cmd.Parameters.AddWithValue(p.Key, p.Value);
        }

        private MouvementStockDto MapMouvement(SqlDataReader reader)
        {
            return new MouvementStockDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                DateTransaction = ReadDate(reader, "DateTransaction"),
                TypeMouvement = FormaterType(ReadNullableString(reader, "TypeMouvement")),
                Quantite = ReadDec(reader, "Quantite"),
                PrixUnitaire = ReadDec(reader, "PrixUnitaire"),
                Montant = ReadDec(reader, "Montant") ?? CalculerMontant(reader),
                PrixTotal = ReadDec(reader, "PrixTotal"),
                Reference = ReadNullableString(reader, "Reference"),
                Commentaire = ReadNullableString(reader, "Commentaire"),
                Motif = ReadNullableString(reader, "Motif"),
                Etat = ReadNullableString(reader, "Etat"),
                QuantiteAvant = ReadDec(reader, "QuantiteAvant"),
                QuantiteApres = ReadDec(reader, "QuantiteApres"),
                QuanteStockage = ReadDec(reader, "QuanteStockage"),
                EstAvarier = ReadBit(reader, "EstAvarier"),
                EstGater = ReadBit(reader, "EstGater"),
                EstMouvementMatierePremiere = ReadBit(reader, "EstMouvementMatierePremiere"),
                IdArticle = ReadNullableGuid(reader, "IdArticle"),
                IdMatierePremiere = ReadNullableGuid(reader, "IdMatierePremiere"),
                IdStock = ReadNullableGuid(reader, "IdStock"),
                IdAutresMag = ReadNullableGuid(reader, "IdAutresMag"),
                IdEntreprise = ReadNullableGuid(reader, "IdEntreprise"),
                IdDetailTransaction = ReadNullableGuid(reader, "idDetailTransaction"),
                IdUtilisateur = ReadNullableGuid(reader, "idUtilsateur"),
                ReferenceParentMvt = ReadNullableGuid(reader, "ReferenceParentMvt"),
                IdMachine = ReadNullableGuid(reader, "IdMachine"),
                IdentifientMachine = ReadInt(reader, "IdentifientMachine"),
                Utilisateur = ReadNullableString(reader, "Utilisateur"),
                NomArticle = ReadNullableString(reader, "NomArticle")
            };
        }

        /// <summary>Montant absent en base : quantite x prix unitaire (null si l'un des deux manque).</summary>
        private static decimal? CalculerMontant(SqlDataReader reader)
        {
            var q = ReadDec(reader, "Quantite");
            var p = ReadDec(reader, "PrixUnitaire");
            return q.HasValue && p.HasValue ? q.Value * p.Value : null;
        }

        /// <summary>decimal? sans exception : NULL -> null.</summary>
        private static decimal? ReadDec(SqlDataReader reader, string colonne)
        {
            var i = reader.GetOrdinal(colonne);
            return reader.IsDBNull(i) ? null : Convert.ToDecimal(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static DateTime? ReadDate(SqlDataReader reader, string colonne)
        {
            var i = reader.GetOrdinal(colonne);
            return reader.IsDBNull(i) ? null : Convert.ToDateTime(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool? ReadBit(SqlDataReader reader, string colonne)
        {
            var i = reader.GetOrdinal(colonne);
            if (reader.IsDBNull(i)) return null;
            var v = reader.GetValue(i);
            return v is bool b ? b : Convert.ToInt64(v) != 0;
        }

        private static int? ReadInt(SqlDataReader reader, string colonne)
        {
            var i = reader.GetOrdinal(colonne);
            return reader.IsDBNull(i) ? null : Convert.ToInt32(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Type renvoye par l'API : toute entree ("Entrée stock", "ENTREE", "Retour"...) -> "Entree",
        /// toute sortie ("Sortie Vente", "VENTE", "Perte"...) -> "Sortie". Type inconnu : renvoye tel quel.
        /// </summary>
        private static string? FormaterType(string? brut) => NormaliserType(brut) ?? brut;

        /// <summary>
        /// Valide / normalise le type a l'enregistrement : toute entree devient "Entree", toute sortie "Sortie"
        /// (Entree, Entrée, Entrée stock, Retour... / Sortie, Sortie Vente, Vente, Perte...). Autre : null (refuse).
        /// </summary>
        private static string? NormaliserType(string? brut)
        {
            if (string.IsNullOrWhiteSpace(brut)) return null;
            var t = brut.Trim();
            var cle = t.ToUpperInvariant().Replace("É", "E").Replace("_", " ");

            if (cle is "ENTREE" or "RETOUR" or "AJUSTEMENT POSITIF" or "ENTREE STOCK") return TypeEntreeStock;
            if (cle is "SORTIE" or "VENTE" or "SORTIE VENTE" or "PERTE" or "AJUSTEMENT NEGATIF") return TypeSortieVente;
            if (cle.StartsWith("ENTR")) return TypeEntreeStock;
            if (cle.StartsWith("SORTIE")) return TypeSortieVente;
            return null;
        }

        // ========================================
        // MÉTHODES HELPER PAGINATION
        // ========================================

        /// <summary>
        /// Crée les métadonnées de pagination
        /// </summary>
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

        /// <summary>
        /// Crée pagination vide (aucun résultat)
        /// </summary>
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