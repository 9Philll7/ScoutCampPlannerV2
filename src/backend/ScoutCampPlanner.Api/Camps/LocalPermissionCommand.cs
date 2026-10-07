using ScoutCampPlanner.Platform.Application.Authorization;

namespace ScoutCampPlanner.Api.Camps;

public sealed record LocalPermissionCommand(Guid CampId, Guid TransferId, string Permission, bool Granted)
{
    public static LocalPermissionCommand? Parse(string[] args)
    {
        const string command = "--local-permission-command";
        if (!args.Any(value => value.StartsWith("--local-permission-", StringComparison.Ordinal))) return null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--local-permission-", StringComparison.Ordinal)) continue;
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) ||
                !values.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Invalid local permission command.");
            i++;
        }
        if (values.Count != 5 || !values.TryGetValue(command, out string? action) || action is not ("grant" or "revoke") ||
            values.GetValueOrDefault("--local-permission-confirm") != "true" ||
            !Guid.TryParse(values.GetValueOrDefault("--local-permission-camp"), out Guid campId) || campId == Guid.Empty ||
            !Guid.TryParse(values.GetValueOrDefault("--local-permission-transfer"), out Guid transferId) || transferId == Guid.Empty ||
            !LocalCampAccessPolicy.IsExplicitPermission(values.GetValueOrDefault("--local-permission-name") ?? ""))
            throw new ArgumentException("An explicit action, camp, transfer, supported permission and confirmation are required.");
        return new(campId, transferId, values["--local-permission-name"], action == "grant");
    }
}
