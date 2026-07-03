using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

// Services de base
using Api_BuildTech.Services;
using Api_BuildTech.Services.messagerie;

// Controllers - Articles & Stock
using Api_BuildTech.Controllers.Articles;
using Api_BuildTech.Controllers.ArticleStock;
using Api_BuildTech.Controllers.Categorie;

// Controllers - Gestion Métier
using Api_BuildTech.Controllers.Clients;
using Api_BuildTech.Controllers.Fournisseurs;
using Api_BuildTech.Controllers.Organisation;
using Api_BuildTech.Controllers.Entreprise;
using Api_BuildTech.Controllers.Users;

// Controllers - Restaurant & Tables
using Api_BuildTech.Controllers.Table;
using Api_BuildTech.Controllers.Serveur;
using Api_BuildTech.Controllers.Sessions;
using Api_BuildTech.Controllers.DomaineRestaurant;

// Controllers - Configuration
using Api_BuildTech.Controllers.TypeServices;
using Api_BuildTech.Controllers.TypePaiement;
using Api_BuildTech.Controllers.UniteMesures;
using Api_BuildTech.Controllers.ParametrePos;
using Api_BuildTech.Controllers.ModelRecu;

// Controllers - Factures & Transactions
using Api_BuildTech.Controllers.Factures;
using Api_BuildTech.Controllers.DetailTransactions;
using Api_BuildTech.Controllers.Paiments;
using Api_BuildTech.Controllers.POS;

// Controllers - Articles & Composants
using Api_BuildTech.Controllers.Composants;
using Api_BuildTech.Controllers.CategorieComposants;
using Api_BuildTech.Controllers.CompositionArticle;

// Controllers - Stock & Matières Premières
using Api_BuildTech.Controllers.MatierePremiere;
using Api_BuildTech.Controllers.MouvementStock;
using Api_BuildTech.Controllers.AutresMagasin;

// Controllers - Livraison
using Api_BuildTech.Controllers.Livreur;
using Api_BuildTech.Controllers.Livraisons;
using Api_BuildTech.Controllers.DetailLivraisons;

// Controllers - Authentification & Registration
using Api_BuildTech.Controllers.Authentication;
using Api_BuildTech.Controllers.Registration;
using Api_BuildTech.Controllers.Otp;
using Api_BuildTech.Controllers.Subscription;
using Api_BuildTech.Controllers.Plans;
using Api_BuildTech.Controllers.Synchronisation;
using Api_BuildTech.Controllers.Statistics;

// ============================================================================
// CONSTRUCTION DE L'APPLICATION
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. CONFIGURATION DE BASE
// ============================================================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

// ============================================================================
// 2. SWAGGER CONFIGURATION
// ============================================================================

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BuildTechPlatforme API",
        Version = "v1",
        Description = "API de gestion de restaurant multi-tenant avec POS intégré"
    });

    // Support JWT dans Swagger
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
});

// ============================================================================
// 3. CONFIGURATION CORS
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
// 4. CONFIGURATION JWT AUTHENTICATION
// ============================================================================

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey manquante dans appsettings.json");

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
// 5. CONNECTION STRING
// ============================================================================

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionString 'DefaultConnection' non trouvée dans appsettings.json");

// ============================================================================
// 6. INJECTION DE DÉPENDANCES - SERVICES DE BASE
// ============================================================================

