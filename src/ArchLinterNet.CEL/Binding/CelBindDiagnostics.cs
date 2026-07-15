using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Profile;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Shared diagnostic construction for the binder. Every diagnostic produced here uses category
/// <c>"binder"</c> and carries <c>profileId</c>, per the binder implementation-scope requirement
/// in the <c>cel-profile-v1</c> spec. Mirrors <c>Parsing.CelParseDiagnostics</c>.
/// </summary>
internal static class CelBindDiagnostics
{
    public const string Category = "binder";

    public static CelDiagnostic BindingError(CelSourceSpan span, string message, CelProfileId profileId, string identifier)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profileId"] = profileId.Value,
            ["identifier"] = identifier,
        };
        return new CelDiagnostic(CelDiagnosticCode.BindingError, Category, CelDiagnosticSeverity.Error, span, message, parameters);
    }

    public static CelDiagnostic SchemaMismatch(CelSourceSpan span, string message, CelProfileId profileId, string identifier)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profileId"] = profileId.Value,
            ["identifier"] = identifier,
        };
        return new CelDiagnostic(CelDiagnosticCode.SchemaMismatch, Category, CelDiagnosticSeverity.Error, span, message, parameters);
    }

    public static CelDiagnostic TypeMismatch(CelSourceSpan span, string message, CelProfileId profileId, string expectedType, string actualType)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profileId"] = profileId.Value,
            ["expectedType"] = expectedType,
            ["actualType"] = actualType,
        };
        return new CelDiagnostic(CelDiagnosticCode.TypeMismatch, Category, CelDiagnosticSeverity.Error, span, message, parameters);
    }
}
