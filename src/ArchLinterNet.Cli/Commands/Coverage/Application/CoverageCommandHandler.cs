using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Coverage.Application;

internal sealed class CoverageCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    public int Execute(CoverageReportCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine("arch-linter-net coverage report --input <architecture-strict.json> [--changed-files <path>] [--repo-root <path>] [--output <path>] [--max-failure-diagnostics <count>] [--diff-status ok|failed]");
            return CliExitCodes.Success;
        }

        if (options.MaxFailureDiagnostics is < 1 || options.DiffStatus is not ("ok" or "failed"))
        {
            console.Error.WriteLine("Invalid coverage report options.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(fileSystem.ReadAllText(options.InputPath));
            IReadOnlyList<string>? changedFiles = options.DiffStatus == "failed" || options.ChangedFilesPath is null || !fileSystem.FileExists(options.ChangedFilesPath)
                ? null
                : fileSystem.ReadAllText(options.ChangedFilesPath).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string markdown = CoverageReportRenderer.Render(document.RootElement, changedFiles, options.RepositoryRoot,
                options.DiffStatus == "failed", options.MaxFailureDiagnostics);
            if (options.OutputPath is null)
            {
                console.Out.Write(markdown);
            }
            else
            {
                fileSystem.WriteAllText(options.OutputPath, markdown);
            }

            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            console.Error.WriteLine($"Could not render architecture coverage report: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}
