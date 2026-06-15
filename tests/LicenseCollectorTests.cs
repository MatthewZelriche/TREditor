using System.Text.Json;
using System.Text.Json.Serialization;
using LicenseCollector;

namespace TREditor2026.Tests;

public sealed class LicenseCollectorTests
{
    [Fact]
    public void CollectScanPath_FailsWhenSubfolderMissingLicense()
    {
        string root = CreateTempRoot(
            """
            licenses/manifest.yaml
            resource/
            resource/good/LICENSE.md
            resource/missing/
            """
        );
        WriteManifest(
            root,
            """
            version: 1
            scanPaths:
              - path: resource
                category: Assets
            """
        );
        File.WriteAllText(Path.Combine(root, "resource", "good", "LICENSE.md"), "Good License");

        LicenseCollectionResult result = LicenseCollectorService.Collect(
            root,
            Path.Combine(root, "licenses", "manifest.yaml")
        );

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("resource/missing"));
    }

    [Fact]
    public void CollectScanPath_AcceptsCaseInsensitiveLicenseFileNames()
    {
        string root = CreateTempRoot("licenses/manifest.yaml");
        WriteManifest(
            root,
            """
            version: 1
            scanPaths:
              - path: vendor
                category: Code & Shaders
            """
        );
        Directory.CreateDirectory(Path.Combine(root, "vendor", "outlineShader"));
        File.WriteAllText(
            Path.Combine(root, "vendor", "outlineShader", "LICENSE.MD"),
            "Outline License"
        );

        LicenseCollectionResult result = LicenseCollectorService.Collect(
            root,
            Path.Combine(root, "licenses", "manifest.yaml")
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Report!.Entries);
        Assert.Equal("Outline Shader", result.Report.Entries[0].DisplayName);
        Assert.Equal("Outline License", result.Report.Entries[0].LicenseText);
    }

    [Fact]
    public void Collect_IncludesImplicitAndSubmoduleEntries()
    {
        string root = CreateTempRoot(
            """
            licenses/manifest.yaml
            submodules/TRMesh/LICENSE.md
            """
        );
        WriteManifest(
            root,
            """
            version: 1
            implicit:
              - id: godot-engine
                displayName: Godot Engine
                category: Runtime
                license: MIT
                url: https://example.com/godot-license
            submodules:
              - path: submodules/TRMesh
                displayName: TRMesh
            """
        );
        File.WriteAllText(
            Path.Combine(root, "submodules", "TRMesh", "LICENSE.md"),
            "TRMesh License"
        );

        LicenseCollectionResult result = LicenseCollectorService.Collect(
            root,
            Path.Combine(root, "licenses", "manifest.yaml")
        );

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Report!.Entries.Count);
        Assert.Contains(result.Report.Entries, entry => entry.Id == "implicit.godot-engine");
        Assert.Contains(result.Report.Entries, entry => entry.DisplayName == "TRMesh");
    }

    [Fact]
    public void Collect_FailsWhenSubmoduleMissingLicense()
    {
        string root = CreateTempRoot("licenses/manifest.yaml");
        WriteManifest(
            root,
            """
            version: 1
            submodules:
              - path: submodules/Gizmo3D
                displayName: Gizmo3D
            """
        );
        Directory.CreateDirectory(Path.Combine(root, "submodules", "Gizmo3D"));

        LicenseCollectionResult result = LicenseCollectorService.Collect(
            root,
            Path.Combine(root, "licenses", "manifest.yaml")
        );

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors,
            error => error.Contains("submodules/Gizmo3D", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Check_FailsWhenCommittedReportIsMissing()
    {
        string root = CreateTempRoot("licenses/manifest.yaml");
        WriteManifest(
            root,
            """
            version: 1
            scanPaths:
              - path: vendor
                category: Code & Shaders
            """
        );
        Directory.CreateDirectory(Path.Combine(root, "vendor", "outlineShader"));
        File.WriteAllText(
            Path.Combine(root, "vendor", "outlineShader", "LICENSE.md"),
            "Outline License"
        );

        string outputPath = Path.Combine(root, "licenses", "generated", "third-party.json");
        LicenseCollectionResult result = LicenseCollectorService.Check(
            root,
            Path.Combine(root, "licenses", "manifest.yaml"),
            outputPath
        );

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors,
            error => error.Contains("not found", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Check_FailsWhenCommittedReportIsOutOfDate()
    {
        string root = CreateTempRoot("licenses/manifest.yaml");
        WriteManifest(
            root,
            """
            version: 1
            scanPaths:
              - path: vendor
                category: Code & Shaders
            """
        );
        Directory.CreateDirectory(Path.Combine(root, "vendor", "outlineShader"));
        File.WriteAllText(
            Path.Combine(root, "vendor", "outlineShader", "LICENSE.md"),
            "Updated License"
        );

        string outputDirectory = Path.Combine(root, "licenses", "generated");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "third-party.json");
        File.WriteAllText(
            outputPath,
            """
            {
              "generatedAt": "2026-01-01T00:00:00+00:00",
              "entries": [
                {
                  "id": "scan.vendor.outlineShader",
                  "displayName": "Outline Shader",
                  "category": "Code & Shaders",
                  "sourcePath": "vendor/outlineShader",
                  "licenseFile": "vendor/outlineShader/LICENSE.md",
                  "licenseText": "Old License"
                }
              ]
            }
            """
        );

        LicenseCollectionResult result = LicenseCollectorService.Check(
            root,
            Path.Combine(root, "licenses", "manifest.yaml"),
            outputPath
        );

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Errors,
            error => error.Contains("out of date", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Check_SucceedsWhenCommittedReportMatches()
    {
        string root = CreateTempRoot("licenses/manifest.yaml");
        WriteManifest(
            root,
            """
            version: 1
            scanPaths:
              - path: vendor
                category: Code & Shaders
            """
        );
        Directory.CreateDirectory(Path.Combine(root, "vendor", "outlineShader"));
        File.WriteAllText(
            Path.Combine(root, "vendor", "outlineShader", "LICENSE.md"),
            "Outline License"
        );

        string manifestPath = Path.Combine(root, "licenses", "manifest.yaml");
        string outputPath = Path.Combine(root, "licenses", "generated", "third-party.json");
        LicenseCollectionResult collect = LicenseCollectorService.Collect(root, manifestPath);
        LicenseCollectorService.WriteReport(collect.Report!, outputPath);

        LicenseCollectionResult check = LicenseCollectorService.Check(
            root,
            manifestPath,
            outputPath
        );

        Assert.True(check.Succeeded);
    }

    [Fact]
    public void Check_SucceedsWhenLicenseFileUsesCrlfButCommittedReportUsesLf()
    {
        string root = CreateTempRoot("licenses/manifest.yaml");
        WriteManifest(
            root,
            """
            version: 1
            scanPaths:
              - path: vendor
                category: Code & Shaders
            """
        );
        Directory.CreateDirectory(Path.Combine(root, "vendor", "outlineShader"));
        File.WriteAllText(
            Path.Combine(root, "vendor", "outlineShader", "LICENSE.md"),
            "Line one\r\nLine two\r\n"
        );

        string manifestPath = Path.Combine(root, "licenses", "manifest.yaml");
        string outputPath = Path.Combine(root, "licenses", "generated", "third-party.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(
            outputPath,
            """
            {
              "generatedAt": "2026-01-01T00:00:00+00:00",
              "entries": [
                {
                  "id": "scan.vendor.outlineShader",
                  "displayName": "Outline Shader",
                  "category": "Code & Shaders",
                  "sourcePath": "vendor/outlineShader",
                  "licenseFile": "vendor/outlineShader/LICENSE.md",
                  "licenseText": "Line one\nLine two\n"
                }
              ]
            }
            """
        );

        LicenseCollectionResult check = LicenseCollectorService.Check(
            root,
            manifestPath,
            outputPath
        );

        Assert.True(check.Succeeded);
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", "a\nb\nc")]
    [InlineData("a\rb\rc", "a\nb\nc")]
    [InlineData("already\nlf", "already\nlf")]
    public void NormalizeLineEndings_ConvertsToLf(string input, string expected)
    {
        Assert.Equal(expected, LicenseCollectorService.NormalizeLineEndings(input));
    }

    [Theory]
    [InlineData("outlineShader", "Outline Shader")]
    [InlineData("matcap", "Matcap")]
    public void FormatFolderDisplayName_InsertsSpacesForCamelCase(
        string folderName,
        string expected
    )
    {
        Assert.Equal(expected, LicenseCollectorService.FormatFolderDisplayName(folderName));
    }

    private static string CreateTempRoot(string structure)
    {
        string root = Path.Combine(Path.GetTempPath(), "tre-license-" + Guid.NewGuid());
        Directory.CreateDirectory(root);

        foreach (string entry in structure.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = entry.Trim().Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(root, trimmed);
            if (trimmed.EndsWith(Path.DirectorySeparatorChar))
            {
                Directory.CreateDirectory(fullPath.TrimEnd(Path.DirectorySeparatorChar));
                continue;
            }

            string? parentDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
                Directory.CreateDirectory(parentDirectory);
        }

        return root;
    }

    private static void WriteManifest(string root, string yaml)
    {
        string manifestDirectory = Path.Combine(root, "licenses");
        Directory.CreateDirectory(manifestDirectory);
        File.WriteAllText(Path.Combine(manifestDirectory, "manifest.yaml"), yaml);
    }
}
