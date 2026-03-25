using System;
using System.Collections.Generic;

namespace Api_BuildTech.Controllers.ArticleStock
{
    /// <summary>
    /// DTO pour la vue V_STOCK_ARTICLES_ENTREPRISE (ENRICHIE)
    /// Cette vue contient maintenant TOUTES les informations d'un article
    /// </summary>
    public class StockArticleViewDto
    {
        // ========================================
        // IDENTIFIANTS
        // ========================================
        public Guid IdEntreprise { get; set; }
        public Guid IdArticle { get; set; }
        public string? CodeArticle { get; set; }
        public string? CodeBarre { get; set; }

        // ========================================
        // INFORMATIONS ARTICLE
        // ========================================
        public string? Designation { get; set; }
        public string? Description { get; set; }
        public string? DesignationCategorie { get; set; }
        public Guid? IdCathegorie { get; set; }
        public Guid? IdType_Repas { get; set; }
        public string? TypeRepas { get; set; }
        public string? ImageURL { get; set; }

        // ========================================
        // PRIX
        // ========================================
        public decimal? PrixAchat { get; set; }
        public decimal? PrixVente { get; set; }
        public decimal? PrixExterieur { get; set; }
        public decimal? PrixPromo { get; set; }
        public decimal? TauxTva { get; set; }

        // ========================================
        // STOCK
        // ========================================
        public decimal Stock { get; set; }              // Stock initial
        public decimal StockActuel { get; set; }        // Stock calculé (Initial + Entrées - Sorties)
        public decimal SeuilAlerte { get; set; }
        public decimal SeuilStock { get; set; }
        public string? StatutStock { get; set; }        // "En stock", "Alerte", "Rupture"

        // ========================================
        // ÉTATS ET OPTIONS
        // ========================================
        public string? Etat { get; set; }               // "Actif", "Inactif", "Supprimer"
        public bool? Statut { get; set; }
        public bool? EstPos { get; set; }
        public bool? EstStockable { get; set; }
        public bool? EstEnStock { get; set; }
        public bool? EstEnPorter { get; set; }
        public bool? EstExonerer { get; set; }
        public bool? EstPromo { get; set; }
        public bool? EstComposer { get; set; }
        public bool? EstVendableSansComposition { get; set; }
        public bool? AfficherStockPOS { get; set; }
        public string? position { get; set; }
        public DateTime? DatePerenption { get; set; }

        // ========================================
        // TRACKING UTILISATEUR
        // ========================================
        public DateTime? DateCreate { get; set; }
        public Guid? idCreateUser { get; set; }
        public string? NomCreateur { get; set; }
        public string? PrenomCreateur { get; set; }
        public DateTime? DateLastUpdate { get; set; }
        public Guid? idLastUpdateUser { get; set; }
        public string? NomModificateur { get; set; }
        public string? PrenomModificateur { get; set; }

        // ========================================
        // PROPRIÉTÉS CALCULÉES POUR L'UI
        // ========================================
        public bool EstEnAlerte => StatutStock == "Alerte" || StatutStock == "Rupture";
        public bool EstEnRupture => StatutStock == "Rupture";
        public decimal TauxStock => SeuilStock > 0 ? (StockActuel / SeuilStock) * 100 : 0;
        public decimal ValeurStock => StockActuel * (PrixAchat ?? 0);
        public string NomCompletCreateur => $"{NomCreateur} {PrenomCreateur}".Trim();
        public string NomCompletModificateur => $"{NomModificateur} {PrenomModificateur}".Trim();
    }

    /// <summary>
    /// Requête pour filtrer les stocks
    /// </summary>
    public class StockArticleFilterRequest
    {
        /// <summary>
        /// Filtrer par statut stock: "En stock", "Alerte", "Rupture"
        /// </summary>
        public string? StatutStock { get; set; }

        /// <summary>
        /// Filtrer par état: "Actif", "Inactif"
        /// </summary>
        public string? Etat { get; set; }

        /// <summary>
        /// Filtrer par catégorie
        /// </summary>
        public string? DesignationCategorie { get; set; }

        /// <summary>
        /// Recherche par désignation article ou code article
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Stock actuel minimum
        /// </summary>
        public decimal? StockMin { get; set; }

        /// <summary>
        /// Stock actuel maximum
        /// </summary>
        public decimal? StockMax { get; set; }

        /// <summary>
        /// Afficher uniquement les alertes (Alerte + Rupture)
        /// </summary>
        public bool? AlertesOnly { get; set; }

        /// <summary>
        /// Filtrer par articles POS uniquement
        /// </summary>
        public bool? EstPosOnly { get; set; }

        /// <summary>
        /// Filtrer par articles stockables uniquement
        /// </summary>
        public bool? EstStockableOnly { get; set; }

        /// <summary>
        /// Filtrer par articles en promo uniquement
        /// </summary>
        public bool? EstPromoOnly { get; set; }

        /// <summary>
        /// Trier par: "designation", "stock", "categorie", "statut", "prix", "valeur"
        /// </summary>
        public string OrderBy { get; set; } = "designation";

        /// <summary>
        /// Ordre: "asc" ou "desc"
        /// </summary>
        public string OrderDirection { get; set; } = "asc";

        /// <summary>
        /// Pagination
        /// </summary>
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
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
    /// Réponse liste stocks avec pagination
    /// </summary>
    public class StockArticleListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int TotalEnStock { get; set; }
        public int TotalEnAlerte { get; set; }
        public int TotalEnRupture { get; set; }
        public decimal ValeurStockTotal { get; set; }
        public List<StockArticleViewDto> Stocks { get; set; } = new();
        public PaginationMetadata? Pagination { get; set; }
    }

    /// <summary>
    /// Statistiques globales de stock
    /// </summary>
    public class StockStatisticsDto
    {
        public int TotalArticles { get; set; }
        public int TotalEnStock { get; set; }
        public int TotalEnAlerte { get; set; }
        public int TotalEnRupture { get; set; }
        public int TotalStockables { get; set; }
        public int TotalPos { get; set; }
        public int TotalPromo { get; set; }
        public decimal StockTotalUnites { get; set; }
        public decimal ValeurStockTotal { get; set; }
        public decimal PourcentageEnStock { get; set; }
        public decimal PourcentageEnAlerte { get; set; }
        public decimal PourcentageEnRupture { get; set; }

        // Top 5
        public List<StockArticleViewDto> Top5Alertes { get; set; } = new();
        public List<StockArticleViewDto> Top5Ruptures { get; set; } = new();
        public List<StockArticleViewDto> Top5ValeurStock { get; set; } = new();
    }
}