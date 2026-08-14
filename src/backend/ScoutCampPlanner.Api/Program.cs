using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Migrations.PostgreSql;
using ScoutCampPlanner.Migrations.Sqlite;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200", "tauri://localhost", "https://tauri.localhost")
    .AllowAnyHeader()
    .AllowAnyMethod()));
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
builder.Services.AddSingleton<IPasswordPolicy, PasswordPolicy>();
builder.Services.AddSingleton<IPasswordVerifier>(
    _ => new Argon2idPasswordVerifier(Argon2idOperatingMode.Server));
builder.Services.AddScoped<IInitialSetupService, InitialSetupService>();

var app = builder.Build();
app.MapOpenApi();
app.UseCors();

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
app.MapGet("/api/camps", async (CampDbContext db, CancellationToken cancellationToken) =>
    await db.Camps.Select(x => new { x.Id, x.TenantId, x.Name, x.IsFrozen }).ToListAsync(cancellationToken));
app.MapPost("/api/camps/{campId:guid}/offline-package", async (Guid campId, CampPackageService packages, CancellationToken cancellationToken) =>
    Results.File(await packages.StartOfflineTransferAsync(campId, cancellationToken), "application/vnd.scoutcampplanner.camp-package", $"camp-{campId}.scoutcamp"));
app.MapPost("/api/packages/import-initial", async (HttpRequest request, CampPackageService packages, CancellationToken cancellationToken) =>
{
    using var stream = new MemoryStream();
    await request.Body.CopyToAsync(stream, cancellationToken);
    await packages.ImportInitialPackageAsync(stream.ToArray(), cancellationToken);
    return Results.NoContent();
});
app.MapPost("/api/camps/{campId:guid}/return-package", async (Guid campId, CampPackageService packages, CancellationToken cancellationToken) =>
    Results.File(await packages.CreateReturnPackageAsync(campId, cancellationToken), "application/vnd.scoutcampplanner.camp-package", $"camp-{campId}-return.scoutcamp"));
app.MapPost("/api/packages/import-return", async (HttpRequest request, CampPackageService packages, CancellationToken cancellationToken) =>
{
    using var stream = new MemoryStream();
    await request.Body.CopyToAsync(stream, cancellationToken);
    await packages.ImportReturnPackageAsync(stream.ToArray(), cancellationToken);
    return Results.NoContent();
});

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
