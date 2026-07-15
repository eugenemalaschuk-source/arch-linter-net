using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Profile;

namespace ArchLinterNet.CEL.Parsing;

/// <summary>
/// Shared diagnostic construction for the tokenizer and parser. Every diagnostic produced here
/// uses category <c>"parser"</c> and carries <c>profileId</c>, per the tokenizer/parser
/// implementation-scope requirement in the <c>cel-profile-v1</c> spec.
/// </summary>
internal static class CelParseDiagnostics
{
    public const string Category = "parser";

    public static CelDiagnostic SyntaxError(CelSourceSpan span, string message, CelProfileId profileId) =>
        new(
            CelDiagnosticCode.SyntaxError,
            Category,
            CelDiagnosticSeverity.Error,
            span,
            message,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["profileId"] = profileId.Value });

    public static CelDiagnostic UnsupportedFeature(CelSourceSpan span, string message, CelProfileId profileId, string feature)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profileId"] = profileId.Value,
            ["feature"] = feature,
        };
        return new CelDiagnostic(CelDiagnosticCode.UnsupportedFeature, Category, CelDiagnosticSeverity.Error, span, message, parameters);
    }

    public static CelDiagnostic BudgetExceeded(CelSourceSpan span, string limitName, long observedValue, CelProfileId profileId) =>
        new(
            CelDiagnosticCode.BudgetExceeded,
            Category,
            CelDiagnosticSeverity.Error,
            span,
            $"Expression exceeds {limitName} limit (observed {observedValue}, profile '{profileId}').",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["limitName"] = limitName,
                ["observedValue"] = observedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["profileId"] = profileId.Value,
            });
}
