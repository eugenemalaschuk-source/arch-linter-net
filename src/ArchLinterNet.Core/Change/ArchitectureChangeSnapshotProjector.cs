using System.Globalization;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Change;

internal static class ArchitectureChangeSnapshotProjector
{
    internal static ArchitectureChangeSnapshot Project(
        string mode,
        ValidationOutcome validation,
        ArchitectureGraphOutcome namespaceGraph,
        ArchitectureGraphOutcome assemblyGraph,
        IReadOnlyList<ArchitectureBaselineComparisonEntry> baselineDebt,
        string? conditionSetName = null)
    {
        List<ArchitectureChangeEntry> entries = new();
        entries.AddRange(namespaceGraph.Graph.Nodes
            .Where(static node => node.Kind == ArchitectureGraphNodeKind.Namespace)
            .Select(static node => new ArchitectureChangeEntry("namespace", node.Id, node.Id)));
        entries.AddRange(assemblyGraph.Graph.Nodes
            .Where(static node => node.Kind == ArchitectureGraphNodeKind.Assembly)
            .Select(static node => new ArchitectureChangeEntry("assembly", node.Id, node.Id)));
        entries.AddRange(validation.DiscoveredProjectPaths.Select(path => Project(validation.RepositoryRoot, path)));
        entries.AddRange(namespaceGraph.Graph.Edges.Select(static edge => Edge("namespace", edge)));
        entries.AddRange(assemblyGraph.Graph.Edges.Select(static edge => Edge("assembly", edge)));
        entries.AddRange(SemanticEntries(validation.ClassificationRoles));
        entries.AddRange(CoverageBlindSpots(validation));

        List<ArchitectureChangeFinding> findings = ArchitectureFindingMapper
            .FromViolations(validation.Violations.Concat(validation.CoverageFindings), mode)
            .Concat(PolicyLevelFindings(validation, mode))
            .Select(static finding => new ArchitectureChangeFinding(
                finding.CanonicalIdentity,
                finding.Kind,
                finding.ContractName))
            .ToList();
        return new ArchitectureChangeSnapshot(
            ArchitectureChangeSnapshot.CurrentSchemaVersion,
            mode,
            conditionSetName ?? string.Empty,
            entries,
            findings,
            baselineDebt.Select(BaselineIdentity).OrderBy(static value => value, StringComparer.Ordinal).ToArray());
    }

    private static ArchitectureChangeEntry Edge(string level, ArchitectureGraphEdge edge) => new(
        "dependency_edge", level + ":" + edge.SourceId + "->" + edge.TargetId,
        level + ": " + edge.SourceId + " -> " + edge.TargetId);

    private static ArchitectureChangeEntry Project(string repositoryRoot, string projectPath)
    {
        string identity = CanonicalProjectIdentity(repositoryRoot, projectPath);
        return new ArchitectureChangeEntry("project", identity, identity);
    }

    private static string CanonicalProjectIdentity(string repositoryRoot, string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        string root = NormalizePath(repositoryRoot).TrimEnd('/');
        string project = NormalizePath(projectPath);
        string rootWithSeparator = root + "/";
        if (!project.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Discovered project path is outside the authoritative repository root.", nameof(projectPath));
        }

        return project[rootWithSeparator.Length..];
    }

    private static ArchitectureChangeEntry Role(ArchitectureClassificationRoleFact role) => new(
        "semantic_role", role.Subject + "|" + role.Role + "|" + Metadata(role.Metadata),
        role.Subject + " = " + role.Role);

    // Classification facts remain per CLR type/assembly through analysis. Linked marker sources
    // can therefore produce equivalent role and context observations, which are collapsed only in
    // this snapshot projection. Structural keys deliberately retain typed metadata and avoid the
    // snapshot identity encoding so delimiter/type collisions still reach final validation.
    private static IEnumerable<ArchitectureChangeEntry> SemanticEntries(
        IReadOnlyCollection<ArchitectureClassificationRoleFact> roles)
    {
        HashSet<SemanticRoleKey> roleKeys = new();
        HashSet<SemanticContextKey> contextKeys = new();

        foreach (ArchitectureClassificationRoleFact role in roles)
        {
            if (roleKeys.Add(new SemanticRoleKey(role)))
            {
                yield return Role(role);
            }

            foreach (SemanticMetadataEntry metadata in MetadataEntries(role.Metadata))
            {
                if (contextKeys.Add(new SemanticContextKey(role.Subject, metadata)))
                {
                    yield return new ArchitectureChangeEntry(
                        "semantic_context",
                        role.Subject + "|" + metadata.Key + "|" + Value(metadata.Value.Value),
                        role.Subject + ": " + metadata.Key + "=" + Value(metadata.Value.Value));
                }
            }
        }
    }

    private static SemanticMetadataEntry[] MetadataEntries(IReadOnlyDictionary<string, object> metadata) => metadata
        .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
        .Select(static entry => new SemanticMetadataEntry(
            entry.Key,
            new SemanticMetadataValue(entry.Value?.GetType(), entry.Value)))
        .ToArray();

    private readonly record struct SemanticMetadataValue(Type? Type, object? Value);

