using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Migrations.Sqlite;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.MealPlanningSeed;

try
{
    await RunAsync(args);
}
catch (Exception exception)
{
    Console.Error.WriteLine("TEST seed failed; current generation was not replaced: " + exception);
    Environment.ExitCode = 1;
}

static async Task RunAsync(string[] args)
{
// Standalone dev composition root: never referenced/published by the API or desktop.
if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") is not ("Development" or "Test") ||
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is string environment && environment is not ("Development" or "Test"))
    throw new InvalidOperationException("Seed requires an explicit Development/Test environment.");
if (args.Length > 1 || args.Length == 1 && args[0] is not ("--reset" or "--probe" or "--nutrition"))
    throw new ArgumentException("Only --reset/--probe/--nutrition is supported. Arbitrary database targets are deliberately forbidden.");
var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "ScoutCampPlanner.slnx"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("Run from the source repository.");
string directory = Path.Combine(root.FullName, ".dev-data", "meal-planning-increment-2");
Directory.CreateDirectory(directory);
if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
    (File.GetAttributes(Path.GetDirectoryName(directory)!) & FileAttributes.ReparsePoint) != 0)
    throw new InvalidOperationException("Redirected dev-data directories are not supported.");
using var seedLock = new FileStream(Path.Combine(directory, "seed.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
string manifest = Path.Combine(directory, "current.json");
if (File.Exists(manifest) && args.Length == 0)
{
    Console.WriteLine("Existing TEST generation retained (no writes to database). Use --reset for a fresh generation.");
    Console.WriteLine(manifest);
    return;
}
// A reset NEVER deletes or overwrites a database. Publish the pointer only after successful validation.
string generation = "TEST-" + Guid.NewGuid().ToString("N");
string database = Path.Combine(directory, generation + ".db");
if (args.Contains("--nutrition"))
{
    using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifest));
    string filename = manifestDocument.RootElement.GetProperty("database").GetString() ?? "";
    generation = manifestDocument.RootElement.GetProperty("generation").GetString() ?? "";
    if (manifestDocument.RootElement.GetProperty("seed").GetString() != "meal-planning-increment-2-v1" ||
        !System.Text.RegularExpressions.Regex.IsMatch(filename, "^TEST-[0-9a-f]{32}\\.db$") || filename != generation + ".db")
        throw new InvalidOperationException("Invalid TEST generation pointer.");
    database = Path.Combine(directory, filename);
    if (!File.Exists(database) || (File.GetAttributes(database) & FileAttributes.ReparsePoint) != 0)
        throw new InvalidOperationException("The TEST database must exist and must not be redirected.");
}
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false, Mode = args.Contains("--nutrition") ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadWriteCreate }.ToString());
await connection.OpenAsync();
DbContextOptions<T> Options<T>(string module) where T : DbContext => new DbContextOptionsBuilder<T>()
    .UseSqlite(connection, sqlite => sqlite.MigrationsAssembly(typeof(SqliteMigrationsAssembly).Assembly.FullName)
        .MigrationsHistoryTable("__EFMigrationsHistory_" + module)).Options;
await using var platform = new PlatformDbContext(Options<PlatformDbContext>("platform"));
await using var camp = new CampDbContext(Options<CampDbContext>("camp"));
await using var catering = new CateringDbContext(Options<CateringDbContext>("catering"));
if (args.Contains("--nutrition"))
{
    if (!await camp.Camps.AnyAsync(value => value.Id == AcceptanceSeed.Id("camp") && value.Name.StartsWith("TEST ")) ||
        !await platform.UserAccounts.AnyAsync(value => value.Id == AcceptanceSeed.Id("actor") && value.Email == "acceptance@example.test"))
        throw new InvalidOperationException("The selected generation is not the expected acceptance-test camp.");
    string backupPath = Path.Combine(directory, generation + "-before-nutrition-" + Guid.NewGuid().ToString("N") + ".db");
    await using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString()))
    {
        await backup.OpenAsync(); connection.BackupDatabase(backup);
    }
    Console.WriteLine("TEST database backup: " + backupPath);
    await NutritionFixtures.AddAsync(catering);
    return; // Keep current.json and all existing fixtures unchanged.
}
await platform.Database.MigrateAsync();
await camp.Database.MigrateAsync();
await catering.Database.MigrateAsync();
await AcceptanceSeed.CreateAsync(platform, camp, catering);
if (args.Contains("--probe"))
{
    await PackageProbe.RunAsync(platform, camp, catering, Path.Combine(directory, generation + "-local.db"));
    return; // Never change the user's active test generation.
}
string pointer = JsonSerializer.Serialize(new { seed = "meal-planning-increment-2-v1", generation, database = Path.GetFileName(database) });
string temporaryManifest = Path.Combine(directory, generation + ".json");
await File.WriteAllTextAsync(temporaryManifest, pointer);
File.Move(temporaryManifest, manifest, overwrite: true);
Console.WriteLine("PASS: TEST seed created and validated. Previous generations are untouched.");
Console.WriteLine(database);
Console.WriteLine("Login: acceptance@example.test / TEST-only-MealPlanning-2026!");
}
