using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

// Owns argument-only validation, destination safety checks, and the input/output collision
// policy. No analysis is started from this collaborator.
internal sealed class ValidateCommandPreflight
{
    internal const string FormatHuman = "human";
    internal const string FormatJson = "json";
    internal const string FormatSarif = "sarif";

    private const string ProfileDestinationStdout = "stdout";
    private const string ProfileDestinationStderr = "stderr";

    private readonly ICliRuntime _runtime;
    private readonly ICliConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly ValidateCacheCoordinator _cache;

    public ValidateCommandPreflight(
        ICliRuntime runtime,
        ICliConsole console,
        IFileSystem fileSystem,
        ValidateCacheCoordinator cache)
    {
        _runtime = runtime;
        _console = console;
        _fileSystem = fileSystem;
        _cache = cache;
    }

    internal static string ResolveEffectiveFormat(ValidateCommandOptions options)
    {
        if (options.IsFormatExplicit || options.AdditionalSinks.Count == 0)
        {
            return options.Format;
        }

        ReportSink? stdoutSink = options.AdditionalSinks
            .FirstOrDefault(sink => sink.DestinationType == ReportDestinationType.Stdout);
        if (stdoutSink is not null)
        {
            return stdoutSink.Format;
        }

        ReportSink? structuredSink = options.AdditionalSinks
            .FirstOrDefault(sink => sink.Format is FormatJson or FormatSarif);
        return structuredSink?.Format ?? FormatHuman;
    }

