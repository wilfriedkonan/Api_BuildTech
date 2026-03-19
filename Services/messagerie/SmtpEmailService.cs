using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Configuration;

namespace Api_BuildTech.Services.messagerie
{
    /// <summary>
    /// Service d'envoi d'email via SMTP (remplace Brevo)
    /// Compatible avec: Gmail, Outlook, serveurs SMTP personnalisés
    /// Configuration via appsettings.json section "Smtp"
    /// </summary>
    public class SmtpEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _senderEmail;
        private readonly string _senderName;
        private readonly bool _useSsl;

        /// <summary>
        /// Constructeur avec injection de IConfiguration
        /// Lit automatiquement la configuration depuis appsettings.json
        /// </summary>
        public SmtpEmailService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Lecture configuration depuis appsettings.json section "Smtp"
            _smtpHost = _configuration["Smtp:Host"]
                ?? throw new InvalidOperationException("Configuration manquante: Smtp:Host dans appsettings.json");

            _smtpPort = int.TryParse(_configuration["Smtp:Port"], out int port)
                ? port
                : 587; // Port par défaut

            _smtpUsername = _configuration["Smtp:Username"]
                ?? throw new InvalidOperationException("Configuration manquante: Smtp:Username dans appsettings.json");

            _smtpPassword = _configuration["Smtp:Password"]
                ?? throw new InvalidOperationException("Configuration manquante: Smtp:Password dans appsettings.json");

            _senderEmail = _configuration["Smtp:SenderEmail"] ?? _smtpUsername;
            _senderName = _configuration["Smtp:SenderName"] ?? "Build Tech Solutions";

            _useSsl = bool.TryParse(_configuration["Smtp:UseSsl"], out bool useSsl)
                ? useSsl
                : true; // SSL activé par défaut

