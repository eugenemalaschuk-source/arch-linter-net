using System.Globalization;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Profile;

namespace ArchLinterNet.CEL.Evaluation;

/// <summary>
/// Structured runtime diagnostics produced by the bounded evaluator.
/// </summary>
internal static class CelEvaluationDiagnostics
{
    public static CelDiagnostic BudgetExceeded(
        CelSourceSpan span,
        string limitName,
        long observedValue,
        CelProfileId profileId) =>
        new(
            CelDiagnosticCode.BudgetExceeded,
            "evaluator",
            CelDiagnosticSeverity.Error,
            span,
            $"Evaluation exceeded the {limitName} limit.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["limitName"] = limitName,
                ["observedValue"] = observedValue.ToString(CultureInfo.InvariantCulture),
                ["profileId"] = profileId.ToString(),
            });

    public static CelDiagnostic SchemaMismatch(
        string schemaId,
        string expectedSchemaId,
        CelProfileId profileId) =>
        new(
            CelDiagnosticCode.SchemaMismatch,
            "evaluator",
            CelDiagnosticSeverity.Error,
            span: null,
            $"Evaluation context schema '{schemaId}' does not match the compiled program schema '{expectedSchemaId}'.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schemaId"] = schemaId,
                ["expectedSchemaId"] = expectedSchemaId,
                ["profileId"] = profileId.ToString(),
            });

    public static CelDiagnostic MissingKey(
        CelSourceSpan span,
        string key,
        CelProfileId profileId) =>
        new(
            CelDiagnosticCode.EvaluationFailure,
            "evaluator",
            CelDiagnosticSeverity.Error,
            span,
            $"Map key '{key}' was not present in the receiver value.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["failureKind"] = "missingKey",
                ["key"] = key,
                ["profileId"] = profileId.ToString(),
            });

    public static CelDiagnostic InvalidIndex(
        CelSourceSpan span,
        long index,
        int length,
        CelProfileId profileId) =>
        new(
            CelDiagnosticCode.EvaluationFailure,
            "evaluator",
            CelDiagnosticSeverity.Error,
            span,
            $"List index {index} is outside the valid range 0..{Math.Max(length - 1, 0)}.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["failureKind"] = "invalidIndex",
                ["index"] = index.ToString(CultureInfo.InvariantCulture),
                ["length"] = length.ToString(CultureInfo.InvariantCulture),
                ["profileId"] = profileId.ToString(),
            });

    public static CelDiagnostic MissingMember(
        CelSourceSpan span,
        string identifier,
        CelProfileId profileId) =>
        new(
            CelDiagnosticCode.EvaluationFailure,
            "evaluator",
            CelDiagnosticSeverity.Error,
            span,
            $"Object member '{identifier}' was not present in the receiver value.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["failureKind"] = "missingMember",
                ["identifier"] = identifier,
                ["profileId"] = profileId.ToString(),
            });

    public static CelDiagnostic MissingVariable(
        CelSourceSpan span,
        string identifier,
        CelProfileId profileId) =>
        new(
            CelDiagnosticCode.SchemaMismatch,
            "evaluator",
            CelDiagnosticSeverity.Error,
            span,
            $"Evaluation context did not provide a value for variable '{identifier}'.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["identifier"] = identifier,
                ["profileId"] = profileId.ToString(),
            });
}
