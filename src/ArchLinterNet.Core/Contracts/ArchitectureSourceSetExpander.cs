using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Contracts;

// Expands `source_sets` declarations into concrete contract instances, exactly once, during
// ArchitecturePolicyDocumentLoader.Load — after provenance binding so authored locations exist,
// and before the validator pipeline so every existing validator, the contract catalog, the
// executor, baselines, coverage and the reporters keep seeing ordinary single-source contracts.
// See openspec/changes/add-source-set-expansion/design.md.
internal static class ArchitectureSourceSetExpander
{
    // A bound, not a tuning knob: an authored contract that resolves past this many sources is far
    // more likely to be an over-broad glob than a reviewed policy, and an unbounded expansion would
    // silently multiply every downstream check.
    internal const int MaxInstancesPerContract = 500;

    public static void Expand(ArchitectureContractDocument document)
    {
        SourceSetResolver resolver = new(document);
        List<ArchitectureContractExpansion> expansions = new();

        Families.ArchitectureContractGroups groups = document.Contracts;

        ExpandGroup(document, resolver, expansions, "strict_package_dependency",
            groups.StrictPackageDependency, list => groups.StrictPackageDependency = list);
        ExpandGroup(document, resolver, expansions, "audit_package_dependency",
            groups.AuditPackageDependency, list => groups.AuditPackageDependency = list);
        ExpandGroup(document, resolver, expansions, "strict_package_allow_only",
            groups.StrictPackageAllowOnly, list => groups.StrictPackageAllowOnly = list);
        ExpandGroup(document, resolver, expansions, "audit_package_allow_only",
            groups.AuditPackageAllowOnly, list => groups.AuditPackageAllowOnly = list);
        ExpandGroup(document, resolver, expansions, "strict_framework_dependency",
            groups.StrictFrameworkDependency, list => groups.StrictFrameworkDependency = list);
        ExpandGroup(document, resolver, expansions, "audit_framework_dependency",
            groups.AuditFrameworkDependency, list => groups.AuditFrameworkDependency = list);
        ExpandGroup(document, resolver, expansions, "strict_framework_allow_only",
            groups.StrictFrameworkAllowOnly, list => groups.StrictFrameworkAllowOnly = list);
        ExpandGroup(document, resolver, expansions, "audit_framework_allow_only",
            groups.AuditFrameworkAllowOnly, list => groups.AuditFrameworkAllowOnly = list);
        ExpandGroup(document, resolver, expansions, "strict_external",
            groups.StrictExternal, list => groups.StrictExternal = list);
        ExpandGroup(document, resolver, expansions, "audit_external",
            groups.AuditExternal, list => groups.AuditExternal = list);
        ExpandGroup(document, resolver, expansions, "strict_external_allow_only",
            groups.StrictExternalAllowOnly, list => groups.StrictExternalAllowOnly = list);
        ExpandGroup(document, resolver, expansions, "audit_external_allow_only",
            groups.AuditExternalAllowOnly, list => groups.AuditExternalAllowOnly = list);

        foreach (ArchitectureProjectMetadataContract contract in groups.StrictProjectMetadata
                     .Concat(groups.AuditProjectMetadata))
        {
            contract.Projects = InlineSets(
                resolver,
                contract.Name,
                "project_sets",
                ArchitectureSourceSetKind.Project,
                contract.Projects,
                contract.ProjectSets);
        }

        foreach (ArchitectureCompositionContract contract in groups.StrictComposition
                     .Concat(groups.AuditComposition))
        {
            contract.AllowedOnlyInAssemblies = InlineSets(
                resolver,
                contract.Name,
                "allowed_only_in_assembly_sets",
                ArchitectureSourceSetKind.Assembly,
                contract.AllowedOnlyInAssemblies,
                contract.AllowedOnlyInAssemblySets);
        }

        document.SourceExpansion = new ArchitectureSourceExpansionInventory(
            resolver.Resolutions,
            expansions);
    }

    private static void ExpandGroup<TContract>(
        ArchitectureContractDocument document,
        SourceSetResolver resolver,
        List<ArchitectureContractExpansion> expansions,
        string group,
        List<TContract> contracts,
        Action<List<TContract>> assign)
        where TContract : class, IArchitectureSourceExpandableContract
    {
        if (contracts.All(contract => contract.Sources.Count == 0 && contract.SourceSets.Count == 0))
        {
            return;
        }

        List<TContract> expanded = new();

        foreach (TContract contract in contracts)
        {
            if (contract.Sources.Count == 0 && contract.SourceSets.Count == 0)
            {
                expanded.Add(contract);
                continue;
            }

            expanded.AddRange(ExpandContract(document, resolver, expansions, group, contract));
        }

        assign(expanded);
    }

