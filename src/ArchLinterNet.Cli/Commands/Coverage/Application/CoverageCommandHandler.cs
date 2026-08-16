using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;

namespace ArchLinterNet.Cli.Commands.Coverage.Application;

internal sealed class CoverageCommandHandler(ICliConsole console, IFileSystem fileSystem)
{
    public int Extract(string inputPath, string mode, string outputPath)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(fileSystem.ReadAllText(inputPath));
            JsonElement result = document.RootElement.TryGetProperty("results", out JsonElement results) && results.ValueKind == JsonValueKind.Array
                ? results.EnumerateArray().FirstOrDefault(item => item.TryGetProperty("mode", out JsonElement itemMode) && itemMode.GetString() == mode)
                : document.RootElement;
            if (result.ValueKind != JsonValueKind.Object || result.GetProperty("mode").GetString() != mode) throw new JsonException($"The input does not contain a {mode} validation result.");
            fileSystem.WriteAllText(outputPath, result.GetRawText());
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            console.Error.WriteLine($"Could not extract architecture validation result: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

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
            JsonElement report = SelectStrictResult(document.RootElement);
            string markdown = CoverageReportRenderer.Render(report, changedFiles, options.RepositoryRoot,
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

    private static JsonElement SelectStrictResult(JsonElement document)
    {
        if (!document.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array) return document;
        foreach (JsonElement result in results.EnumerateArray()) if (result.TryGetProperty("mode", out JsonElement mode) && mode.GetString() == "strict") return result;
        throw new JsonException("The input does not contain a strict validation result.");
    }
}
