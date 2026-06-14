namespace LicenseCollector;

public sealed class LicenseManifest
{
    public int Version { get; init; } = 1;
    public List<ImplicitLicenseDefinition> Implicit { get; init; } = [];
    public List<ScanPathDefinition> ScanPaths { get; init; } = [];
    public List<SubmoduleDefinition> Submodules { get; init; } = [];
}

public sealed class ImplicitLicenseDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public string? License { get; init; }
    public string? Url { get; init; }
    public string? LicenseText { get; init; }
}

public sealed class ScanPathDefinition
{
    public required string Path { get; init; }
    public required string Category { get; init; }
}

public sealed class SubmoduleDefinition
{
    public required string Path { get; init; }
    public required string DisplayName { get; init; }
}
