using System.Data.Common;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Api.Catering;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Migrations.PostgreSql;
using ScoutCampPlanner.Migrations.Sqlite;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200", "tauri://localhost", "https://tauri.localhost")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ScoutCampPlanner.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = false;
        options.EventsType = typeof(PlatformCookieEvents);
        options.LoginPath = "/api/session";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options => options.AddPolicy("sign-in", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        })));
builder.Services.AddSingleton(TimeProvider.System);
if (int.TryParse(builder.Configuration["ParentProcessId"], out var parentProcessId) && parentProcessId > 0)
{
    builder.Services.AddSingleton(new ParentProcessMonitorOptions(parentProcessId));
    builder.Services.AddHostedService<ParentProcessMonitor>();
}

var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
var connectionString = builder.Configuration["Database:ConnectionString"]
    ?? throw new InvalidOperationException("Database connection string is missing.");
var sqliteBackupRetention = builder.Configuration.GetValue("Database:SqliteBackupRetention", 3);
if (sqliteBackupRetention < 1)
    throw new InvalidOperationException("Database:SqliteBackupRetention must be at least 1.");

builder.Services.AddScoped<DbConnection>(_ => provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
    ? new NpgsqlConnection(connectionString)
    : new SqliteConnection(connectionString));
builder.Services.AddDbContext<PlatformDbContext>((services, options) => Configure(options, services.GetRequiredService<DbConnection>(), provider, "platform"));
builder.Services.AddDbContext<CampDbContext>((services, options) => Configure(options, services.GetRequiredService<DbConnection>(), provider, "camp"));
builder.Services.AddDbContext<CateringDbContext>((services, options) => Configure(options, services.GetRequiredService<DbConnection>(), provider, "catering"));
builder.Services.AddScoped<CampPackageService>();
builder.Services.AddScoped<CampManagementService>();
builder.Services.AddScoped<CateringPlanningService>();
builder.Services.AddScoped<RecipeDraftStore>();
builder.Services.AddScoped<IRecipeDraftStore>(services => services.GetRequiredService<RecipeDraftStore>());
builder.Services.AddScoped<EfRecipeReferences>();
builder.Services.AddScoped<IRecipeRevisionSource>(services => services.GetRequiredService<EfRecipeReferences>());
builder.Services.AddScoped<IRecipePermanentDeleteAuthorization, PlatformRecipeAuthorization>();
builder.Services.AddScoped<RecipeLifecycleService>();
builder.Services.AddSingleton<IPasswordPolicy, PasswordPolicy>();
builder.Services.AddSingleton<IPasswordVerifier>(
    _ => new Argon2idPasswordVerifier(Argon2idOperatingMode.Server));
