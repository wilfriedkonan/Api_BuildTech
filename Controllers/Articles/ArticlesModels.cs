using System;
using System.Collections.Generic;

namespace Api_BuildTech.Controllers.Articles
{
    /// <summary>
    /// DTO Article avec stock calculé automatiquement
    /// </summary>
    public class ArticleDto
    {
        public Guid Id { get; set; }
        public string? CodeArticle { get; set; }
        public string? Designation { get; set; }
        public string? Description { get; set; }
        public string? CodeBarre { get; set; }

        // Prix
        public decimal? PrixAchat { get; set; }
        public decimal? PrixVente { get; set; }
        public decimal? PrixExterieur { get; set; }

        // Configuration POS
        public bool? EstPos { get; set; }
        public int? Position { get; set; }
        public bool? EstExonerer { get; set; }

        // Type & État
        public string? TypeRepas { get; set; }
        public string? Etat { get; set; }
        public DateTime? DatePerenption { get; set; }

        // Stock
        public bool? EstStockable { get; set; }
        public bool? EstEnStock { get; set; }
        public bool? EstEnPorter { get; set; }
        public decimal? Stock { get; set; }
        public decimal? SeuilAlerte { get; set; }
        public string? AfficherStockPOS { get; set; }   // nvarchar(50) en base (ex. "Visible")

        // ✅ Stock calculé automatiquement
        public decimal StockActuel { get; set; }
        public bool AlerteStock { get; set; } // true si stock < seuil

        // Relations
        public Guid? IdEntreprise { get; set; }
        public Guid? IdCathegorie { get; set; }
        public Guid? IdType_Repas { get; set; }
        public Guid? IdStock { get; set; }
        public string? NomCategorie { get; set; }

        // Promo
        public decimal? PrixPromo { get; set; }
        public bool? EstPromo { get; set; }

        // Composition
        public bool? EstComposer { get; set; }
        public bool? EstVendableSansComposition { get; set; }

        // ✅ AJOUTÉ: Visible au catalogue
        public bool? EstVisibleCatalogue { get; set; }

        // Autres
        public decimal? TauxTva { get; set; }
        public string? ImageURL { get; set; }
        public bool? Statut { get; set; }

        // ✅ Tracking utilisateur
        public DateTime? DateCreate { get; set; }
        public Guid? idCreateUser { get; set; }
        public string? NomCreateUser { get; set; }
        public DateTime? DateLastUpdate { get; set; }
        public Guid? idLastUpdateUser { get; set; }
        public string? NomLastUpdateUser { get; set; }
    }

    /// <summary>
    /// Requête de création d'article
    /// </summary>
    public class CreateArticleRequest
    {
        public string? CodeArticle { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CodeBarre { get; set; }

        // Prix
        public decimal PrixAchat { get; set; } = 0;
        public decimal PrixVente { get; set; } = 0;
        public decimal? PrixExterieur { get; set; }

        // Configuration
        public Guid? IdCathegorie { get; set; }
        public bool EstPos { get; set; } = true;
        public bool EstStockable { get; set; } = false;
        public string? Etat { get; set; } = "Actif";

        // Stock
        public decimal Stock { get; set; } = 0;
        public decimal SeuilAlerte { get; set; } = 0;

        // ✅ AJOUTÉ: Visible au catalogue (par défaut true)
        public bool EstVisibleCatalogue { get; set; } = true;

        // Autres
        public decimal TauxTva { get; set; } = 0;
        public string? ImageURL { get; set; }
        public bool Statut { get; set; } = true;
    }

    /// <summary>
    /// Requête de mise à jour d'article
    /// </summary>
    public class UpdateArticleRequest
    {
        public string? CodeArticle { get; set; }
        public string? Designation { get; set; }
        public string? Description { get; set; }
        public string? CodeBarre { get; set; }

        // Prix
        public decimal? PrixAchat { get; set; }
        public decimal? PrixVente { get; set; }
        public decimal? PrixExterieur { get; set; }

        // Configuration
        public Guid? IdCathegorie { get; set; }
        public bool? EstPos { get; set; }
        public int? Position { get; set; }
        public bool? EstExonerer { get; set; }
        public string? Etat { get; set; }

        // Promo
        public bool? EstPromo { get; set; }
        public decimal? PrixPromo { get; set; }

        // Composition
        public bool? EstComposer { get; set; }
        public bool? EstVendableSansComposition { get; set; }

        // Stock
        public decimal? Stock { get; set; }
        public decimal? SeuilAlerte { get; set; }

        // ✅ AJOUTÉ: Visible au catalogue
        public bool? EstVisibleCatalogue { get; set; }

        // Autres
        public decimal? TauxTva { get; set; }
        public string? ImageURL { get; set; }
        public bool? Statut { get; set; }
    }

