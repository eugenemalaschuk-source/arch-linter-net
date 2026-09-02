using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands.Report.Application;

internal sealed class ReportCommandHandler(
    ICliConsole console,
    IFileSystem fileSystem)
{
    private const string HelpText =
        """
        arch-linter-net report pr — render an architecture pull-request report

        Usage:
          arch-linter-net report pr --health <architecture-health.json> --change <architecture-change.json> [options]

        Options:
          --health <path>       Canonical architecture-health/v1 artifact (required)
          --change <path>       Canonical architecture-change report artifact (required)
          --output <path>       Write Markdown to this path (default: standard output)
          --max-details <count> Maximum entries per detail section (default: 20)
          -h, --help            Show this help message

        The report is a local projection of the supplied canonical artifacts. It performs no
        architecture analysis, GitHub/API access, policy loading, or external evidence evaluation.

        Exit codes:
          0   Report rendered successfully
          2   Invalid arguments, artifact, or output path
        """;

    public int Execute(PrReportCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (string.IsNullOrWhiteSpace(options.HealthPath)
            || string.IsNullOrWhiteSpace(options.ChangePath)
            || options.MaxDetails <= 0)
        {
            console.Error.WriteLine("Report pr requires --health, --change, and a positive --max-details.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            string? collision = FindOutputCollision(options);
            if (collision is not null)
            {
                console.Error.WriteLine(collision);
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (!fileSystem.FileExists(options.HealthPath))
            {
                console.Error.WriteLine($"Could not read architecture Health artifact: file not found '{options.HealthPath}'.");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            if (!fileSystem.FileExists(options.ChangePath))
            {
                console.Error.WriteLine($"Could not read architecture change artifact: file not found '{options.ChangePath}'.");
                return CliExitCodes.InvalidArgumentsOrRuntimeError;
            }

            ArchitecturePrReportProjection projection = ArchitecturePrReportProjector.ReadAndProject(
                fileSystem.ReadAllText(options.HealthPath),
                fileSystem.ReadAllText(options.ChangePath));
            string markdown = PrReportMarkdownRenderer.Render(projection, options.MaxDetails);
            if (options.OutputPath is null)
            {
                console.Out.Write(markdown);
            }
            else
            {
                string tempPath = fileSystem.WriteAllTextToTemp(options.OutputPath, markdown);
                fileSystem.RenameTempToTarget(tempPath, options.OutputPath);
            }

            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            console.Error.WriteLine($"Could not render architecture PR report: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    internal static string? FindOutputCollision(PrReportCommandOptions options)
    {
        if (options.OutputPath is null)
        {
            return null;
        }

        string output = Path.GetFullPath(options.OutputPath);
        foreach ((string name, string path) in new[]
        {
            ("--health", options.HealthPath),
            ("--change", options.ChangePath),
        })
        {
            if (string.Equals(output, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                return $"--output destination '{options.OutputPath}' matches {name} input '{path}'";
            }
        }

        return null;
    }
}