    private static IEnumerable<TContract> ExpandContract<TContract>(
        ArchitectureContractDocument document,
        SourceSetResolver resolver,
        List<ArchitectureContractExpansion> expansions,
        string group,
        TContract contract)
        where TContract : class, IArchitectureSourceExpandableContract
    {
        if (!string.IsNullOrWhiteSpace(contract.Source))
        {
            throw new InvalidOperationException(
                $"Contract '{contract.Name}' in '{group}' declares both 'source' and " +
                "'sources'/'source_sets'. Declare exactly one source selector: an exact 'source', " +
                "or the multi-source 'sources'/'source_sets' form.");
        }

        string authoredId = contract.Id ?? ArchitecturePolicyDocumentLoader.NormalizeToContractId(contract.Name);

        // Selector per resolved source, first writer wins, so overlapping sets and repeated members
        // collapse to exactly one instance and the reported selector is deterministic.
        Dictionary<string, (string? SetName, string Selector)> selectors = new(StringComparer.Ordinal);
        List<string> optionalReasons = new();

        foreach (string source in contract.Sources)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            resolver.ValidateExplicitSource(contract.Name, group, contract.SourceKind, source);
            selectors.TryAdd(source, (null, source));
        }

        foreach (string setName in contract.SourceSets)
        {
            ArchitectureSourceSetResolution resolution =
                resolver.Resolve(contract.Name, group, contract.SourceKind, setName);

            if (resolution.ResolvedSources.Count == 0)
            {
                optionalReasons.Add(resolution.Reason);
                continue;
            }

            foreach (string source in resolution.ResolvedSources)
            {
                selectors.TryAdd(source, (resolution.Name, resolver.SelectorFor(resolution.Name, source)));
            }
        }

