namespace ScoutCampPlanner.Catering.Domain;

internal static class CatalogName
{
    public static (string Display, string Normalized) Normalize(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string display = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (display.Length > maximumLength)
            throw new ArgumentException($"Name must not exceed {maximumLength} characters.", parameterName);

        return (display, display.ToUpperInvariant());
    }
}