    /// <summary>
    /// Réponse liste d'articles
    /// </summary>
    public class ArticleListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int TotalStockables { get; set; }
        public int TotalEnAlerte { get; set; }
        public List<ArticleDto> Articles { get; set; } = new();

        // ✅ Métadonnées de pagination
        public PaginationMetadata? Pagination { get; set; }
    }

    /// <summary>
    /// Métadonnées de pagination
    /// </summary>
    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }      // Page actuelle
        public int PageSize { get; set; }         // Éléments par page
        public int TotalPages { get; set; }       // Nombre total de pages
        public int TotalRecords { get; set; }     // Total enregistrements
        public bool HasPrevious { get; set; }     // Peut aller à page précédente
        public bool HasNext { get; set; }         // Peut aller à page suivante
    }

    /// <summary>
    /// Requête de filtres pour GET all articles
    /// </summary>
    public class GetArticlesFilterRequest
    {
        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        // Filtres
        public string? SearchTerm { get; set; }           // Recherche dans Designation/CodeArticle
        public Guid? IdCathegorie { get; set; }          // Filtre par catégorie
        public bool? EstStockable { get; set; }          // Filtre articles stockables
        public bool? EstVisibleCatalogue { get; set; }   // Filtre articles visibles
        public bool? EstPos { get; set; }                // Filtre articles POS
        public bool? EstPromo { get; set; }              // Filtre articles en promo
        public string? Etat { get; set; }                // Filtre par état (Actif, Inactif, etc.)
        public decimal? PrixVenteMin { get; set; }       // Filtre prix min
        public decimal? PrixVenteMax { get; set; }       // Filtre prix max

        // Tri
        public string OrderBy { get; set; } = "designation";  // Champ de tri (designation, prixvente, etc.)
        public string OrderDirection { get; set; } = "asc";   // asc ou desc
    }

    public class CatalogueCategory
    {
        public Guid IdCategorie { get; set; }
        public string NomCategorie { get; set; } = string.Empty;
        public int OrdreCategorie { get; set; }
        public string? ImageURL { get; set; }
        public string? Description { get; set; }

        // Statistiques
        public int NombreArticles { get; set; }
        public int NombreArticlesPromo { get; set; }
        public decimal PrixMin { get; set; }
        public decimal PrixMax { get; set; }
        public decimal PrixMoyen { get; set; }
    }

    /// <summary>
    /// DTO pour article dans le catalogue public
    /// Version allégée (sans données internes)
    /// </summary>
    public class CatalogueArticle
    {
        public Guid IdArticle { get; set; }
        public string? CodeArticle { get; set; }
        public string NomArticle { get; set; } = string.Empty;
        public string? DescriptionArticle { get; set; }
        public string? CodeBarre { get; set; }

        // Prix
        public decimal PrixVente { get; set; }
        public decimal? PrixAchat { get; set; }     // Non exposé au public
        public decimal? PrixExterieur { get; set; }
        public decimal? PrixPromo { get; set; }
        public bool EstPromo { get; set; }

        // Stock
        public decimal Stock { get; set; }
        public decimal? SeuilAlerte { get; set; }  // Non exposé au public
        public bool EstStockable { get; set; }
        public bool EstPos { get; set; }
        public string? AfficherStockPOS { get; set; }   // nvarchar(50) en base (ex. "Visible")

        // Présentation
        public string? ImageURL { get; set; }
        public int Position { get; set; }
        public decimal TauxTva { get; set; }

        // Catégorie
        public string? NomCategorie { get; set; }
        public Guid IdCategorie { get; set; }

        // Dates
        public DateTime? DateCreate { get; set; }
        public DateTime? DateLastUpdate { get; set; }
    }

    /// <summary>
    /// Requête de filtre pour articles catalogue
    /// </summary>
    public class CatalogueArticleFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Guid? IdCategorie { get; set; }
        public string? SearchTerm { get; set; }
        public bool? EstPromo { get; set; }
        public decimal? PrixMin { get; set; }
        public decimal? PrixMax { get; set; }
        public string OrderBy { get; set; } = "designation";      // designation, prix, date, position
        public string OrderDirection { get; set; } = "asc";        // asc, desc
    }

    /// <summary>
    /// Réponse pour catégories
    /// </summary>
    public class CatalogueResponseDto
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public PaginationMetadata? Pagination { get; set; }
        public List<CatalogueCategory> Categories { get; set; } = new();
    }

    /// <summary>
    /// Réponse pour articles
    /// </summary>
    public class CatalogueArticlesResponseDto
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public string? CategoryName { get; set; }  // Nom catégorie si filtre par catégorie
        public PaginationMetadata? Pagination { get; set; }
        public List<CatalogueArticle> Articles { get; set; } = new();
    }
}