    internal int? TryWriteImmediateResponse(ValidateCommandOptions options)
    {
        if (options.ShowHelp)
        {
            _console.Out.WriteLine(ValidateCommandDefinition.HelpText);
            return CliExitCodes.Success;
        }

        if (options.ShowVersion)
        {
            _console.Out.WriteLine($"arch-linter-net {_runtime.Version}");
            return CliExitCodes.Success;
        }

        if (!TryParseModes(options.Mode, out _, out string? modeError))
        {
            WriteImmediateError(options, modeError!);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.Format is not (FormatHuman or FormatJson or FormatSarif))
        {
            WriteImmediateError(options, $"Invalid format: {options.Format}. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.ReportParseError is not null)
        {
            WriteImmediateError(options, options.ReportParseError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.ExternalEvidenceParseError is not null)
        {
            WriteImmediateError(options, options.ExternalEvidenceParseError);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!ValidationExecutionSemantics.TryGetWaiverEvaluationDate(
                options.WaiverEvaluationDate, out _, out string? waiverDateError))
        {
            WriteImmediateError(options, waiverDateError!);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.IsFormatExplicit && options.AdditionalSinks.Count > 0)
        {
            WriteImmediateError(options,
                "--format/--json cannot be combined with --report. " +
                "Use --report <format>=stdout to route output to stdout.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? reportCollision = FindReportFileCollision(options);
        if (reportCollision is not null)
        {
            WriteImmediateError(options, reportCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        string? profileCollision = FindProfileFileCollision(options);
        if (profileCollision is not null)
        {
            WriteImmediateError(options, profileCollision);
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!PreValidateReportDestinations(options) || !PreValidateProfileDestination(options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (!_cache.PreValidateCacheDestination(options))
        {
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        return null;
    }

    internal static bool TryParseModes(string rawMode, out IReadOnlyList<string> modes, out string? error)
    {
        List<string> parsed = rawMode.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parsed.Count == 0 || parsed.Any(mode => mode is not ("strict" or "audit")))
        {
            modes = Array.Empty<string>();
            error = $"Invalid mode: {rawMode}. Use 'strict', 'audit', or a comma-separated combination of both.";
            return false;
        }

        modes = parsed;
        error = null;
        return true;
    }

    internal static string? FindReportFileCollision(ValidateCommandOptions options)
    {
        HashSet<string> inputFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(options.PolicyPath),
        };

        if (options.BaselinePath is not null)
        {
            inputFiles.Add(Path.GetFullPath(options.BaselinePath));
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (inputFiles.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches an input file";
            }
        }

        return null;
    }

    internal static string? FindProfileFileCollision(ValidateCommandOptions options)
    {
        if (!TryGetProfileFilePath(options, out string? profilePath))
        {
            return null;
        }

        HashSet<string> inputFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(options.PolicyPath),
        };
        if (options.BaselinePath is not null)
        {
            inputFiles.Add(Path.GetFullPath(options.BaselinePath));
        }

        if (inputFiles.Contains(profilePath!))
        {
            return $"--profile destination '{options.ProfileDestination}' matches an input file";
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType == ReportDestinationType.File
                && sink.FilePath is not null
                && string.Equals(profilePath, Path.GetFullPath(sink.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                return $"--profile destination '{options.ProfileDestination}' matches --report destination '{sink.FilePath}'";
            }
        }

        return null;
    }

    internal static string? FindProfileFileCollision(
        ValidateCommandOptions options, IEnumerable<string> inputPaths, string inputDescription)
    {
        if (!TryGetProfileFilePath(options, out string? profilePath))
        {
            return null;
        }

        string? matchedPath = inputPaths.FirstOrDefault(inputPath =>
            string.Equals(profilePath, Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase));
        return matchedPath is null
            ? null
            : $"--profile destination '{options.ProfileDestination}' matches {inputDescription} '{matchedPath}'";
    }

    internal static bool TryGetProfileFilePath(ValidateCommandOptions options, out string? profilePath)
    {
        if (options.ProfileDestination is null
            || options.ProfileDestination is ProfileDestinationStdout or ProfileDestinationStderr)
        {
            profilePath = null;
            return false;
        }

        profilePath = Path.GetFullPath(options.ProfileDestination);
        return true;
    }

    internal static string? FindImportFileCollision(ValidateCommandOptions options, IReadOnlyList<string> policyImportPaths)
    {
        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            string sinkFullPath = Path.GetFullPath(sink.FilePath);
            string? matchedImportPath = policyImportPaths
                .FirstOrDefault(importPath => string.Equals(sinkFullPath, importPath, StringComparison.OrdinalIgnoreCase));
            if (matchedImportPath is not null)
            {
                return $"--report destination '{sink.FilePath}' matches imported policy file '{matchedImportPath}'";
            }
        }

        return null;
    }

    internal static string? FindReceiptFileCollision(
        ValidateCommandOptions options, IReadOnlyList<string> resolvedAssemblyPaths)
    {
        HashSet<string> loadedPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string assemblyPath in resolvedAssemblyPaths)
        {
            loadedPaths.Add(Path.GetFullPath(assemblyPath));
            loadedPaths.Add(Path.GetFullPath(BuildReceiptStore.ReceiptPathFor(assemblyPath)));
        }

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (loadedPaths.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches a build artifact or receipt loaded during this run";
            }
        }

        return null;
    }

    internal static string? FindDiscoveredProjectFileCollision(
        ValidateCommandOptions options, IReadOnlyList<string> discoveredProjectPaths)
    {
        HashSet<string> loadedProjectPaths = new(
            discoveredProjectPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);

        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType == ReportDestinationType.File
                && sink.FilePath is not null
                && loadedProjectPaths.Contains(Path.GetFullPath(sink.FilePath)))
            {
                return $"--report destination '{sink.FilePath}' matches a project file loaded during this run";
            }
        }

        return null;
    }

    internal bool PreValidateReportDestinations(ValidateCommandOptions options)
    {
        foreach (ReportSink sink in options.AdditionalSinks)
        {
            if (sink.DestinationType != ReportDestinationType.File || sink.FilePath is null)
            {
                continue;
            }

            if (!_fileSystem.CanWriteToDirectory(sink.FilePath))
            {
                WriteImmediateError(options, $"Cannot write report to '{sink.FilePath}': destination is not writable");
                return false;
            }
        }

        return true;
    }

    internal bool PreValidateProfileDestination(ValidateCommandOptions options)
    {
        if (!TryGetProfileFilePath(options, out string? profilePath)
            || _fileSystem.CanWriteToDirectory(profilePath!))
        {
            return true;
        }

        WriteImmediateError(options, $"Cannot write profile to '{options.ProfileDestination}': destination is not writable");
        return false;
    }

    internal void WriteImmediateError(ValidateCommandOptions options, string message)
    {
        CliErrorOutputWriter.Write(_console, options.Format, "invalid-arguments", message);
    }
}
