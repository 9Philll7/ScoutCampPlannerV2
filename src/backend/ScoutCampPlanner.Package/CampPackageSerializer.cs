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
        if (package.CampStages is null || package.ParticipantEstimates is null || package.CampStageFoodFactors is null)
            throw new CampPackageValidationException("Camp stages are missing.");
        if (package.Manifest.FormatVersion != CampPackageVersions.Current)
            throw new CampPackageValidationException($"Unsupported package version {package.Manifest.FormatVersion}.");
        if (package.Manifest.TenantId != package.Tenant.Id || package.Manifest.CampId != package.Camp.Id)
            throw new CampPackageValidationException("Manifest identity does not match package data.");
        if (package.Camp.TenantId != package.Tenant.Id)
            throw new CampPackageValidationException("Camp does not belong to package tenant.");
        if (package.Camp.StartDate == default || package.Camp.EndDate == default ||
            package.Camp.EndDate < package.Camp.StartDate)
            throw new CampPackageValidationException("Camp period is invalid or missing.");
        if (!Enum.TryParse<ScoutCampPlanner.Camp.Domain.CampStructureMode>(
                package.Camp.StructureMode, ignoreCase: false, out var structureMode))
            throw new CampPackageValidationException("Camp structure mode is invalid.");
        if (package.Camp.StructureLevelNames is null ||
            structureMode == ScoutCampPlanner.Camp.Domain.CampStructureMode.Free && package.Camp.StructureLevelNames.Count != 0 ||
            structureMode == ScoutCampPlanner.Camp.Domain.CampStructureMode.Fixed && package.Camp.StructureLevelNames.Count == 0 ||
            package.Camp.StructureLevelNames.Any(name => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) ||
            package.Camp.StructureLevelNames.Select(name => name.Trim().ToUpperInvariant()).Distinct().Count() != package.Camp.StructureLevelNames.Count)
            throw new CampPackageValidationException("Camp structure levels are invalid.");
        if (package.CampStages.Any(x => x.CampId != package.Camp.Id) ||
            package.ParticipantEstimates.Any(x => x.CampId != package.Camp.Id) ||
            package.CampStageFoodFactors.Any(x => x.CampId != package.Camp.Id) ||
            package.StructureNodes.Any(x => x.CampId != package.Camp.Id) ||
            package.MealPlans.Any(x => x.CampId != package.Camp.Id))
            throw new CampPackageValidationException("Package contains data for another camp.");
        if (package.CampStages.Count == 0 || package.CampStages.Select(x => x.Id).Distinct().Count() != package.CampStages.Count ||
            package.CampStages.Select(x => x.Name.Trim().ToUpperInvariant()).Distinct().Count() != package.CampStages.Count ||
            package.CampStages.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 100 || x.SortOrder < 0) ||
            package.CampStages.Select(x => x.SortOrder).Order().Where((value, index) => value != index).Any())
            throw new CampPackageValidationException("Camp stages are invalid.");
        var stageIds = package.CampStages.Select(x => x.Id).ToHashSet();
        if (package.CampStageFoodFactors.Count != package.CampStages.Count ||
            package.CampStageFoodFactors.Select(x => x.Id).Distinct().Count() != package.CampStageFoodFactors.Count ||
            package.CampStageFoodFactors.Select(x => x.CampStageId).Distinct().Count() != package.CampStageFoodFactors.Count ||
            package.CampStageFoodFactors.Any(x => x.Id == Guid.Empty || !stageIds.Contains(x.CampStageId) ||
                string.IsNullOrWhiteSpace(x.StageName) || x.Factor < 0.1m || x.Factor > 3m || decimal.Round(x.Factor, 2) != x.Factor))
            throw new CampPackageValidationException("Camp stage food factors are invalid.");
        var structureIds = package.StructureNodes.Select(x => x.Id).ToHashSet();
        if (package.ParticipantEstimates.Select(x => x.Id).Distinct().Count() != package.ParticipantEstimates.Count ||
            package.ParticipantEstimates.Select(x => new { x.StructureNodeId, x.CampStageId }).Distinct().Count() != package.ParticipantEstimates.Count ||
            package.ParticipantEstimates.Any(x => x.Id == Guid.Empty || !stageIds.Contains(x.CampStageId) ||
                !structureIds.Contains(x.StructureNodeId) || x.ChildYouthCount < 0 || x.LeaderCount < 0 ||
                x.ChildYouthCount == 0 && x.LeaderCount == 0))
            throw new CampPackageValidationException("Participant estimates are invalid.");
        var nodeIds = package.StructureNodes.Select(node => node.Id).ToHashSet();
        if (nodeIds.Count != package.StructureNodes.Count || package.StructureNodes.Any(node =>
            node.Id == Guid.Empty || node.ParentId == node.Id ||
            node.ParentId is Guid parentId && !nodeIds.Contains(parentId)))
            throw new CampPackageValidationException("Camp structure identity or parent reference is invalid.");
        if (package.ParticipantEstimates.Any(estimate =>
            package.StructureNodes.Any(node => node.ParentId == estimate.StructureNodeId)))
            throw new CampPackageValidationException("Participant estimates must belong to leaf nodes.");
        if (structureMode == ScoutCampPlanner.Camp.Domain.CampStructureMode.Fixed)
        {
            var nodesById = package.StructureNodes.ToDictionary(node => node.Id);
            foreach (var node in package.StructureNodes)
            {
                int depth = 1; Guid? parentId = node.ParentId; var visited = new HashSet<Guid> { node.Id };
                while (parentId is Guid id)
                {
                    if (!visited.Add(id))
                        throw new CampPackageValidationException("Camp structure contains a cycle.");
                    depth++; parentId = nodesById[id].ParentId;
                }
                if (depth > package.Camp.StructureLevelNames.Count)
                    throw new CampPackageValidationException("Camp structure exceeds its fixed depth.");
            }
        }
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