            // Log configuration (sans le mot de passe)
            Console.WriteLine("📧 Configuration SMTP chargée:");
            Console.WriteLine($"   Host: {_smtpHost}");
            Console.WriteLine($"   Port: {_smtpPort}");
            Console.WriteLine($"   Username: {_smtpUsername}");
            Console.WriteLine($"   SenderEmail: {_senderEmail}");
            Console.WriteLine($"   SenderName: {_senderName}");
            Console.WriteLine($"   UseSsl: {_useSsl}");
        }

        /// <summary>
        /// Envoie un email simple (texte ou HTML)
        /// </summary>
        /// <param name="toEmail">Email destinataire</param>
        /// <param name="toName">Nom du destinataire</param>
        /// <param name="subject">Sujet de l'email</param>
        /// <param name="body">Corps du message (HTML ou texte)</param>
        /// <param name="isHtml">true pour HTML, false pour texte brut</param>
        /// <returns>true si envoyé avec succès, false sinon</returns>
        public async Task<bool> SendEmailAsync(
            string toEmail,
            string toName,
            string subject,
            string body,
            bool isHtml = true)
        {
            try
            {
                var message = new MimeMessage();

                // Expéditeur
                message.From.Add(new MailboxAddress(_senderName, _senderEmail));

                // Destinataire
                message.To.Add(new MailboxAddress(toName, toEmail));

                // Sujet
                message.Subject = subject;

                // Corps du message
                message.Body = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain)
                {
                    Text = body
                };

                // Envoi via SMTP
                using var client = new SmtpClient();

                // Connexion au serveur SMTP
                await client.ConnectAsync(_smtpHost, _smtpPort, _useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

                // Authentification
                await client.AuthenticateAsync(_smtpUsername, _smtpPassword);

                // Envoi
                await client.SendAsync(message);

                // Déconnexion
                await client.DisconnectAsync(true);

                Console.WriteLine($"✅ Email envoyé avec succès à {toEmail}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur envoi email à {toEmail}: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Envoie un email d'invitation utilisateur
        /// </summary>
        /// <param name="toEmail">Email du nouvel utilisateur</param>
        /// <param name="toName">Nom complet du nouvel utilisateur</param>
        /// <param name="invitationUrl">URL d'activation du compte</param>
        /// <param name="expirationDate">Date d'expiration du lien</param>
        /// <returns>true si envoyé avec succès, false sinon</returns>
        public async Task<bool> SendInvitationEmailAsync(
            string toEmail,
            string toName,
            string invitationUrl
            )
        {
            var htmlContent = $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Invitation Build Tech Solutions</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f7f9;
            margin: 0;
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 40px auto;
            background-color: #ffffff;
            border-radius: 12px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            padding: 40px 30px;
            text-align: center;
            color: #ffffff;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content p {{
            color: #333333;
            line-height: 1.6;
            margin: 0 0 15px 0;
        }}
        .cta-button {{
            display: inline-block;
            margin: 30px 0;
            padding: 16px 40px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: #ffffff;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
        }}
        .info-box {{
            background-color: #f0f4ff;
            border-left: 4px solid #667eea;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            background-color: #f8f9fa;
            padding: 20px 30px;
            text-align: center;
            color: #666;
            font-size: 13px;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>🎉 Bienvenue sur Build Tech Solutions</h1>
        </div>
        <div class='content'>
            <p>Bonjour <strong>{toName}</strong>,</p>
            <p>Vous avez été invité à rejoindre la plateforme Build Tech Solutions.</p>
            <div class='info-box'>
                <p><strong>📧 Email:</strong> {toEmail}</p>
            </div>
            <p><strong>Pour compléter votre inscription, cliquez sur le bouton ci-dessous :</strong></p>
            <div style='text-align: center;'>
                <a href='{invitationUrl}' class='cta-button'>
                    🔐 Compléter mon inscription
                </a>
            </div>
            <p style='margin-top: 30px; color: #666; font-size: 14px;'>
                Si le bouton ne fonctionne pas, copiez ce lien :<br>
                <a href='{invitationUrl}' style='color: #667eea;'>{invitationUrl}</a>
            </p>
        </div>
        <div class='footer'>
            <p>&copy; 2025 Build Tech Solutions. Tous droits réservés.</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(
                toEmail,
                toName,
                "🎉 Invitation à rejoindre Build Tech Solutions",
                htmlContent,
                true
            );
        }

        /// <summary>
        /// Envoie un email de réinitialisation de mot de passe
        /// </summary>
        /// <param name="toEmail">Email de l'utilisateur</param>
        /// <param name="toName">Nom complet de l'utilisateur</param>
        /// <param name="resetUrl">URL de réinitialisation</param>
        /// <param name="expirationDate">Date d'expiration du lien</param>
        /// <returns>true si envoyé avec succès, false sinon</returns>
        public async Task<bool> SendResetPasswordEmailAsync(
            string toEmail,
            string toName,
            string resetUrl,
            DateTime expirationDate)
        {
            var htmlContent = $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Réinitialisation mot de passe</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f7f9;
            margin: 0;
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 40px auto;
            background-color: #ffffff;
            border-radius: 12px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #f43f5e 0%, #e11d48 100%);
            padding: 40px 30px;
            text-align: center;
            color: #ffffff;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content p {{
            color: #333333;
            line-height: 1.6;
            margin: 0 0 15px 0;
        }}
        .cta-button {{
            display: inline-block;
            margin: 30px 0;
            padding: 16px 40px;
            background: linear-gradient(135deg, #f43f5e 0%, #e11d48 100%);
            color: #ffffff;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
        }}
        .info-box {{
            background-color: #fef2f2;
            border-left: 4px solid #f43f5e;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .warning-box {{
            background-color: #fff7ed;
            border-left: 4px solid #f97316;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            background-color: #f8f9fa;
            padding: 20px 30px;
            text-align: center;
            color: #666;
            font-size: 13px;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <div style='font-size: 48px; margin-bottom: 10px;'>🔒</div>
            <h1>Réinitialisation de mot de passe</h1>
        </div>
        <div class='content'>
            <p>Bonjour <strong>{toName}</strong>,</p>
            <p>Vous avez demandé à réinitialiser votre mot de passe sur Build Tech Solutions.</p>
            <div class='info-box'>
                <p><strong>📧 Email:</strong> {toEmail}</p>
                <p><strong>⏰ Lien valide jusqu'au:</strong> {expirationDate:dd/MM/yyyy à HH:mm} (1 heure)</p>
            </div>
            <p><strong>Pour définir un nouveau mot de passe, cliquez sur le bouton ci-dessous :</strong></p>
            <div style='text-align: center;'>
                <a href='{resetUrl}' class='cta-button'>
                    🔐 Réinitialiser mon mot de passe
                </a>
            </div>
            <p style='margin-top: 30px; color: #666; font-size: 14px;'>
                Si le bouton ne fonctionne pas, copiez ce lien :<br>
                <a href='{resetUrl}' style='color: #f43f5e;'>{resetUrl}</a>
            </p>
            <div class='warning-box'>
                <p><strong>⚠️ Vous n'avez pas demandé cette réinitialisation ?</strong></p>
                <p>Ignorez cet email. Votre mot de passe reste inchangé.</p>
            </div>
        </div>
        <div class='footer'>
            <p>&copy; 2025 Build Tech Solutions. Tous droits réservés.</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(
                toEmail,
                toName,
                "🔒 Réinitialisation de votre mot de passe - Build Tech Solutions",
                htmlContent,
                true
            );
        }

        /// <summary>
        /// Envoie un OTP par email
        /// </summary>
        /// <param name="toEmail">Email du destinataire</param>
        /// <param name="otp">Code OTP à envoyer</param>
        /// <returns>true si envoyé avec succès, false sinon</returns>
        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp)
        {
            var htmlContent = $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; }}
        .header {{ text-align: center; color: #333; }}
        .otp-code {{ font-size: 32px; font-weight: bold; color: #667eea; text-align: center; padding: 20px; background-color: #f0f4ff; border-radius: 8px; margin: 20px 0; letter-spacing: 5px; }}
        .footer {{ text-align: center; color: #666; font-size: 12px; margin-top: 30px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Code de vérification</h1>
            <p>Votre code OTP pour Build Tech Solutions</p>
        </div>
        <div class='otp-code'>{otp}</div>
        <p style='text-align: center; color: #666;'>Ce code expire dans 10 minutes.</p>
        <p style='text-align: center; color: #999; font-size: 14px;'>Si vous n'avez pas demandé ce code, ignorez cet email.</p>
        <div class='footer'>
            <p>&copy; 2025 Build Tech Solutions</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(
                toEmail,
                "",
                "🔐 Votre code de vérification OTP - Build Tech Solutions",
                htmlContent,
                true
            );
        }

        /// <summary>
        /// Test de connexion SMTP
        /// Vérifie que le serveur est accessible et que les identifiants sont corrects
        /// </summary>
        /// <returns>true si la connexion réussit, false sinon</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var client = new SmtpClient();

                Console.WriteLine($"🔍 Test connexion SMTP: {_smtpHost}:{_smtpPort}");

                await client.ConnectAsync(_smtpHost, _smtpPort, _useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
                Console.WriteLine("✅ Connexion établie");

                await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                Console.WriteLine("✅ Authentification réussie");

                await client.DisconnectAsync(true);
                Console.WriteLine("✅ Déconnexion propre");

                Console.WriteLine("✅ Test connexion SMTP: SUCCÈS");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test connexion SMTP: ÉCHEC");
                Console.WriteLine($"   Erreur: {ex.Message}");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                return false;
            }
        }
    }
}