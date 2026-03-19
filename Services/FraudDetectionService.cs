using Api_BuildTech.Services;
using Microsoft.Data.SqlClient;

namespace Api_BuildTech.Services
{
    public class FraudDetectionService : DatabaseService
    {
        public FraudDetectionService(
            string connectionString,
            ILogger<FraudDetectionService> logger)
            : base(connectionString, logger)
        {
        }

        /// <summary>
        /// Détecte les activités suspectes pour une entreprise
        /// </summary>
        public async Task<FraudDetectionResult> CheckSuspiciousActivityAsync(Guid idEntreprise)
        {
            var result = new FraudDetectionResult
            {
                IdEntreprise = idEntreprise,
                Alerts = new List<FraudAlert>()
            };

            try
            {
                // 1. Vérifier nombre d'utilisateurs vs limite
                await CheckUserLimitAsync(idEntreprise, result);

                // 2. Vérifier nombre de factures vs limite
                await CheckInvoiceLimitAsync(idEntreprise, result);

                // 3. Vérifier validations multiples simultanées
                await CheckExcessiveValidationsAsync(idEntreprise, result);

                // 4. Vérifier tokens révoqués utilisés
                await CheckRevokedTokenUsageAsync(idEntreprise, result);

                result.HasFraud = result.Alerts.Count > 0;

                if (result.HasFraud)
                {
                    // Logger et alerter admin
                    await LogFraudAlertAsync(result);

                    // Révoquer tous les tokens si fraude grave
                    if (result.Alerts.Any(a => a.Severity == "Critical"))
                    {
                        await RevokeAllTokensAsync(idEntreprise);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur détection fraude {idEntreprise}");
            }

            return result;
        }

        #region Vérifications Spécifiques

        private async Task CheckUserLimitAsync(Guid idEntreprise, FraudDetectionResult result)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    (SELECT COUNT(*) FROM UTILISATEURS WHERE IdEntreprise = @IdEntreprise AND IsActive = 1) AS CurrentUsers,
                    p.MaxUsers
                FROM SUBSCRIPTIONS s
                INNER JOIN PLANS p ON s.IdPlan = p.Id
                WHERE s.IdEntreprise = @IdEntreprise
                AND s.Status = 'Active'", conn);

            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                int currentUsers = reader.GetInt32(0);
                int maxUsers = reader.GetInt32(1);

                // Alerte si dépassement de 20%
                if (currentUsers > maxUsers * 1.2)
                {
                    result.Alerts.Add(new FraudAlert
                    {
                        Type = "UserLimitExceeded",
                        Severity = "High",
                        Message = $"Nombre d'utilisateurs suspect : {currentUsers} (limite {maxUsers})",
                        CurrentValue = currentUsers,
                        ExpectedLimit = maxUsers
                    });
                }
            }
        }

        private async Task CheckInvoiceLimitAsync(Guid idEntreprise, FraudDetectionResult result)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    (SELECT COUNT(*) FROM FACTURE 
                     WHERE IdEntreprise = @IdEntreprise 
                     AND MONTH(Date) = MONTH(GETUTCDATE()) 
                     AND YEAR(Date) = YEAR(GETUTCDATE())) AS InvoicesThisMonth,
                    p.MaxInvoicesPerMonth
                FROM SUBSCRIPTIONS s
                INNER JOIN PLANS p ON s.IdPlan = p.Id
                WHERE s.IdEntreprise = @IdEntreprise
                AND s.Status = 'Active'", conn);

            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                int invoicesThisMonth = reader.GetInt32(0);
                int maxInvoices = reader.GetInt32(1);

                // Alerte si dépassement de 50% (et pas illimité)
                if (maxInvoices != -1 && invoicesThisMonth > maxInvoices * 1.5)
                {
                    result.Alerts.Add(new FraudAlert
                    {
                        Type = "InvoiceLimitExceeded",
                        Severity = "Critical",
                        Message = $"Nombre de factures suspect : {invoicesThisMonth} (limite {maxInvoices})",
                        CurrentValue = invoicesThisMonth,
                        ExpectedLimit = maxInvoices
                    });
                }
            }
        }

        private async Task CheckExcessiveValidationsAsync(Guid idEntreprise, FraudDetectionResult result)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT COUNT(*) 
                FROM SUBSCRIPTION_VALIDATIONS
                WHERE IdEntreprise = @IdEntreprise
                AND CreatedAt > DATEADD(MINUTE, -5, GETUTCDATE())", conn);

            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            await conn.OpenAsync();
            int recentValidations = (int)await cmd.ExecuteScalarAsync();

            // Alerte si plus de 10 validations en 5 minutes
            if (recentValidations > 10)
            {
                result.Alerts.Add(new FraudAlert
                {
                    Type = "ExcessiveValidations",
                    Severity = "Medium",
                    Message = $"Trop de validations en peu de temps : {recentValidations}",
                    CurrentValue = recentValidations,
                    ExpectedLimit = 10
                });
            }
        }

        private async Task CheckRevokedTokenUsageAsync(Guid idEntreprise, FraudDetectionResult result)
        {
            // TODO: Implémenter si nécessaire
            await Task.CompletedTask;
        }

        #endregion

        #region Actions Fraude

        private async Task LogFraudAlertAsync(FraudDetectionResult result)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                INSERT INTO FRAUD_ALERTS (Id, IdEntreprise, AlertType, Severity, Message, DetectedAt)
                VALUES (@Id, @IdEntreprise, @AlertType, @Severity, @Message, GETUTCDATE())", conn);

            await conn.OpenAsync();

            foreach (var alert in result.Alerts)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("@IdEntreprise", result.IdEntreprise);
                cmd.Parameters.AddWithValue("@AlertType", alert.Type);
                cmd.Parameters.AddWithValue("@Severity", alert.Severity);
                cmd.Parameters.AddWithValue("@Message", alert.Message);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogWarning($"FRAUDE DÉTECTÉE: {alert.Severity} - {alert.Message}");
            }
        }

        private async Task RevokeAllTokensAsync(Guid idEntreprise)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE SUBSCRIPTION_VALIDATIONS
                SET IsRevoked = 1
                WHERE IdEntreprise = @IdEntreprise
                AND IsRevoked = 0", conn);

            cmd.Parameters.AddWithValue("@IdEntreprise", idEntreprise);

            await conn.OpenAsync();
            int revoked = await cmd.ExecuteNonQueryAsync();

            _logger.LogWarning($"Révocation de {revoked} tokens pour entreprise {idEntreprise} (fraude détectée)");
        }

        #endregion
    }

    #region Models

    public class FraudDetectionResult
    {
        public Guid IdEntreprise { get; set; }
        public bool HasFraud { get; set; }
        public List<FraudAlert> Alerts { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    public class FraudAlert
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // Low, Medium, High, Critical
        public string Message { get; set; } = string.Empty;
        public int? CurrentValue { get; set; }
        public int? ExpectedLimit { get; set; }
    }

    #endregion
}