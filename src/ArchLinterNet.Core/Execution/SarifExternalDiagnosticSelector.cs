using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

/// <summary>Filters, fingerprints, orders, and deduplicates trusted external SARIF diagnostics.</summary>
public sealed class SarifExternalDiagnosticSelector
{
    /// <summary>Selects only diagnostics authorized by their immutable trusted-reader snapshots.</summary>
    public SarifExternalDiagnosticSelectionResult Select(
        IEnumerable<SarifExternalDiagnosticSelectionInput> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        SarifExternalDiagnosticSelectionInput[] validatedInputs = inputs
            .Select(ValidateInput)
            .ToArray();
        var candidates = new List<SelectionCandidate>();
        var mismatches = new List<SarifExternalDiagnosticFilterMismatch>();

        foreach (IGrouping<string, SarifExternalDiagnosticSelectionInput> group in validatedInputs
                     .GroupBy(input => input.Evidence.Authorization!.GroupIdentity, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SarifExternalDiagnosticSelectionInput[] groupInputs = group.ToArray();
            SarifEvidenceAuthorizationSnapshot authorization = groupInputs[0].Evidence.Authorization!;
            SarifExternalDiagnosticFilterAuthorization filter = authorization.DiagnosticFilter!;
            SourceOccurrence[] occurrences = groupInputs
                .SelectMany(input => input.Evidence.SourceDiagnostics.Select(source =>
                    new SourceOccurrence(input.Evidence, source)))
                .ToArray();

            if (filter.RequireMatches)
            {
                mismatches.AddRange(FindRequiredFilterMismatches(
                    authorization.LogicalId,
                    filter,
                    occurrences,
                    cancellationToken));
            }

            foreach (SourceOccurrence occurrence in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TrySelect(filter, occurrence.Source, out SarifExternalDiagnosticGovernanceMode mode))
                {
                    continue;
                }

                SarifExternalDiagnosticFingerprint fingerprint = SelectFingerprint(occurrence.Evidence, occurrence.Source);
                string identity = CreateCanonicalIdentity(occurrence.Evidence, occurrence.Source, mode, fingerprint);
                candidates.Add(new SelectionCandidate(occurrence.Evidence, occurrence.Source, mode, fingerprint, identity));
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

    private static SarifExternalDiagnosticSelectionInput ValidateInput(
        SarifExternalDiagnosticSelectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        SarifEvidenceReadResult evidence = input.Evidence;
        if (!evidence.IsValid)
        {
            throw new ArgumentException(
                "External diagnostics can be selected only from a valid trusted evidence result.",
                nameof(input));
        }

        SarifEvidenceAuthorizationSnapshot? authorization = evidence.Authorization;
        if (authorization?.DiagnosticFilter is null)
        {
            throw new ArgumentException(
                "Each selected external-evidence result must have a diagnostic_filter captured by the trust reader.",
                nameof(input));
        }

        SarifEvidenceProvenance provenance = evidence.Provenance;
        if (!string.Equals(authorization.LogicalId, evidence.LogicalId, StringComparison.Ordinal)
            || !string.Equals(authorization.Tool, provenance.ToolName, StringComparison.Ordinal)
            || !string.Equals(authorization.ToolVersion, provenance.ToolVersion, StringComparison.Ordinal)
            || !string.Equals(authorization.Run, provenance.RunId, StringComparison.Ordinal)
            || authorization.ValidatedContext is null)
        {
            throw new ArgumentException(
                "The trusted evidence authorization snapshot does not match its validated provenance.",
                nameof(input));
        }

        return input;
    }

    private static IEnumerable<SarifExternalDiagnosticFilterMismatch> FindRequiredFilterMismatches(
        string logicalEvidenceId,
        SarifExternalDiagnosticFilterAuthorization filter,
        IReadOnlyList<SourceOccurrence> occurrences,
        CancellationToken cancellationToken)
    {
        var matchedRuleIds = new HashSet<string>(StringComparer.Ordinal);
        var matchedRuleTags = new HashSet<string>(StringComparer.Ordinal);
        var matchedProjects = new HashSet<string>(StringComparer.Ordinal);
        var matchedPathPrefixes = new HashSet<string>(StringComparer.Ordinal);
        var matchedSeverities = new HashSet<string>(StringComparer.Ordinal);

        foreach (SourceOccurrence occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SarifEvidenceSourceDiagnostic source = occurrence.Source;
            if (MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.RuleId)
                && source.RuleId is not null)
            {
                matchedRuleIds.Add(source.RuleId);
            }

            if (MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.RuleTag))
            {
                matchedRuleTags.UnionWith(source.DriverRuleTags);
            }

            if (MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.Project)
                && source.Project is not null)
            {
                matchedProjects.Add(source.Project);
            }

            if (MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.PathPrefix))
            {
                matchedPathPrefixes.UnionWith(filter.PathPrefixes.Where(prefix =>
                    MatchesPathPrefix(source.PrimaryLocation?.Path, prefix)));
            }

            if (MatchesExcept(filter, source, SarifExternalDiagnosticFilterDimension.Severity))
            {
                matchedSeverities.Add(SeverityToken(source.SourceSeverity));
            }
        }

