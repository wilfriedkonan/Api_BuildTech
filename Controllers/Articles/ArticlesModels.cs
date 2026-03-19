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
        public bool? AfficherStockPOS { get; set; }

        // ✅ AJOUTÉ : Stock calculé automatiquement
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

        // Autres
        public decimal? TauxTva { get; set; }
        public string? ImageURL { get; set; }
        public bool? Statut { get; set; }

        // ✅ AJOUTÉ : Tracking utilisateur
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

        // ✅ AJOUTÉ : Métadonnées de pagination
        public PaginationMetadata? Pagination { get; set; }
    }

    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }      // Page actuelle
        public int PageSize { get; set; }         // Éléments par page
        public int TotalPages { get; set; }       // Nombre total de pages
        public int TotalRecords { get; set; }     // Total enregistrements
        public bool HasPrevious { get; set; }     // Peut aller à page précédente
        public bool HasNext { get; set; }         // Peut aller à page suivante
    }
}