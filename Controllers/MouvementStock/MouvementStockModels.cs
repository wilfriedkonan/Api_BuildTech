namespace Api_BuildTech.Controllers.MouvementStock
{
    /// <summary>
    /// DTO Mouvement de Stock (colonnes de dbo.MOUVEMENT_STOCK)
    /// </summary>
    public class MouvementStockDto
    {
        public Guid Id { get; set; }
        public DateTime? DateTransaction { get; set; }
        public string? TypeMouvement { get; set; }
        public decimal? Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public decimal? Montant { get; set; }
        public decimal? PrixTotal { get; set; }
        public string? Reference { get; set; }
        public string? Commentaire { get; set; }
        public string? Motif { get; set; }

        // Etat du mouvement ("Supprimer" = suppression logique ; la table n'a pas de colonne EstSupprimer)
        public string? Etat { get; set; }
        /// <summary>Conserve pour les clients existants : calcule a partir de Etat.</summary>
        public bool? EstSupprimer => Etat == null ? null : string.Equals(Etat, "Supprimer", StringComparison.OrdinalIgnoreCase);

        // Stock avant / apres le mouvement
        public decimal? QuantiteAvant { get; set; }
        public decimal? QuantiteApres { get; set; }
        public decimal? QuanteStockage { get; set; }

        // Indicateurs
        public bool? EstAvarier { get; set; }
        public bool? EstGater { get; set; }
        public bool? EstMouvementMatierePremiere { get; set; }

        // Liens
        public Guid? IdArticle { get; set; }
        public Guid? IdMatierePremiere { get; set; }
        public Guid? IdStock { get; set; }
        public Guid? IdAutresMag { get; set; }
        public Guid? IdEntreprise { get; set; }
        public Guid? IdDetailTransaction { get; set; }
        public Guid? IdUtilisateur { get; set; }          // colonne idUtilsateur (faute d'origine dans la table)
        public Guid? ReferenceParentMvt { get; set; }     // uniqueidentifier en base
        public Guid? IdMachine { get; set; }
        public int? IdentifientMachine { get; set; }
        public string? Utilisateur { get; set; }

        // Relations
        public string? NomArticle { get; set; }
        public string? NomMatiere { get; set; }           // non renseigne (table matiere premiere non fournie)
    }

    /// <summary>
    /// Requête de création de mouvement
    /// </summary>
    public class CreateMouvementStockRequest
    {
        public DateTime? DateTransaction { get; set; }    // absent : date du jour
        public string TypeMouvement { get; set; } = string.Empty; // "Entree" ou "Sortie" (autres libelles convertis : Entrée stock, Sortie Vente, Vente...)
        public decimal Quantite { get; set; }
        public decimal PrixUnitaire { get; set; }
        public string? Reference { get; set; }
        public string? Commentaire { get; set; }
        public string? Motif { get; set; }

        public Guid? IdArticle { get; set; }
        public Guid? IdStock { get; set; }
        /// <summary>Ignore : l'entreprise vient toujours du jeton de l'utilisateur connecte.</summary>
        public Guid IdEntreprise { get; set; }
    }

    /// <summary>
    /// Requête de mise à jour de mouvement
    /// </summary>
    public class UpdateMouvementStockRequest
    {
        public decimal? Quantite { get; set; }
        public decimal? PrixUnitaire { get; set; }
        public string? Commentaire { get; set; }
        public string? Motif { get; set; }
    }

    /// <summary>
    /// Métadonnées de pagination
    /// </summary>
    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public bool HasPrevious { get; set; }
        public bool HasNext { get; set; }
    }

    /// <summary>
    /// Réponse liste mouvements avec pagination
    /// </summary>
    public class MouvementStockListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public decimal TotalEntrees { get; set; }
        public decimal TotalSorties { get; set; }
        public decimal ValeurEntrees { get; set; }
        public decimal ValeurSorties { get; set; }
        public decimal SoldeQuantite { get; set; }
        public decimal SoldeValeur { get; set; }
        public List<MouvementStockDto> Mouvements { get; set; } = new();

        public PaginationMetadata? Pagination { get; set; }
    }
}