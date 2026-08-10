using System.Globalization;
using System.Text;
using System.Text.Json;
using ScoutCampPlanner.PasswordDenylist;

const string usage = """
Usage:
  dotnet run --project tools/ScoutCampPlanner.PasswordDenylist -- \
    --input <hibp-sha1-count-file> \
    --output <denylist-file> \
    --dataset-version <version> \
    --source-date <yyyy-MM-dd> \
    [--entries <count>] [--overwrite]
""";

try
{
    var arguments = ParseArguments(args);
    string inputPath = Path.GetFullPath(GetRequired(arguments, "--input"));
    string outputPath = Path.GetFullPath(GetRequired(arguments, "--output"));
    string datasetVersion = GetRequired(arguments, "--dataset-version");
    string sourceDateText = GetRequired(arguments, "--source-date");
    bool overwrite = arguments.ContainsKey("--overwrite");
    int entryCount = 100_000;
    if (arguments.TryGetValue("--entries", out var countText) &&
        !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out entryCount))
    {
        throw new ArgumentException("--entries must be a positive integer.");
    }

    if (!DateOnly.TryParseExact(
            sourceDateText,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var sourceDate))
    {
        throw new ArgumentException("--source-date must use yyyy-MM-dd.");
    }

    if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Input and output paths must be different.");
    }

    if (File.Exists(outputPath) && !overwrite)
    {
        throw new IOException("Output already exists. Pass --overwrite to replace it.");
    }

    string? outputDirectory = Path.GetDirectoryName(outputPath);
    if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
    {
        throw new DirectoryNotFoundException("Output directory does not exist.");
    }

    string temporaryPath = Path.Combine(
        outputDirectory,
        $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
    try
    {
        await using var inputStream = new FileStream(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(
            inputStream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024 * 1024,
            leaveOpen: false);
        await using var outputStream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        var result = await PwnedPasswordsGenerator.GenerateAsync(
            reader,
            outputStream,
            new DenylistGenerationOptions(datasetVersion, sourceDate, entryCount),
            ["ScoutCampPlanner"]);
        await outputStream.FlushAsync();
        outputStream.Close();

        File.Move(temporaryPath, outputPath, overwrite);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
    finally
    {
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }
    }
}
catch (Exception exception) when (
    exception is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(usage);
    Environment.ExitCode = 1;
}

static Dictionary<string, string?> ParseArguments(string[] values)
{
    var result = new Dictionary<string, string?>(StringComparer.Ordinal);
    for (var index = 0; index < values.Length; index++)
    {
        string name = values[index];
        if (name == "--overwrite")
        {
            if (!result.TryAdd(name, null))
            {
                throw new ArgumentException($"Duplicate argument: {name}");
            }

            continue;
        }

        if (name is not ("--input" or "--output" or "--dataset-version" or "--source-date" or "--entries"))
        {
            throw new ArgumentException($"Unknown argument: {name}");
        }

        if (index + 1 >= values.Length || values[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for argument: {name}");
        }

        if (!result.TryAdd(name, values[++index]))
        {
            throw new ArgumentException($"Duplicate argument: {name}");
        }
    }

    return result;
}

static string GetRequired(IReadOnlyDictionary<string, string?> arguments, string name) =>
    arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required argument: {name}");
