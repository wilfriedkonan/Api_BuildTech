namespace Api_BuildTech.Controllers.Otp
{
    /// <summary>
    /// Requête pour générer un OTP
    /// </summary>
    public class GenerateOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Purpose { get; set; } = "REGISTRATION"; // REGISTRATION, LOGIN, PASSWORD_RESET
    }

    /// <summary>
    /// Réponse après génération d'OTP
    /// </summary>
    public class GenerateOtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public int RemainingAttempts { get; set; }

        // Pour DEBUG uniquement - à retirer en production
        public string? DebugCode { get; set; }
    }

    /// <summary>
    /// Requête pour valider un OTP
    /// </summary>
    public class ValidateOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Purpose { get; set; } = "REGISTRATION";
    }

    /// <summary>
    /// Réponse après validation d'OTP
    /// </summary>
    public class ValidateOtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public int RemainingAttempts { get; set; }
    }

    /// <summary>
    /// Modèle OTP interne
    /// </summary>
    public class OtpCode
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public string? IpAddress { get; set; }
    }
}