    private readonly record struct SemanticMetadataEntry(string Key, SemanticMetadataValue Value);

    private readonly record struct SemanticContextKey(string Subject, SemanticMetadataEntry Metadata);

    private sealed class SemanticRoleKey : IEquatable<SemanticRoleKey>
    {
        private readonly string _subject;
        private readonly string _role;
        private readonly SemanticMetadataEntry[] _metadata;

        internal SemanticRoleKey(ArchitectureClassificationRoleFact role)
        {
            _subject = role.Subject;
            _role = role.Role;
            _metadata = MetadataEntries(role.Metadata);
        }

        public bool Equals(SemanticRoleKey? other)
        {
            return other is not null
                && string.Equals(_subject, other._subject, StringComparison.Ordinal)
                && string.Equals(_role, other._role, StringComparison.Ordinal)
                && _metadata.SequenceEqual(other._metadata);
        }

        public override bool Equals(object? obj) => obj is SemanticRoleKey other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(_subject, StringComparer.Ordinal);
            hash.Add(_role, StringComparer.Ordinal);
            foreach (SemanticMetadataEntry metadata in _metadata)
            {
                hash.Add(metadata.Key, StringComparer.Ordinal);
                hash.Add(metadata.Value);
            }

            return hash.ToHashCode();
        }
    }

    // Aggregate, policy-level contracts (policy_consistency, unmatched_ignored_violations) report
    // findings with no per-violation source/target edge; they still need to appear here so their
    // drift is tracked like any other contract's. Gated on config the same way the human/JSON
    // reporting paths already are (see ReportCoordinator), so a contract family the caller turned
    // off does not leak permanently "new" findings into every future snapshot.
    private static IEnumerable<ArchitectureFinding> PolicyLevelFindings(ValidationOutcome validation, string mode)
    {
        if (validation.PolicyConsistencyConfig != "off")
        {
            foreach (PolicyConsistencyDiagnostic finding in validation.PolicyConsistencyFindings)
            {
                yield return ArchitectureFindingMapper.FromDiagnostic(finding, mode);
            }
        }

        if (validation.UnmatchedIgnoredViolationsConfig != "off")
        {
            foreach (ArchitectureUnmatchedIgnoredViolation unmatched in validation.UnmatchedIgnoredViolations)
            {
                yield return ArchitectureFindingMapper.FromDiagnostic(
                    ArchitectureDiagnosticMapper.FromUnmatchedIgnore(unmatched), mode);
            }
        }
    }

    private static IEnumerable<ArchitectureChangeEntry> CoverageBlindSpots(ValidationOutcome validation) => validation.CoverageSummaries
        .SelectMany(summary => summary.UncoveredItems.Select(item => Coverage("uncovered", summary, item.Item)))
        .Concat(validation.CoverageSummaries.SelectMany(summary => summary.StaleItems.Select(item => Coverage("stale", summary, item))))
        .Concat(validation.CoverageSummaries.SelectMany(summary => summary.UnknownItems.Select(item => Coverage("unknown", summary, item))));

    private static ArchitectureChangeEntry Coverage(string state, ArchitectureCoverageSummary summary, string item) => new(
        "coverage_blind_spot", (summary.ContractId ?? summary.ContractName) + "|" + summary.Scope + "|" + state + "|" + item,
        state + " " + summary.Scope + ": " + item);

    private static ArchitectureChangeEntry Coverage(
        string state, ArchitectureCoverageSummary summary, ArchitectureCoverageSummaryEvidenceItem item) =>
        Coverage(state, summary, RuleInputCoverageIdentityItem(summary, item));

    // Only the rule_input scope needs more than Item to identify a stale/unknown blind spot:
    // BuildRuleInputSummary keys Item on the referenced contract id alone, so a contract with two
    // problematic rule inputs produces two items sharing one Item, and both collapsed into one
    // entry (#683). Its Evidence carries the semantic discriminator "<input role>:<layer>".
    //
    // Every other scope already keys Item uniquely per fact (project coverage uses project.Path
    // with Evidence = the assembly name, semantic coverage uses the type/selector), so they are
    // deliberately left on the Item-only identity: folding Evidence in for them would change the
    // identity of existing, unchanged coverage facts and make the next change report show every
    // one of them as a spurious removed+new pair (#686 PR review round 4).
    private static string RuleInputCoverageIdentityItem(
        ArchitectureCoverageSummary summary, ArchitectureCoverageSummaryEvidenceItem item) =>
        summary.Scope == "rule_input" ? item.Item + "|" + item.Evidence : item.Item;

    private static string BaselineIdentity(ArchitectureBaselineComparisonEntry entry)
    {
        ArchitectureViolationIdentity? identity = entry.Identity;
        return identity is null
            ? throw new ArgumentException("Frozen baseline debt must have an authoritative identity.", nameof(entry))
            : ArchitectureViolationIdentityJson.Serialize(identity);
    }

    private static string Metadata(IReadOnlyDictionary<string, object> metadata) => string.Join(";", metadata
        .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
        .Select(entry => entry.Key + "=" + Value(entry.Value)));

    private static string Value(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
