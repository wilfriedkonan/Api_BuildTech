namespace Api_BuildTech.Controllers.Authentication
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        /// <summary>Id du compte web (COMPTES_WEB.Id).</summary>
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public Guid OrganisationId { get; set; }
        /// <summary>Entreprise ACTIVE de la session (Guid.Empty si l'organisation n'en a aucune de valide).</summary>
        public Guid IdEntreprise { get; set; }
        public string? NomEntreprise { get; set; }
        public bool IsSuperAdmin { get; set; }
        /// <summary>Proprietaire | Administrateur | Gestionnaire | Lecteur</summary>
        public string? Role { get; set; }
        /// <summary>Rôle existant de la table ROLES (Owner | Manager | Cashier | Waiter), via COMPTES_WEB.IdRole. Null si IdRole absent.</summary>
        public string? RoleCaisse { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Toutes les entreprises accessibles de l'organisation (pour le sélecteur du front).</summary>
        public List<EntrepriseSummary> Entreprises { get; set; } = new();
    }

    public class EntrepriseSummary
    {
        public Guid Id { get; set; }
        public string? Designation { get; set; }
        public string? CodeEntreprise { get; set; }
    }

    public class SwitchEntrepriseRequest
    {
        public Guid IdEntreprise { get; set; }
    }

    /// <summary>OBSOLÈTE : ligne UTILISATEURS (comptes de caisse, synchronisation uniquement). Plus utilisée pour l'authentification.</summary>
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public string? Nom { get; set; }
        public string? Prenom { get; set; }
        public string MotDePasse { get; set; } = string.Empty;
        public Guid IdEntreprise { get; set; }
        public Guid? IdRole { get; set; }
        public Guid? IdPermission { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class Role
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}