// Services essentiels
builder.Services.AddScoped<DatabaseService>(sp =>
    new DatabaseService(
        connectionString,
        sp.GetRequiredService<ILogger<DatabaseService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<SmtpEmailService>();

builder.Services.AddScoped<OtpService>(sp =>
    new OtpService(
        connectionString,
        sp.GetRequiredService<ILogger<OtpService>>(),
        sp.GetRequiredService<SmtpEmailService>(),
        sp.GetRequiredService<IConfiguration>()
    ));

builder.Services.AddScoped<AuthService>(sp =>
    new AuthService(
        connectionString,
        sp.GetRequiredService<ILogger<AuthService>>(),
        sp.GetRequiredService<IHttpContextAccessor>(),
        sp.GetRequiredService<IConfiguration>()
    ));

builder.Services.AddScoped<SyncService>(sp =>
    new SyncService(
        connectionString,
        sp.GetRequiredService<ILogger<SyncService>>()
    ));

builder.Services.AddScoped<SubscriptionService>(sp =>
    new SubscriptionService(
        connectionString,
        sp.GetRequiredService<ILogger<SubscriptionService>>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 7. INJECTION DE DÉPENDANCES - GESTION ENTREPRISE & UTILISATEURS
// ============================================================================

builder.Services.AddScoped<OrganisationService>(sp =>
    new OrganisationService(
        connectionString,
        sp.GetRequiredService<ILogger<OrganisationService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

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
// 8. INJECTION DE DÉPENDANCES - GESTION ARTICLES & CATÉGORIES
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

builder.Services.AddScoped<ArticleStockService>(sp =>
    new ArticleStockService(
        connectionString,
        sp.GetRequiredService<ILogger<ArticleStockService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 9. INJECTION DE DÉPENDANCES - GESTION CLIENTS & FOURNISSEURS
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
// 10. INJECTION DE DÉPENDANCES - FACTURES & TABLES
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
// 11. INJECTION DE DÉPENDANCES - CONFIGURATION & PARAMÈTRES
// ============================================================================

builder.Services.AddScoped<TypeServicesService>(sp =>
    new TypeServicesService(
        connectionString,
        sp.GetRequiredService<ILogger<TypeServicesService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<TypePaiementService>(sp =>
    new TypePaiementService(
        connectionString,
        sp.GetRequiredService<ILogger<TypePaiementService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<UniteMesuresService>(sp =>
    new UniteMesuresService(
        connectionString,
        sp.GetRequiredService<ILogger<UniteMesuresService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<ParametrePosService>(sp =>
    new ParametrePosService(
        connectionString,
        sp.GetRequiredService<ILogger<ParametrePosService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<ModelRecuService>(sp =>
    new ModelRecuService(
        connectionString,
        sp.GetRequiredService<ILogger<ModelRecuService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 12. INJECTION DE DÉPENDANCES - RESTAURANT ESSENTIELS
// ============================================================================

builder.Services.AddScoped<DomaineRestaurantService>(sp =>
    new DomaineRestaurantService(
        connectionString,
        sp.GetRequiredService<ILogger<DomaineRestaurantService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<ServeurService>(sp =>
    new ServeurService(
        connectionString,
        sp.GetRequiredService<ILogger<ServeurService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<SessionsService>(sp =>
    new SessionsService(
        connectionString,
        sp.GetRequiredService<ILogger<SessionsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 13. INJECTION DE DÉPENDANCES - TRANSACTIONS CRITIQUES
// ============================================================================

builder.Services.AddScoped<DetailTransactionsService>(sp =>
    new DetailTransactionsService(
        connectionString,
        sp.GetRequiredService<ILogger<DetailTransactionsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<PaimentsService>(sp =>
    new PaimentsService(
        connectionString,
        sp.GetRequiredService<ILogger<PaimentsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 14. INJECTION DE DÉPENDANCES - ARTICLES & COMPOSANTS
// ============================================================================

builder.Services.AddScoped<CategorieComposantsService>(sp =>
    new CategorieComposantsService(
        connectionString,
        sp.GetRequiredService<ILogger<CategorieComposantsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<ComposantsService>(sp =>
    new ComposantsService(
        connectionString,
        sp.GetRequiredService<ILogger<ComposantsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<CompositionArticleService>(sp =>
    new CompositionArticleService(
        connectionString,
        sp.GetRequiredService<ILogger<CompositionArticleService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 15. INJECTION DE DÉPENDANCES - STOCK & MATIÈRES PREMIÈRES
// ============================================================================

builder.Services.AddScoped<MatierePremiereService>(sp =>
    new MatierePremiereService(
        connectionString,
        sp.GetRequiredService<ILogger<MatierePremiereService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<MouvementStockService>(sp =>
    new MouvementStockService(
        connectionString,
        sp.GetRequiredService<ILogger<MouvementStockService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<AutresMagasinService>(sp =>
    new AutresMagasinService(
        connectionString,
        sp.GetRequiredService<ILogger<AutresMagasinService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 16. INJECTION DE DÉPENDANCES - SYSTÈME LIVRAISON
// ============================================================================

builder.Services.AddScoped<LivreurService>(sp =>
    new LivreurService(
        connectionString,
        sp.GetRequiredService<ILogger<LivreurService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<LivraisonsService>(sp =>
    new LivraisonsService(
        connectionString,
        sp.GetRequiredService<ILogger<LivraisonsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<DetailLivraisonsService>(sp =>
    new DetailLivraisonsService(
        connectionString,
        sp.GetRequiredService<ILogger<DetailLivraisonsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

// ============================================================================
// 17. INJECTION DE DÉPENDANCES - PLANS & SUBSCRIPTION
// ============================================================================

builder.Services.AddScoped<PlansService>(sp =>
    new PlansService(
        connectionString,
        sp.GetRequiredService<ILogger<PlansService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<RegistrationOrchestrator>(sp =>
    new RegistrationOrchestrator(
        connectionString,
        sp.GetRequiredService<ILogger<RegistrationOrchestrator>>(),
        sp.GetRequiredService<OrganisationService>(),
        sp.GetRequiredService<EntrepriseService>(),
        sp.GetRequiredService<UsersService>(),
        sp.GetRequiredService<SubscriptionService>(),
        sp.GetRequiredService<SmtpEmailService>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<OtpService>()
    ));

// ============================================================================
// 18. INJECTION DE DÉPENDANCES - POS (Point of Sale)
// ============================================================================

builder.Services.AddScoped<PosService>(sp =>
    new PosService(
        connectionString,
        sp.GetRequiredService<ILogger<PosService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));

builder.Services.AddScoped<StatisticsService>(sp =>
    new StatisticsService(
        connectionString,
        sp.GetRequiredService<ILogger<StatisticsService>>(),
        sp.GetRequiredService<IHttpContextAccessor>()
    ));
builder.Services.AddScoped<RapportsPdfService>();
// ============================================================================
// 19. BUILD DE L'APPLICATION
// ============================================================================

var app = builder.Build();

// ============================================================================
// 20. CONFIGURATION MIDDLEWARE
// ============================================================================

// Swagger en développement
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BuildTechPlatforme API v1");
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

// ============================================================================
// 21. MESSAGES DE DÉMARRAGE
// ============================================================================

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("========================================");
logger.LogInformation("🚀 API BuildTechPlatforme démarrée avec succès !");
logger.LogInformation("========================================");
logger.LogInformation("📊 Configuration:");
logger.LogInformation("   - 35 tables SQL configurées");
logger.LogInformation("   - ~210 endpoints API disponibles");
logger.LogInformation("   - POS intégré (9 endpoints)");
logger.LogInformation("   - Multi-tenant activé");
logger.LogInformation("   - JWT authentication activée");
logger.LogInformation("========================================");
logger.LogInformation("📚 Swagger: https://localhost:7xxx/swagger");
logger.LogInformation("========================================");

// ============================================================================
// 22. RUN APPLICATION
// ============================================================================

app.Run();
