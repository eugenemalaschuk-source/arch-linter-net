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
        var processedLogicalEvidenceIds = new SortedSet<string>(StringComparer.Ordinal);

        foreach (IGrouping<string, SarifExternalDiagnosticSelectionInput> group in validatedInputs
                     .GroupBy(input => input.Evidence.Authorization!.GroupIdentity, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SarifExternalDiagnosticSelectionInput[] groupInputs = group.ToArray();
            SarifEvidenceAuthorizationSnapshot authorization = groupInputs[0].Evidence.Authorization!;
            processedLogicalEvidenceIds.Add(authorization.LogicalId);
            SarifExternalDiagnosticFilterAuthorization filter = authorization.DiagnosticFilter!;
            var matcher = new FilterMatcher(filter);
            SourceOccurrence[] occurrences = groupInputs
                .SelectMany(input => input.Evidence.SourceDiagnostics.Select(source =>
                    new SourceOccurrence(input.Evidence, source)))
                .ToArray();
            RequiredFilterMatchAccumulator? requiredMatches = filter.RequireMatches
                ? new RequiredFilterMatchAccumulator()
                : null;

            foreach (SourceOccurrence occurrence in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FilterMatch match = matcher.Match(occurrence.Source);
                requiredMatches?.Record(matcher, occurrence.Source, match);
                if (!match.IsComplete
                    || !matcher.TryGetGovernanceMode(
                        occurrence.Source.SourceSeverity,
                        out SarifExternalDiagnosticGovernanceMode mode))
                {
                    continue;
                }

                SarifExternalDiagnosticFingerprint fingerprint = SelectFingerprint(occurrence.Evidence, occurrence.Source);
                string identity = CreateCanonicalIdentity(occurrence.Evidence, occurrence.Source, mode, fingerprint);
                candidates.Add(new SelectionCandidate(occurrence.Evidence, occurrence.Source, mode, fingerprint, identity));
            }

            if (requiredMatches is not null)
            {
                mismatches.AddRange(requiredMatches.CreateMismatches(authorization.LogicalId, matcher));
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
        return new SarifExternalDiagnosticSelectionResult(
            diagnostics,
            orderedMismatches,
            processedLogicalEvidenceIds.ToArray());
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

    private sealed class FilterMatcher
    {
        private readonly HashSet<string> _ruleIds;
        private readonly HashSet<string> _ruleTags;
        private readonly HashSet<string> _projects;
        private readonly Dictionary<string, string> _severity;

        public FilterMatcher(SarifExternalDiagnosticFilterAuthorization filter)
        {
            Filter = filter;
            ValidateBounds(filter);
            _ruleIds = new HashSet<string>(filter.RuleIds, StringComparer.Ordinal);
            _ruleTags = new HashSet<string>(filter.RuleTags, StringComparer.Ordinal);
            _projects = new HashSet<string>(filter.Projects, StringComparer.Ordinal);
            _severity = new Dictionary<string, string>(filter.Severity, StringComparer.Ordinal);
        }

        public SarifExternalDiagnosticFilterAuthorization Filter { get; }

        public FilterMatch Match(SarifEvidenceSourceDiagnostic source)
        {
            bool ruleId = _ruleIds.Count == 0
                || source.RuleId is not null && _ruleIds.Contains(source.RuleId);
            bool ruleTag = _ruleTags.Count == 0
                || source.DriverRuleTags.Any(_ruleTags.Contains);
            bool project = _projects.Count == 0
                || source.Project is not null && _projects.Contains(source.Project);
            bool pathPrefix = Filter.PathPrefixes.Count == 0
                || Filter.PathPrefixes.Any(prefix => MatchesPathPrefix(source.PrimaryLocation?.Path, prefix));
            bool severity = _severity.ContainsKey(SeverityToken(source.SourceSeverity));
            return new FilterMatch(ruleId, ruleTag, project, pathPrefix, severity);
        }

        public IEnumerable<string> MatchingPathPrefixes(string? path) =>
            Filter.PathPrefixes.Where(prefix => MatchesPathPrefix(path, prefix));

        public bool IsConfiguredRuleTag(string tag) => _ruleTags.Contains(tag);

        public bool TryGetGovernanceMode(
            SarifEvidenceSourceSeverity sourceSeverity,
            out SarifExternalDiagnosticGovernanceMode mode)
        {
            mode = default;
            return _severity.TryGetValue(SeverityToken(sourceSeverity), out string? configuredMode)
                && TryParseGovernanceMode(configuredMode, out mode);
        }

        private static void ValidateBounds(SarifExternalDiagnosticFilterAuthorization filter)
        {
            ValidateSelectorCount("rule_ids", filter.RuleIds.Count);
            ValidateSelectorCount("rule_tags", filter.RuleTags.Count);
            ValidateSelectorCount("projects", filter.Projects.Count);
            ValidateSelectorCount("path_prefixes", filter.PathPrefixes.Count);
            if (filter.Severity.Count > ExternalDiagnosticFilterRules.SupportedSeverities.Length)
            {
                throw new ArgumentException(
                    "The captured diagnostic_filter.severity map exceeds the supported source-severity bound.",
                    nameof(filter));
            }
        }

        private static void ValidateSelectorCount(string name, int count)
        {
            if (count > ExternalDiagnosticFilterRules.MaxValuesPerSelector)
            {
                throw new ArgumentException(
                    $"The captured diagnostic_filter.{name} list exceeds the " +
                    $"{ExternalDiagnosticFilterRules.MaxValuesPerSelector}-value bound.");
            }
        }
    }

    private readonly record struct FilterMatch(
        bool RuleId,
        bool RuleTag,
        bool Project,
        bool PathPrefix,
        bool Severity)
    {
        public bool IsComplete => RuleId && RuleTag && Project && PathPrefix && Severity;

        public bool MatchesExcept(SarifExternalDiagnosticFilterDimension dimension) => dimension switch
        {
            SarifExternalDiagnosticFilterDimension.RuleId => RuleTag && Project && PathPrefix && Severity,
            SarifExternalDiagnosticFilterDimension.RuleTag => RuleId && Project && PathPrefix && Severity,
            SarifExternalDiagnosticFilterDimension.Project => RuleId && RuleTag && PathPrefix && Severity,
            SarifExternalDiagnosticFilterDimension.PathPrefix => RuleId && RuleTag && Project && Severity,
            SarifExternalDiagnosticFilterDimension.Severity => RuleId && RuleTag && Project && PathPrefix,
            _ => false,
        };
    }

    private sealed class RequiredFilterMatchAccumulator
    {
        private readonly HashSet<string> _ruleIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _ruleTags = new(StringComparer.Ordinal);
        private readonly HashSet<string> _projects = new(StringComparer.Ordinal);
        private readonly HashSet<string> _pathPrefixes = new(StringComparer.Ordinal);
        private readonly HashSet<string> _severities = new(StringComparer.Ordinal);

        public void Record(
            FilterMatcher matcher,
            SarifEvidenceSourceDiagnostic source,
            FilterMatch match)
        {
            if (match.MatchesExcept(SarifExternalDiagnosticFilterDimension.RuleId)
                && source.RuleId is not null)
            {
                _ruleIds.Add(source.RuleId);
            }

            if (match.MatchesExcept(SarifExternalDiagnosticFilterDimension.RuleTag))
            {
                foreach (string tag in source.DriverRuleTags.Where(matcher.IsConfiguredRuleTag))
                {
                    _ruleTags.Add(tag);
                }
            }

            if (match.MatchesExcept(SarifExternalDiagnosticFilterDimension.Project)
                && source.Project is not null)
            {
                _projects.Add(source.Project);
            }

            if (match.MatchesExcept(SarifExternalDiagnosticFilterDimension.PathPrefix))
            {
                _pathPrefixes.UnionWith(matcher.MatchingPathPrefixes(source.PrimaryLocation?.Path));
            }

            if (match.MatchesExcept(SarifExternalDiagnosticFilterDimension.Severity))
            {
                _severities.Add(SeverityToken(source.SourceSeverity));
            }
        }

        public IEnumerable<SarifExternalDiagnosticFilterMismatch> CreateMismatches(
            string logicalEvidenceId,
            FilterMatcher matcher)
        {
            foreach (string ruleId in matcher.Filter.RuleIds.Where(ruleId => !_ruleIds.Contains(ruleId)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    logicalEvidenceId, SarifExternalDiagnosticFilterDimension.RuleId, ruleId);
            }

            foreach (string tag in matcher.Filter.RuleTags.Where(tag => !_ruleTags.Contains(tag)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    logicalEvidenceId, SarifExternalDiagnosticFilterDimension.RuleTag, tag);
            }

            foreach (string project in matcher.Filter.Projects.Where(project => !_projects.Contains(project)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    logicalEvidenceId, SarifExternalDiagnosticFilterDimension.Project, project);
            }

            foreach (string prefix in matcher.Filter.PathPrefixes.Where(prefix => !_pathPrefixes.Contains(prefix)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    logicalEvidenceId, SarifExternalDiagnosticFilterDimension.PathPrefix, prefix);
            }

            foreach (string severity in matcher.Filter.Severity.Keys.Where(severity => !_severities.Contains(severity)))
            {
                yield return new SarifExternalDiagnosticFilterMismatch(
                    logicalEvidenceId, SarifExternalDiagnosticFilterDimension.Severity, severity);
            }
        }
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
