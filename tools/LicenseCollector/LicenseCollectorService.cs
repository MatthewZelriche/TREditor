using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LicenseCollector;

public sealed class LicenseCollectionResult
{
    public required bool Succeeded { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public ThirdPartyLicenseReport? Report { get; init; }
}

public static class LicenseCollectorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static LicenseCollectionResult Collect(string rootDirectory, string manifestPath)
    {
        List<string> errors = [];
        string rootFullPath = Path.GetFullPath(rootDirectory);
        string manifestFullPath = Path.GetFullPath(manifestPath);

        if (!File.Exists(manifestFullPath))
        {
            return Failure([$"Manifest not found: {manifestFullPath}"]);
        }

        LicenseManifest manifest;
        try
        {
            manifest = LoadManifest(manifestFullPath);
        }
        catch (Exception exception)
        {
            return Failure([$"Failed to load manifest: {exception.Message}"]);
        }

        List<ThirdPartyLicenseEntry> entries = [];

        foreach (ImplicitLicenseDefinition implicitEntry in manifest.Implicit)
        {
            entries.Add(
                new ThirdPartyLicenseEntry
                {
                    Id = $"implicit.{implicitEntry.Id}",
                    DisplayName = implicitEntry.DisplayName,
                    Category = implicitEntry.Category,
                    License = implicitEntry.License,
                    SourceUrl = implicitEntry.Url,
                    LicenseText = implicitEntry.LicenseText,
                }
            );
        }

        CollectScanPaths(rootFullPath, manifest.ScanPaths, entries, errors);
        CollectSubmodules(rootFullPath, manifest.Submodules, entries, errors);

        if (errors.Count > 0)
        {
            return new LicenseCollectionResult { Succeeded = false, Errors = errors };
        }

        entries.Sort(
            (left, right) =>
            {
                int categoryCompare = string.Compare(
                    left.Category,
                    right.Category,
                    StringComparison.OrdinalIgnoreCase
                );
                if (categoryCompare != 0)
                    return categoryCompare;

                return string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.OrdinalIgnoreCase
                );
            }
        );

