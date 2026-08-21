using ArchLinterNet.Core.PolicyContext;

namespace ArchLinterNet.Core.PolicyWeakening;

internal static class ArchitecturePolicyWeakeningComparisonSupport
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    internal static IReadOnlyDictionary<string, ArchitecturePolicyContextContract> ContractMap(
        ArchitecturePolicyContextExport context,
        string? mode,
        string side)
    {
        IEnumerable<ArchitecturePolicyContextContract> contracts = mode is null
            ? context.Contracts
            : context.Contracts.Where(contract => contract.Mode == mode);
        return contracts.ToDictionary(ContractKey, _comparer);
    }

    internal static IReadOnlyDictionary<string, ArchitecturePolicyContextContract> ControlMap(
        ArchitecturePolicyContextExport context,
        string mode) => context.Contracts
        .Where(contract => contract.Mode == mode)
        .ToDictionary(ControlKey, _comparer);

    internal static IReadOnlyDictionary<string, ArchitecturePolicyContextSourceExpansion> ExpansionMap(
        ArchitecturePolicyContextExport context,
        string side) => context.SourceExpansions.ToDictionary(ExpansionKey, _comparer);

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> TopLevelFactValues(ArchitecturePolicyContextContract contract) => contract.Facts
        .Where(fact => fact.Values.Count > 0)
        .ToDictionary(fact => fact.Name, fact => fact.Values, _comparer);

    internal static IReadOnlyDictionary<string, string> FactEvidenceMap(ArchitecturePolicyContextContract contract) => contract.Facts
        .GroupBy(fact => fact.Name, _comparer)
        .ToDictionary(
            group => group.Key,
            group => string.Join("\u001e", group.Select(FactKey).OrderBy(value => value, _comparer)),
            _comparer);

    internal static bool IsForbiddenFact(string name) => name == "forbidden" || name.StartsWith("forbidden_", StringComparison.Ordinal);

    internal static bool IsAllowFact(string name) => name == "allowed"
        || name == "allowed_types"
        || name.StartsWith("allowed_only_in_", StringComparison.Ordinal)
        || name is "allowed_importers" or "allowed_friend_assemblies" or "allowed_public_constants";

    internal static bool IsScopeInventoryFact(string name) => name is "assemblies" or "projects" or "source_assemblies";

    internal static bool IsKnownDirectionalFact(string name) => IsForbiddenFact(name)
        || IsAllowFact(name)
        || IsScopeInventoryFact(name)
        || IsFactDependentSelectorFact(name)
        || name is "layers" or "optional_inputs";

    internal static bool IsFactDependentSelectorFact(string name) => name is "types_matching"
        or "exclude_types_matching"
        or "surface_selector"
        or "files_matching"
        or "exclude_files_matching";

    internal static IReadOnlyDictionary<string, bool> OptionalLayerMap(ArchitecturePolicyContextContract contract) => contract.Facts
        .Where(fact => fact.Name == "layers")
        .SelectMany(fact => fact.Items)
        .Select(layer => new
        {
            Name = FactValue(layer, "name"),
            Optional = FactValue(layer, "optional"),
        })
        .Where(layer => !string.IsNullOrWhiteSpace(layer.Name) && layer.Optional is "true" or "false")
        .ToDictionary(layer => layer.Name!, layer => layer.Optional == "true", _comparer);

    internal static IReadOnlySet<string> OptionalInputKeys(ArchitecturePolicyContextContract contract) => contract.Facts
        .Where(fact => fact.Name == "optional_inputs")
        .SelectMany(fact => fact.Items)
        .Select(input => string.Join("\u001f", FactValue(input, "contract_id"), FactValue(input, "input"), FactValue(input, "layer")))
        .Where(key => key != "\u001f\u001f")
        .ToHashSet(_comparer);

    internal static bool HasFactDependentSelectorChange(
        ArchitecturePolicyContextContract baseline,
        ArchitecturePolicyContextContract current) => !SelectorEvidence(baseline).SequenceEqual(SelectorEvidence(current), _comparer);

    internal static string[] SelectorEvidence(ArchitecturePolicyContextContract contract)
    {
        List<string> evidence = contract.Selectors.Select(SelectorKey).ToList();
        evidence.AddRange(contract.Exclusions.Select(SelectorKey));
        evidence.AddRange(contract.AdapterBindings.Select(binding => SelectorKey(binding.Adapter)));
        evidence.AddRange(contract.AdapterBindings.Select(binding => SelectorKey(binding.ExpectedPort)));
        evidence.AddRange(contract.AdapterBindings.SelectMany(binding => binding.AllowedContexts).Select(SelectorKey));
        evidence.AddRange(contract.Facts
            .Where(fact => IsFactDependentSelectorFact(fact.Name))
            .Select(FactKey));
        return evidence.OrderBy(value => value, _comparer).ToArray();
    }

    internal static ArchitecturePolicyWeakeningFinding CreateFinding(
        string kind,
        string controlIdentity,
        string classification,
        string severity,
        IReadOnlyList<string> baseValues,
        IReadOnlyList<string> currentValues,
        ArchitecturePolicyContextProvenance? baseProvenance,
        ArchitecturePolicyContextProvenance? currentProvenance,
        IReadOnlyList<string> affectedSubjects,
        string? rationale)
    {
        string[] orderedBase = baseValues.OrderBy(value => value, _comparer).ToArray();
        string[] orderedCurrent = currentValues.OrderBy(value => value, _comparer).ToArray();
        string identity = string.Join("\u001f", kind, controlIdentity, string.Join("\u001e", orderedBase), string.Join("\u001e", orderedCurrent));
        return new ArchitecturePolicyWeakeningFinding(
            identity,
            kind,
            controlIdentity,
            classification,
            severity,
            orderedBase,
            orderedCurrent,
            baseProvenance,
            currentProvenance,
            affectedSubjects.OrderBy(value => value, _comparer).ToArray(),
            rationale);
    }

    internal static string ControlIdentity(ArchitecturePolicyContextContract contract) => contract.Family + ":" + contract.Id;

    internal static string ControlKey(ArchitecturePolicyContextContract contract) => contract.Family + "\u001f" + contract.Id;

    internal static string ContractKeyFromExpansion(ArchitecturePolicyContextSourceExpansion expansion)
    {
        string mode = expansion.Group.StartsWith("audit_", StringComparison.Ordinal) ? "audit" : "strict";
        string family = expansion.Group[(expansion.Group.IndexOf('_') + 1)..];
        return mode + "\u001f" + family + "\u001f" + expansion.AuthoredContractId;
    }

    internal static string ExpandedInstanceKey(ArchitecturePolicyContextExpandedInstance instance) => string.Join(
        "\u001f", instance.ContractId, instance.Source ?? string.Empty, instance.SetName ?? string.Empty, instance.Selector ?? string.Empty);

    internal static string ExpansionExclusionKey(ArchitecturePolicyContextExpandedExclusion exclusion) => string.Join(
        "\u001f", exclusion.Source ?? string.Empty, exclusion.SetName ?? string.Empty, exclusion.Selector ?? string.Empty);

    internal static string ExceptionKey(ArchitecturePolicyContextException exceptionItem) => string.Join(
        "\u001f", exceptionItem.Scope, exceptionItem.Subject, exceptionItem.Kind, exceptionItem.Details,
        exceptionItem.IgnoredViolation?.SourceType ?? string.Empty,
        exceptionItem.IgnoredViolation?.ForbiddenReference ?? string.Empty);

    internal static string ExceptionControl(ArchitecturePolicyContextException exceptionItem) => exceptionItem.Scope + ":" + exceptionItem.Subject + ":" + exceptionItem.Kind;

    internal static bool IsUniversalException(ArchitecturePolicyContextException exceptionItem)
    {
        if (exceptionItem.Kind != "ignored_violation")
        {
            return false;
        }

        return exceptionItem.IgnoredViolation is { SourceType: "*", ForbiddenReference: "*" };
    }

    internal static bool IsBroadExceptionName(ArchitecturePolicyContextException exceptionItem) => exceptionItem.Details.Contains("shared", StringComparison.OrdinalIgnoreCase)
        || exceptionItem.Details.Contains("common", StringComparison.OrdinalIgnoreCase)
        || exceptionItem.Details.Contains("utils", StringComparison.OrdinalIgnoreCase);

    private static string ContractKey(ArchitecturePolicyContextContract contract) => contract.Mode + "\u001f" + contract.Family + "\u001f" + contract.Id;

    private static string ExpansionKey(ArchitecturePolicyContextSourceExpansion expansion) => string.Join(
        "\u001f", expansion.Group, expansion.AuthoredContractId, expansion.SelectorField ?? string.Empty);

    private static string SelectorKey(ArchitecturePolicyContextSelector selector) => string.Join(
        "\u001f",
        selector.Kind,
        selector.Role,
        string.Join("\u001e", selector.Metadata.OrderBy(item => item.Key, _comparer).Select(item => item.Key + "=" + item.Value)),
        selector.When ?? string.Empty);

    private static string FactKey(ArchitecturePolicyContextContractFact fact) => fact.Name
        + "=" + string.Join("\u001e", fact.Values.OrderBy(value => value, _comparer))
        + "[" + string.Join("|", fact.Items.Select(FactKey).OrderBy(value => value, _comparer)) + "]";

    private static string FactValue(ArchitecturePolicyContextContractFact fact, string name) => fact.Items
        .SingleOrDefault(item => item.Name == name)?.Values.SingleOrDefault() ?? string.Empty;
}