        foreach (string ruleId in filter.RuleIds.Where(ruleId => !matchedRuleIds.Contains(ruleId)))
        {
            yield return new SarifExternalDiagnosticFilterMismatch(
                logicalEvidenceId, SarifExternalDiagnosticFilterDimension.RuleId, ruleId);
        }

        foreach (string tag in filter.RuleTags.Where(tag => !matchedRuleTags.Contains(tag)))
        {
            yield return new SarifExternalDiagnosticFilterMismatch(
                logicalEvidenceId, SarifExternalDiagnosticFilterDimension.RuleTag, tag);
        }

        foreach (string project in filter.Projects.Where(project => !matchedProjects.Contains(project)))
        {
            yield return new SarifExternalDiagnosticFilterMismatch(
                logicalEvidenceId, SarifExternalDiagnosticFilterDimension.Project, project);
        }

        foreach (string prefix in filter.PathPrefixes.Where(prefix => !matchedPathPrefixes.Contains(prefix)))
        {
            yield return new SarifExternalDiagnosticFilterMismatch(
                logicalEvidenceId, SarifExternalDiagnosticFilterDimension.PathPrefix, prefix);
        }

        foreach (string severity in filter.Severity.Keys.Where(severity => !matchedSeverities.Contains(severity)))
        {
            yield return new SarifExternalDiagnosticFilterMismatch(
                logicalEvidenceId, SarifExternalDiagnosticFilterDimension.Severity, severity);
        }
    }

    private static bool TrySelect(
        SarifExternalDiagnosticFilterAuthorization filter,
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
        SarifExternalDiagnosticFilterAuthorization filter,
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
        SarifEvidenceReadResult evidence,
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
            "sha256:" + StableHash(FallbackIdentityParts(evidence, source)));
    }

    private static string CreateCanonicalIdentity(
        SarifEvidenceReadResult evidence,
        SarifEvidenceSourceDiagnostic source,
        SarifExternalDiagnosticGovernanceMode mode,
        SarifExternalDiagnosticFingerprint fingerprint)
    {
        string?[] fingerprintParts =
        [
            fingerprint.Origin.ToString(), fingerprint.SourceName, fingerprint.Value,
            SeverityToken(source.SourceSeverity), mode.ToString(),
        ];
        return "external-diagnostic:v2:"
            + StableHash(FallbackIdentityParts(evidence, source).Concat(fingerprintParts));
    }

    private static IEnumerable<string?> FallbackIdentityParts(
        SarifEvidenceReadResult evidence,
        SarifEvidenceSourceDiagnostic source)
    {
        SarifEvidenceProvenance provenance = evidence.Provenance;
        SarifEvidenceResolvedContext? context = provenance.Context;
        SarifEvidenceSourceLocation? location = source.PrimaryLocation;
        SarifEvidenceSourceRegion? region = location?.Region;
        return
        [
            "external-diagnostic-fallback-v2",
            evidence.LogicalId,
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
            .ThenBy(candidate => ProvenanceSortKey(candidate.Evidence.Provenance), StringComparer.Ordinal)
            .ToArray();
        foreach (SelectionCandidate _ in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        SarifEvidenceProvenance[] provenance = occurrences
            .Select(candidate => candidate.Evidence.Provenance)
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

    private sealed record SourceOccurrence(
        SarifEvidenceReadResult Evidence,
        SarifEvidenceSourceDiagnostic Source);

    private sealed record SelectionCandidate(
        SarifEvidenceReadResult Evidence,
        SarifEvidenceSourceDiagnostic Source,
        SarifExternalDiagnosticGovernanceMode Mode,
        SarifExternalDiagnosticFingerprint Fingerprint,
        string CanonicalIdentity);
}
