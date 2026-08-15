using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.Discovery;
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

    private const string ProjectSetsFieldName = "project_sets";

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
        ExpandGroup(document, resolver, expansions, "strict_assembly_dependency",
            groups.StrictAssemblyDependency, list => groups.StrictAssemblyDependency = list);
        ExpandGroup(document, resolver, expansions, "audit_assembly_dependency",
            groups.AuditAssemblyDependency, list => groups.AuditAssemblyDependency = list);
        ExpandGroup(document, resolver, expansions, "strict_assembly_allow_only",
            groups.StrictAssemblyAllowOnly, list => groups.StrictAssemblyAllowOnly = list);
        ExpandGroup(document, resolver, expansions, "audit_assembly_allow_only",
            groups.AuditAssemblyAllowOnly, list => groups.AuditAssemblyAllowOnly = list);

        ExpansionContext inlineContext = new(document, resolver, expansions);
        if (!HasDeferredProjectUniverse(document))
        {
            ExpandInlineGroup(inlineContext, "strict_project_metadata",
                groups.StrictProjectMetadata, contract => contract.Projects, (contract, values) => contract.Projects = values,
                contract => contract.ProjectSets, new SourceSetField(ProjectSetsFieldName, ArchitectureSourceSetKind.Project));
            ExpandInlineGroup(inlineContext, "audit_project_metadata",
                groups.AuditProjectMetadata, contract => contract.Projects, (contract, values) => contract.Projects = values,
                contract => contract.ProjectSets, new SourceSetField(ProjectSetsFieldName, ArchitectureSourceSetKind.Project));
            document.ProjectSourceSetsExpanded = true;
        }
        ExpandInlineGroup(inlineContext, "strict_composition",
            groups.StrictComposition, contract => contract.AllowedOnlyInAssemblies,
            (contract, values) => contract.AllowedOnlyInAssemblies = values,
            contract => contract.AllowedOnlyInAssemblySets,
            new SourceSetField("allowed_only_in_assembly_sets", ArchitectureSourceSetKind.Assembly));
        ExpandInlineGroup(inlineContext, "audit_composition",
            groups.AuditComposition, contract => contract.AllowedOnlyInAssemblies,
            (contract, values) => contract.AllowedOnlyInAssemblies = values,
            contract => contract.AllowedOnlyInAssemblySets,
            new SourceSetField("allowed_only_in_assembly_sets", ArchitectureSourceSetKind.Assembly));

        ArchitectureLayerTemplateContainerExpansionRecorder.Record(
            document, expansions, "strict_layer_templates", groups.StrictLayerTemplates);
        ArchitectureLayerTemplateContainerExpansionRecorder.Record(
            document, expansions, "audit_layer_templates", groups.AuditLayerTemplates);

        document.Provenance.ResetValidationSubject();

        document.SourceExpansion = new ArchitectureSourceExpansionInventory(
            resolver.Resolutions,
            expansions);
    }

    // Solution-derived project paths are unavailable to the policy loader. Bind only project-kind
    // sets once discovery has applied include/exclude filtering, preserving the eager expansion of
    // every other kind and its inventory evidence.
    internal static void BindProjectSets(ArchitectureContractDocument document, ProjectDiscoveryResult discovery)
    {
        if (!HasDeferredProjectUniverse(document) || document.ProjectSourceSetsExpanded)
        {
            return;
        }

        try
        {
            string[] universe = discovery.DiscoveredProjects
                .Select(project => project.Path)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            SourceSetResolver resolver = new(document, universe);
            List<ArchitectureContractExpansion> expansions = new();
            ExpansionContext context = new(document, resolver, expansions);
            Families.ArchitectureContractGroups groups = document.Contracts;

            ExpandInlineGroup(context, "strict_project_metadata",
                groups.StrictProjectMetadata, contract => contract.Projects, (contract, values) => contract.Projects = values,
                contract => contract.ProjectSets, new SourceSetField(ProjectSetsFieldName, ArchitectureSourceSetKind.Project));
            ExpandInlineGroup(context, "audit_project_metadata",
                groups.AuditProjectMetadata, contract => contract.Projects, (contract, values) => contract.Projects = values,
                contract => contract.ProjectSets, new SourceSetField(ProjectSetsFieldName, ArchitectureSourceSetKind.Project));

            document.SourceExpansion = new ArchitectureSourceExpansionInventory(
                document.SourceExpansion.Sets.Concat(resolver.Resolutions.Where(set =>
                    set.Kind == ArchitectureSourceSetKind.Project)).ToArray(),
                document.SourceExpansion.Contracts.Concat(expansions).ToArray());
            new ProjectMetadataValidator().Validate(document);
            document.ProjectSourceSetsExpanded = true;
        }
        catch (InvalidOperationException exception)
        {
            Exception enriched = document.Provenance.EnrichValidationException(exception);
            throw ReferenceEquals(enriched, exception) ? exception : enriched;
        }
        finally
        {
            document.Provenance.ResetValidationSubject();
        }
    }

    private static bool HasDeferredProjectUniverse(ArchitectureContractDocument document) =>
        !string.IsNullOrWhiteSpace(document.Analysis.Solution);

    private static void ExpandGroup<TContract>(
        ArchitectureContractDocument document,
        SourceSetResolver resolver,
        List<ArchitectureContractExpansion> expansions,
        string group,
        List<TContract> contracts,
        Action<List<TContract>> assign)
        where TContract : class, IArchitectureSourceExpandableContract
    {
        if (contracts.All(contract => contract.Sources.Count == 0 && contract.SourceSets.Count == 0
            && contract.ExcludedSources.Count == 0 && contract.ExcludedSourceSets.Count == 0))
        {
            return;
        }

        RequireUniqueAuthoredIds(document, contracts);

        List<TContract> expanded = new();

        foreach (TContract contract in contracts)
        {
            if (contract.Sources.Count == 0 && contract.SourceSets.Count == 0
                && contract.ExcludedSources.Count == 0 && contract.ExcludedSourceSets.Count == 0)
            {
                expanded.Add(contract);
                continue;
            }

            expanded.AddRange(ExpandContract(document, resolver, expansions, group, contract));
        }

        assign(expanded);
    }

    // Expansion derives per-instance ids ("<authored-id>/<source>") before DuplicateIdValidator
    // runs, so two contracts sharing one authored id would otherwise become distinct instance ids
    // and stop being reported as duplicates — silently splitting one reviewed identity in two and
    // making authored-id selection and rule-input coverage resolve only one of them. The authored
    // ids are still intact here, so the same rule DuplicateIdValidator enforces is applied first.
    private static void RequireUniqueAuthoredIds<TContract>(
        ArchitectureContractDocument document,
        List<TContract> contracts)
        where TContract : class, IArchitectureContract
    {
        string[] duplicates = contracts
            .Select(contract => contract.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .GroupBy(id => id!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length == 0)
        {
            return;
        }

        document.Provenance.SetValidationSubject(
            contracts.First(contract =>
                string.Equals(contract.Id, duplicates[0], StringComparison.OrdinalIgnoreCase)));

        throw new InvalidOperationException(
            $"Duplicate contract IDs found: {string.Join(", ", duplicates)}. Each contract ID must be " +
            "unique within its contract type and mode group.");
    }

    private static void ValidateNotBothExactAndMultiSource<TContract>(TContract contract, string group)
        where TContract : IArchitectureSourceExpandableContract
    {
        if (string.IsNullOrWhiteSpace(contract.Source))
        {
            return;
        }

        bool declaresMultiSource = contract.Sources.Count > 0 || contract.SourceSets.Count > 0;
        throw new InvalidOperationException(declaresMultiSource
            ? $"Contract '{contract.Name}' in '{group}' declares both 'source' and " +
              "'sources'/'source_sets'. Declare exactly one source selector: an exact 'source', " +
              "or the multi-source 'sources'/'source_sets' form."
            : $"Contract '{contract.Name}' in '{group}' declares an exact 'source' together with " +
              "'exclude_sources'/'exclude_source_sets'. Subtraction only applies to the multi-source " +
              "'sources'/'source_sets' form; remove the exclusion, or switch 'source' to 'sources'.");
    }

    private static List<TContract> ExpandContract<TContract>(
        ArchitectureContractDocument document,
        SourceSetResolver resolver,
        List<ArchitectureContractExpansion> expansions,
        string group,
        TContract contract)
        where TContract : class, IArchitectureSourceExpandableContract
    {
        ValidateNotBothExactAndMultiSource(contract, group);

        // Every throw below is enriched by ArchitecturePolicyDocumentLoader with the current
        // validation subject's location, so point it at the authored contract before resolving.
        document.Provenance.SetValidationSubject(contract);

        string authoredId = contract.Id ?? ArchitecturePolicyDocumentLoader.NormalizeToContractId(contract.Name);
        ArchitecturePolicySourceLocation? contractLocation = document.Provenance.LocationFor(contract);

        ArchitectureSourceSetInclusionResolver.SourceSelectionState state =
            ArchitectureSourceSetInclusionResolver.ResolveIncludedSources(
                document, resolver, group, contract, authoredId, contractLocation);
        int includedCountBeforeExclusions = state.Selectors.Count;

        // A stable snapshot of what was actually included, taken before any exclusion mutates
        // `state.Selectors`.
        Dictionary<string, (string? SetName, string Selector)> includedSelectors =
            new(state.Selectors, StringComparer.Ordinal);
        HashSet<string> includedSnapshot = new(includedSelectors.Keys, StringComparer.Ordinal);

        List<ArchitectureExpandedContractExclusion> exclusions =
            ArchitectureSourceSetExclusionApplicator.Apply(
                document, resolver, group, contract, contractLocation, state, includedSnapshot);

        ArchitectureSourceSetExclusionApplicator.ValidateSelectorCounts(
            contract, group, state, includedCountBeforeExclusions);

        List<ArchitectureExpandedContractInstance> instances = new();
        List<TContract> expandedContracts = new();

        foreach (string source in state.Selectors.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            (string? setName, string selector) = state.Selectors[source];
            string instanceId = $"{authoredId}/{ArchitecturePolicyDocumentLoader.NormalizeToContractId(source)}";

            var instance = (TContract)contract.CloneForSource(source);
            instance.Id = instanceId;
            instance.ExpansionOrigin = new ArchitectureSourceExpansionOrigin(
                authoredId, contract.Name, source, setName, selector);

            document.Provenance.BindExpandedContract(contract, instance, group);
            instances.Add(CreateExpandedInstance(
                instanceId, source, setName, selector,
                state.InstanceLocations.GetValueOrDefault(source), contractLocation,
                state.SourceSetReferenceLocations.GetValueOrDefault(source)));
            expandedContracts.Add(instance);
        }

        expansions.Add(new ArchitectureContractExpansion(
            group,
            authoredId,
            contract.Name,
            contract.SourceSets.ToArray(),
            instances)
        {
            OptionalEmpty = includedCountBeforeExclusions == 0 && state.OptionalReasons.Count > 0,
            OptionalReason = string.Join("; ", state.OptionalReasons.Where(reason => !string.IsNullOrWhiteSpace(reason))),
            PolicyLocation = contractLocation,
            Exclusions = exclusions,
            Inclusions = state.Inclusions
        });

        return expandedContracts;
    }

    // Bundles the three values every expansion step threads through unchanged, so a step's own
    // signature only has to name what makes it different from its neighbors (kept the S107
    // parameter-count gate under its authored-contract, not just resolved-source, threshold).
    private sealed record ExpansionContext(
        ArchitectureContractDocument Document,
        SourceSetResolver Resolver,
        List<ArchitectureContractExpansion> Expansions);

    private readonly record struct SourceSetField(string Name, ArchitectureSourceSetKind Kind);

    private static List<string> InlineSets(
        ExpansionContext context,
        string group,
        IArchitectureContract contract,
        string contractName,
        SourceSetField field,
        List<string> declared,
        List<string> setNames)
    {
        if (setNames.Count == 0)
        {
            return declared;
        }

        List<string> resolved = new(declared);
        Dictionary<string, (string SetName, string Selector, int ReferenceIndex)> setValues = new(StringComparer.Ordinal);
        List<string> optionalReasons = new();
        ArchitecturePolicySourceLocation? contractLocation = context.Document.Provenance.LocationFor(contract);
        string authoredId = contract.Id ?? ArchitecturePolicyDocumentLoader.NormalizeToContractId(contract.Name);
        List<ArchitectureExpandedContractInstance> inclusions = new();

        for (int index = 0; index < setNames.Count; index++)
        {
            string setName = setNames[index];
            ArchitectureSourceSetResolution resolution = context.Resolver.Resolve(contractName, field.Name, field.Kind, setName);
            ArchitecturePolicySourceLocation? referenceLocation = ExclusionLocation(
                context.Document, contractLocation, field.Name, index);
            if (resolution.ResolvedSources.Count == 0)
            {
                optionalReasons.Add(resolution.Reason);
                inclusions.Add(new ArchitectureExpandedContractInstance(authoredId, null, resolution.Name, null)
                {
                    PolicyLocation = referenceLocation,
                    AuthoredContractPolicyLocation = contractLocation,
                    SourceSetReferencePolicyLocation = referenceLocation,
                    OptionalEmpty = true,
                    OptionalReason = resolution.Reason
                });
                continue;
            }

            foreach (string value in resolution.ResolvedSources)
            {
                string selector = context.Resolver.SelectorFor(resolution.Name, value);
                inclusions.Add(CreateExpandedInstance(
                    authoredId, value, resolution.Name, selector,
                    context.Resolver.LocationFor(resolution.Name, value), contractLocation, referenceLocation));
                if (resolved.Contains(value, StringComparer.Ordinal))
                {
                    continue;
                }

                resolved.Add(value);
                setValues.TryAdd(value, (resolution.Name, selector, index));
            }
        }

        context.Expansions.Add(new ArchitectureContractExpansion(
            group, authoredId, contract.Name, setNames.ToArray(),
            setValues.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ArchitectureExpandedContractInstance(
                    authoredId, pair.Key, pair.Value.SetName, pair.Value.Selector)
                {
                    PolicyLocation = context.Resolver.LocationFor(pair.Value.SetName, pair.Key),
                    AuthoredContractPolicyLocation = contractLocation,
                    SourceSetReferencePolicyLocation = ExclusionLocation(
                        context.Document, contractLocation, field.Name, pair.Value.ReferenceIndex)
                })
                .ToArray())
        {
            Kind = ArchitectureContractExpansionKind.InlineUnion,
            SelectorField = field.Name,
            OptionalEmpty = resolved.Count == 0 && optionalReasons.Count > 0,
            OptionalReason = string.Join("; ", optionalReasons.Where(reason => !string.IsNullOrWhiteSpace(reason))),
            PolicyLocation = contractLocation,
            Inclusions = inclusions
        });

        return resolved
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    private static void ExpandInlineGroup<TContract>(
        ExpansionContext context,
        string group,
        IEnumerable<TContract> contracts,
        Func<TContract, List<string>> declared,
        Action<TContract, List<string>> assign,
        Func<TContract, List<string>> setNames,
        SourceSetField field)
        where TContract : class, IArchitectureContract
    {
        foreach (TContract contract in contracts)
        {
            context.Document.Provenance.SetValidationSubject(contract);
            assign(contract, InlineSets(context, group, contract, contract.Name,
                field, declared(contract), setNames(contract)));
        }
    }

    internal static ArchitecturePolicySourceLocation? ExclusionLocation(
        ArchitectureContractDocument document,
        ArchitecturePolicySourceLocation? contractLocation,
        string fieldName,
        int index)
    {
        if (contractLocation is null)
        {
            return null;
        }

        string path = ArchitecturePolicyProvenancePath.AppendIndex(
            ArchitecturePolicyProvenancePath.AppendProperty(contractLocation.YamlPath, fieldName),
            index);
        return document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location)
            ? location
            : contractLocation with { YamlPath = path };
    }

    internal static ArchitectureExpandedContractInstance CreateExpandedInstance(
        string contractId,
        string source,
        string? setName,
        string selector,
        ArchitecturePolicySourceLocation? policyLocation,
        ArchitecturePolicySourceLocation? contractLocation,
        ArchitecturePolicySourceLocation? sourceSetReferenceLocation) =>
        new(contractId, source, setName, selector)
        {
            PolicyLocation = policyLocation,
            AuthoredContractPolicyLocation = contractLocation,
            SourceSetReferencePolicyLocation = sourceSetReferenceLocation
        };

    internal sealed class SourceSetResolver
    {
        private readonly ArchitectureContractDocument _document;
        private readonly IReadOnlyList<string>? _projectUniverse;
        private readonly Dictionary<string, ArchitectureSourceSetResolution> _resolutions =
            new(StringComparer.Ordinal);
        private readonly Dictionary<(string Set, string Source), string> _selectors = new();
        private readonly Dictionary<(string Set, string Source), ArchitecturePolicySourceLocation?> _itemLocations = new();
        private readonly List<ArchitectureSourceSetResolution> _ordered = new();

        public SourceSetResolver(ArchitectureContractDocument document, IReadOnlyList<string>? projectUniverse = null)
        {
            _document = document;
            _projectUniverse = projectUniverse;

            // Every declared set is resolved eagerly, so an unusable or empty declaration is a
            // policy error whether or not a contract happens to reference it.
            foreach ((string name, ArchitectureSourceSet set) in document.SourceSets)
            {
                if (set.Kind == ArchitectureSourceSetKind.Project &&
                    HasDeferredProjectUniverse(document) && projectUniverse is null)
                {
                    continue;
                }
                // Point diagnostics at the authored `source_sets.<name>` node — including its
                // originating fragment for a composed policy — before it can throw.
                document.Provenance.SetValidationSubject(set);
                ArchitectureSourceSetResolution resolution = ResolveDeclaration(name, set);
                _resolutions[name] = resolution;
                _ordered.Add(resolution);
            }

            document.Provenance.ResetValidationSubject();
        }

        public IReadOnlyList<ArchitectureSourceSetResolution> Resolutions => _ordered;

        public string SelectorFor(string setName, string source) =>
            _selectors.TryGetValue((setName, source), out string? selector) ? selector : source;

        // The specific `members[i]`/`globs[i]` entry that resolved this source, falling back to the
        // set's own root location when the item-level path couldn't be resolved (e.g. a set with no
        // provenance, such as one built directly in a test rather than loaded from YAML).
        public ArchitecturePolicySourceLocation? LocationFor(string setName, string source)
        {
            if (_itemLocations.TryGetValue((setName, source), out ArchitecturePolicySourceLocation? location))
            {
                return location;
            }

            return _resolutions.TryGetValue(setName, out ArchitectureSourceSetResolution? resolution)
                ? resolution.PolicyLocation
                : null;
        }

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
            RequireUsableDeclaration(name, set);

            SortedSet<string> resolved = new(StringComparer.Ordinal);
            AddMembers(name, set, resolved);
            AddGlobMatches(name, set, resolved);

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

        private static void RequireUsableDeclaration(string name, ArchitectureSourceSet set)
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

        }

        private void AddMembers(string name, ArchitectureSourceSet set, SortedSet<string> resolved)
        {
            int index = 0;
            foreach (string member in set.Members)
            {
                int memberIndex = index++;
                if (string.IsNullOrWhiteSpace(member))
                {
                    continue;
                }

                if (!IsInUniverse(set.Kind, member))
                {
                    throw new InvalidOperationException(
                        $"Source set '{name}' lists member '{member}' that is not declared in " +
                        $"{UniverseName(set.Kind)}. A set must not introduce sources the policy does " +
                        "not analyze.");
                }

                resolved.Add(member);
                _selectors.TryAdd((name, member), member);
                _itemLocations.TryAdd((name, member), ItemLocation(name, "members", memberIndex));
            }
        }

        private void AddGlobMatches(string name, ArchitectureSourceSet set, SortedSet<string> resolved)
        {
            IReadOnlyList<string> universe = Universe(set.Kind);

            int index = 0;
            foreach (string glob in set.Globs)
            {
                int globIndex = index++;
                if (string.IsNullOrWhiteSpace(glob))
                {
                    continue;
                }

                if (universe.Count == 0 && !(set.Kind == ArchitectureSourceSetKind.Project && _projectUniverse is not null))
                {
                    throw new InvalidOperationException(
                        $"Source set '{name}' declares glob '{glob}' but {UniverseName(set.Kind)} is " +
                        "empty. Declare the inputs the glob may resolve against.");
                }

                string[] matches = set.Kind == ArchitectureSourceSetKind.Project
                    ? universe.Where(candidate => ProjectPathGlob.IsMatch(candidate, glob)).ToArray()
                    : universe.Where(candidate => NamespaceGlobPattern.Parse(glob).Match(candidate).Matched).ToArray();

                if (matches.Length == 0 && !set.Optional)
                {
                    throw new InvalidOperationException(
                        $"Source set '{name}' declares glob '{glob}' that matches nothing in " +
                        $"{UniverseName(set.Kind)}. Fix the glob, or declare the set " +
                        "'optional: true' with a reason if the absence is intentional.");
                }

                ArchitecturePolicySourceLocation? globLocation = ItemLocation(name, "globs", globIndex);
                foreach (string candidate in matches)
                {
                    resolved.Add(candidate);
                    _selectors.TryAdd((name, candidate), glob);
                    _itemLocations.TryAdd((name, candidate), globLocation);
                }
            }
        }

        // The set's own root location (source_sets.<name>) with "/<field>/<index>" appended - the
        // authored member/glob entry that actually produced a given resolved source, rather than
        // just the set it came from.
        private ArchitecturePolicySourceLocation? ItemLocation(string setName, string field, int index)
        {
            ArchitecturePolicySourceLocation? setLocation = _document.Provenance.LocationForSourceSet(setName);
            if (setLocation is null)
            {
                return null;
            }

            string path = ArchitecturePolicyProvenancePath.AppendIndex(
                ArchitecturePolicyProvenancePath.AppendProperty(setLocation.YamlPath, field), index);
            return _document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location)
                ? location
                : setLocation with { YamlPath = path };
        }

        private bool IsInUniverse(ArchitectureSourceSetKind kind, string value)
        {
            IReadOnlyList<string> universe = Universe(kind);

            // A policy that declares no targets or no projects is a small policy that names its
            // sources directly; there is nothing to check the member against in that case.
            return (universe.Count == 0 && !(kind == ArchitectureSourceSetKind.Project && _projectUniverse is not null))
                || universe.Contains(value, StringComparer.Ordinal);
        }

        private IReadOnlyList<string> Universe(ArchitectureSourceSetKind kind) => kind switch
        {
            ArchitectureSourceSetKind.Assembly => _document.Analysis.TargetAssemblies,
            ArchitectureSourceSetKind.Layer => _document.Layers.Keys.ToArray(),
            _ => _projectUniverse ?? _document.Analysis.Projects
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
