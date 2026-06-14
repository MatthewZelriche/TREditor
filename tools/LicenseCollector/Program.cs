namespace LicenseCollector;

internal static class Program
{
    private static int Main(string[] args)
    {
        string rootDirectory = Directory.GetCurrentDirectory();
        string manifestPath = Path.Combine(rootDirectory, "licenses", "manifest.yaml");
        string outputPath = Path.Combine(
            rootDirectory,
            "licenses",
            "generated",
            "third-party.json"
        );

        bool checkOnly = false;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--root":
                    rootDirectory = RequireValue(args, ref index, argument);
                    break;
                case "--manifest":
                    manifestPath = RequireValue(args, ref index, argument);
                    break;
                case "--output":
                    outputPath = RequireValue(args, ref index, argument);
                    break;
                case "--check":
                    checkOnly = true;
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown argument: {argument}");
                    PrintUsage();
                    return 1;
            }
        }

        LicenseCollectionResult result = checkOnly
            ? LicenseCollectorService.Check(rootDirectory, manifestPath, outputPath)
            : LicenseCollectorService.Collect(rootDirectory, manifestPath);
        if (!result.Succeeded || result.Report == null)
        {
            Console.Error.WriteLine(
                checkOnly ? "License check failed:" : "License collection failed:"
            );
            foreach (string error in result.Errors)
                Console.Error.WriteLine($"  - {error}");
            return 1;
        }

        if (checkOnly)
        {
            Console.WriteLine(
                $"License check passed ({result.Report.Entries.Count} entries match {Path.GetFullPath(outputPath)})."
            );
            return 0;
        }

        LicenseCollectorService.WriteReport(result.Report, outputPath);
        Console.WriteLine(
            $"Wrote {result.Report.Entries.Count} license entries to {Path.GetFullPath(outputPath)}."
        );
        return 0;
    }

    private static string RequireValue(string[] args, ref int index, string argumentName)
    {
        if (index + 1 >= args.Length)
            throw new InvalidOperationException($"Missing value for {argumentName}.");

        index++;
        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Usage: LicenseCollector [--root <path>] [--manifest <path>] [--output <path>] [--check]

              --root       Repository root (default: current directory)
              --manifest   Path to licenses/manifest.yaml
              --output     Path to licenses/generated/third-party.json
              --check      Verify the committed report matches current licenses
            """
        );
    }
}
