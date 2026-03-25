using Api_BuildTech.Controllers.ArticleStock;
using Api_BuildTech.Controllers.Articles;
using Api_BuildTech.Controllers.Authentication;
using Api_BuildTech.Controllers.AutresMagasin;
using Api_BuildTech.Controllers.Categorie;
using Api_BuildTech.Controllers.CategorieComposants;
using Api_BuildTech.Controllers.Clients;
using Api_BuildTech.Controllers.Composants;
using Api_BuildTech.Controllers.CompositionArticle;
using Api_BuildTech.Controllers.DetailLivraisons;
using Api_BuildTech.Controllers.DetailTransactions;
using Api_BuildTech.Controllers.DomaineRestaurant;
using Api_BuildTech.Controllers.Entreprise;
using Api_BuildTech.Controllers.Factures;
using Api_BuildTech.Controllers.Fournisseurs;
using Api_BuildTech.Controllers.Livraisons;
using Api_BuildTech.Controllers.Livreur;
using Api_BuildTech.Controllers.MatierePremiere;
using Api_BuildTech.Controllers.ModelRecu;
using Api_BuildTech.Controllers.MouvementStock;
using Api_BuildTech.Controllers.Organisation;
using Api_BuildTech.Controllers.Otp;
using Api_BuildTech.Controllers.Paiments;
using Api_BuildTech.Controllers.ParametrePos;
using Api_BuildTech.Controllers.Plans;
using Api_BuildTech.Controllers.Registration;
using Api_BuildTech.Controllers.Serveur;
using Api_BuildTech.Controllers.Sessions;
using Api_BuildTech.Controllers.Subscription;
using Api_BuildTech.Controllers.Synchronisation;
using Api_BuildTech.Controllers.Table;
using Api_BuildTech.Controllers.TypePaiement;
using Api_BuildTech.Controllers.TypeServices;
using Api_BuildTech.Controllers.UniteMesures;
using Api_BuildTech.Controllers.Users;
using Api_BuildTech.Services;
using Api_BuildTech.Services.messagerie;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// CONFIGURATION DE BASE
// ============================================================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TSALACH API",
        Version = "v1",
        Description = "API de gestion de restaurant multi-tenant"
    });

    // ✅ Ajouter le support JWT dans Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
}); builder.Services.AddHttpContextAccessor();

// ============================================================================
// CONFIGURATION CORS
// ============================================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============================================================================
// CONFIGURATION JWT AUTHENTICATION
// ============================================================================

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey manquante dans appsettings.json");
var issuer = jwtSettings["Issuer"] ?? "TSALACH";
var audience = jwtSettings["Audience"] ?? "TSALACH-API";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ============================================================================
// CONNECTION STRING
// ============================================================================

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' non trouvée dans appsettings.json");

// ============================================================================
// INJECTION DE DÉPENDANCES - SERVICES DE BASE
// ============================================================================

// Service email
builder.Services.AddScoped<SmtpEmailService>();
builder.Services.AddScoped<OtpService>(sp =>
    new OtpService(
        connectionString,
        sp.GetRequiredService<ILogger<OtpService>>(),
        sp.GetRequiredService<SmtpEmailService>(),
        sp.GetRequiredService<IConfiguration>()
    ));
