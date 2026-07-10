using System.Text;
using System.Text.Json;

namespace Api_BuildTech.Services.messagerie
{
    public class Whatsappservice
    {
        private readonly IConfiguration _configuration;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _instance;
        private readonly string _phoneNumber;
        private readonly bool _enabled;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Constructeur avec injection de IConfiguration et HttpClient
        /// Lit automatiquement la configuration depuis appsettings.json section "WhatsApp"
        /// </summary>
        public Whatsappservice(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Lecture configuration depuis appsettings.json section "WhatsApp"
            _apiUrl = _configuration["WhatsApp:ApiUrl"]
                ?? throw new InvalidOperationException("Configuration manquante: WhatsApp:ApiUrl dans appsettings.json");

            _apiKey = _configuration["WhatsApp:ApiKey"]
                ?? throw new InvalidOperationException("Configuration manquante: WhatsApp:ApiKey dans appsettings.json");

            _instance = _configuration["WhatsApp:Instance"]
                ?? throw new InvalidOperationException("Configuration manquante: WhatsApp:Instance dans appsettings.json");

            _phoneNumber = _configuration["WhatsApp:PhoneNumber"]
                ?? "0596215539"; // Numéro par défaut

            _enabled = bool.TryParse(_configuration["WhatsApp:Enabled"], out bool enabled)
                ? enabled
                : true; // Activé par défaut

            // Log configuration (sans les secrets)
            Console.WriteLine("📱 Configuration WhatsApp chargée:");
            Console.WriteLine($"   ApiUrl: {_apiUrl}");
            Console.WriteLine($"   ApiKey: {MaskApiKey(_apiKey)}"); // Afficher seulement les derniers caractères
            Console.WriteLine($"   Instance: {_instance}");
            Console.WriteLine($"   PhoneNumber: {_phoneNumber}");
            Console.WriteLine($"   Enabled: {_enabled}");
        }

