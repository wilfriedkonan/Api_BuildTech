namespace Api_BuildTech.Controllers.Fournisseurs
{
    /// <summary>
    /// DTO Fournisseur
    /// </summary>
    public class FournisseurDto
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Nom { get; set; }  // Nom du fournisseur (raison sociale)
        public string? Specialite { get; set; }
        public string? Contact { get; set; }  // Téléphone principal
        public string? Email { get; set; }
        public string? NRC { get; set; }  // Numéro Registre Commerce
        public Guid? IdEntreprise { get; set; }
        public string? Etat { get; set; }  // Actif/Inactif/Supprimer
        public string? Adresse { get; set; }
        public bool? Statut { get; set; }  

        // Tracking
        public DateTime? DateCreate { get; set; }
        public Guid? idCreateUser { get; set; }
        public string? NomCreateUser { get; set; }
        public DateTime? DateLastUpdate { get; set; }
        public Guid? idLastUpdateUser { get; set; }
        public string? NomLastUpdateUser { get; set; }
    }

    /// <summary>
    /// Requête de création de fournisseur
    /// </summary>
    public class CreateFournisseurRequest
    {
        public string? Code { get; set; }
        public string Nom { get; set; } = string.Empty;  // Requis
        public string? Specialite { get; set; }
        public string? Contact { get; set; }
        public string? Email { get; set; }
        public string? NRC { get; set; }
        public string? Etat { get; set; } = "Actif";
        public string? Adresse { get; set; }
        public bool? Statut { get; set; } = true;
    }

    /// <summary>
    /// Requête de mise à jour de fournisseur
    /// </summary>
    public class UpdateFournisseurRequest
    {
        public string? Code { get; set; }
        public string? Nom { get; set; }
        public string? Specialite { get; set; }
        public string? Contact { get; set; }
        public string? Email { get; set; }
        public string? NRC { get; set; }
        public string? Etat { get; set; }
        public string? Adresse { get; set; }
        public bool? Statut { get; set; }
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
    /// Réponse liste fournisseurs avec pagination
    /// </summary>
    public class FournisseurListResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
        public int TotalActifs { get; set; }
        public int TotalInactifs { get; set; }
        public List<FournisseurDto> Fournisseurs { get; set; } = new();

        // ✅ AJOUTÉ : Pagination
        public PaginationMetadata? Pagination { get; set; }
    }
}