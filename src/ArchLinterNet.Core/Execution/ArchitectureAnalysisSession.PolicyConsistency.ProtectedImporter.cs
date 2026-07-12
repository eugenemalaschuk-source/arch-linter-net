using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    private List<PolicyConsistencyDiagnostic> FindProtectedImporterConflicts()
    {
        List<ArchitectureProtectedContract> protectedContracts = Document.Contracts.StrictProtected
            .Concat(Document.Contracts.AuditProtected)
            .ToList();

        List<(string Source, string Target, string Name, string? Id)> forbiddingDependencies =
            BuildForbiddingDependencies();

        List<PolicyConsistencyDiagnostic> conflicts = new();

        conflicts.AddRange(FindProtectedContractsForbiddenByDependencies(protectedContracts, forbiddingDependencies));
        conflicts.AddRange(FindProtectedContractOverlapConflicts(protectedContracts));

        return conflicts
            .OrderBy(f => f.ContractName, StringComparer.Ordinal)
            .ThenBy(f => string.Join(",", f.Layers), StringComparer.Ordinal)
            .ToList();
    }

    private List<(string Source, string Target, string Name, string? Id)> BuildForbiddingDependencies()
    {
        // Strict forbidden/protected rules that forbid an importer for the same surface:
        // a dependency contract forbidding `importer -> protectedSurface`, or another
        // protected contract over the same surface that does NOT list the importer as allowed.
        List<(string Source, string Target, string Name, string? Id)> forbiddingDependencies = new();

        foreach (ArchitectureDependencyContract c in Document.Contracts.Strict.Concat(Document.Contracts.Audit))
        {
            foreach (string target in c.Forbidden)
            {
                forbiddingDependencies.Add((c.Source, target, c.Name, c.Id));
            }
        }

        return forbiddingDependencies;
    }

    private static List<PolicyConsistencyDiagnostic> FindProtectedContractsForbiddenByDependencies(
        List<ArchitectureProtectedContract> protectedContracts,
        List<(string Source, string Target, string Name, string? Id)> forbiddingDependencies)
    {
        List<PolicyConsistencyDiagnostic> conflicts = new();

        foreach (ArchitectureProtectedContract protectedContract in protectedContracts)
        {
            foreach (string protectedSurface in protectedContract.Protected)
            {
                foreach (string allowedImporter in protectedContract.AllowedImporters)
                {
                    conflicts.AddRange(FindForbiddingDependencyConflicts(
                        protectedContract, protectedSurface, allowedImporter, forbiddingDependencies));
                }
            }
        }

        return conflicts;
    }

    private static List<PolicyConsistencyDiagnostic> FindForbiddingDependencyConflicts(
        ArchitectureProtectedContract protectedContract,
        string protectedSurface,
        string allowedImporter,
        List<(string Source, string Target, string Name, string? Id)> forbiddingDependencies)
    {
        List<PolicyConsistencyDiagnostic> conflicts = new();

        foreach (var dep in forbiddingDependencies)
        {
            if (!string.Equals(dep.Source, allowedImporter, StringComparison.Ordinal)
                || !string.Equals(dep.Target, protectedSurface, StringComparison.Ordinal))
            {
                continue;
            }

            conflicts.Add(CreateProtectedImporterForbiddenConflict(
                protectedContract, protectedSurface, allowedImporter, dep));
        }

        return conflicts;
    }

    private static PolicyConsistencyDiagnostic CreateProtectedImporterForbiddenConflict(
        ArchitectureProtectedContract protectedContract,
        string protectedSurface,
        string allowedImporter,
        (string Source, string Target, string Name, string? Id) dep)
    {
        List<string> conflictNames = new() { protectedContract.Name, dep.Name };
        List<string> conflictIds = new();
        if (protectedContract.Id != null) conflictIds.Add(protectedContract.Id);
        if (dep.Id != null) conflictIds.Add(dep.Id);

        return new PolicyConsistencyDiagnostic(
            protectedContract.Name,
            protectedContract.Id,
            "protected-importer-conflict",
            $"Protected contract '{protectedContract.Name}' allows '{allowedImporter}' to import " +
            $"protected surface '{protectedSurface}', but contract '{dep.Name}' forbids that same import.",
            conflictIds,
            conflictNames,
            new[] { protectedSurface, allowedImporter });
    }

    private static List<PolicyConsistencyDiagnostic> FindProtectedContractOverlapConflicts(
        List<ArchitectureProtectedContract> protectedContracts)
    {
        // Protected contracts are exhaustive allow-lists: any importer not listed in
        // AllowedImporters is implicitly forbidden from referencing the protected surface.
        // Two protected contracts guarding the same surface with different AllowedImporters
        // sets are therefore in direct conflict over any importer one allows and the other omits.
        List<PolicyConsistencyDiagnostic> conflicts = new();

        for (int i = 0; i < protectedContracts.Count; i++)
        {
            for (int j = 0; j < protectedContracts.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                ArchitectureProtectedContract allowing = protectedContracts[i];
                ArchitectureProtectedContract forbidding = protectedContracts[j];

                if (allowing.Id != null && string.Equals(allowing.Id, forbidding.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                conflicts.AddRange(FindOverlapConflictsBetween(allowing, forbidding));
            }
        }

        return conflicts;
    }

    private static List<PolicyConsistencyDiagnostic> FindOverlapConflictsBetween(
        ArchitectureProtectedContract allowing,
        ArchitectureProtectedContract forbidding)
    {
        List<PolicyConsistencyDiagnostic> conflicts = new();

        IEnumerable<string> sharedSurfaces = allowing.Protected.Intersect(forbidding.Protected, StringComparer.Ordinal);

        foreach (string protectedSurface in sharedSurfaces)
        {
            foreach (string allowedImporter in allowing.AllowedImporters)
            {
                if (forbidding.AllowedImporters.Contains(allowedImporter, StringComparer.Ordinal)
                    || string.Equals(allowedImporter, protectedSurface, StringComparison.Ordinal))
                {
                    continue;
                }

                conflicts.Add(CreateProtectedContractOverlapConflict(allowing, forbidding, protectedSurface, allowedImporter));
            }
        }

        return conflicts;
    }

    private static PolicyConsistencyDiagnostic CreateProtectedContractOverlapConflict(
        ArchitectureProtectedContract allowing,
        ArchitectureProtectedContract forbidding,
        string protectedSurface,
        string allowedImporter)
    {
        List<string> conflictNames = new() { allowing.Name, forbidding.Name };
        List<string> conflictIds = new();
        if (allowing.Id != null) conflictIds.Add(allowing.Id);
        if (forbidding.Id != null) conflictIds.Add(forbidding.Id);

        return new PolicyConsistencyDiagnostic(
            allowing.Name,
            allowing.Id,
            "protected-importer-conflict",
            $"Protected contract '{allowing.Name}' allows '{allowedImporter}' to import " +
            $"protected surface '{protectedSurface}', but protected contract '{forbidding.Name}' " +
            "guards the same surface without listing that importer as allowed.",
            conflictIds,
            conflictNames,
            new[] { protectedSurface, allowedImporter });
    }
}
