using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

/// <summary>
/// Projects the effective contract catalog and the already-authoritative waiver lifecycle into
/// the Core policy inventory. This type intentionally does not inspect findings or raw YAML.
/// </summary>
internal static class ArchitecturePolicyInventoryProjector
{
    private const string CoverageFamily = "coverage";
    private const string AsmdefFamily = "asmdef";

    private static readonly string[] _knownWaiverStates =
    [
        "active",
        "stale",
        "expired",
        "metadata_incomplete",
        "invalid",
    ];

    /// <summary>
    /// Projects the exact selected mode scope. A null or empty selection means all contracts in
    /// that mode, matching <see cref="ArchitectureAnalysisSession.IsContractSelected(string?)"/>.
    /// </summary>
    internal static ArchitecturePolicyInventory Project(
        ArchitectureContractDocument document,
        string mode,
        IReadOnlyCollection<ArchitectureWaiverLifecycleRecord> waivers,
        IReadOnlyCollection<string>? selectedContractIds = null,
        bool includeAsmdefContracts = true,
        bool includeCoverageContracts = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(waivers);

        if (mode is not ("strict" or "audit"))
        {
            throw new ArgumentException($"Invalid mode: {mode}. Use 'strict' or 'audit'.", nameof(mode));
        }

        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);
        HashSet<RuleIdentity> identities = new(RuleIdentityComparer.Instance);
        int strict = 0;
        int audit = 0;
        int coverage = 0;

        foreach (ArchitectureContractDescriptor descriptor in catalog.Descriptors)
        {
            if (descriptor.Mode != mode
                || !includeAsmdefContracts && descriptor.Family == AsmdefFamily
                || !includeCoverageContracts && descriptor.Family == CoverageFamily
                || !IsSelected(descriptor, selectedContractIds))
            {
                continue;
            }

            // Source-set expansion replaces one authored contract by executable aliases and keeps
            // the authored identity in AuthoredId. Every other family remains one identity per
            // effective Id/name, scoped by mode/group/family so distinct controls cannot collide.
            string identity = descriptor.AuthoredId ?? descriptor.Id ?? descriptor.Name;
            if (!identities.Add(new RuleIdentity(mode, descriptor.Group, descriptor.Family, identity)))
            {
                continue;
            }

            if (descriptor.Family == CoverageFamily)
            {
                coverage++;
            }
            else if (mode == "strict")
            {
                strict++;
            }
            else
            {
                audit++;
            }
        }

        ArchitectureWaiverLifecycleRecord[] canonicalWaivers = waivers
            .Select(ValidateWaiverState)
            .OrderBy(record => record.Id, StringComparer.Ordinal)
            .ThenBy(record => record.ContractGroup, StringComparer.Ordinal)
            .ThenBy(record => record.ContractName, StringComparer.Ordinal)
            .ThenBy(record => record.SourceType, StringComparer.Ordinal)
            .ThenBy(record => record.ForbiddenReference, StringComparer.Ordinal)
            .ToArray();

        int active = canonicalWaivers.Count(record => record.State == "active");
        int stale = canonicalWaivers.Count(record => record.State == "stale");
        int expired = canonicalWaivers.Count(record => record.State == "expired");
        int metadataIncomplete = canonicalWaivers.Count(record => record.State == "metadata_incomplete");
        int invalid = canonicalWaivers.Count(record => record.State == "invalid");

        return new ArchitecturePolicyInventory(
            ArchitecturePolicyInventory.CurrentSchemaId,
            checked(strict + audit + coverage),
            new ArchitecturePolicyInventoryRules(strict, audit, coverage),
            new ArchitecturePolicyInventoryIgnoreDebt(
                canonicalWaivers.Length, active, stale, expired, metadataIncomplete, invalid),
            canonicalWaivers);
    }

    private static bool IsSelected(
        ArchitectureContractDescriptor descriptor,
        IReadOnlyCollection<string>? selectedContractIds)
    {
        if (selectedContractIds is not { Count: > 0 })
        {
            return true;
        }

        return (descriptor.Id is not null && Contains(selectedContractIds, descriptor.Id))
            || (descriptor.AuthoredId is not null && Contains(selectedContractIds, descriptor.AuthoredId));
    }

    private static bool Contains(IReadOnlyCollection<string> values, string value) =>
        values.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static ArchitectureWaiverLifecycleRecord ValidateWaiverState(
        ArchitectureWaiverLifecycleRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!_knownWaiverStates.Contains(record.State, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown architecture waiver lifecycle state '{record.State}'. " +
                "The architecture-policy-inventory/v1 projection cannot classify it safely.");
        }

        return record;
    }

    private readonly record struct RuleIdentity(string Mode, string Group, string Family, string Identity);

    private sealed class RuleIdentityComparer : IEqualityComparer<RuleIdentity>
    {
        public static RuleIdentityComparer Instance { get; } = new();

        public bool Equals(RuleIdentity x, RuleIdentity y) =>
            string.Equals(x.Mode, y.Mode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Group, y.Group, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Family, y.Family, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Identity, y.Identity, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(RuleIdentity obj) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Mode),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Group),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Family),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Identity));
    }
}