        if (selectors.Count == 0 && optionalReasons.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.Name}' in '{group}' resolved no sources from its " +
                "'sources'/'source_sets' declaration. Declare at least one usable source, or mark " +
                "the referenced set 'optional: true' with a reason if the absence is intentional.");
        }

        if (selectors.Count > MaxInstancesPerContract)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.Name}' in '{group}' expands to {selectors.Count} sources, " +
                $"which exceeds the supported limit of {MaxInstancesPerContract}. Narrow the " +
                "declared globs or split the contract.");
        }

        List<ArchitectureExpandedContractInstance> instances = new();
        List<TContract> expandedContracts = new();

        foreach (string source in selectors.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            (string? setName, string selector) = selectors[source];
            string instanceId = $"{authoredId}/{ArchitecturePolicyDocumentLoader.NormalizeToContractId(source)}";

            var instance = (TContract)contract.CloneForSource(source);
            instance.Id = instanceId;
            instance.ExpansionOrigin = new ArchitectureSourceExpansionOrigin(
                authoredId, contract.Name, source, setName, selector);

            document.Provenance.BindExpandedContract(contract, instance, group);
            instances.Add(new ArchitectureExpandedContractInstance(instanceId, source, setName, selector));
            expandedContracts.Add(instance);
        }

        expansions.Add(new ArchitectureContractExpansion(
            group,
            authoredId,
            contract.Name,
            contract.SourceSets.ToArray(),
            instances)
        {
            OptionalEmpty = instances.Count == 0,
            OptionalReason = string.Join("; ", optionalReasons.Where(reason => !string.IsNullOrWhiteSpace(reason))),
            PolicyLocation = document.Provenance.LocationFor(contract)
        });

        return expandedContracts;
    }

    private static List<string> InlineSets(
        SourceSetResolver resolver,
        string contractName,
        string field,
        ArchitectureSourceSetKind kind,
        List<string> declared,
        List<string> setNames)
    {
        if (setNames.Count == 0)
        {
            return declared;
        }

        List<string> resolved = new(declared);

        foreach (string setName in setNames)
        {
            resolved.AddRange(resolver.Resolve(contractName, field, kind, setName).ResolvedSources);
        }

        return resolved
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class SourceSetResolver
    {
        private readonly ArchitectureContractDocument _document;
        private readonly Dictionary<string, ArchitectureSourceSetResolution> _resolutions =
            new(StringComparer.Ordinal);
        private readonly Dictionary<(string Set, string Source), string> _selectors = new();
        private readonly List<ArchitectureSourceSetResolution> _ordered = new();

        public SourceSetResolver(ArchitectureContractDocument document)
        {
            _document = document;

            // Every declared set is resolved eagerly, so an unusable or empty declaration is a
            // policy error whether or not a contract happens to reference it.
            foreach ((string name, ArchitectureSourceSet set) in document.SourceSets)
            {
                ArchitectureSourceSetResolution resolution = ResolveDeclaration(name, set);
                _resolutions[name] = resolution;
                _ordered.Add(resolution);
            }
        }

        public IReadOnlyList<ArchitectureSourceSetResolution> Resolutions => _ordered;

        public string SelectorFor(string setName, string source) =>
            _selectors.TryGetValue((setName, source), out string? selector) ? selector : source;

        public ArchitectureSourceSetResolution Resolve(
            string contractName,
            string field,
            ArchitectureSourceSetKind kind,
            string setName)
        {
            if (!_resolutions.TryGetValue(setName, out ArchitectureSourceSetResolution? resolution))
            {
                throw new InvalidOperationException(
                    $"Contract '{contractName}' references unknown source set '{setName}' in " +
                    $"'{field}'. Declare it under the document-level 'source_sets' map.");
            }

            if (resolution.Kind != kind)
            {
                throw new InvalidOperationException(
                    $"Contract '{contractName}' references source set '{setName}' of kind " +
                    $"'{Describe(resolution.Kind)}' in '{field}', which selects sources of kind " +
                    $"'{Describe(kind)}'. Declare a set whose 'kind' matches the referencing field.");
            }

            return resolution;
        }

        public void ValidateExplicitSource(
            string contractName,
            string group,
            ArchitectureSourceSetKind kind,
            string source)
        {
            if (IsInUniverse(kind, source))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Contract '{contractName}' in '{group}' lists source '{source}' in 'sources' that is " +
                $"not declared in {UniverseName(kind)}. Every expanded source must be a declared " +
                "policy input.");
        }

        private ArchitectureSourceSetResolution ResolveDeclaration(string name, ArchitectureSourceSet set)
        {
            if (set.Members.Count == 0 && set.Globs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Source set '{name}' declares neither 'members' nor 'globs'. A set with nothing " +
                    "to resolve cannot expand any contract.");
            }

            if (set.Optional && string.IsNullOrWhiteSpace(set.Reason))
            {
                throw new InvalidOperationException(
                    $"Source set '{name}' declares 'optional: true' without a 'reason'. An " +
                    "intentionally empty set must record why it is empty.");
            }

            if (set.Kind == ArchitectureSourceSetKind.Project && set.Globs.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Source set '{name}' declares 'globs' with 'kind: project'. Project sets accept " +
                    "explicit 'members' only, because project identities are paths rather than " +
                    "dotted names.");
            }

            SortedSet<string> resolved = new(StringComparer.Ordinal);

            foreach (string member in set.Members.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (!IsInUniverse(set.Kind, member))
                {
                    throw new InvalidOperationException(
                        $"Source set '{name}' lists member '{member}' that is not declared in " +
                        $"{UniverseName(set.Kind)}. A set must not introduce sources the policy does " +
                        "not analyze.");
                }

                resolved.Add(member);
                _selectors.TryAdd((name, member), member);
            }

            IReadOnlyList<string> universe = Universe(set.Kind);

            foreach (string glob in set.Globs.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (universe.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Source set '{name}' declares glob '{glob}' but {UniverseName(set.Kind)} is " +
                        "empty. Declare the inputs the glob may resolve against.");
                }

                NamespaceGlobPattern pattern = NamespaceGlobPattern.Parse(glob);
                bool matchedAny = false;

                foreach (string candidate in universe)
                {
                    if (!pattern.Match(candidate).Matched)
                    {
                        continue;
                    }

                    matchedAny = true;
                    resolved.Add(candidate);
                    _selectors.TryAdd((name, candidate), glob);
                }

                if (!matchedAny && !set.Optional)
                {
                    throw new InvalidOperationException(
                        $"Source set '{name}' declares glob '{glob}' that matches nothing in " +
                        $"{UniverseName(set.Kind)}. Fix the glob, or declare the set " +
                        "'optional: true' with a reason if the absence is intentional.");
                }
            }

            if (resolved.Count == 0 && !set.Optional)
            {
                throw new InvalidOperationException(
                    $"Source set '{name}' resolved to no source. Declare at least one usable member " +
                    "or glob, or declare the set 'optional: true' with a reason.");
            }

            return new ArchitectureSourceSetResolution(
                name,
                set.Kind,
                resolved.ToArray(),
                set.Optional,
                set.Reason)
            {
                PolicyLocation = _document.Provenance.LocationForSourceSet(name)
            };
        }

        private bool IsInUniverse(ArchitectureSourceSetKind kind, string value)
        {
            IReadOnlyList<string> universe = Universe(kind);

            // A policy that declares no targets or no projects is a small policy that names its
            // sources directly; there is nothing to check the member against in that case.
            return universe.Count == 0 || universe.Contains(value, StringComparer.Ordinal);
        }

        private IReadOnlyList<string> Universe(ArchitectureSourceSetKind kind) => kind switch
        {
            ArchitectureSourceSetKind.Assembly => _document.Analysis.TargetAssemblies,
            ArchitectureSourceSetKind.Layer => _document.Layers.Keys.ToArray(),
            _ => _document.Analysis.Projects
        };

        private static string UniverseName(ArchitectureSourceSetKind kind) => kind switch
        {
            ArchitectureSourceSetKind.Assembly => "'analysis.target_assemblies'",
            ArchitectureSourceSetKind.Layer => "'layers'",
            _ => "'analysis.projects'"
        };

        private static string Describe(ArchitectureSourceSetKind kind) => kind switch
        {
            ArchitectureSourceSetKind.Assembly => "assembly",
            ArchitectureSourceSetKind.Layer => "layer",
            _ => "project"
        };
    }
}
