using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureContractRunner(
    ArchitectureAnalysisContext context,
    ArchitectureContractDocument document,
    HashSet<string>? selectedContractIds = null,
    bool enableUnmatchedIgnoreTracking = true,
    IReadOnlyList<string>? preprocessorSymbols = null)
{
    private readonly ArchitectureAnalysisContext _context = context ?? throw new ArgumentNullException(nameof(context));

    private readonly ArchitectureContractDocument _document =
        document ?? throw new ArgumentNullException(nameof(document));

    private readonly HashSet<string>? _selectedContractIds = selectedContractIds;

    private readonly bool _enableUnmatchedIgnoreTracking = enableUnmatchedIgnoreTracking;

    private readonly IReadOnlyList<string>? _preprocessorSymbols = preprocessorSymbols;

    private readonly List<ArchitectureUnmatchedIgnoredViolation> _unmatchedIgnoredViolations = new();

    private readonly List<ArchitectureBaselineCandidate> _baselineCandidates = new();

    public IReadOnlyList<ArchitectureUnmatchedIgnoredViolation> UnmatchedIgnoredViolations
        => _unmatchedIgnoredViolations;

    public IReadOnlyList<ArchitectureBaselineCandidate> BaselineCandidates
        => _baselineCandidates;

    private static void RecordUnmatchedIgnores(
        string contractName,
        string? contractId,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        ArchitectureIgnoreUsageTracker? tracker,
        List<ArchitectureUnmatchedIgnoredViolation> result)
    {
        if (tracker == null)
        {
            return;
        }

        for (int i = 0; i < ignoredViolations.Count; i++)
        {
            if (tracker.IsMatched(i))
            {
                continue;
            }

            var ignore = ignoredViolations[i];
            result.Add(new ArchitectureUnmatchedIgnoredViolation(
                contractName, contractId, i, ignore.SourceType, ignore.ForbiddenReference, ignore.Reason));
        }
    }

    private void AddBaselineCandidate(string contractGroup, string? contractId, string sourceType, string forbiddenReference)
    {
        if (contractId != null)
        {
            _baselineCandidates.Add(new ArchitectureBaselineCandidate(contractGroup, contractId, sourceType, forbiddenReference));
        }
    }

    private string? ResolveContractGroup(IArchitectureContract contract)
    {
        if (_document.Contracts.Strict.Contains(contract)) return "strict";
        if (_document.Contracts.Audit.Contains(contract)) return "audit";
        if (_document.Contracts.StrictLayers.Contains(contract)) return "strict_layers";
        if (_document.Contracts.AuditLayers.Contains(contract)) return "audit_layers";
        if (_document.Contracts.StrictAllowOnly.Contains(contract)) return "strict_allow_only";
        if (_document.Contracts.AuditAllowOnly.Contains(contract)) return "audit_allow_only";
        if (_document.Contracts.StrictCycles.Contains(contract)) return "strict_cycles";
        if (_document.Contracts.AuditCycles.Contains(contract)) return "audit_cycles";
        if (_document.Contracts.StrictAcyclicSiblings.Contains(contract)) return "strict_acyclic_siblings";
        if (_document.Contracts.AuditAcyclicSiblings.Contains(contract)) return "audit_acyclic_siblings";
        if (_document.Contracts.StrictMethodBody.Contains(contract)) return "strict_method_body";
        if (_document.Contracts.AuditMethodBody.Contains(contract)) return "audit_method_body";
        if (_document.Contracts.StrictIndependence.Contains(contract)) return "strict_independence";
        if (_document.Contracts.AuditIndependence.Contains(contract)) return "audit_independence";
        if (_document.Contracts.StrictProtected.Contains(contract)) return "strict_protected";
        if (_document.Contracts.AuditProtected.Contains(contract)) return "audit_protected";
        if (_document.Contracts.StrictExternal.Contains(contract)) return "strict_external";
        if (_document.Contracts.AuditExternal.Contains(contract)) return "audit_external";

        if (contract.Id == null) return null;

        if (AnyWithId(_document.Contracts.Strict, contract.Id)) return "strict";
        if (AnyWithId(_document.Contracts.Audit, contract.Id)) return "audit";
        if (AnyWithId(_document.Contracts.StrictLayers, contract.Id)) return "strict_layers";
        if (AnyWithId(_document.Contracts.AuditLayers, contract.Id)) return "audit_layers";
        if (AnyWithId(_document.Contracts.StrictAllowOnly, contract.Id)) return "strict_allow_only";
        if (AnyWithId(_document.Contracts.AuditAllowOnly, contract.Id)) return "audit_allow_only";
        if (AnyWithId(_document.Contracts.StrictCycles, contract.Id)) return "strict_cycles";
        if (AnyWithId(_document.Contracts.AuditCycles, contract.Id)) return "audit_cycles";
        if (AnyWithId(_document.Contracts.StrictAcyclicSiblings, contract.Id)) return "strict_acyclic_siblings";
        if (AnyWithId(_document.Contracts.AuditAcyclicSiblings, contract.Id)) return "audit_acyclic_siblings";
        if (AnyWithId(_document.Contracts.StrictMethodBody, contract.Id)) return "strict_method_body";
        if (AnyWithId(_document.Contracts.AuditMethodBody, contract.Id)) return "audit_method_body";
        if (AnyWithId(_document.Contracts.StrictIndependence, contract.Id)) return "strict_independence";
        if (AnyWithId(_document.Contracts.AuditIndependence, contract.Id)) return "audit_independence";
        if (AnyWithId(_document.Contracts.StrictProtected, contract.Id)) return "strict_protected";
        if (AnyWithId(_document.Contracts.AuditProtected, contract.Id)) return "audit_protected";
        if (AnyWithId(_document.Contracts.StrictExternal, contract.Id)) return "strict_external";
        if (AnyWithId(_document.Contracts.AuditExternal, contract.Id)) return "audit_external";
        return null;
    }

    private static bool AnyWithId<T>(List<T> contracts, string id) where T : IArchitectureContract
    {
        return contracts.Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsContractSelected(string? contractId)
    {
        return _selectedContractIds == null || _selectedContractIds.Count == 0
            || (contractId != null && _selectedContractIds.Contains(contractId));
    }

    public IEnumerable<ArchitectureDependencyContract> StrictContracts()
    {
        return _document.Contracts.Strict;
    }

    public IEnumerable<ArchitectureDependencyContract> AuditContracts()
    {
        return _document.Contracts.Audit;
    }

    public IEnumerable<ArchitectureLayerContract> StrictLayerContracts()
    {
        return _document.Contracts.StrictLayers;
    }

    public IEnumerable<ArchitectureLayerContract> AuditLayerContracts()
    {
        return _document.Contracts.AuditLayers;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> StrictAllowOnlyContracts()
    {
        return _document.Contracts.StrictAllowOnly;
    }

    public IEnumerable<ArchitectureAllowOnlyContract> AuditAllowOnlyContracts()
    {
        return _document.Contracts.AuditAllowOnly;
    }

    public IEnumerable<ArchitectureCycleContract> StrictCycleContracts()
    {
        return _document.Contracts.StrictCycles;
    }

    public IEnumerable<ArchitectureCycleContract> AuditCycleContracts()
    {
        return _document.Contracts.AuditCycles;
    }

    public IEnumerable<ArchitectureMethodBodyContract> StrictMethodBodyContracts()
    {
        return _document.Contracts.StrictMethodBody;
    }

    public IEnumerable<ArchitectureMethodBodyContract> AuditMethodBodyContracts()
    {
        return _document.Contracts.AuditMethodBody;
    }

    public IEnumerable<ArchitectureAsmdefContract> StrictAsmdefContracts()
    {
        return _document.Contracts.StrictAsmdef;
    }

    public IEnumerable<ArchitectureAsmdefContract> AuditAsmdefContracts()
    {
        return _document.Contracts.AuditAsmdef;
    }

    public IEnumerable<ArchitectureIndependenceContract> StrictIndependenceContracts()
    {
        return _document.Contracts.StrictIndependence;
    }

    public IEnumerable<ArchitectureIndependenceContract> AuditIndependenceContracts()
    {
        return _document.Contracts.AuditIndependence;
    }

    public IEnumerable<ArchitectureProtectedContract> StrictProtectedContracts()
    {
        return _document.Contracts.StrictProtected;
    }

    public IEnumerable<ArchitectureProtectedContract> AuditProtectedContracts()
    {
        return _document.Contracts.AuditProtected;
    }

    public IEnumerable<ArchitectureExternalDependencyContract> StrictExternalContracts()
    {
        return _document.Contracts.StrictExternal;
    }

    public IEnumerable<ArchitectureExternalDependencyContract> AuditExternalContracts()
    {
        return _document.Contracts.AuditExternal;
    }

    public IEnumerable<ArchitectureAcyclicSiblingContract> StrictAcyclicSiblingContracts()
    {
        return _document.Contracts.StrictAcyclicSiblings;
    }

    public IEnumerable<ArchitectureAcyclicSiblingContract> AuditAcyclicSiblingContracts()
    {
        return _document.Contracts.AuditAcyclicSiblings;
    }

    public List<ArchitectureViolation> CheckConfiguration()
    {
        return CheckConfiguration(strict: true);
    }

    public List<ArchitectureViolation> CheckConfiguration(bool strict)
    {
        List<ArchitectureViolation> violations = new();

        foreach (string missingAssembly in _context.MissingAssemblyNames)
        {
            string probeInfo = _context.AssemblyProbingPaths.Count > 0
                ? $" Probing paths: {string.Join("; ", _context.AssemblyProbingPaths)}"
                : string.Empty;

            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                missingAssembly,
                "missing target assembly",
                new[] { $"Assembly '{missingAssembly}' is declared in analysis.target_assemblies but could not be resolved.{probeInfo}" }));
        }

        HashSet<string> referencedLayers = new(StringComparer.Ordinal);
        HashSet<string> referencedExternalGroups = new(StringComparer.Ordinal);

        void AddLayerNames(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                referencedLayers.Add(name);
            }
        }

        void AddExternalGroupNames(IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                referencedExternalGroups.Add(name);
            }
        }

        if (strict)
        {
            foreach (ArchitectureDependencyContract c in _document.Contracts.Strict)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in _document.Contracts.StrictAllowOnly)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Allowed);
            }

            foreach (ArchitectureCycleContract c in _document.Contracts.StrictCycles)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in _document.Contracts.StrictMethodBody)
            {
                AddLayerNames(new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in _document.Contracts.StrictIndependence)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureLayerContract c in _document.Contracts.StrictLayers)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureProtectedContract c in _document.Contracts.StrictProtected)
            {
                AddLayerNames(c.Protected);
                AddLayerNames(c.AllowedImporters);
            }

            foreach (ArchitectureExternalDependencyContract c in _document.Contracts.StrictExternal)
            {
                AddLayerNames(new[] { c.Source });
                AddExternalGroupNames(c.Forbidden);
            }
        }
        else
        {
            foreach (ArchitectureDependencyContract c in _document.Contracts.Audit)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Forbidden);
            }

            foreach (ArchitectureAllowOnlyContract c in _document.Contracts.AuditAllowOnly)
            {
                AddLayerNames(new[] { c.Source });
                AddLayerNames(c.Allowed);
            }

            foreach (ArchitectureCycleContract c in _document.Contracts.AuditCycles)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureMethodBodyContract c in _document.Contracts.AuditMethodBody)
            {
                AddLayerNames(new[] { c.Source });
            }

            foreach (ArchitectureIndependenceContract c in _document.Contracts.AuditIndependence)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureLayerContract c in _document.Contracts.AuditLayers)
            {
                AddLayerNames(c.Layers);
            }

            foreach (ArchitectureProtectedContract c in _document.Contracts.AuditProtected)
            {
                AddLayerNames(c.Protected);
                AddLayerNames(c.AllowedImporters);
            }

            foreach (ArchitectureExternalDependencyContract c in _document.Contracts.AuditExternal)
            {
                AddLayerNames(new[] { c.Source });
                AddExternalGroupNames(c.Forbidden);
            }
        }

        foreach (string layerName in referencedLayers)
        {
            ArchitectureLayer layer = ArchitectureLayerResolver.ResolveLayer(_document, "<configuration>", layerName);

            if (layer.External)
            {
                continue;
            }

            Type[] types = ArchitectureTypeScanner.FindTypesInLayer(_context.TargetAssemblies, layer);

            if (types.Length == 0)
            {
                violations.Add(new ArchitectureViolation(
                    "<configuration>",
                    null,
                    ArchitectureLayerResolver.DescribeLayer(layer),
                    "empty layer namespace",
                    new[] { $"Layer '{layerName}' namespace '{layer.Namespace}' contains no types in loaded assemblies." }));
            }
        }

        foreach (string groupName in referencedExternalGroups)
        {
            if (!_document.ExternalDependencies.TryGetValue(groupName, out ArchitectureExternalDependencyGroup? group))
            {
                violations.Add(new ArchitectureViolation(
                    "<configuration>",
                    null,
                    groupName,
                    "unknown external dependency group",
                    new[]
                    {
                        $"External dependency group '{groupName}' is referenced by a contract but is not declared in external_dependencies."
                    })
                {
                    ForbiddenExternalGroup = groupName
                });

                continue;
            }

            if (ArchitectureExternalDependencyResolver.HasUsableMatchers(group))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                "<configuration>",
                null,
                groupName,
                "invalid external dependency group",
                new[]
                {
                    $"External dependency group '{groupName}' must declare at least one non-empty namespace_prefixes or type_prefixes matcher."
                })
            {
                ForbiddenExternalGroup = groupName
            });
        }

        return violations;
    }

}
