using Microsoft.Data.SqlClient;
using Api_BuildTech.Services.messagerie;

namespace Api_BuildTech.Controllers.Otp
{
    public class OtpService
    {
        private readonly string _connectionString;
        private readonly ILogger<OtpService> _logger;
        private readonly SmtpEmailService _emailService;
        private readonly Whatsappservice _whatsappService;
        private readonly IConfiguration _configuration;

        // Configuration OTP
        private readonly int _otpLength = 6;
        private readonly int _otpExpirationMinutes = 10;
        private readonly int _maxAttempts = 5;
        private readonly int _cooldownMinutes = 5; // Attente avant nouveau OTP

        public OtpService(
            string connectionString,
            ILogger<OtpService> logger,
            SmtpEmailService emailService,
            Whatsappservice whatsappservice,
            IConfiguration configuration)
        {
            _connectionString = connectionString;
            _logger = logger;
            _emailService = emailService;
            _whatsappService = whatsappservice;
            _configuration = configuration;
        }

        /// <summary>
        /// Génère et envoie un OTP par email
        /// </summary>
        public async Task<GenerateOtpResponse> GenerateAndSendOtpAsync(
            string email,
            string purpose,
            string? ipAddress = null)
        {
            try
            {
                _logger.LogInformation($"Génération OTP pour {email} ({purpose})");

                // 1. Vérifier cooldown (éviter spam)
                if (await IsInCooldownAsync(email, purpose))
                {
                    return new GenerateOtpResponse
                    {
                        Success = false,
                        Message = $"Veuillez attendre {_cooldownMinutes} minutes avant de demander un nouveau code"
                    };
                }

                // 2. Invalider les anciens OTP non utilisés
                await InvalidateOldOtpsAsync(email, purpose);

                // 3. Générer le code OTP
                var code = GenerateOtpCode();
                var expiresAt = DateTime.Now.AddMinutes(_otpExpirationMinutes);

                // 4. Enregistrer en base de données
                await SaveOtpAsync(email, code, purpose, expiresAt, ipAddress);

                // 5. Envoyer par email
                //var emailSent = await _emailService.SendOtpEmailAsync(email, code);

                //if (!emailSent)
                //{
                //    return new GenerateOtpResponse
                //    {
                //        Success = false,
                //        Message = "Erreur lors de l'envoi de l'email"
                //    };
                //}

                // 6. Envoyer par Whatsapp (optionnel)


                _logger.LogInformation($"✅ OTP généré et envoyé à {email}");

                return new GenerateOtpResponse
                {
                    Success = true,
                    Message = "Code de vérification envoyé par email",
                    ExpiresAt = expiresAt,
                    RemainingAttempts = _maxAttempts,
                    // DebugCode = code // À retirer en production !
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur génération OTP pour {email}");
                return new GenerateOtpResponse
                {
                    Success = false,
                    Message = "Erreur lors de la génération du code"
                };
            }
        }
        public async Task<GenerateOtpResponse> GenerateAndSendOtpAsyncByWha(
           string email,
           string purpose,
           string? telephone,
           string? nom,
           string? entreprise,
           decimal? amount,
           string? ipAddress = null
        )
        {
            try
            {
                _logger.LogInformation($"Génération OTP pour {email} ({purpose})");

                // 1. Vérifier cooldown (éviter spam)
                if (await IsInCooldownAsync(email, purpose))
                {
                    return new GenerateOtpResponse
                    {
                        Success = false,
                        Message = $"Veuillez attendre {_cooldownMinutes} minutes avant de demander un nouveau code"
                    };
                }

                // 2. Invalider les anciens OTP non utilisés
                await InvalidateOldOtpsAsync(email, purpose);

                // 3. Générer le code OTP
                var code = GenerateOtpCode();
                var expiresAt = DateTime.Now.AddMinutes(_otpExpirationMinutes);

                // 4. Enregistrer en base de données
                await SaveOtpAsync(email, code, purpose, expiresAt, ipAddress);

                // 5. Envoyer par email
                //var emailSent = await _emailService.SendOtpEmailAsync(email, code);

                //if (!emailSent)
                //{
                //    return new GenerateOtpResponse
                //    {
                //        Success = false,
                //        Message = "Erreur lors de l'envoi de l'email"
                //    };
                //}

                // 6. Envoyer par Whatsapp (optionnel)

                var whatsappSent = await _whatsappService.SendPaymentInvitationAsync(telephone, nom, entreprise, Convert.ToDecimal(amount));
                if (!whatsappSent)
                {
                    return new GenerateOtpResponse
                    {
                        Success = false,
                        Message = "Erreur lors de l'envoi de Whatsapp"
                    };
                }
                _logger.LogInformation($"✅ OTP généré et envoyé à {email}");

                return new GenerateOtpResponse
                {
                    Success = true,
                    Message = "Code de vérification envoyé par email",
                    ExpiresAt = expiresAt,
                    RemainingAttempts = _maxAttempts,
                    // DebugCode = code // À retirer en production !
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur génération OTP pour {email}");
                return new GenerateOtpResponse
                {
                    Success = false,
                    Message = "Erreur lors de la génération du code"
                };
            }
        }
        /// <summary>
        /// Valide un code OTP
        /// </summary>
        public async Task<ValidateOtpResponse> ValidateOtpAsync(
            string email,
            string code,
            string purpose)
        {
            try
            {
                _logger.LogInformation($"Validation OTP pour {email} ({purpose})");

                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // 1. Récupérer l'OTP le plus récent et valide
                var otp = await GetLatestValidOtpAsync(email, purpose, conn);

                if (otp == null)
                {
                    return new ValidateOtpResponse
                    {
                        Success = false,
                        IsValid = false,
                        Message = "Aucun code de vérification trouvé ou code expiré"
                    };
                }

                // 2. Vérifier si déjà utilisé
                if (otp.IsUsed)
                {
                    return new ValidateOtpResponse
                    {
                        Success = false,
                        IsValid = false,
                        Message = "Ce code a déjà été utilisé"
                    };
                }

                // 3. Vérifier si expiré
                if (otp.ExpiresAt < DateTime.Now)
                {
                    return new ValidateOtpResponse
                    {
                        Success = false,
                        IsValid = false,
                        Message = "Ce code a expiré. Demandez un nouveau code."
                    };
                }

                // 4. Incrémenter le compteur de tentatives
                await IncrementAttemptCountAsync(otp.Id, conn);
                otp.AttemptCount++;

                // 5. Vérifier le nombre de tentatives
                if (otp.AttemptCount > _maxAttempts)
                {
                    await InvalidateOtpAsync(otp.Id, conn);
                    return new ValidateOtpResponse
                    {
                        Success = false,
                        IsValid = false,
                        Message = "Nombre maximum de tentatives atteint. Demandez un nouveau code."
                    };
                }

                // 6. Vérifier le code
                if (otp.Code != code)
                {
                    var remaining = _maxAttempts - otp.AttemptCount;
                    return new ValidateOtpResponse
                    {
                        Success = false,
                        IsValid = false,
                        Message = $"Code incorrect. {remaining} tentative(s) restante(s).",
                        RemainingAttempts = remaining
                    };
                }

                // 7. Code correct ! Marquer comme utilisé
                await MarkOtpAsUsedAsync(otp.Id, conn);

                _logger.LogInformation($"✅ OTP validé pour {email}");

                return new ValidateOtpResponse
                {
                    Success = true,
                    IsValid = true,
                    Message = "Code validé avec succès",
                    RemainingAttempts = _maxAttempts - otp.AttemptCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur validation OTP pour {email}");
                return new ValidateOtpResponse
                {
                    Success = false,
                    IsValid = false,
                    Message = "Erreur lors de la validation du code"
                };
            }
        }

        // ========================================
        // MÉTHODES PRIVÉES
        // ========================================

        private string GenerateOtpCode()
        {
            var random = new Random();
            return random.Next(1000, 9999).ToString();
        }

        private async Task<bool> IsInCooldownAsync(string email, string purpose)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT COUNT(*)
                FROM OTP_CODES
                WHERE Email = @Email
                AND Purpose = @Purpose
                AND CreatedAt > DATEADD(MINUTE, -@CooldownMinutes, GETDATE())";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Purpose", purpose);
            cmd.Parameters.AddWithValue("@CooldownMinutes", _cooldownMinutes);

            var count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        private async Task InvalidateOldOtpsAsync(string email, string purpose)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                UPDATE OTP_CODES
                SET IsUsed = 1,
                    UsedAt = GETDATE()
                WHERE Email = @Email
                AND Purpose = @Purpose
                AND IsUsed = 0";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Purpose", purpose);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task SaveOtpAsync(
            string email,
            string code,
            string purpose,
            DateTime expiresAt,
            string? ipAddress)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO OTP_CODES (
                    Id, Email, Code, Purpose, CreatedAt, ExpiresAt, 
                    IsUsed, AttemptCount, IpAddress
                )
                VALUES (
                    @Id, @Email, @Code, @Purpose, @CreatedAt, @ExpiresAt,
                    0, 0, @IpAddress
                )";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Code", code);
            cmd.Parameters.AddWithValue("@Purpose", purpose);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            cmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);
            cmd.Parameters.AddWithValue("@IpAddress", (object?)ipAddress ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<OtpCode?> GetLatestValidOtpAsync(
            string email,
            string purpose,
            SqlConnection conn)
        {
            var sql = @"
                SELECT TOP 1
                    Id, Email, Code, Purpose, CreatedAt, ExpiresAt,
                    IsUsed, UsedAt, AttemptCount, LastAttemptAt, IpAddress
                FROM OTP_CODES
                WHERE Email = @Email
                AND Purpose = @Purpose
                ORDER BY CreatedAt DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Purpose", purpose);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new OtpCode
                {
                    Id = reader.GetGuid(0),
                    Email = reader.GetString(1),
                    Code = reader.GetString(2),
                    Purpose = reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    ExpiresAt = reader.GetDateTime(5),
                    IsUsed = reader.GetBoolean(6),
                    UsedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    AttemptCount = reader.GetInt32(8),
                    LastAttemptAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    IpAddress = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
            }

            return null;
        }

        private async Task IncrementAttemptCountAsync(Guid otpId, SqlConnection conn)
        {
            var sql = @"
                UPDATE OTP_CODES
                SET AttemptCount = AttemptCount + 1,
                    LastAttemptAt = GETDATE()
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", otpId);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task MarkOtpAsUsedAsync(Guid otpId, SqlConnection conn)
        {
            var sql = @"
                UPDATE OTP_CODES
                SET IsUsed = 1,
                    UsedAt = GETDATE()
                WHERE Id = @Id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", otpId);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task InvalidateOtpAsync(Guid otpId, SqlConnection conn)
        {
            await MarkOtpAsUsedAsync(otpId, conn);
        }
    }
}