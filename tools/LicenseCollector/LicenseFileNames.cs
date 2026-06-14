namespace LicenseCollector;

internal static class LicenseFileNames
{
    internal static readonly string[] KnownNames =
    [
        "LICENSE",
        "LICENSE.txt",
        "LICENSE.md",
        "COPYING",
        "NOTICE",
    ];

    internal static string? FindInDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        foreach (string filePath in Directory.GetFiles(directory))
        {
            string fileName = Path.GetFileName(filePath);
            foreach (string knownName in KnownNames)
            {
                if (string.Equals(fileName, knownName, StringComparison.OrdinalIgnoreCase))
                    return filePath;
            }
        }

        return null;
    }
}
