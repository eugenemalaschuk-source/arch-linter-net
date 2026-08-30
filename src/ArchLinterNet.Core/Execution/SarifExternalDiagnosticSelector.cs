using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

/// <summary>Filters, fingerprints, orders, and deduplicates trusted external SARIF diagnostics.</summary>
public sealed class SarifExternalDiagnosticSelector
{
    /// <summary>Selects only policy-authorized diagnostics from already trusted reader results.</summary>
    public SarifExternalDiagnosticSelectionResult Select(
        IEnumerable<SarifExternalDiagnosticSelectionInput> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var candidates = new List<SelectionCandidate>();
        var mismatches = new List<SarifExternalDiagnosticFilterMismatch>();
        foreach (SarifExternalDiagnosticSelectionInput input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateInput(input);
            ArchitectureExternalEvidenceDiagnosticFilter filter = input.Requirement.DiagnosticFilter!;
            if (filter.RequireMatches)
            {
                mismatches.AddRange(FindRequiredFilterMismatches(input, filter, cancellationToken));
            }

            foreach (SarifEvidenceSourceDiagnostic source in input.Evidence.SourceDiagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TrySelect(filter, source, out SarifExternalDiagnosticGovernanceMode mode))
                {
                    continue;
                }

                SarifExternalDiagnosticFingerprint fingerprint = SelectFingerprint(input, source);
                string identity = CreateCanonicalIdentity(input, source, fingerprint);
                candidates.Add(new SelectionCandidate(input, source, mode, fingerprint, identity));
            }
        }

