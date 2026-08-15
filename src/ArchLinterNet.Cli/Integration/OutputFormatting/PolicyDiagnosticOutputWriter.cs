using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Integration.OutputFormatting;

/// <summary>Owns the shared presentation of typed policy-loading and validation diagnostics.</summary>
internal static class PolicyDiagnosticOutputWriter
{
    public static bool TryWriteJson(ICliConsole console, Exception exception)
    {
        (ArchitecturePolicyDiagnostic? Diagnostic, string? Category) policyError = exception switch
        {
            ArchitecturePolicyLoadException loadException => (loadException.Diagnostic as ArchitecturePolicyDiagnostic, loadException.Category),
            ArchitecturePolicyValidationException validationException => (validationException.Diagnostic as ArchitecturePolicyDiagnostic, null),
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
        ArchitectureFinding finding = ArchitectureFindingMapper.FromPolicyError(message, diagnostic, category);
        Dictionary<string, object?> json = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding);
        json["message"] = message;
        json["error_category"] = category;
        json["policy_location"] = finding.PolicyOrigin is null
            ? null
            : ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(finding.PolicyOrigin);
        json["related_policy_locations"] = finding.RelatedPolicyOrigins
            .Select(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson);
        json["import_chain"] = diagnostic.ImportChain;
        return JsonSerializer.Serialize(json);
    }

    public static bool TryWriteHuman(ICliConsole console, string prefix, Exception exception)
    {
        ArchitecturePolicyDiagnostic? diagnostic = exception switch
        {
            ArchitecturePolicyLoadException loadException => loadException.Diagnostic as ArchitecturePolicyDiagnostic,
            ArchitecturePolicyValidationException validationException => validationException.Diagnostic as ArchitecturePolicyDiagnostic,
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
        ArchitectureFinding finding = ArchitectureFindingMapper.FromPolicyError(message, diagnostic);
        var details = (ArchitecturePolicyErrorDiagnostic)finding.Details;
        string location = finding.PolicyOrigin is null
            ? string.Empty
            : $" (policy: {finding.PolicyOrigin.SourcePath}:{finding.PolicyOrigin.YamlPath}; root: {finding.PolicyOrigin.RootPath})";
        string text = $"{prefix}: {details.Message}{location}";
        if (details.ImportChain.Count > 0)
        {
            text += $"\nImport chain: {string.Join(" -> ", details.ImportChain)}";
        }

        return text;
    }
}
