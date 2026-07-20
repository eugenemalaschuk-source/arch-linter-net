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
        ArchitecturePolicyDiagnostic? diagnostic = exception switch
        {
            ArchitecturePolicyImportException importException => importException.Diagnostic,
            ArchitecturePolicyValidationException validationException => validationException.Diagnostic,
            _ => null,
        };
        if (diagnostic is null)
        {
            return false;
        }

        WriteJson(console, exception.Message, diagnostic);
        return true;
    }

    public static void WriteJson(ICliConsole console, string message, ArchitecturePolicyDiagnostic diagnostic)
    {
        console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "architecture_policy_error",
            message,
            policy_location = diagnostic.Location is null ? null : ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson(diagnostic.Location),
            related_policy_locations = diagnostic.RelatedLocations.Select(ArchitectureDiagnosticFormatter.FormatPolicyLocationForJson),
            import_chain = diagnostic.ImportChain,
        }));
    }
}