// Service de base pour accès database
builder.Services.AddScoped<DatabaseService>(sp =>
    new DatabaseService(
        connectionString,
        sp.GetRequiredService<ILogger<DatabaseService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// Service d'authentification
builder.Services.AddScoped<AuthService>(sp =>
    new AuthService(
        connectionString,
        sp.GetRequiredService<ILogger<AuthService>>(),
        sp.GetRequiredService<IHttpContextAccessor>(),
        sp.GetRequiredService<IConfiguration>()
    ));

// Service de synchronisation
builder.Services.AddScoped<SyncService>(sp =>
    new SyncService(
        connectionString,
        sp.GetRequiredService<ILogger<SyncService>>()
    ));

// Service d'abonnement/subscription
builder.Services.AddScoped<SubscriptionService>(sp =>
    new SubscriptionService(
        connectionString,
        sp.GetRequiredService<ILogger<SubscriptionService>>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GESTION ENTREPRISE & UTILISATEURS
// ============================================================================

// OrganisationService
builder.Services.AddScoped<OrganisationService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<OrganisationService>>();
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

    return new OrganisationService(
        connectionString,
        logger,
        httpContextAccessor
    );
});

// 2. EntrepriseService

builder.Services.AddScoped<EntrepriseService>(sp =>
    new EntrepriseService(
        connectionString,
        sp.GetRequiredService<ILogger<EntrepriseService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<UsersService>(sp =>
    new UsersService(
        connectionString,
        sp.GetRequiredService<ILogger<UsersService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GESTION ARTICLES & CATÉGORIES
// ============================================================================

builder.Services.AddScoped<CategorieService>(sp =>
    new CategorieService(
        connectionString,
        sp.GetRequiredService<ILogger<CategorieService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<ArticlesService>(sp =>
    new ArticlesService(
        connectionString,
        sp.GetRequiredService<ILogger<ArticlesService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GESTION CLIENTS & FOURNISSEURS
// ============================================================================

builder.Services.AddScoped<ClientsService>(sp =>
    new ClientsService(
        connectionString,
        sp.GetRequiredService<ILogger<ClientsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<FournisseursService>(sp =>
    new FournisseursService(
        connectionString,
        sp.GetRequiredService<ILogger<FournisseursService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - FACTURES & TABLES
// ============================================================================

builder.Services.AddScoped<FactureService>(sp =>
    new FactureService(
        connectionString,
        sp.GetRequiredService<ILogger<FactureService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<TableService>(sp =>
    new TableService(
        connectionString,
        sp.GetRequiredService<ILogger<TableService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GROUPE 1 : CONFIGURATION
// ============================================================================

// TYPE_SERVICES
builder.Services.AddScoped<TypeServicesService>(sp =>
    new TypeServicesService(
        connectionString,
        sp.GetRequiredService<ILogger<TypeServicesService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// TYPE_PAIEMENT
builder.Services.AddScoped<TypePaiementService>(sp =>
    new TypePaiementService(
        connectionString,
        sp.GetRequiredService<ILogger<TypePaiementService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// UNITE_MESURES
builder.Services.AddScoped<UniteMesuresService>(sp =>
    new UniteMesuresService(
        connectionString,
        sp.GetRequiredService<ILogger<UniteMesuresService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GROUPE 2 : CONFIGURATION SUITE
// ============================================================================

// PARAMETRE_POS
builder.Services.AddScoped<ParametrePosService>(sp =>
    new ParametrePosService(
        connectionString,
        sp.GetRequiredService<ILogger<ParametrePosService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// MODEL_RECU
builder.Services.AddScoped<ModelRecuService>(sp =>
    new ModelRecuService(
        connectionString,
        sp.GetRequiredService<ILogger<ModelRecuService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GROUPE 3 : RESTAURANT ESSENTIELS
// ============================================================================

// DOMAINE_RESTAURANT
builder.Services.AddScoped<DomaineRestaurantService>(sp =>
    new DomaineRestaurantService(
        connectionString,
        sp.GetRequiredService<ILogger<DomaineRestaurantService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// SERVEUR
builder.Services.AddScoped<ServeurService>(sp =>
    new ServeurService(
        connectionString,
        sp.GetRequiredService<ILogger<ServeurService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// SESSIONS
builder.Services.AddScoped<SessionsService>(sp =>
    new SessionsService(
        connectionString,
        sp.GetRequiredService<ILogger<SessionsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GROUPE 4 : TRANSACTIONS CRITIQUES
// ============================================================================

// DETAIL_TRANSACTIONS
builder.Services.AddScoped<DetailTransactionsService>(sp =>
    new DetailTransactionsService(
        connectionString,
        sp.GetRequiredService<ILogger<DetailTransactionsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// PAIMENTS
builder.Services.AddScoped<PaimentsService>(sp =>
    new PaimentsService(
        connectionString,
        sp.GetRequiredService<ILogger<PaimentsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GROUPE 5 : STRUCTURE ARTICLES
// ============================================================================

// CATHEGORIE_COMPOSENTS
builder.Services.AddScoped<CategorieComposantsService>(sp =>
    new CategorieComposantsService(
        connectionString,
        sp.GetRequiredService<ILogger<CategorieComposantsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// COMPOSANTS
builder.Services.AddScoped<ComposantsService>(sp =>
    new ComposantsService(
        connectionString,
        sp.GetRequiredService<ILogger<ComposantsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GROUPE 6 : SYSTÈME LIVRAISON
// ============================================================================

// LIVREUR
builder.Services.AddScoped<LivreurService>(sp =>
    new LivreurService(
        connectionString,
        sp.GetRequiredService<ILogger<LivreurService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// LIVRAISONS
builder.Services.AddScoped<LivraisonsService>(sp =>
    new LivraisonsService(
        connectionString,
        sp.GetRequiredService<ILogger<LivraisonsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// DETAIL_LIVRAISONS
builder.Services.AddScoped<DetailLivraisonsService>(sp =>
    new DetailLivraisonsService(
        connectionString,
        sp.GetRequiredService<ILogger<DetailLivraisonsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// INJECTION DE DÉPENDANCES - GROUPE 7 : STOCK & MATIÈRES PREMIÈRES
// ============================================================================

// MATIERE_PREMIERE
builder.Services.AddScoped<MatierePremiereService>(sp =>
    new MatierePremiereService(
        connectionString,
        sp.GetRequiredService<ILogger<MatierePremiereService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// VU STOCK + ARTICLE 
builder.Services.AddScoped<ArticleStockService>(sp =>
    new ArticleStockService(
        connectionString,
        sp.GetRequiredService<ILogger<ArticleStockService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// COMPOSITION_ARTICLE

builder.Services.AddScoped<CompositionArticleService>(sp =>
    new CompositionArticleService(
        connectionString,
        sp.GetRequiredService<ILogger<CompositionArticleService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// MOUVEMENT_STOCK
builder.Services.AddScoped<MouvementStockService>(sp =>
    new MouvementStockService(
        connectionString,
        sp.GetRequiredService<ILogger<MouvementStockService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// AUTRES_MAGASIN
builder.Services.AddScoped<AutresMagasinService>(sp =>
    new AutresMagasinService(
        connectionString,
        sp.GetRequiredService<ILogger<AutresMagasinService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// Ordonnancement de l'inscription complète d'un tenant (organisation + entreprise + utilisateur + abonnement)
builder.Services.AddScoped<RegistrationOrchestrator>(sp =>
{

    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<RegistrationOrchestrator>>();
    var organisationService = sp.GetRequiredService<OrganisationService>();
    var entrepriseService = sp.GetRequiredService<EntrepriseService>();
    var usersService = sp.GetRequiredService<UsersService>();
    var subscriptionService = sp.GetRequiredService<SubscriptionService>();
    var smtpService = sp.GetRequiredService<SmtpEmailService>();
    var otpService = sp.GetRequiredService<OtpService>();
    //var configuration = sp.GetRequiredService<IConfiguration>();

    return new RegistrationOrchestrator(
        connectionString,
        logger,
        organisationService,
        entrepriseService,
        usersService,
        subscriptionService,
        smtpService,
        config,
        otpService


    );
});

// Ajouter le service Plans
builder.Services.AddScoped<PlansService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<PlansService>>();
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

    return new PlansService(
        config.GetConnectionString("DefaultConnection") ?? "",
        logger,
        httpContextAccessor
    );
});
// ============================================================================
// BUILD & CONFIGURATION MIDDLEWARE
// ============================================================================

var app = builder.Build();

// Swagger en développement
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TSALACH API v1");
        c.RoutePrefix = "swagger";
    });
}

// HTTPS redirection
app.UseHttpsRedirection();

// CORS
app.UseCors("AllowAll");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Mapping controllers
app.MapControllers();

// Message de démarrage
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("========================================");
logger.LogInformation("API TSALACH démarrée avec succès !");
logger.LogInformation("========================================");
logger.LogInformation("35 tables configurées");
logger.LogInformation("~200 endpoints disponibles");
logger.LogInformation("Swagger: https://localhost:5001/swagger");
logger.LogInformation("========================================");

app.Run();