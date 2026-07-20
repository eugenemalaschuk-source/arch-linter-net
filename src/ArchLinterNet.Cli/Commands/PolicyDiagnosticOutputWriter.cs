using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Cli.Commands;

internal static class PolicyDiagnosticOutputWriter
{
    public static bool TryWriteJson(ICliConsole console, Exception exception)
    {
        (ArchitecturePolicyDiagnostic? Diagnostic, ArchitecturePolicyImportErrorCategory? Category) policyError = exception switch
        {
            ArchitecturePolicyImportException importException => (importException.Diagnostic, importException.Category),
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
        ArchitecturePolicyImportErrorCategory? category = null)
    {
        console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "architecture_policy_error",
            message,
            error_category = category?.ToString(),
            policy_location = diagnostic.Location is null ? null : ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(diagnostic.Location),
            related_policy_locations = diagnostic.RelatedLocations.Select(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson),
            import_chain = diagnostic.ImportChain,
        }));
    }
}