builder.Services.AddScoped<IInitialSetupService, InitialSetupService>();
builder.Services.AddScoped<IPasswordAuthenticationService, PasswordAuthenticationService>();
builder.Services.AddScoped<ISessionTerminationService, SessionTerminationService>();
builder.Services.AddScoped<PlatformCookieEvents>();
string auditDirectory = builder.Configuration["Audit:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "audit-security");
IAuditKeyBundleProtection auditProtection = OperatingSystem.IsWindows()
    ? new WindowsDpapiAuditKeyBundleProtection()
    : new PlainAuditKeyBundleProtection();
builder.Services.AddSingleton<IAuditProtectedMaterialStore>(
    new FileAuditProtectedMaterialStore(auditDirectory, auditProtection));
builder.Services.AddSingleton<IAuditSigningKeyProvider, ProtectedMaterialAuditSigningKeyProvider>();
builder.Services.AddSingleton<AuditRuntimeState>();
builder.Services.AddScoped<IAuditJournalAppender, AuditJournalAppender>();
builder.Services.AddScoped<IAuditedOperationExecutor, AuditedOperationExecutor>();
builder.Services.AddScoped<AuditRuntimeBootstrapper>();

var app = builder.Build();
app.MapOpenApi();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseBootstrapper.InitializeAsync(
        scope.ServiceProvider.GetRequiredService<DbConnection>(),
        scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
        scope.ServiceProvider.GetRequiredService<CampDbContext>(),
        scope.ServiceProvider.GetRequiredService<CateringDbContext>(),
        provider,
        scope.ServiceProvider.GetRequiredService<TimeProvider>(),
        sqliteBackupRetention);
    await scope.ServiceProvider.GetRequiredService<AuditRuntimeBootstrapper>().InitializeAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", databaseProvider = provider }));
app.MapGet("/api/setup/status", async (IInitialSetupService setup, CancellationToken cancellationToken) =>
    Results.Ok(await setup.GetStatusAsync(cancellationToken)));
app.MapPost("/api/setup", async (
    InitialSetupRequest request,
    IInitialSetupService setup,
    CancellationToken cancellationToken) =>
{
    InitialSetupResult result = await setup.CompleteAsync(request, cancellationToken);
    if (result.IsSuccessful)
        return Results.Created("/api/session", new { result.UserId, result.TenantId });

    return result.Failure == InitialSetupFailure.AlreadyCompleted
        ? Results.Conflict(new { code = "setup_already_completed" })
        : Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [result.Failure is InitialSetupFailure.InvalidTenantName ? "tenantName" :
                result.Failure is InitialSetupFailure.InvalidEmail ? "email" : "password"] =
                [result.Failure switch
                {
                    InitialSetupFailure.InvalidTenantName => "Bitte gib einen Namen für die Organisation ein.",
                    InitialSetupFailure.InvalidEmail => "Bitte gib eine gültige E-Mail-Adresse ein.",
                    InitialSetupFailure.PasswordTooShort => "Das Passwort muss mindestens 8 Zeichen lang sein.",
                    InitialSetupFailure.PasswordTooLong => "Das Passwort darf höchstens 128 Zeichen lang sein.",
                    _ => "Das Passwort ist zu leicht zu erraten.",
                }]
        });
});
app.MapPost("/api/session", async (
    SignInRequest request,
    IPasswordAuthenticationService authentication,
    HttpContext httpContext,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    SignInResult result = await authentication.SignInAsync(request, cancellationToken);
    if (!result.IsSuccessful || result.User is null || result.SessionId is null)
        return Results.Unauthorized();

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, result.User.UserId.ToString()),
        new Claim(PlatformClaimTypes.SessionId, result.SessionId.Value.ToString()),
    };
    DateTimeOffset now = timeProvider.GetUtcNow();
    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties
        {
            AllowRefresh = false,
            IsPersistent = false,
            IssuedUtc = now,
            ExpiresUtc = now.AddHours(12),
        });
    return Results.Ok(result.User);
}).RequireRateLimiting("sign-in");
app.MapGet("/api/session", async (
    ClaimsPrincipal principal,
    PlatformDbContext database,
    CancellationToken cancellationToken) =>
{
    Guid userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    string email = await database.UserAccounts.Where(value => value.Id == userId)
        .Select(value => value.Email).SingleAsync(cancellationToken);
    return Results.Ok(new { userId, email });
}).RequireAuthorization();
app.MapDelete("/api/session", async (
    ClaimsPrincipal principal,
    ISessionTerminationService termination,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (Guid.TryParse(principal.FindFirstValue(PlatformClaimTypes.SessionId), out Guid sessionId))
    {
        Guid userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await termination.SignOutAsync(sessionId, userId, cancellationToken);
    }
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
}).RequireAuthorization();
app.MapGet("/api/tenants", async (
    ClaimsPrincipal principal, CampManagementService management, CancellationToken cancellationToken) =>
    await management.ListTenantsAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken))
    .RequireAuthorization();
app.MapGet("/api/tenants/{tenantId:guid}/camp-administrator-candidates", async (
    Guid tenantId, ClaimsPrincipal principal, CampManagementService management, CancellationToken cancellationToken) =>
    await management.ListAdministratorCandidatesAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), tenantId, cancellationToken))
    .RequireAuthorization();
app.MapGet("/api/tenants/{tenantId:guid}/camps", async (
    Guid tenantId, ClaimsPrincipal principal, CampManagementService management, CancellationToken cancellationToken) =>
    await management.ListCampsAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), tenantId, cancellationToken))
    .RequireAuthorization();
