using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.History;
using ArchLinterNet.Core.History.Reporting;

namespace ArchLinterNet.Cli.Commands.History.Application;

// A report is fully materialized before stdout is touched. That makes a Unicode rejection
// fail-closed even if optional enrichment only fails while its late report fields are rendered.
internal static class HistoryReportOutputWriter
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool TryWriteJson(ICliConsole console, Func<string> render)
    {
        if (!TryRenderJson(console, render, out string? json))
        {
            return false;
        }

        console.WriteCanonicalJson(json!);
        return true;
    }

    // Multi-sink routing needs the validated bytes before it stages any destination. Keeping this
    // boundary shared with the single-stdout path prevents a Unicode regression in one route.
    public static bool TryRenderJson(ICliConsole console, Func<string> render, out string? json)
    {
        json = null;
        try
        {
            json = render();
            _ = _strictUtf8.GetBytes(json);
            return true;
        }
        catch (CanonicalJsonUnicodeException)
        {
            json = null;
            return WriteSerializationDiagnostic(console);
        }
        catch (EncoderFallbackException)
        {
            json = null;
            return WriteSerializationDiagnostic(console);
        }
    }

    private static bool WriteSerializationDiagnostic(ICliConsole console)
    {
        console.Error.Write(HistoryDiagnosticJsonWriter.Write(new HistoryDiagnostic(
            HistoryDiagnosticKind.ReportSerializationInvalid,
            "The release architecture forensics report contains invalid Unicode scalar content.")));
        return false;
    }
}