        return new LicenseCollectionResult
        {
            Succeeded = true,
            Errors = [],
            Report = new ThirdPartyLicenseReport
            {
                GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
                Entries = entries,
            },
        };
    }

    public static void WriteReport(ThirdPartyLicenseReport report, string outputPath)
    {
        string outputFullPath = Path.GetFullPath(outputPath);
        string? outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        string json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(outputFullPath, json);
    }

    public static LicenseCollectionResult Check(
        string rootDirectory,
        string manifestPath,
        string outputPath
    )
    {
        LicenseCollectionResult collection = Collect(rootDirectory, manifestPath);
        if (!collection.Succeeded || collection.Report == null)
            return collection;

        string outputFullPath = Path.GetFullPath(outputPath);
        if (!File.Exists(outputFullPath))
        {
            return new LicenseCollectionResult
            {
                Succeeded = false,
                Errors =
                [
                    $"Committed license report not found at '{ToRepoRelativePath(rootDirectory, outputFullPath)}'. "
                        + "Run the collector without --check to generate it.",
                ],
            };
        }

        ThirdPartyLicenseReport committedReport;
        try
        {
            committedReport = ReadReport(outputFullPath);
        }
        catch (Exception exception)
        {
            return Failure([$"Failed to read committed license report: {exception.Message}"]);
        }

        if (!EntriesMatch(collection.Report.Entries, committedReport.Entries))
        {
            return new LicenseCollectionResult
            {
                Succeeded = false,
                Errors =
                [
                    "Committed third-party license report is out of date. "
                        + "Regenerate it with: dotnet run --project tools/LicenseCollector -- --root .",
                ],
            };
        }

        return new LicenseCollectionResult
        {
            Succeeded = true,
            Errors = [],
            Report = collection.Report,
        };
    }

    internal static ThirdPartyLicenseReport ReadReport(string outputPath)
    {
        string json = File.ReadAllText(outputPath);
        ThirdPartyLicenseReport? report = JsonSerializer.Deserialize<ThirdPartyLicenseReport>(
            json,
            JsonOptions
        );

        return report
            ?? throw new InvalidOperationException(
                "Committed license report deserialized to null."
            );
    }

    internal static bool EntriesMatch(
        IReadOnlyList<ThirdPartyLicenseEntry> generated,
        IReadOnlyList<ThirdPartyLicenseEntry> committed
    )
    {
        string generatedJson = JsonSerializer.Serialize(generated, JsonOptions);
        string committedJson = JsonSerializer.Serialize(committed, JsonOptions);
        return string.Equals(generatedJson, committedJson, StringComparison.Ordinal);
    }

    internal static LicenseManifest LoadManifest(string manifestPath)
    {
        string yaml = File.ReadAllText(manifestPath);
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        LicenseManifest manifest =
            deserializer.Deserialize<LicenseManifest>(yaml)
            ?? throw new InvalidOperationException("Manifest deserialized to null.");

        if (manifest.Version != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported manifest version '{manifest.Version}'. Expected version 1."
            );
        }

        return manifest;
    }

    private static void CollectScanPaths(
        string rootDirectory,
        IReadOnlyList<ScanPathDefinition> scanPaths,
        List<ThirdPartyLicenseEntry> entries,
        List<string> errors
    )
    {
        foreach (ScanPathDefinition scanPath in scanPaths)
        {
            string scanDirectory = Path.Combine(rootDirectory, scanPath.Path);
            if (!Directory.Exists(scanDirectory))
            {
                errors.Add($"Scan path does not exist: {scanPath.Path}");
                continue;
            }

            List<string> subdirectories = Directory
                .GetDirectories(scanDirectory)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string subdirectory in subdirectories)
            {
                string folderName = Path.GetFileName(subdirectory);
                string? licenseFile = LicenseFileNames.FindInDirectory(subdirectory);
                if (licenseFile == null)
                {
                    errors.Add(
                        $"Missing license file in '{ToRepoRelativePath(rootDirectory, subdirectory)}'. "
                            + $"Expected one of: {string.Join(", ", LicenseFileNames.KnownNames)}."
                    );
                    continue;
                }

                string relativeSourcePath = ToRepoRelativePath(rootDirectory, subdirectory);
                entries.Add(
                    new ThirdPartyLicenseEntry
                    {
                        Id =
                            $"scan.{scanPath.Path.Replace('\\', '.').Replace('/', '.')}.{folderName}",
                        DisplayName = FormatFolderDisplayName(folderName),
                        Category = scanPath.Category,
                        SourcePath = relativeSourcePath,
                        LicenseFile = ToRepoRelativePath(rootDirectory, licenseFile),
                        LicenseText = File.ReadAllText(licenseFile),
                    }
                );
            }
        }
    }

    private static void CollectSubmodules(
        string rootDirectory,
        IReadOnlyList<SubmoduleDefinition> submodules,
        List<ThirdPartyLicenseEntry> entries,
        List<string> errors
    )
    {
        foreach (SubmoduleDefinition submodule in submodules)
        {
            string submoduleDirectory = Path.Combine(rootDirectory, submodule.Path);
            if (!Directory.Exists(submoduleDirectory))
            {
                errors.Add($"Submodule path does not exist: {submodule.Path}");
                continue;
            }

            string? licenseFile = LicenseFileNames.FindInDirectory(submoduleDirectory);
            if (licenseFile == null)
            {
                errors.Add(
                    $"Missing license file in submodule '{submodule.Path}'. "
                        + $"Expected one of: {string.Join(", ", LicenseFileNames.KnownNames)}."
                );
                continue;
            }

            entries.Add(
                new ThirdPartyLicenseEntry
                {
                    Id = $"submodule.{Path.GetFileName(submodule.Path)}",
                    DisplayName = submodule.DisplayName,
                    Category = "Submodules",
                    SourcePath = submodule.Path.Replace('\\', '/'),
                    LicenseFile = ToRepoRelativePath(rootDirectory, licenseFile),
                    LicenseText = File.ReadAllText(licenseFile),
                }
            );
        }
    }

    public static string FormatFolderDisplayName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return folderName;

        string spaced = string.Concat(
            folderName.Select(
                (character, index) =>
                    index > 0 && char.IsUpper(character) && !char.IsUpper(folderName[index - 1])
                        ? " " + character
                        : character.ToString()
            )
        );

        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static string ToRepoRelativePath(string rootDirectory, string fullPath)
    {
        string relativePath = Path.GetRelativePath(rootDirectory, fullPath);
        return relativePath.Replace('\\', '/');
    }

    private static LicenseCollectionResult Failure(IReadOnlyList<string> errors) =>
        new() { Succeeded = false, Errors = errors };
}
