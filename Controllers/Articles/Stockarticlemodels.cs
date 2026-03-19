using System;
using System.Collections.Generic;

namespace Api_BuildTech.Controllers.Articles
{
    /// <summary>
    /// DTO pour la vue V_STOCK_ARTICLES_ENTREPRISE
    /// </summary>
    public class StockArticleViewDto
    {
        public Guid IdEntreprise { get; set; }
        public Guid IdArticle { get; set; }
        public string? DesignationArticle { get; set; }
        public string? DesignationCategorie { get; set; }
        public decimal StockActuel { get; set; }
        public decimal SeuilStock { get; set; }
        public string? StatutStock { get; set; }

        // Propriétés calculées pour l'UI
        public bool EstEnAlerte => StatutStock == "Alerte" || StatutStock == "Rupture";
        public bool EstEnRupture => StatutStock == "Rupture";
        public decimal TauxStock => SeuilStock > 0 ? (StockActuel / SeuilStock) * 100 : 0;
    }

    /// <summary>
    /// Requête pour filtrer les stocks
    /// </summary>
    public class StockArticleFilterRequest
    {
        /// <summary>
        /// Filtrer par statut: "En stock", "Alerte", "Rupture"
        /// </summary>
        public string? StatutStock { get; set; }

        /// <summary>
        /// Filtrer par catégorie
        /// </summary>
        public string? DesignationCategorie { get; set; }

        /// <summary>
        /// Recherche par désignation article
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
        /// Trier par: "designation", "stock", "categorie", "statut"
        /// </summary>
        public string OrderBy { get; set; } = "designation";

        /// <summary>
        /// Ordre: "asc" ou "desc"
        /// </summary>
        public string OrderDirection { get; set; } = "asc";
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
        public decimal StockTotalUnites { get; set; }
        public decimal ValeurStockTotal { get; set; }
        public decimal PourcentageEnStock { get; set; }
        public decimal PourcentageEnAlerte { get; set; }
        public decimal PourcentageEnRupture { get; set; }

        // Top 5
        public List<StockArticleViewDto> Top5Alertes { get; set; } = new();
        public List<StockArticleViewDto> Top5Ruptures { get; set; } = new();
    }
}