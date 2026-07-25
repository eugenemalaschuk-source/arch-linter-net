using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands;

internal static class PolicyDiagnosticOutputWriter
{
    public static bool TryWriteJson(ICliConsole console, Exception exception)
    {
        (ArchitecturePolicyDiagnostic? Diagnostic, string? Category) policyError = exception switch
        {
            ArchitecturePolicyLoadException loadException => (loadException.Diagnostic, loadException.Category),
            ArchitecturePolicyValidationException validationException => (validationException.Diagnostic, null),
            _ => (null, null),
        };
        if (policyError.Diagnostic is null)
        {
            return false;
        }

        WriteJson(console, exception.Message, policyError.Diagnostic, policyError.Category);
        return true;
    }

    public static void WriteJson(
        ICliConsole console,
        string message,
        ArchitecturePolicyDiagnostic diagnostic,
        string? category = null)
    {
        console.Out.WriteLine(BuildJsonText(message, diagnostic, category));
    }

    public static string BuildJsonText(
        string message,
        ArchitecturePolicyDiagnostic diagnostic,
        string? category = null)
    {
        return JsonSerializer.Serialize(new
        {
            kind = "architecture_policy_error",
            message,
            error_category = category,
            policy_location = diagnostic.Location is null ? null : ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(diagnostic.Location),
            related_policy_locations = diagnostic.RelatedLocations.Select(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson),
            import_chain = diagnostic.ImportChain,
        });
    }

    public static bool TryWriteHuman(ICliConsole console, string prefix, Exception exception)
    {
        ArchitecturePolicyDiagnostic? diagnostic = exception switch
        {
            ArchitecturePolicyLoadException loadException => loadException.Diagnostic,
            ArchitecturePolicyValidationException validationException => validationException.Diagnostic,
            _ => null,
        };
        if (diagnostic is null)
        {
            return false;
        }

        WriteHuman(console, prefix, exception.Message, diagnostic);
        return true;
    }

    public static void WriteHuman(
        ICliConsole console,
        string prefix,
        string message,
        ArchitecturePolicyDiagnostic diagnostic)
    {
        console.Error.WriteLine(BuildHumanText(prefix, message, diagnostic));
    }

    public static string BuildHumanText(
        string prefix,
        string message,
        ArchitecturePolicyDiagnostic diagnostic)
    {
        string location = diagnostic.Location is null
            ? string.Empty
            : $" (policy: {diagnostic.Location.SourcePath}:{diagnostic.Location.YamlPath}; root: {diagnostic.Location.RootPath})";
        string text = $"{prefix}: {message}{location}";
        if (diagnostic.ImportChain.Count > 0)
        {
            text += $"\nImport chain: {string.Join(" -> ", diagnostic.ImportChain)}";
        }

        return text;
    }
}
