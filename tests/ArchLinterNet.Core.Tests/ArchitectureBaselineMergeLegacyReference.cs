using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Test-only copy of the pre-index merge behavior for small differential cases and benchmark
/// output checks. Keep its structured key aligned with the previous service's IdentityEquals.
/// </summary>
internal static class ArchitectureBaselineMergeLegacyReference
{
    internal static void MergeStrict(
        ArchitectureContractDocument policy,
        ArchitectureBaselineDocument baseline,
        LegacyWorkCounter? workCounter = null)
    {
        List<ArchitectureDependencyContract> contracts = policy.Contracts.Strict.ToList();
        bool isStructured = baseline.Version is 2 or 3;

        foreach (ArchitectureBaselineContractEntry baselineEntry in baseline.Baseline.Strict)
        {
            ArchitectureDependencyContract? contract = contracts.FirstOrDefault(candidate =>
            {
                if (workCounter is not null)
                {
                    workCounter.ContractIdComparisons++;
                }

                return string.Equals(candidate.Id, baselineEntry.Id, StringComparison.OrdinalIgnoreCase);
            });

            if (contract is null)
            {
                continue;
            }

            foreach (ArchitectureBaselineIgnoredViolation baselineIgnore in baselineEntry.IgnoredViolations)
            {
                bool isDuplicate = false;
                foreach (ArchitectureIgnoredViolation existing in contract.IgnoredViolations)
                {
                    if (workCounter is not null)
                    {
                        workCounter.IgnoreIdentityComparisons++;
                    }

                    if (isStructured
                        ? IdentityEquals(existing, baselineIgnore)
                        : string.Equals(existing.SourceType, baselineIgnore.SourceType, StringComparison.Ordinal)
                            && string.Equals(existing.ForbiddenReference, baselineIgnore.ForbiddenReference, StringComparison.Ordinal))
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (isDuplicate)
                {
                    continue;
                }

                contract.IgnoredViolations.Add(ToPolicyIgnore(baselineIgnore, isStructured));
                if (workCounter is not null)
                {
                    workCounter.LoadedIgnoresAdded++;
                }
            }
        }
    }

    internal static string StrictIgnoreDigest(ArchitectureContractDocument policy)
    {
        return IgnoreDigest(policy.Contracts.Strict.SelectMany(contract =>
            contract.IgnoredViolations.Select(ignore => Snapshot(contract.Id, ignore))));
    }

    internal static string StrictExternalIgnoreDigest(ArchitectureContractDocument policy)
    {
        return IgnoreDigest(policy.Contracts.StrictExternal.SelectMany(contract =>
            contract.IgnoredViolations.Select(ignore => Snapshot(contract.Id, ignore))));
    }

    internal static void MergeImportedExternal(
        ArchitectureContractDocument policy,
        ArchitectureBaselineDocument baseline)
    {
        // The old merger materialized each group before examining its entries. Imported diagnostics
        // then bypassed contract lookup and never became native contract ignores.
        List<IArchitectureContract> contracts = policy.Contracts.StrictExternal
            .Select(contract => (IArchitectureContract)contract)
            .ToList();
        HashSet<string> externalEvidenceIds = new(
            policy.ExternalEvidence.Select(requirement => requirement.Id),
            StringComparer.Ordinal);
        var unknownIds = new List<(string GroupName, string ContractId)>();

        foreach (ArchitectureBaselineContractEntry entry in baseline.Baseline.StrictExternal)
        {
            bool containsImportedDiagnostic = baseline.Version is 2 or 3
                && entry.IgnoredViolations.Any(ignore =>
                    string.Equals(ignore.Kind, "external_diagnostic", StringComparison.Ordinal));
            bool containsOnlyImportedDiagnostics = containsImportedDiagnostic
                && entry.IgnoredViolations.All(ignore =>
                    string.Equals(ignore.Kind, "external_diagnostic", StringComparison.Ordinal));
            if (containsOnlyImportedDiagnostics)
            {
                if (!externalEvidenceIds.Contains(entry.Id))
                {
                    unknownIds.Add(("strict_external", entry.Id));
                }

                continue;
            }

            IArchitectureContract? contract = contracts.FirstOrDefault(candidate =>
            {
                return string.Equals(candidate.Id, entry.Id, StringComparison.OrdinalIgnoreCase);
            });
            if (contract is null)
            {
                unknownIds.Add(("strict_external", entry.Id));
            }
            else
            {
                throw new InvalidOperationException("The sparse imported-diagnostic fixture contains a native contract entry.");
            }
        }
    }

    private static string IgnoreDigest(IEnumerable<BaselineMergeIgnoreSnapshot> snapshots)
    {
        BaselineMergeIgnoreSnapshot[] orderedSnapshots = snapshots.ToArray();

        byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderedSnapshots));
        return Convert.ToHexStringLower(SHA256.HashData(json));
    }

    private static BaselineMergeIgnoreSnapshot Snapshot(string? contractId, ArchitectureIgnoredViolation ignore) => new(
        contractId,
        ignore.SourceType,
        ignore.ForbiddenReference,
        ignore.Reason,
        ignore.Issue,
        ignore.IdentityVersion,
        ignore.ContractFamily,
        ignore.Kind,
        ignore.SourceAssembly,
        ignore.SourceMember,
        ignore.TargetAssembly,
        ignore.TargetType,
        ignore.TargetMember,
        ignore.Occurrence,
        ignore.Configuration,
        ignore.IsFromLoadedBaseline);

    private static bool IdentityEquals(
        ArchitectureIgnoredViolation existing,
        ArchitectureBaselineIgnoredViolation candidate)
    {
        return existing.IdentityVersion == ArchitectureViolationIdentity.CurrentVersion
            && string.Equals(existing.ContractFamily, candidate.ContractFamily, StringComparison.Ordinal)
            && string.Equals(existing.SourceAssembly, candidate.SourceAssembly, StringComparison.Ordinal)
            && string.Equals(existing.SourceType, candidate.SourceType, StringComparison.Ordinal)
            && string.Equals(existing.SourceMember, candidate.SourceMember, StringComparison.Ordinal)
            && string.Equals(existing.TargetAssembly, candidate.TargetAssembly, StringComparison.Ordinal)
            && string.Equals(existing.TargetType, candidate.TargetType, StringComparison.Ordinal)
            && string.Equals(existing.TargetMember, candidate.TargetMember, StringComparison.Ordinal)
            && (existing.Occurrence ?? 0) == (candidate.Occurrence ?? 0)
            && string.Equals(existing.Configuration, candidate.Configuration, StringComparison.Ordinal);
    }

    private static ArchitectureIgnoredViolation ToPolicyIgnore(
        ArchitectureBaselineIgnoredViolation baselineIgnore,
        bool isStructured)
    {
        return new ArchitectureIgnoredViolation
        {
            SourceType = baselineIgnore.SourceType,
            ForbiddenReference = baselineIgnore.ForbiddenReference,
            Reason = baselineIgnore.Reason,
            IdentityVersion = isStructured
                ? baselineIgnore.IdentityVersion ?? ArchitectureViolationIdentity.CurrentVersion
                : null,
            ContractFamily = isStructured ? baselineIgnore.ContractFamily : null,
            Kind = isStructured ? baselineIgnore.Kind : null,
            SourceAssembly = isStructured ? baselineIgnore.SourceAssembly : null,
            SourceMember = isStructured ? baselineIgnore.SourceMember : null,
            TargetAssembly = isStructured ? baselineIgnore.TargetAssembly : null,
            TargetType = isStructured ? baselineIgnore.TargetType : null,
            TargetMember = isStructured ? baselineIgnore.TargetMember : null,
            Occurrence = isStructured ? baselineIgnore.Occurrence ?? 0 : null,
            Configuration = isStructured ? baselineIgnore.Configuration : null,
            IsFromLoadedBaseline = true,
        };
    }

    internal sealed class LegacyWorkCounter
    {
        internal long ContractIdComparisons { get; set; }

        internal long IgnoreIdentityComparisons { get; set; }

        internal long LoadedIgnoresAdded { get; set; }
    }

    private sealed record BaselineMergeIgnoreSnapshot(
        string? ContractId,
        string SourceType,
        string ForbiddenReference,
        string Reason,
        string? Issue,
        int? IdentityVersion,
        string? ContractFamily,
        string? Kind,
        string? SourceAssembly,
        string? SourceMember,
        string? TargetAssembly,
        string? TargetType,
        string? TargetMember,
        int? Occurrence,
        string? Configuration,
        bool IsFromLoadedBaseline);
}
