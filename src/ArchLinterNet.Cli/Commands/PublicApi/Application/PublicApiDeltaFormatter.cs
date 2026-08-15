using System.Buffers;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.PublicApi.Application;

// Renders an API delta by projecting it onto the same ArchitectureViolation/PublicApiSurfacePayload
// shape the contract checker produces, then handing it to the existing Core formatters. That is
// what makes human, JSON, and SARIF output of `public-api diff` carry literally the same normalized
// delta records as a strict validation run — parity is structural here, not re-implemented per
// format.
internal static class PublicApiDeltaFormatter
{
    private const string ContractName = "public-api-delta";

    // Cached so the per-entry name split does not allocate a fresh separator array on every call.
    private static readonly SearchValues<char> _nameTerminators = SearchValues.Create("(:");

    private static readonly string[] _typeLevelKinds =
        { "class", "interface", "struct", "enum", "delegate", "ctor" };
    private const string DeltaCategory = "public API surface";

    public static IReadOnlyList<ArchitectureViolation> ToViolations(string contractId, PublicApiDelta delta)
    {
        return delta.All.Select(entry => new ArchitectureViolation(
            ContractName,
            contractId,
            DeclaringTypeOf(entry.Signature),
            DeltaCategory,
            new[] { entry.Signature })
        {
            Payload = new PublicApiSurfacePayload(
                UndeclaredApiSignature: entry.Signature,
                ApiAssemblyName: entry.AssemblyName,
                ApiDeltaKind: KindName(entry.Kind),
                PreviousApiSignature: entry.PreviousSignature)
        }).ToArray();
    }

    public static string Format(
        ICliRuntime runtime, string format, string contractId, PublicApiDelta delta)
    {
        IReadOnlyList<ArchitectureViolation> violations = ToViolations(contractId, delta);

        return format switch
        {
            "json" => runtime.FormatResultForCiArtifacts(
                "strict",
                passed: !delta.HasChanges,
                violations,
                Array.Empty<string>(),
                Array.Empty<ArchitectureCycleFinding>(),
                Array.Empty<ArchitectureViolation>(),
                Array.Empty<ArchitectureUnmatchedIgnoredViolation>(),
                Array.Empty<PolicyConsistencyDiagnostic>(),
                Array.Empty<Core.Reporting.ArchitectureCoverageSummary>(),
                Array.Empty<ArchitectureClassificationConflict>(),
                Array.Empty<ArchitectureClassificationMetadataFailure>(),
                Array.Empty<ArchitectureClassificationRoleFact>(),
                null,
                Array.Empty<BuildStatePreflightDiagnostic>()),
            "sarif" => runtime.FormatResultAsSarif(
                "strict",
                violations,
                Array.Empty<string>(),
                Array.Empty<ArchitectureCycleFinding>(),
                Array.Empty<BuildStatePreflightDiagnostic>()),
            _ => FormatForHumans(runtime, delta, violations),
        };
    }

    public static string Summary(PublicApiDelta delta)
    {
        return $"added: {delta.Added.Count}, removed: {delta.Removed.Count}, changed: {delta.Changed.Count}";
    }

    private static string FormatForHumans(
        ICliRuntime runtime, PublicApiDelta delta, IReadOnlyList<ArchitectureViolation> violations)
    {
        if (!delta.HasChanges)
        {
            return "Public API snapshot is in sync: no additions, removals, or signature changes.";
        }

        return string.Join(
            Environment.NewLine,
            $"Public API delta ({Summary(delta)}):",
            runtime.FormatViolationsForHumans(violations));
    }

    private static string KindName(PublicApiDeltaKind kind) => kind switch
    {
        PublicApiDeltaKind.Added => "added",
        PublicApiDeltaKind.Removed => "removed",
        _ => "changed",
    };

    // Mirrors the declaring-type derivation the checker applies to removed members: a signature is
    // all the identity a delta entry has, since the removed side has no live reflection entry.
    private static string DeclaringTypeOf(string signature)
    {
        int kindSeparator = signature.IndexOf(' ', StringComparison.Ordinal);
        if (kindSeparator < 0)
        {
            return signature;
        }

        string kind = signature[..kindSeparator];
        string remainder = signature[(kindSeparator + 1)..];

        int cut = remainder.AsSpan().IndexOfAny(_nameTerminators);
        string name = (cut < 0 ? remainder : remainder[..cut]).TrimEnd();

        if (_typeLevelKinds.Contains(kind, StringComparer.Ordinal))
        {
            return name;
        }

        int lastSeparator = name.LastIndexOf('.');
        return lastSeparator <= 0 ? name : name[..lastSeparator];
    }
}
