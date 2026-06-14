using Godot;

namespace TREditor2026.Tests;

public sealed class ThirdPartyLicenseCatalogTests
{
    [Fact]
    public void BuildEntryText_IncludesLicenseTextAndMetadata()
    {
        ThirdPartyLicenseEntry entry = new()
        {
            DisplayName = "Godot Engine",
            Category = "Runtime",
            License = "MIT",
            SourceUrl = "https://example.com/godot",
            LicenseText = "Permission is hereby granted...",
        };

        string text = LicensesDialog.BuildEntryText(entry);

        Assert.Contains("Godot Engine", text);
        Assert.Contains("License: MIT", text);
        Assert.Contains("https://example.com/godot", text);
        Assert.Contains("Permission is hereby granted...", text);
    }

    [Fact]
    public void BuildEntryText_FallsBackToUrlWhenLicenseTextMissing()
    {
        ThirdPartyLicenseEntry entry = new()
        {
            DisplayName = "Godot Engine",
            Category = "Runtime",
            SourceUrl = "https://example.com/godot",
        };

        string text = LicensesDialog.BuildEntryText(entry);

        Assert.Contains("Full license text is available at the URL above.", text);
        Assert.Contains("https://example.com/godot", text);
    }
}
