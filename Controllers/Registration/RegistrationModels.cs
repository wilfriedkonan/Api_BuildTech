namespace Api_BuildTech.Controllers.Registration
{
    public class RegistrationRequest
    {
        // Informations Utilisateur Owner
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; }
        public string? Password { get; set; } = string.Empty;
        public string Nom { get; set; }
        public string? Prenom { get; set; }

        // Entreprise DEJA EXISTANTE sur la plateforme (creee en amont, ex. synchronisee depuis une caisse locale).
        // Renseigne : on cree le compte web (COMPTES_WEB) rattache a l'organisation de cette entreprise.
        //  - entreprise deja sur la plateforme : EntrepriseName, IdPlan, SubscriptionDurationMonths sont ignores ;
        //  - entreprise ABSENTE : elle est creee avec cet Id (EntrepriseName requis), avec une organisation generee
        //    automatiquement (ou IdOrganisation/OrganisationName), et sa souscription si IdPlan + duree sont fournis.
        // Reserve aux super-admins (session JWT, claim is_super_admin).
        public Guid? IdEntreprise { get; set; }

        // Informations Entreprise
        public string EntrepriseName { get; set; } = string.Empty;
        public string? Contact { get; set; }
        public string? Localisation { get; set; }
        public string? Pays { get; set; }
        public string? Ville { get; set; }
        public string? Commune { get; set; }

        // Informations Organisation
        public Guid? IdOrganisation { get; set; } // Si null, nouvelle organisation sera créée
        public string? OrganisationName { get; set; } // Utilisé si nouvelle organisation

        // Informations Souscription
        public Guid IdPlan { get; set; }
        public int SubscriptionDurationMonths { get; set; } = 0; 

        // Métadonnées
        public string? ReferralCode { get; set; }
        public string? Source { get; set; } // web, mobile, local
    }

    /// <summary>
    /// Résultat de l'inscription
    /// </summary>
    public class RegistrationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        // Code de refus lisible par le front (mode "entreprise existante") :
        // ENTREPRISE_SUPPRIMEE | ORGANISATION_INTROUVABLE | PLAN_INVALIDE | PROPRIETAIRE_EXISTANT | EMAIL_UTILISE | DONNEES_INVALIDES | ERREUR
        public string? ErrorCode { get; set; }

        // Mode "entreprise existante" : ce qui a été créé automatiquement
        public bool EntrepriseCreee { get; set; }
        public bool OrganisationCreee { get; set; }

        // IDs créés
        public Guid? OrganisationId { get; set; }
        public Guid? EntrepriseId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? SubscriptionId { get; set; }

        // Informations de connexion
        public string? ValidationToken { get; set; }
        public string? CodeEntreprise { get; set; }

        // Informations d'abonnement
        public string? PlanName { get; set; }
        public DateTime? SubscriptionExpiresAt { get; set; }

        // Credentials pour version locale
        public LocalInstallationCredentials? LocalCredentials { get; set; }
    }

    /// <summary>
    /// Credentials pour installation locale
    /// </summary>
    public class LocalInstallationCredentials
    {
        public string DatabaseConnectionString { get; set; } = string.Empty;
        public string SyncApiKey { get; set; } = string.Empty;
        public string SyncEndpoint { get; set; } = string.Empty;
        public Guid EntrepriseId { get; set; }
    }
}