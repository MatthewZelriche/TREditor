namespace LicenseCollector;

public sealed class ThirdPartyLicenseEntry
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public string? SourcePath { get; init; }
    public string? LicenseFile { get; init; }
    public string? License { get; init; }
    public string? SourceUrl { get; init; }
    public string? LicenseText { get; init; }
}

public sealed class ThirdPartyLicenseReport
{
    public required List<ThirdPartyLicenseEntry> Entries { get; init; }
}