app.MapPost("/api/tenants/{tenantId:guid}/camps", async (
    Guid tenantId, CreateCampRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    CreateCampResult result = await management.CreateAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), tenantId, request, cancellationToken);
    if (result.IsSuccessful)
        return Results.Created($"/api/tenants/{tenantId}/camps/{result.Camp!.Id}", result.Camp);
    return result.Failure == CreateCampFailure.Forbidden
        ? Results.Forbid()
        : Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [result.Failure switch
            {
                CreateCampFailure.InvalidName => "name",
                CreateCampFailure.InvalidPeriod or CreateCampFailure.DuplicateCamp => "period",
                _ => "initialAdministratorMembershipIds",
            }] =
                [result.Failure switch
                {
                    CreateCampFailure.InvalidName => "Bitte gib einen Lagernamen mit höchstens 200 Zeichen ein.",
                    CreateCampFailure.InvalidPeriod => "Das Enddatum darf nicht vor dem Startdatum liegen.",
                    CreateCampFailure.DuplicateCamp => "Ein Lager mit diesem Namen und Zeitraum existiert bereits.",
                    CreateCampFailure.MissingAdministrator => "Wähle mindestens einen Camp-Administrator aus.",
                    _ => "Die ausgewählten Camp-Administratoren sind nicht gültig.",
                }]
        });
}).RequireAuthorization();
app.MapGet("/api/tenants/{tenantId:guid}/stage-template", async (
    Guid tenantId, ClaimsPrincipal principal, CampManagementService management, CancellationToken cancellationToken) =>
{
    var entries = await management.GetStageTemplateAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), tenantId, cancellationToken);
    return entries is null ? Results.NotFound() : Results.Ok(entries);
}).RequireAuthorization();
app.MapPut("/api/tenants/{tenantId:guid}/stage-template", async (
    Guid tenantId, UpdateStageTemplateRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    var failure = await management.UpdateStageTemplateAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), tenantId, request, cancellationToken);
    return failure switch
    {
        UpdateStageTemplateFailure.None => Results.NoContent(),
        UpdateStageTemplateFailure.Forbidden => Results.Forbid(),
        _ => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["stageNames"] = ["Es werden 1 bis 50 eindeutige Stufennamen mit höchstens 100 Zeichen benötigt."],
        }),
    };
}).RequireAuthorization();
app.MapGet("/api/tenants/{tenantId:guid}/catering-stage-factors", async (
    Guid tenantId, ClaimsPrincipal principal, CampManagementService camps, CateringPlanningService catering,
    CancellationToken cancellationToken) =>
{
    var stages = await camps.GetStageTemplateAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), tenantId, cancellationToken);
    if (stages is null) return Results.NotFound();
    var factors = await catering.GetTenantFactorsAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), tenantId,
        stages.Select(value => value.Name).ToList(), cancellationToken);
    return factors is null ? Results.NotFound() : Results.Ok(factors);
}).RequireAuthorization();
app.MapPut("/api/tenants/{tenantId:guid}/catering-stage-factors", async (
    Guid tenantId, UpdateTenantStageFoodFactorsRequest request, ClaimsPrincipal principal,
    CampManagementService camps, CateringPlanningService catering, CancellationToken cancellationToken) =>
{
    Guid actorId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var stages = await camps.GetStageTemplateAsync(actorId, tenantId, cancellationToken);
    if (stages is null) return Results.NotFound();
    var failure = await catering.UpdateTenantFactorsAsync(actorId, tenantId,
        stages.Select(value => value.Name).ToList(), request, cancellationToken);
    return failure switch
    {
        UpdateFoodFactorsFailure.None => Results.NoContent(),
        UpdateFoodFactorsFailure.Forbidden => Results.Forbid(),
        _ => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["factors"] = ["Für jede Stufe wird ein Faktor von 0,1 bis 3,0 mit höchstens zwei Nachkommastellen benötigt."],
        }),
    };
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}", async (
    Guid campId, UpdateCampRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    UpdateCampResult result = await management.UpdateAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, request, cancellationToken);
    if (result.IsSuccessful) return Results.Ok(result.Camp);
    if (result.Failure == UpdateCampFailure.NotFound) return Results.NotFound();
    if (result.Failure == UpdateCampFailure.Frozen)
        return Results.Conflict(new { code = "camp_frozen" });
    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        [result.Failure == UpdateCampFailure.InvalidName ? "name" : "period"] =
            [result.Failure switch
            {
                UpdateCampFailure.InvalidName => "Bitte gib einen Lagernamen mit höchstens 200 Zeichen ein.",
                UpdateCampFailure.InvalidPeriod => "Das Enddatum darf nicht vor dem Startdatum liegen.",
                _ => "Ein Lager mit diesem Namen und Zeitraum existiert bereits.",
            }]
    });
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/structure", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService management,
    CancellationToken cancellationToken) =>
{
    IReadOnlyList<StructureNodeSummary>? nodes = await management.ListStructureAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, cancellationToken);
    return nodes is null ? Results.NotFound() : Results.Ok(nodes);
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/stages", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService management, CancellationToken cancellationToken) =>
{
    var entries = await management.GetCampStagesAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, cancellationToken);
    return entries is null ? Results.NotFound() : Results.Ok(entries);
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/planning-summary", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService management, CancellationToken cancellationToken) =>
{
    var summary = await management.GetPlanningSummaryAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, cancellationToken);
    return summary is null ? Results.NotFound() : Results.Ok(summary);
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/catering-stage-factors", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService camps, CateringPlanningService catering,
    CancellationToken cancellationToken) =>
{
    var context = await camps.GetCampStageContextAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, cancellationToken);
    if (context is null) return Results.NotFound();
    return Results.Ok(await catering.GetCampFactorsAsync(context.TenantId, campId,
        context.Stages.Select(value => new CampStageReference(value.Id, value.Name)).ToList(), cancellationToken));
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/catering-stage-factors", async (
    Guid campId, UpdateCampStageFoodFactorsRequest request, ClaimsPrincipal principal,
    CampManagementService camps, CateringPlanningService catering, CancellationToken cancellationToken) =>
{
    Guid actorId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    if (!await camps.HasCampPermissionAsync(actorId, campId,
        ScoutCampPlanner.Platform.Application.Authorization.Permissions.Camp.Edit, cancellationToken))
        return Results.NotFound();
    var context = await camps.GetCampStageContextAsync(actorId, campId, cancellationToken);
    if (context is null) return Results.NotFound();
    var failure = await catering.UpdateCampFactorsAsync(actorId, context.TenantId, campId,
        context.Stages.Select(value => new CampStageReference(value.Id, value.Name)).ToList(), request, cancellationToken);
    return failure == UpdateFoodFactorsFailure.None ? Results.NoContent() : Results.ValidationProblem(
        new Dictionary<string, string[]> { ["factors"] = ["Ungültige Lagerfaktoren."] });
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/weighted-food-summary", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService camps, CateringPlanningService catering,
    CancellationToken cancellationToken) =>
{
    Guid actorId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var context = await camps.GetCampStageContextAsync(actorId, campId, cancellationToken);
    var summary = await camps.GetPlanningSummaryAsync(actorId, campId, cancellationToken);
    if (context is null || summary is null) return Results.NotFound();
    var factors = await catering.GetCampFactorsAsync(context.TenantId, campId,
        context.Stages.Select(value => new CampStageReference(value.Id, value.Name)).ToList(), cancellationToken);
    return Results.Ok(CateringPlanningService.CalculateWeightedTotals(summary, factors));
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/meal-plan", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService camps, CateringPlanningService catering,
    CancellationToken cancellationToken) =>
{
    Guid actorId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var context = await camps.GetCampMealContextAsync(actorId, campId, cancellationToken);
    return context is null ? Results.NotFound() : Results.Ok(await catering.GetMealPlanAsync(
        campId, context.StartDate, context.EndDate, cancellationToken));
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/meal-types", async (
    Guid campId, UpdateCampMealTypesRequest request, ClaimsPrincipal principal,
    CampManagementService camps, CateringPlanningService catering, CancellationToken cancellationToken) =>
{
    Guid actorId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    if (!await camps.HasCampPermissionAsync(actorId, campId,
        ScoutCampPlanner.Platform.Application.Authorization.Permissions.Camp.Edit, cancellationToken)) return Results.NotFound();
    var context = await camps.GetCampMealContextAsync(actorId, campId, cancellationToken);
    if (context is null) return Results.NotFound();
    var failure = await catering.UpdateMealTypesAsync(actorId, context.TenantId, campId,
        context.StartDate, context.EndDate, context.IsFrozen, request, cancellationToken);
    return failure switch { UpdateCampMealsFailure.None => Results.NoContent(),
        UpdateCampMealsFailure.Frozen => Results.Conflict(new { code = "camp_frozen" }),
        _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["names"] = ["Ungültige oder doppelte Mahlzeitenbezeichnungen."] }) };
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/meals/{mealId:guid}/activity", async (
    Guid campId, Guid mealId, UpdateCampMealActivityRequest request, ClaimsPrincipal principal,
    CampManagementService camps, CateringPlanningService catering, CancellationToken cancellationToken) =>
{
    Guid actorId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    if (!await camps.HasCampPermissionAsync(actorId, campId,
        ScoutCampPlanner.Platform.Application.Authorization.Permissions.Camp.Edit, cancellationToken)) return Results.NotFound();
    var context = await camps.GetCampMealContextAsync(actorId, campId, cancellationToken);
    if (context is null) return Results.NotFound();
    var failure = await catering.SetMealActivityAsync(actorId, context.TenantId, campId, mealId, context.IsFrozen, request, cancellationToken);
    return failure switch { UpdateCampMealsFailure.None => Results.NoContent(),
        UpdateCampMealsFailure.Frozen => Results.Conflict(new { code = "camp_frozen" }), _ => Results.NotFound() };
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/stages", async (
    Guid campId, UpdateStageTemplateRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    var failure = await management.UpdateCampStagesAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, request, cancellationToken);
    return failure switch
    {
        UpdateStageTemplateFailure.None => Results.NoContent(),
        UpdateStageTemplateFailure.Forbidden => Results.Forbid(),
        _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["stageNames"] = ["Ungültige Lagerstufen."] }),
    };
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/structure/configuration", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService management, CancellationToken cancellationToken) =>
{
    StructureConfiguration? configuration = await management.GetStructureConfigurationAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, cancellationToken);
    return configuration is null ? Results.NotFound() : Results.Ok(configuration);
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/structure/configuration", async (
    Guid campId, UpdateStructureConfigurationRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    bool updated = await management.UpdateStructureConfigurationAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, request, cancellationToken);
    return updated ? Results.NoContent() : Results.Conflict(new { code = "invalid_structure_configuration" });
}).RequireAuthorization();
app.MapPost("/api/camps/{campId:guid}/structure", async (
    Guid campId, CreateStructureNodeRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    CreateStructureNodeResult result = await management.CreateStructureNodeAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, request, cancellationToken);
    if (result.IsSuccessful)
        return Results.Created($"/api/camps/{campId}/structure/{result.Node!.Id}", result.Node);
    if (result.Failure == CreateStructureNodeFailure.NotFound) return Results.NotFound();
    if (result.Failure == CreateStructureNodeFailure.Frozen)
        return Results.Conflict(new { code = "camp_frozen" });
    if (result.Failure == CreateStructureNodeFailure.MaximumDepthReached)
        return Results.Conflict(new { code = "maximum_structure_depth_reached" });
    if (result.Failure == CreateStructureNodeFailure.HasEstimates)
        return Results.Conflict(new { code = "structure_node_has_estimates" });
    return Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["name"] = [result.Failure == CreateStructureNodeFailure.DuplicateName
            ? "Auf dieser Ebene existiert bereits ein Eintrag mit diesem Namen."
            : "Bitte gib einen Namen mit höchstens 200 Zeichen ein."]
    });
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/structure/{nodeId:guid}", async (
    Guid campId, Guid nodeId, RenameStructureNodeRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    var failure = await management.RenameStructureNodeAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, nodeId, request, cancellationToken);
    return failure switch
    {
        RenameStructureNodeFailure.None => Results.NoContent(),
        RenameStructureNodeFailure.NotFound => Results.NotFound(),
        RenameStructureNodeFailure.Frozen => Results.Conflict(new { code = "camp_frozen" }),
        RenameStructureNodeFailure.DuplicateName => Results.Conflict(new { code = "duplicate_structure_name" }),
        _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Bitte gib einen Namen mit höchstens 200 Zeichen ein."] }),
    };
}).RequireAuthorization();
app.MapDelete("/api/camps/{campId:guid}/structure/{nodeId:guid}", async (
    Guid campId, Guid nodeId, ClaimsPrincipal principal, CampManagementService management,
    CancellationToken cancellationToken) =>
{
    DeleteStructureNodeFailure failure = await management.DeleteStructureNodeAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, nodeId, cancellationToken);
    return failure switch
    {
        DeleteStructureNodeFailure.None => Results.NoContent(),
        DeleteStructureNodeFailure.NotFound => Results.NotFound(),
        DeleteStructureNodeFailure.Frozen => Results.Conflict(new { code = "camp_frozen" }),
        DeleteStructureNodeFailure.HasChildren => Results.Conflict(new { code = "structure_node_has_children" }),
        _ => Results.Conflict(new { code = "structure_node_has_estimates" }),
    };
}).RequireAuthorization();
app.MapGet("/api/camps/{campId:guid}/structure/{nodeId:guid}/participant-estimates", async (
    Guid campId, Guid nodeId, ClaimsPrincipal principal, CampManagementService management,
    CancellationToken cancellationToken) =>
{
    var estimates = await management.GetParticipantEstimatesAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, nodeId, cancellationToken);
    return estimates is null ? Results.NotFound() : Results.Ok(estimates);
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/structure/{nodeId:guid}/participant-estimates", async (
    Guid campId, Guid nodeId, UpdateParticipantEstimatesRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    var failure = await management.UpdateParticipantEstimatesAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, nodeId, request, cancellationToken);
    return failure switch
    {
        UpdateParticipantEstimatesFailure.None => Results.NoContent(),
        UpdateParticipantEstimatesFailure.NotFound => Results.NotFound(),
        UpdateParticipantEstimatesFailure.Frozen => Results.Conflict(new { code = "camp_frozen" }),
        UpdateParticipantEstimatesFailure.NotLeaf => Results.Conflict(new { code = "structure_node_not_leaf" }),
        UpdateParticipantEstimatesFailure.NotParticipantLevel => Results.Conflict(new { code = "structure_node_not_participant_level" }),
        _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["estimates"] = ["Ungültige Schätzwerte."] }),
    };
}).RequireAuthorization();
app.MapPut("/api/camps/{campId:guid}/structure/{nodeId:guid}/parent", async (
    Guid campId, Guid nodeId, MoveStructureNodeRequest request, ClaimsPrincipal principal,
    CampManagementService management, CancellationToken cancellationToken) =>
{
    MoveStructureNodeFailure failure = await management.MoveStructureNodeAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, nodeId, request, cancellationToken);
    return failure switch
    {
        MoveStructureNodeFailure.None => Results.NoContent(),
        MoveStructureNodeFailure.NotFound => Results.NotFound(),
        MoveStructureNodeFailure.Frozen => Results.Conflict(new { code = "camp_frozen" }),
        MoveStructureNodeFailure.Cycle => Results.Conflict(new { code = "structure_cycle" }),
        MoveStructureNodeFailure.DuplicateName => Results.Conflict(new { code = "duplicate_structure_name" }),
        _ => Results.Conflict(new { code = "maximum_structure_depth_reached" }),
    };
}).RequireAuthorization();
app.MapGet("/api/camps", () => Results.BadRequest(new { code = "tenant_context_required" }))
    .RequireAuthorization();
