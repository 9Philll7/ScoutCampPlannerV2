using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ScoutCampPlanner.Package;

public static class CampPackageSerializer
{
    private const string PayloadEntryName = "payload.json";
    private const string ChecksumEntryName = "payload.sha256";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static byte[] Serialize(CampPackagePayload package)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(package, Options);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, PayloadEntryName, payload);
            WriteEntry(archive, ChecksumEntryName, System.Text.Encoding.ASCII.GetBytes(Convert.ToHexString(SHA256.HashData(payload))));
        }
        return output.ToArray();
    }

    public static CampPackagePayload Deserialize(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            using var input = new MemoryStream(bytes.ToArray());
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            var payload = ReadEntry(archive, PayloadEntryName);
            var checksum = System.Text.Encoding.ASCII.GetString(ReadEntry(archive, ChecksumEntryName));
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(checksum),
                    SHA256.HashData(payload)))
                throw new CampPackageValidationException("Package checksum is invalid.");

            var package = JsonSerializer.Deserialize<CampPackagePayload>(payload, Options)
                ?? throw new CampPackageValidationException("Package payload is empty.");
            Validate(package);
            return package;
        }
        catch (CampPackageValidationException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or FormatException)
        {
            throw new CampPackageValidationException($"Package cannot be read: {exception.Message}");
        }
    }

    private static void Validate(CampPackagePayload package)
    {
        if (package.Manifest.FormatVersion != CampPackageVersions.Current)
            throw new CampPackageValidationException($"Unsupported package version {package.Manifest.FormatVersion}.");
        if (package.Manifest.TenantId != package.Tenant.Id || package.Manifest.CampId != package.Camp.Id)
            throw new CampPackageValidationException("Manifest identity does not match package data.");
        if (package.Camp.TenantId != package.Tenant.Id)
            throw new CampPackageValidationException("Camp does not belong to package tenant.");
        if (package.Camp.StartDate == default || package.Camp.EndDate == default ||
            package.Camp.EndDate < package.Camp.StartDate)
            throw new CampPackageValidationException("Camp period is invalid or missing.");
        if (package.CookingUnits.Any(x => x.CampId != package.Camp.Id) || package.MealPlans.Any(x => x.CampId != package.Camp.Id))
            throw new CampPackageValidationException("Package contains data for another camp.");
        var expectedModules = new[] { "Camp", "Catering" };
        if (!expectedModules.All(package.Manifest.IncludedModules.Contains))
            throw new CampPackageValidationException("Required module data is missing.");
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] data)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(data);
    }

    private static byte[] ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new CampPackageValidationException($"Package entry '{name}' is missing.");
        using var stream = entry.Open();
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
