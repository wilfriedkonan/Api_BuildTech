namespace Api_BuildTech.Controllers.Subscription
{
    /// <summary>
    /// Requête de validation d'abonnement
    /// </summary>
    public class ValidateSubscriptionRequest
    {
        public Guid IdEntreprise { get; set; }
        public string ApiKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Réponse de validation d'abonnement
    /// </summary>
    public class SubscriptionValidationResponse
    {
        public bool IsValid { get; set; }
        public string? Message { get; set; }
        public string? ValidationToken { get; set; }  // JWT signé
        public string? PlanName { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public SubscriptionLimits? Limits { get; set; }
        public bool BlockAccess { get; set; }
    }

    /// <summary>
    /// Limites d'abonnement
    /// </summary>
    public class SubscriptionLimits
    {
        public int MaxUsers { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public int MaxStorageMB { get; set; }
        public bool HasUnlimitedInvoices => MaxInvoicesPerMonth == -1;
        public bool HasUnlimitedStorage => MaxStorageMB == -1;
    }

    /// <summary>
    /// Requête de validation d'opération critique
    /// </summary>
    public class ValidateOperationRequest
    {
        public Guid IdEntreprise { get; set; }
        public string OperationType { get; set; } = string.Empty; // AddUser, CreateInvoice, Upload
        public string ValidationToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Réponse de validation d'opération
    /// </summary>
    public class ValidateOperationResponse
    {
        public bool IsAllowed { get; set; }
        public string? Message { get; set; }
        public int? CurrentUsage { get; set; }
        public int? Limit { get; set; }
        public int? Remaining { get; set; }
    }

    /// <summary>
    /// Informations d'usage de l'abonnement
    /// </summary>
    public class SubscriptionUsage
    {
        public Guid IdEntreprise { get; set; }
        public int CurrentUsers { get; set; }
        public int MaxUsers { get; set; }
        public int InvoicesThisMonth { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public int StorageUsedMB { get; set; }
        public int MaxStorageMB { get; set; }
        public int RemainingUsers => MaxUsers - CurrentUsers;
        public int RemainingInvoices => MaxInvoicesPerMonth == -1 ? -1 : MaxInvoicesPerMonth - InvoicesThisMonth;
        public int RemainingStorageMB => MaxStorageMB == -1 ? -1 : MaxStorageMB - StorageUsedMB;
    }

    /// <summary>
    /// Plan d'abonnement (SERVER-ONLY)
    /// </summary>
    public class Plan
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int MaxUsers { get; set; }
        public int MaxInvoicesPerMonth { get; set; }
        public int MaxStorageMB { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Abonnement actif (SERVER-ONLY)
    /// </summary>
    public class Subscription
    {
        public Guid Id { get; set; }
        public Guid IdEntreprise { get; set; }
        public Guid IdPlan { get; set; }
        public string Status { get; set; } = string.Empty; // Active, Expired, Suspended
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool? LastValidated { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Token de validation d'abonnement
    /// </summary>
    public class SubscriptionValidationToken
    {
        public Guid Id { get; set; }
        public Guid IdEntreprise { get; set; }
        public string ValidationToken { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}