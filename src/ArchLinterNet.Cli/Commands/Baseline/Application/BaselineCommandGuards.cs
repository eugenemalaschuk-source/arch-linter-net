using System.Diagnostics.CodeAnalysis;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Baseline.Application;

internal static class BaselineCommandGuards
{
    public static bool TryValidateMode(ICliConsole console, string format, string mode)
    {
        if (mode is "strict" or "audit" or "all")
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-arguments", $"Invalid mode: {mode}. Use 'strict', 'audit', or 'all'.");
        return false;
    }

    public static bool TryValidateFormat(ICliConsole console, string format, bool hasFormatConflict)
    {
        if (hasFormatConflict)
        {
            CliErrorOutputWriter.Write(console, format, "invalid-arguments", "--json cannot be combined with --format.");
            return false;
        }

        if (format is "human" or "json" or "sarif")
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-format", "Invalid format. Use 'human', 'json', or 'sarif'.");
        return false;
    }

    public static bool TryRequireBaselinePath(
        ICliConsole console,
        string format,
        string command,
        [NotNullWhen(true)] string? baselinePath)
    {
        if (baselinePath != null)
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "invalid-arguments", $"--baseline is required for {command}.");
        return false;
    }

    public static bool TryValidatePolicyFile(ICliConsole console, IFileSystem fileSystem, string format, string policyPath)
    {
        if (fileSystem.FileExists(policyPath))
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "configuration-error", $"Policy file not found: {policyPath}");
        return false;
    }

    public static bool TryValidateBaselineFile(ICliConsole console, IFileSystem fileSystem, string format, string baselinePath)
    {
        if (fileSystem.FileExists(baselinePath))
        {
            return true;
        }

        CliErrorOutputWriter.Write(console, format, "configuration-error", $"Baseline file not found: {baselinePath}");
        return false;
    }

    public static void WriteOutcomeFailure(
        ICliConsole console,
        string format,
        string? error,
        IReadOnlyCollection<ArchitectureViolation> configurationViolations,
        string lifecycleVerb)
    {
        if (error != null)
        {
            CliErrorOutputWriter.Write(console, format, "configuration-error", error);
            return;
        }

        CliErrorOutputWriter.WriteConfigurationViolations(console, format, lifecycleVerb, configurationViolations);
    }
}