        /// <summary>
        /// Envoyer un message d'invitation à payer l'abonnement
        /// Montant variable - Format: 15000 CFA
        /// </summary>
        /// <param name="toPhone">Numéro WhatsApp du destinataire (format: 2250596215539)</param>
        /// <param name="userName">Nom de l'utilisateur</param>
        /// <param name="amount">Montant à payer en FCFA</param>
        /// <returns>true si envoyé avec succès, false sinon</returns>
        public async Task<bool> SendPaymentInvitationAsync(
            string toPhone,
            string userName,
            string entreprise,
            decimal amount)
        {
            try
            {
                var infoAdmin = BuildAvertissementInscription(entreprise, amount, toPhone);
                var successInfo = await SendWhatsAppMessageAsync(_phoneNumber, infoAdmin);

                var message = BuildPaymentInvitationMessage(userName, entreprise, amount);
                var success = await SendWhatsAppMessageAsync(toPhone, message);

                if (success)
                {
                    Console.WriteLine($"✅ Message d'invitation de paiement envoyé avec succès à {toPhone}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur SendPaymentInvitationAsync à {toPhone}: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Envoyer un message de bienvenue
        /// Message fixe: "Nous restons à votre écoute..."
        /// </summary>
        /// <param name="toPhone">Numéro WhatsApp du destinataire (format: 2250596215539)</param>
        /// <param name="userName">Nom de l'utilisateur</param>
        /// <returns>true si envoyé avec succès, false sinon</returns>
        public async Task<bool> SendWelcomeMessageAsync(
            string toPhone,
            string userName)
        {
            try
            {
                var message = BuildWelcomeMessage(userName);
                var success = await SendWhatsAppMessageAsync(toPhone, message);

                if (success)
                {
                    Console.WriteLine($"✅ Message de bienvenue envoyé avec succès à {toPhone}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur SendWelcomeMessageAsync à {toPhone}: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Envoyer un message personnalisé
        /// </summary>
        /// <param name="toPhone">Numéro WhatsApp du destinataire (format: 2250596215539)</param>
        /// <param name="message">Message personnalisé (max 4096 caractères)</param>
        /// <returns>true si envoyé avec succès, false sinon</returns>
        public async Task<bool> SendCustomMessageAsync(
            string toPhone,
            string message)
        {
            try
            {
                // Valider le message
                if (string.IsNullOrWhiteSpace(message))
                {
                    Console.WriteLine("❌ Le message ne peut pas être vide");
                    return false;
                }

                if (message.Length > 4096)
                {
                    Console.WriteLine("❌ Le message est trop long (max 4096 caractères)");
                    return false;
                }

                var success = await SendWhatsAppMessageAsync(toPhone, message);

                if (success)
                {
                    Console.WriteLine($"✅ Message personnalisé envoyé avec succès à {toPhone}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur SendCustomMessageAsync à {toPhone}: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Test de connexion API WhatsApp
        /// Vérifie que l'API est accessible et que les paramètres sont corrects
        /// </summary>
        /// <returns>true si la connexion réussit, false sinon</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                Console.WriteLine($"🔍 Test connexion API WhatsApp: {_apiUrl}");

                var requestBody = new
                {
                    instance = _instance,
                    phone = "0596215539",
                    message = "Test de connexion API WhatsApp - Ignorez ce message",
                    delay = true
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                // ✅ AJOUTER LA CLÉ API EN HEADER
                var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                {
                    Content = jsonContent
                };
                request.Headers.Add("X-Api-Key", _apiKey);
                request.Headers.Add("Accept", "*/*");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Connexion établie");
                    Console.WriteLine("✅ Authentification réussie");
                    Console.WriteLine("✅ Test connexion API WhatsApp: SUCCÈS");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Test connexion API WhatsApp: ÉCHEC");
                    Console.WriteLine($"   Erreur: {response.StatusCode}");
                    Console.WriteLine($"   Réponse: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test connexion API WhatsApp: ÉCHEC");
                Console.WriteLine($"   Erreur: {ex.Message}");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // MÉTHODES PRIVÉES
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Construire le message d'invitation à payer
        /// </summary>
        private string BuildPaymentInvitationMessage(string userName, string entreprise, decimal amount)
        {
            return $@"Bonjour {userName} de {entreprise},
 
🎉 Votre inscription a bien été prise en compte!
 
Nous vous invitons à payer la somme de *{amount:N0} CFA* afin d'activer votre compte.
 
📱 *Moyens de paiement disponibles:*
• Wave Money
• MTN Money
 
💰 *Numéro de paiement:* {_phoneNumber}
 
⏱️ *Note:* Pour le moment, nous effectuons les activations manuelles. Votre compte sera activé dans les 24 heures suivant la réception du paiement.
 
Merci de votre confiance! 🙏";
        }
        private string BuildAvertissementInscription(string entreprise, decimal amount, string telephone)
        {
            return $@" 🎉 Nouvelle inscription ENTREPRISE : {entreprise} Formule de {amount} Contact {telephone},
 
Date et heure : {DateTime.UtcNow}.";
        }

        /// <summary>
        /// Construire le message de bienvenue
        /// </summary>
        private string BuildWelcomeMessage(string userName)
        {
            return $@"Bienvenue {userName}! 👋
 
Nous sommes heureux de vous accueillir dans notre communauté! 🎉
 
📞 *Nous restons à votre écoute* à travers ce canal de communication WhatsApp pour:
• Répondre à vos préoccupations
• Vous aider au plus vite
• Vous assister au mieux de nos capacités
 
✨ *Avantages de nous joindre par WhatsApp:*
📱 Réponses rapides
🎯 Support personnalisé
🕐 Disponibilité étendue
 
N'hésitez pas à nous envoyer vos questions! Nous sommes là pour vous! 😊";
        }

        /// <summary>
        /// Envoyer le message via l'API WhatsApp externe
        /// </summary>
        private async Task<bool> SendWhatsAppMessageAsync(string phone, string message)
        {
            try
            {
                if (!_enabled)
                {
                    Console.WriteLine("⚠️ WhatsApp désactivé dans la configuration");
                    return false;
                }

                // Préparer la requête
                var requestBody = new
                {
                    instance = _instance,
                    phone = phone,
                    message = message,
                    delay = true
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                Console.WriteLine($"📤 Envoi WhatsApp à {phone}...");

                // ✅ CRÉER LA REQUÊTE AVEC LA CLÉ API EN HEADER
                var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                {
                    Content = jsonContent
                };

                // Ajouter les headers
                request.Headers.Add("X-Api-Key", _apiKey);
                request.Headers.Add("Accept", "*/*");

                // Envoyer la requête
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✅ WhatsApp envoyé avec succès à {phone}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Erreur API WhatsApp: {response.StatusCode}");
                    Console.WriteLine($"   Réponse: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception lors de l'envoi WhatsApp à {phone}");
                Console.WriteLine($"   Erreur: {ex.Message}");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                return false;
            }
        }

        /// <summary>
        /// Masquer la clé API pour l'affichage (sécurité)
        /// Affiche seulement les derniers 8 caractères
        /// </summary>
        private string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "***";

            if (apiKey.Length <= 8)
                return "***";

            return "***" + apiKey.Substring(apiKey.Length - 8);
        }
    }
}