app.MapPost("/api/camps/{campId:guid}/offline-package", async (
    Guid campId, ClaimsPrincipal principal, CampManagementService management,
    CampPackageService packages, CancellationToken cancellationToken) =>
    await management.HasCampPermissionAsync(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId,
        ScoutCampPlanner.Platform.Application.Authorization.Permissions.Camp.ExportPackage, cancellationToken)
        ? Results.File(await packages.StartOfflineTransferAsync(campId, cancellationToken),
            "application/vnd.scoutcampplanner.camp-package", $"camp-{campId}.scoutcamp")
        : Results.NotFound())
    .RequireAuthorization();
app.MapPost("/api/packages/import-initial", async (HttpRequest request, CampPackageService packages, CancellationToken cancellationToken) =>
{
    using var stream = new MemoryStream();
    await request.Body.CopyToAsync(stream, cancellationToken);
    await packages.ImportInitialPackageAsync(stream.ToArray(), cancellationToken);
    return Results.NoContent();
}).RequireAuthorization();
app.MapPost("/api/camps/{campId:guid}/return-package", async (Guid campId, CampPackageService packages, CancellationToken cancellationToken) =>
    Results.File(await packages.CreateReturnPackageAsync(campId, cancellationToken), "application/vnd.scoutcampplanner.camp-package", $"camp-{campId}-return.scoutcamp"))
    .RequireAuthorization();
app.MapPost("/api/packages/import-return", async (HttpRequest request, CampPackageService packages, CancellationToken cancellationToken) =>
{
    using var stream = new MemoryStream();
    await request.Body.CopyToAsync(stream, cancellationToken);
    await packages.ImportReturnPackageAsync(stream.ToArray(), cancellationToken);
    return Results.NoContent();
}).RequireAuthorization();

app.Run();

static void Configure(DbContextOptionsBuilder options, DbConnection connection, string provider, string module)
{
    if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connection, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(PostgreSqlMigrationsAssembly).Assembly.FullName);
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", module);
        });
    }
    else
    {
        options.UseSqlite(connection, sqlite =>
        {
            sqlite.MigrationsAssembly(typeof(SqliteMigrationsAssembly).Assembly.FullName);
            sqlite.MigrationsHistoryTable($"__EFMigrationsHistory_{module}");
        });
    }
}
