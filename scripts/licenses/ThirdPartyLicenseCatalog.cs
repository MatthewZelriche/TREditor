using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public sealed class ThirdPartyLicenseEntry
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("category")]
    public string Category { get; init; } = "";

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; init; }

    [JsonPropertyName("licenseFile")]
    public string LicenseFile { get; init; }

    [JsonPropertyName("license")]
    public string License { get; init; }

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; init; }

    [JsonPropertyName("licenseText")]
    public string LicenseText { get; init; }
}

public sealed class ThirdPartyLicenseReport
{
    [JsonPropertyName("entries")]
    public List<ThirdPartyLicenseEntry> Entries { get; init; } = [];
}

public static class ThirdPartyLicenseCatalog
{
    private const string ReportPath = "res://licenses/generated/third-party.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ThirdPartyLicenseReport Load()
    {
        if (!FileAccess.FileExists(ReportPath))
        {
            throw new InvalidOperationException(
                $"Third-party license report not found at '{ReportPath}'."
            );
        }

        using FileAccess file = FileAccess.Open(ReportPath, FileAccess.ModeFlags.Read);
        string json = file.GetAsText();
        ThirdPartyLicenseReport report =
            JsonSerializer.Deserialize<ThirdPartyLicenseReport>(json, JsonOptions)
            ?? throw new InvalidOperationException("Third-party license report was empty.");

        return report;
    }
}
