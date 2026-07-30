namespace ArchLinterNet.Core.Model;

// The typed result of source-set expansion. It lives in Model, not Contracts, because Reporting
// and the CLI project it into JSON, SARIF and `explain` output and must not depend on the policy
// document model (see architecture/dependencies.arch.yml's Reporting and CLI boundaries). The
// authored, YAML-bound `ArchitectureSourceSet` declaration stays in Contracts.

// The identity domain of a source set. It also fixes which declared policy input a set's globs
// resolve against, so a set can never widen analysis beyond what `analysis`/`layers` already
// declare (see openspec/specs/source-set-expansion).
public enum ArchitectureSourceSetKind
{
    Assembly,
    Layer,
    Project
}

// Carried by every expanded contract instance so diagnostics, coverage and `explain` can name the
// authored contract, the set that produced the instance, and the exact selector that matched -
// none of which is recoverable from the derived instance id alone.
public sealed record ArchitectureSourceExpansionOrigin(
    string AuthoredContractId,
    string AuthoredContractName,
    string ResolvedSource,
    string? SetName,
    string Selector);

public sealed record ArchitectureSourceSetResolution(
    string Name,
    ArchitectureSourceSetKind Kind,
    IReadOnlyList<string> ResolvedSources,
    bool Optional,
    string Reason)
{
    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }
}

public sealed record ArchitectureExpandedContractInstance(
    string ContractId,
    string Source,
    string? SetName,
    string Selector);

public sealed record ArchitectureExpandedContractExclusion(
    string Source,
    string? SetName,
    string Selector,
    bool Matched)
{
    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }
}

// Fan-out contracts become one executable contract per resolved source. List-shaped consumers
// keep one executable contract and union set values into one of its selectors; recording that as
// a distinct kind prevents report consumers from mistaking those values for derived contracts.
public enum ArchitectureContractExpansionKind
{
    FanOut,
    InlineUnion
}

public sealed record ArchitectureContractExpansion(
    string Group,
    string AuthoredContractId,
    string AuthoredContractName,
    IReadOnlyList<string> SetNames,
    IReadOnlyList<ArchitectureExpandedContractInstance> Instances)
{
    public ArchitectureContractExpansionKind Kind { get; init; } = ArchitectureContractExpansionKind.FanOut;

    // The authored list-shaped selector which consumed the source sets for InlineUnion entries.
    // Null for FanOut entries, whose source selector is always `sources`/`source_sets`.
    public string? SelectorField { get; init; }

    // True when every referenced set resolved to nothing and declared `optional: true`. The
    // contract produced no instances on purpose, which is a reported state rather than silence.
    public bool OptionalEmpty { get; init; }

    public string OptionalReason { get; init; } = string.Empty;

    public ArchitecturePolicySourceLocation? PolicyLocation { get; init; }

    public IReadOnlyList<ArchitectureExpandedContractExclusion> Exclusions { get; init; } =
        Array.Empty<ArchitectureExpandedContractExclusion>();
}

public sealed class ArchitectureSourceExpansionInventory
{
    public static ArchitectureSourceExpansionInventory Empty { get; } = new(
        Array.Empty<ArchitectureSourceSetResolution>(),
        Array.Empty<ArchitectureContractExpansion>());

    public ArchitectureSourceExpansionInventory(
        IReadOnlyList<ArchitectureSourceSetResolution> sets,
        IReadOnlyList<ArchitectureContractExpansion> contracts)
    {
        Sets = sets;
        Contracts = contracts;
    }

    public IReadOnlyList<ArchitectureSourceSetResolution> Sets { get; }

    public IReadOnlyList<ArchitectureContractExpansion> Contracts { get; }

    public bool IsEmpty => Sets.Count == 0 && Contracts.Count == 0;

    // Instance ids produced by the authored contract id, used so contract selection and rule-input
    // coverage `contract_ids` keep accepting the authored id after expansion derived new ids.
    //
    // Spans every expansion carrying this authored id, not just the first: duplicate ids are
    // rejected only within one contract type and mode group, so the same authored id legitimately
    // exists in, say, both `strict_external` and `audit_package_dependency`. Returning one group's
    // instances would silently cover only part of what the author referenced.
    public IReadOnlyList<string> InstanceIdsFor(string authoredContractId)
    {
        return Contracts
            .Where(expansion =>
                string.Equals(expansion.AuthoredContractId, authoredContractId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(expansion => expansion.Instances.Select(instance => instance.ContractId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // Authored ids represented by the inventory, including optional-empty fan-out contracts. The
    // latter have no descriptors after expansion but must remain selectable by the authored id.
    public IReadOnlyList<string> AuthoredContractIds()
    {
        return Contracts
            .Select(expansion => expansion.AuthoredContractId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