        SarifSelectedExternalDiagnostic[] diagnostics = candidates
            .GroupBy(candidate => candidate.CanonicalIdentity, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => CreateSelectedDiagnostic(group, cancellationToken))
            .ToArray();
        SarifExternalDiagnosticFilterMismatch[] orderedMismatches = mismatches
            .Distinct()
            .OrderBy(mismatch => mismatch.LogicalEvidenceId, StringComparer.Ordinal)
            .ThenBy(mismatch => mismatch.Dimension)
            .ThenBy(mismatch => mismatch.Value, StringComparer.Ordinal)
            .ToArray();
        return new SarifExternalDiagnosticSelectionResult(diagnostics, orderedMismatches);
    }

    private static void ValidateInput(SarifExternalDiagnosticSelectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Requirement.DiagnosticFilter is null)
        {
            throw new ArgumentException(
                "Each selected external-evidence requirement must declare diagnostic_filter.",
                nameof(input));
        }

        if (!input.Evidence.IsValid)
        {
            throw new ArgumentException(
                "External diagnostics can be selected only from a valid trusted evidence result.",
                nameof(input));
        }

        if (!string.Equals(input.Requirement.Id, input.Evidence.LogicalId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The trusted evidence logical identity must match its selecting policy requirement.",
                nameof(input));
        }
    }

    private static IEnumerable<SarifExternalDiagnosticFilterMismatch> FindRequiredFilterMismatches(
        SarifExternalDiagnosticSelectionInput input,
        ArchitectureExternalEvidenceDiagnosticFilter filter,
        CancellationToken cancellationToken)
    {
        foreach (string ruleId in filter.RuleIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!input.Evidence.SourceDiagnostics.Any(source =>
                    string.Equals(source.RuleId, ruleId, StringComparison.Ordinal)
                    && MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.RuleId)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    input.Requirement.Id, SarifExternalDiagnosticFilterDimension.RuleId, ruleId);
            }
        }

        foreach (string tag in filter.RuleTags)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!input.Evidence.SourceDiagnostics.Any(source =>
                    source.DriverRuleTags.Contains(tag, StringComparer.Ordinal)
                    && MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.RuleTag)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    input.Requirement.Id, SarifExternalDiagnosticFilterDimension.RuleTag, tag);
            }
        }

        foreach (string project in filter.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!input.Evidence.SourceDiagnostics.Any(source =>
                    string.Equals(source.Project, project, StringComparison.Ordinal)
                    && MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.Project)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    input.Requirement.Id, SarifExternalDiagnosticFilterDimension.Project, project);
            }
        }

        foreach (string pathPrefix in filter.PathPrefixes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!input.Evidence.SourceDiagnostics.Any(source =>
                    MatchesPathPrefix(source.PrimaryLocation?.Path, pathPrefix)
                    && MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.PathPrefix)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    input.Requirement.Id, SarifExternalDiagnosticFilterDimension.PathPrefix, pathPrefix);
            }
        }

        foreach (string sourceSeverity in filter.Severity.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!input.Evidence.SourceDiagnostics.Any(source =>
                    string.Equals(SeverityToken(source.SourceSeverity), sourceSeverity, StringComparison.Ordinal)
                    && MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.Severity)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    input.Requirement.Id, SarifExternalDiagnosticFilterDimension.Severity, sourceSeverity);
            }
        }
    }

    private static bool TrySelect(
        ArchitectureExternalEvidenceDiagnosticFilter filter,
        SarifEvidenceSourceDiagnostic source,
        out SarifExternalDiagnosticGovernanceMode mode)
    {
        mode = default;
        if (!MatchesExcept(filter, source, excludedDimension: null))
        {
            return false;
        }

        return filter.Severity.TryGetValue(SeverityToken(source.SourceSeverity), out string? configuredMode)
            && TryParseGovernanceMode(configuredMode, out mode);
    }

    private static bool MatchesExcept(
        ArchitectureExternalEvidenceDiagnosticFilter filter,
        SarifEvidenceSourceDiagnostic source,
        SarifExternalDiagnosticFilterDimension? excludedDimension)
    {
        return (excludedDimension == SarifExternalDiagnosticFilterDimension.RuleId
                || filter.RuleIds.Count == 0
                || source.RuleId is not null && filter.RuleIds.Contains(source.RuleId, StringComparer.Ordinal))
            && (excludedDimension == SarifExternalDiagnosticFilterDimension.RuleTag
                || filter.RuleTags.Count == 0
                || source.DriverRuleTags.Any(tag => filter.RuleTags.Contains(tag, StringComparer.Ordinal)))
            && (excludedDimension == SarifExternalDiagnosticFilterDimension.Project
                || filter.Projects.Count == 0
                || source.Project is not null && filter.Projects.Contains(source.Project, StringComparer.Ordinal))
            && (excludedDimension == SarifExternalDiagnosticFilterDimension.PathPrefix
                || filter.PathPrefixes.Count == 0
                || filter.PathPrefixes.Any(prefix => MatchesPathPrefix(source.PrimaryLocation?.Path, prefix)))
            && (excludedDimension == SarifExternalDiagnosticFilterDimension.Severity
                || filter.Severity.ContainsKey(SeverityToken(source.SourceSeverity)));
    }

    private static bool MatchesPathPrefix(string? path, string prefix)
    {
        if (path is null || !ExternalDiagnosticFilterRules.IsSafePathPrefix(prefix))
        {
            return false;
        }

        return prefix.EndsWith("/", StringComparison.Ordinal)
            ? path.StartsWith(prefix, StringComparison.Ordinal)
            : string.Equals(path, prefix, StringComparison.Ordinal)
                || path.StartsWith(prefix + "/", StringComparison.Ordinal);
    }

    private static SarifExternalDiagnosticFingerprint SelectFingerprint(
        SarifExternalDiagnosticSelectionInput input,
        SarifEvidenceSourceDiagnostic source)
    {
        SarifEvidenceSourceFingerprint? preferredSource = source.Fingerprints
            .Where(fingerprint => !string.IsNullOrWhiteSpace(fingerprint.Value))
            .OrderBy(fingerprint => fingerprint.Name, StringComparer.Ordinal)
            .ThenBy(fingerprint => fingerprint.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (preferredSource is not null)
        {
            return new SarifExternalDiagnosticFingerprint(
                SarifExternalDiagnosticFingerprintOrigin.Source,
                preferredSource.Value,
                preferredSource.Name);
        }

        return new SarifExternalDiagnosticFingerprint(
            SarifExternalDiagnosticFingerprintOrigin.Deterministic,
            "sha256:" + StableHash(FallbackIdentityParts(input, source)));
    }

    private static string CreateCanonicalIdentity(
        SarifExternalDiagnosticSelectionInput input,
        SarifEvidenceSourceDiagnostic source,
        SarifExternalDiagnosticFingerprint fingerprint)
    {
        string?[] fingerprintParts = [
            fingerprint.Origin.ToString(), fingerprint.SourceName, fingerprint.Value,
        ];
        return "external-diagnostic:v1:"
            + StableHash(FallbackIdentityParts(input, source).Concat(fingerprintParts));
    }

    private static IEnumerable<string?> FallbackIdentityParts(
        SarifExternalDiagnosticSelectionInput input,
        SarifEvidenceSourceDiagnostic source)
    {
        SarifEvidenceProvenance provenance = input.Evidence.Provenance;
        SarifEvidenceResolvedContext? context = provenance.Context;
        SarifEvidenceSourceLocation? location = source.PrimaryLocation;
        SarifEvidenceSourceRegion? region = location?.Region;
        return
        [
            "external-diagnostic-fallback-v1",
            input.Requirement.Id,
            context?.Repository,
            context?.Revision,
            context?.Scope,
            provenance.ToolName,
            provenance.ToolVersion,
            source.RuleId,
            source.Project,
            location?.Path,
            NullableInvariant(region?.StartLine),
            NullableInvariant(region?.StartColumn),
            NullableInvariant(region?.EndLine),
            NullableInvariant(region?.EndColumn),
            NullableInvariant(region?.CharOffset),
            NullableInvariant(region?.CharLength),
        ];
    }

    private static SarifSelectedExternalDiagnostic CreateSelectedDiagnostic(
        IGrouping<string, SelectionCandidate> group,
        CancellationToken cancellationToken)
    {
        SelectionCandidate[] occurrences = group
            .OrderBy(candidate => SourceSortKey(candidate.Source), StringComparer.Ordinal)
            .ThenBy(candidate => ProvenanceSortKey(candidate.Input.Evidence.Provenance), StringComparer.Ordinal)
            .ToArray();
        foreach (SelectionCandidate _ in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        SarifEvidenceProvenance[] provenance = occurrences
            .Select(candidate => candidate.Input.Evidence.Provenance)
            .Distinct()
            .OrderBy(ProvenanceSortKey, StringComparer.Ordinal)
            .ToArray();
        SelectionCandidate representative = occurrences[0];
        return new SarifSelectedExternalDiagnostic(
            group.Key,
            representative.Source,
            representative.Mode,
            representative.Fingerprint,
            provenance);
    }

    private static string SourceSortKey(SarifEvidenceSourceDiagnostic source)
    {
        IEnumerable<string?> parts =
        [
            source.RuleId,
            SeverityToken(source.SourceSeverity),
            source.Project,
            source.PrimaryLocation?.Path,
            NullableInvariant(source.PrimaryLocation?.Region?.StartLine),
            NullableInvariant(source.PrimaryLocation?.Region?.StartColumn),
            NullableInvariant(source.PrimaryLocation?.Region?.EndLine),
            NullableInvariant(source.PrimaryLocation?.Region?.EndColumn),
            NullableInvariant(source.PrimaryLocation?.Region?.CharOffset),
            NullableInvariant(source.PrimaryLocation?.Region?.CharLength),
        ];
        parts = parts.Concat(source.DriverRuleTags.OrderBy(tag => tag, StringComparer.Ordinal));
        parts = parts.Concat(source.FingerprintPairs
            .OrderBy(pair => pair.IsPartial)
            .ThenBy(pair => pair.Name, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => $"{pair.IsPartial}:{pair.Name}:{pair.Value}"));
        return StableKey(parts.Append(source.Message));
    }

    private static string ProvenanceSortKey(SarifEvidenceProvenance provenance) => StableKey(
    [
        provenance.LogicalId,
        provenance.Context?.Repository,
        provenance.Context?.Revision,
        provenance.Context?.Scope,
        provenance.ToolName,
        provenance.ToolVersion,
        provenance.ArtifactSha256,
        provenance.ArtifactPath,
        provenance.RunId,
    ]);

    private static bool TryParseGovernanceMode(string? value, out SarifExternalDiagnosticGovernanceMode mode)
    {
        mode = value switch
        {
            "strict" => SarifExternalDiagnosticGovernanceMode.Strict,
            "audit" => SarifExternalDiagnosticGovernanceMode.Audit,
            _ => default,
        };
        return value is "strict" or "audit";
    }

    private static string SeverityToken(SarifEvidenceSourceSeverity severity) => severity switch
    {
        SarifEvidenceSourceSeverity.Error => "error",
        SarifEvidenceSourceSeverity.Warning => "warning",
        SarifEvidenceSourceSeverity.Note => "note",
        SarifEvidenceSourceSeverity.None => "none",
        _ => "unspecified",
    };

    private static string? NullableInvariant(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string StableHash(IEnumerable<string?> values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(StableKey(values))));

    private static string StableKey(IEnumerable<string?> values)
    {
        var builder = new StringBuilder();
        foreach (string? value in values)
        {
            string current = value ?? "<null>";
            builder.Append(current.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(current);
            builder.Append(';');
        }

        return builder.ToString();
    }

    private sealed record SelectionCandidate(
        SarifExternalDiagnosticSelectionInput Input,
        SarifEvidenceSourceDiagnostic Source,
        SarifExternalDiagnosticGovernanceMode Mode,
        SarifExternalDiagnosticFingerprint Fingerprint,
        string CanonicalIdentity);
}
