using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitectureSemanticCoverageService
{
    private const string UnclassifiedSemanticFact = "unclassified semantic fact";

    private readonly ArchitectureAnalysisSession _session;
    private Type[]? _orderedTypes;
    private Type[]? _classifiedTypes;
    private List<ArchitectureCoverageSummaryEvidenceItem>? _semanticStaleItems;
    private Dictionary<Type, string?>? _semanticGovernanceByType;
    private Dictionary<string, List<Type>>? _typesByDiagnosticSubject;
    private readonly Dictionary<ArchitectureContextualConsumerReference, ContextualConsumerMatcher> _contextualMatchers = new();

    public ArchitectureSemanticCoverageService(ArchitectureAnalysisSession session)
    {
        _session = session;
    }

    private ArchitectureContractDocument Document => _session.Document;

    private ArchitectureTypeIndex TypeIndex => _session.TypeIndex;

    private ArchitectureRoleIndex RoleIndex => _session.RoleIndex;

    private ArchitectureAnalysisFactService Facts => _session.Facts;

    private ArchitectureExpressionFactService ExpressionFacts => _session.ExpressionFacts;

    private IReadOnlyCollection<ArchitectureContextualConsumerReference> RegisteredContextualConsumers =>
        _session.RegisteredContextualConsumers;

    internal ArchitectureCoverageSummary BuildSummary(ArchitectureCoverageContract contract)
    {
        List<ArchitectureCoverageSummaryExcludedItem> excludedItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> uncoveredItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> staleItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> unknownItems = new();
        List<ArchitectureCoverageSummaryEvidenceItem> coveredItems = new();
        Type[] types = GetOrderedTypes();

        foreach (Type type in types)
        {
            if (!IsSemanticCoverageTypeInScope(contract, type))
            {
                continue;
            }

            if (!RoleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor))
            {
                uncoveredItems.Add(new ArchitectureCoverageSummaryEvidenceItem(
                    ArchitectureTypeNames.SafeFullName(type), UnclassifiedSemanticFact));
                continue;
            }

            ArchitectureCoverageExclusion? exclusion = contract.Exclude.FirstOrDefault(
                candidate => MatchesSemanticExclusion(candidate, descriptor));
            if (exclusion != null)
            {
                excludedItems.Add(new ArchitectureCoverageSummaryExcludedItem(
                    ArchitectureTypeNames.SafeFullName(type), exclusion.Reason, DescribeSemanticFact(descriptor)));
                continue;
            }

            string? governance = DescribeSemanticGovernance(type);
            (governance == null ? uncoveredItems : coveredItems).Add(new ArchitectureCoverageSummaryEvidenceItem(
                ArchitectureTypeNames.SafeFullName(type), governance == null
                    ? DescribeSemanticFact(descriptor)
                    : $"{DescribeSemanticFact(descriptor)}; governed by {governance}"));
        }

        staleItems.AddRange(GetSemanticStaleItems(types));
        unknownItems.AddRange(GetSemanticUnknownItems(contract));

        return new ArchitectureCoverageSummary(
            contract.Name, contract.Id, contract.Scope,
            new ArchitectureCoverageSummaryCounts(
                coveredItems.Count, excludedItems.Count, uncoveredItems.Count, staleItems.Count, unknownItems.Count),
            excludedItems, uncoveredItems, staleItems, unknownItems, coveredItems);
    }

    internal List<ArchitectureViolation> Check(ArchitectureCoverageContract contract)
    {
        List<ArchitectureViolation> findings = new();
        Type[] types = GetOrderedTypes();
        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);

        foreach (Type type in types)
        {
            CheckType(contract, executionContext, type, findings);
        }

        foreach (ArchitectureCoverageSummaryEvidenceItem stale in GetSemanticStaleItems(types))
        {
            AddSemanticDiagnosticFinding(findings, executionContext, contract, stale, "stale semantic selector");
        }

        foreach (ArchitectureCoverageSummaryEvidenceItem unknown in GetSemanticUnknownItems(contract))
        {
            string violation = unknown.Evidence.StartsWith("classification conflict:", StringComparison.Ordinal)
                ? "classification conflict"
                : "classification metadata failure";
            AddSemanticDiagnosticFinding(findings, executionContext, contract, unknown, violation);
        }

        _session.CollectUnmatchedIgnores(executionContext);
        return findings;
    }

    private void CheckType(
        ArchitectureCoverageContract contract,
        ArchitectureContractExecutionContext executionContext,
        Type type,
        List<ArchitectureViolation> findings)
    {
        if (!IsSemanticCoverageTypeInScope(contract, type))
        {
            return;
        }

        string subject = ArchitectureTypeNames.SafeFullName(type);
        string sourceAssembly = ArchitectureTypeNames.SafeAssemblyName(type) ?? string.Empty;
        if (!RoleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor))
        {
            AddUnclassifiedTypeFinding(contract, executionContext, subject, sourceAssembly, findings);
            return;
        }

        if (contract.Exclude.Any(exclusion => MatchesSemanticExclusion(exclusion, descriptor))
            || DescribeSemanticGovernance(type) != null)
        {
            return;
        }

        AddUncoveredRoleFinding(contract, executionContext, subject, sourceAssembly, descriptor, findings);
    }

    private static void AddUnclassifiedTypeFinding(
        ArchitectureCoverageContract contract,
        ArchitectureContractExecutionContext executionContext,
        string subject,
        string sourceAssembly,
        List<ArchitectureViolation> findings)
    {
        if (executionContext.IsIgnored(
                subject,
                UnclassifiedSemanticFact,
                sourceAssembly: sourceAssembly,
                targetType: subject,
                targetMember: UnclassifiedSemanticFact))
        {
            return;
        }

        findings.Add(new ArchitectureViolation(contract.Name, contract.Id, subject,
            UnclassifiedSemanticFact, new[] { subject }));
    }

    private static void AddUncoveredRoleFinding(
        ArchitectureCoverageContract contract,
        ArchitectureContractExecutionContext executionContext,
        string subject,
        string sourceAssembly,
        ArchitectureTypeClassificationResult descriptor,
        List<ArchitectureViolation> findings)
    {
        const string UncoveredSemanticRole = "uncovered semantic role";
        if (executionContext.IsIgnored(
                subject,
                UncoveredSemanticRole,
                sourceAssembly: sourceAssembly,
                targetType: subject,
                targetMember: UncoveredSemanticRole))
        {
            return;
        }

        findings.Add(new ArchitectureViolation(contract.Name, contract.Id, subject,
            UncoveredSemanticRole, new[] { DescribeSemanticFact(descriptor) }));
    }

    private static bool IsSemanticCoverageTypeInScope(ArchitectureCoverageContract contract, Type type)
    {
        return contract.Roots.Count == 0 || contract.Roots.Any(root =>
            ArchitectureCoverageMatchingService.MatchesNamespaceRoot(root, ArchitectureTypeNames.SafeNamespace(type)));
    }

    private static void AddSemanticDiagnosticFinding(
        List<ArchitectureViolation> findings,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureCoverageContract contract,
        ArchitectureCoverageSummaryEvidenceItem item,
        string violation)
    {
        if (!executionContext.IsIgnored(item.Item, violation))
        {
            findings.Add(new ArchitectureViolation(contract.Name, contract.Id, item.Item, violation, new[] { item.Evidence }));
        }
    }

    private string? DescribeSemanticGovernance(Type type)
    {
        _semanticGovernanceByType ??= new Dictionary<Type, string?>();
        if (_semanticGovernanceByType.TryGetValue(type, out string? cachedGovernance))
        {
            return cachedGovernance;
        }

        ArchitectureLayer? layer = Document.Layers.Values
            .Where(candidate => candidate.Selector != null && Facts.MatchesLayer(candidate, type))
            .OrderBy(ArchitectureLayerResolver.DescribeLayer, StringComparer.Ordinal)
            .FirstOrDefault();
        if (layer != null)
        {
            string governance = $"layer {ArchitectureLayerResolver.DescribeLayer(layer)}";
            _semanticGovernanceByType[type] = governance;
            return governance;
        }

        ArchitectureContextualConsumerReference? consumer = RegisteredContextualConsumers
            .Where(candidate => MatchesContextualConsumer(candidate, type))
            .OrderBy(candidate => candidate.Description, StringComparer.Ordinal)
            .FirstOrDefault();
        string? contextualGovernance = consumer == null ? null : $"contextual consumer {DescribeConsumer(consumer)}";
        _semanticGovernanceByType[type] = contextualGovernance;
        return contextualGovernance;
    }

    private List<ArchitectureCoverageSummaryEvidenceItem> GetSemanticStaleItems(IEnumerable<Type> types)
    {
        if (_semanticStaleItems != null)
        {
            return _semanticStaleItems;
        }

        List<ArchitectureCoverageSummaryEvidenceItem> items = new();
        foreach (ArchitectureLayer layer in Document.Layers.Values
                     .Where(layer => layer.Selector != null && !layer.External)
                     .OrderBy(ArchitectureLayerResolver.DescribeLayer, StringComparer.Ordinal))
        {
            if (!types.Any(type => RoleIndex.TryGetRole(type, out _) && Facts.MatchesLayer(layer, type)))
                items.Add(new ArchitectureCoverageSummaryEvidenceItem(ArchitectureLayerResolver.DescribeLayer(layer), "semantic selector matched no classified type"));
        }
        foreach (ArchitectureContextualConsumerReference consumer in RegisteredContextualConsumers
                     .Where(consumer => !types.Any(type => MatchesContextualConsumer(consumer, type)))
                     .OrderBy(consumer => consumer.Description, StringComparer.Ordinal))
        {
            items.Add(new ArchitectureCoverageSummaryEvidenceItem(DescribeConsumer(consumer), "contextual semantic selector matched no classified type"));
        }
        _semanticStaleItems = items;
        return items;
    }

    private List<ArchitectureCoverageSummaryEvidenceItem> GetSemanticUnknownItems(ArchitectureCoverageContract contract)
    {
        List<ArchitectureCoverageSummaryEvidenceItem> items = new();
        items.AddRange(RoleIndex.Conflicts
            .Where(conflict => IsSemanticDiagnosticInScope(contract, conflict.Subject))
            .OrderBy(conflict => conflict.Subject, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.Source)
            .ThenBy(conflict => conflict.WinningRole, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.DiscardedRole, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.MetadataDetail, StringComparer.Ordinal)
            .Select(conflict => new ArchitectureCoverageSummaryEvidenceItem(conflict.Subject,
                $"classification conflict: source={conflict.Source}; winning={conflict.WinningRole}; discarded={conflict.DiscardedRole}; metadata={conflict.MetadataDetail ?? "<none>"}")));
        items.AddRange(RoleIndex.MetadataFailures
            .Where(failure => IsSemanticDiagnosticInScope(contract, failure.Subject))
            .OrderBy(failure => failure.Subject, StringComparer.Ordinal)
            .ThenBy(failure => failure.Source)
            .ThenBy(failure => failure.MetadataKey, StringComparer.Ordinal)
            .ThenBy(failure => failure.Reason, StringComparer.Ordinal)
            .Select(failure => new ArchitectureCoverageSummaryEvidenceItem(failure.Subject,
                $"metadata failure: source={failure.Source}; key={failure.MetadataKey}; reason={failure.Reason}")));
        return items;
    }

    private bool IsSemanticDiagnosticInScope(ArchitectureCoverageContract contract, string subject)
    {
        _typesByDiagnosticSubject ??= BuildTypesByDiagnosticSubject();
        return _typesByDiagnosticSubject.TryGetValue(subject, out List<Type>? types)
               && types.Any(type => IsSemanticCoverageTypeInScope(contract, type));
    }

    private bool MatchesContextualConsumer(ArchitectureContextualConsumerReference consumer, Type type)
    {
        return GetContextualMatcher(consumer).Matches(type);
    }

    private Type[] GetOrderedTypes() => _orderedTypes ??= TypeIndex.AllTypes()
        .OrderBy(type => ArchitectureTypeNames.SafeFullName(type), StringComparer.Ordinal)
        .ToArray();

    private Type[] GetClassifiedTypes() => _classifiedTypes ??= RoleIndex.ClassifiedTypes().ToArray();

    private Dictionary<string, List<Type>> BuildTypesByDiagnosticSubject()
    {
        Dictionary<string, List<Type>> result = new(StringComparer.Ordinal);
        foreach (Type type in GetOrderedTypes())
        {
            Add(ArchitectureTypeNames.SafeFullName(type), type);
            Add(type.Assembly.GetName().Name, type);
        }

        return result;

        void Add(string? subject, Type type)
        {
            if (string.IsNullOrEmpty(subject))
            {
                return;
            }

            if (!result.TryGetValue(subject, out List<Type>? matchingTypes))
            {
                matchingTypes = new List<Type>();
                result.Add(subject, matchingTypes);
            }

            matchingTypes.Add(type);
        }
    }

    private ContextualConsumerMatcher GetContextualMatcher(ArchitectureContextualConsumerReference consumer)
    {
        if (_contextualMatchers.TryGetValue(consumer, out ContextualConsumerMatcher? matcher))
        {
            return matcher;
        }

        ArchitectureContextSelector selector = CreateContextualSelector(
            consumer.Role, consumer.Metadata, consumer.When,
            consumer.CompiledWhen as ArchLinterNet.CEL.Compilation.CelCompiledPredicate,
            consumer.WhenLocation, consumer.WhenContractName);
        ArchitectureContextSelector? sourceSelector = consumer.SourceRole == null
            ? null
            : CreateContextualSelector(
                consumer.SourceRole, consumer.SourceMetadata!, consumer.SourceWhen,
                consumer.SourceCompiledWhen as ArchLinterNet.CEL.Compilation.CelCompiledPredicate,
                consumer.SourceWhenLocation, consumer.SourceWhenContractName);
        matcher = new ContextualConsumerMatcher(this, selector, sourceSelector);
        _contextualMatchers.Add(consumer, matcher);
        return matcher;
    }

    private sealed class ContextualConsumerMatcher(
        ArchitectureSemanticCoverageService owner,
        ArchitectureContextSelector selector,
        ArchitectureContextSelector? sourceSelector)
    {
        private readonly Type[] _classifiedTypes = sourceSelector == null
            ? Array.Empty<Type>()
            : owner.GetClassifiedTypes();
        private readonly List<int> _matchingSourceIndexes = new();
        private int _nextSourceCandidate;
        private bool _sourceCandidatesExhausted;

        internal bool Matches(Type targetType)
        {
            if (sourceSelector == null)
            {
                return ArchitectureContextSelectorMatcher.Matches(
                    selector, targetType, owner.RoleIndex, sourceDescriptor: null, owner.ExpressionFacts, sourceType: null);
            }

            if (!HasSourceRelativeConstraint(selector)
                && !ArchitectureContextSelectorMatcher.MatchesLiteral(
                    selector, targetType, owner.RoleIndex, sourceDescriptor: null))
            {
                int nextSourceIndex = _matchingSourceIndexes.Count;
                while (GetMatchingSourceAt(nextSourceIndex++) != null)
                {
                }

                return false;
            }

            int matchingSourceIndex = 0;
            while (true)
            {
                Type? sourceType = GetMatchingSourceAt(matchingSourceIndex++);
                if (sourceType == null)
                {
                    return false;
                }

                if (owner.RoleIndex.TryGetRole(sourceType, out ArchitectureTypeClassificationResult sourceDescriptor)
                    && ArchitectureContextSelectorMatcher.Matches(
                        selector, targetType, owner.RoleIndex, sourceDescriptor, owner.ExpressionFacts, sourceType))
                {
                    return true;
                }
            }
        }

        private Type? GetMatchingSourceAt(int matchingSourceIndex)
        {
            while (_matchingSourceIndexes.Count <= matchingSourceIndex && !_sourceCandidatesExhausted)
            {
                if (_nextSourceCandidate == _classifiedTypes.Length)
                {
                    _sourceCandidatesExhausted = true;
                    break;
                }

                int candidateIndex = _nextSourceCandidate++;
                Type candidate = _classifiedTypes[candidateIndex];
                if (ArchitectureContextSelectorMatcher.Matches(
                        sourceSelector!, candidate, owner.RoleIndex, sourceDescriptor: null,
                        owner.ExpressionFacts, sourceType: null))
                {
                    _matchingSourceIndexes.Add(candidateIndex);
                }
            }

            return matchingSourceIndex < _matchingSourceIndexes.Count
                ? _classifiedTypes[_matchingSourceIndexes[matchingSourceIndex]]
                : null;
        }

        private static bool HasSourceRelativeConstraint(ArchitectureContextSelector candidateSelector) =>
            candidateSelector.Metadata.Values
                .OfType<string>()
                .Any(value => value.StartsWith("!{source.metadata.", StringComparison.Ordinal));
    }

    // Rebuilds a synthetic ArchitectureContextSelector from a coverage-facing consumer reference
    // for matching purposes only. WhenLocation/WhenContractName must be carried through too (not
    // just When/CompiledWhen) — otherwise an evaluation error triggered from this coverage-matching
    // path loses YAML provenance even though the same error triggered from ordinary violation
    // checking (which always uses the real, originally-compiled selector) has it.
    private static ArchitectureContextSelector CreateContextualSelector(
        string role,
        IReadOnlyDictionary<string, object> metadata,
        string? when,
        ArchLinterNet.CEL.Compilation.CelCompiledPredicate? compiledWhen,
        ArchitecturePolicySourceLocation? whenLocation,
        string? whenContractName)
    {
        return new ArchitectureContextSelector
        {
            Role = role,
            Metadata = new Dictionary<string, object>(metadata, StringComparer.Ordinal),
            When = when,
            CompiledWhen = compiledWhen,
            WhenLocation = whenLocation,
            WhenContractName = whenContractName
        };
    }

    private static bool MatchesSemanticExclusion(
        ArchitectureCoverageExclusion exclusion,
        ArchitectureTypeClassificationResult descriptor)
    {
        return exclusion.Metadata != null
               && string.Equals(exclusion.Role, descriptor.Role, StringComparison.Ordinal)
               && exclusion.Metadata.All(entry => descriptor.Metadata.TryGetValue(entry.Key, out object? actual)
                                                  && ArchitectureMetadataValueComparer.ValuesEqual(actual, entry.Value));
    }

    private static string DescribeSemanticFact(ArchitectureTypeClassificationResult descriptor)
    {
        string metadata = descriptor.Metadata.Count == 0
            ? string.Empty
            : $" metadata={string.Join(",", descriptor.Metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={FormatSemanticMetadataValue(entry.Value)}"))}";
        return $"role={descriptor.Role}{metadata}";
    }

    private static string FormatSemanticMetadataValue(object? value)
    {
        if (value is System.Collections.IEnumerable sequence and not string)
            return $"[{string.Join(",", sequence.Cast<object?>().Select(FormatSemanticMetadataValue))}]";
        return value switch
        {
            null => "null",
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string DescribeConsumer(ArchitectureContextualConsumerReference consumer)
    {
        return consumer.Description;
    }
}
