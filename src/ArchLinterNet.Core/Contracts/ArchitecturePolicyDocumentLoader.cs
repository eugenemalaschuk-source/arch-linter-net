using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public sealed partial class ArchitecturePolicyDocumentLoader : IArchitecturePolicyDocumentLoader
{
    private const string MetadataKey = "metadata";
    private const string SourceKey = "source";
    private const string ForbiddenKey = "forbidden";
    private const string WhenKey = "when";
    private const string ContractsKey = "contracts";
    private const string UnnamedContractName = "<unnamed>";

    private static readonly string[] _targetContextAllowedKeys = { "metadata" };
    private static readonly string[] _adapterBindingAllowedKeys = { "adapter", "expected_port", "allowed_contexts" };

    private readonly IArchitectureFileSystem _fileSystem;
    private readonly IArchitecturePolicyPathResolver _pathResolver;
    private readonly ArchitecturePolicyImportGraphResolver _importResolver;
    private readonly ArchitecturePolicySourceParser _sourceParser;

    public ArchitecturePolicyDocumentLoader()
        : this(ArchitectureFileSystem.Real)
    {
    }

    public ArchitecturePolicyDocumentLoader(IArchitectureFileSystem fileSystem)
        : this(fileSystem, new ArchitecturePolicyPathResolver())
    {
    }

    internal ArchitecturePolicyDocumentLoader(
        IArchitectureFileSystem fileSystem,
        IArchitecturePolicyPathResolver pathResolver)
    {
        _fileSystem = fileSystem;
        _pathResolver = pathResolver;
        _sourceParser = new ArchitecturePolicySourceParser();
        _importResolver = new ArchitecturePolicyImportGraphResolver(fileSystem, pathResolver, _sourceParser);
    }

    public ArchitectureContractDocument Load(string policyPath)
    {
        ArchitecturePolicyRootPath? resolvedRoot = EnsureSelectedRootIsRegularFile(policyPath);

        ArchitecturePolicySourceDescriptor rootDescriptor = resolvedRoot is null
            ? ArchitecturePolicyProvenanceFactory.CreateRootDescriptor(_pathResolver, policyPath)
            : ArchitecturePolicyProvenanceFactory.CreateRootDescriptor(resolvedRoot);
        if (resolvedRoot is null && !_fileSystem.FileExists(policyPath))
        {
            throw ArchitecturePolicyDiagnosticFactory.Exception(
                ArchitecturePolicyImportErrorCategory.MissingFile,
                $"Root policy file not found: {rootDescriptor.SourcePath}",
                ArchitecturePolicyDiagnosticFactory.Location(rootDescriptor));
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                new ArchitectureClassificationMetadataScalarNodeDeserializer(),
                syntax => syntax.Before<YamlDotNet.Serialization.NodeDeserializers.ScalarNodeDeserializer>())
            .Build();

        string yaml = ArchitecturePolicySourceReader.ReadAllText(
            _fileSystem,
            resolvedRoot?.PhysicalPath ?? policyPath,
            rootDescriptor.SourcePath,
            resolvedRoot?.FileIdentity,
            ArchitecturePolicyDiagnosticFactory.Location(rootDescriptor),
            rootDescriptor.ImportChain);
        ArchitecturePolicyProvenanceIndex provenance;
        if (ArchitecturePolicySourceParser.ContainsImports(yaml, rootDescriptor))
        {
            IReadOnlyList<ArchitecturePolicySource> sources = resolvedRoot is null
                ? _importResolver.Resolve(policyPath, yaml)
                : _importResolver.Resolve(resolvedRoot, rootDescriptor, yaml);
            ArchitecturePolicyCompositionResult composition =
                new ArchitecturePolicyDocumentComposer().Compose(sources);
            yaml = composition.Yaml;
            provenance = composition.Provenance;
            ArchitecturePolicyEffectiveSchemaValidator.Validate(yaml, provenance);
        }
        else
        {
            provenance = ArchitecturePolicyProvenanceFactory.CreateMonolithic(rootDescriptor, policyPath, yaml);
        }

        try
        {
            ValidateRawLayerYaml(yaml, provenance);
            ValidateRawContextualContractYaml(yaml, provenance);
            ValidateRawSemanticCoverageYaml(yaml, provenance);
            ValidateRawLayoutConventionYaml(yaml, provenance);
            ValidateRawWhenFieldLocations(yaml);
        }
        catch (InvalidOperationException exception)
        {
            Exception enriched = provenance.EnrichValidationException(exception);
            if (ReferenceEquals(enriched, exception))
            {
                throw;
            }

            throw enriched;
        }
        finally
        {
            provenance.ResetValidationSubject();
        }

        ArchitectureContractDocument? document = deserializer.Deserialize<ArchitectureContractDocument>(yaml);

        if (document == null)
        {
            throw new InvalidOperationException("Failed to deserialize architecture contract YAML.");
        }

        AssignFallbackIds(document);
        document.Provenance = provenance;
        provenance.Bind(document);
        document.ClassificationPathDeferred = DetectClassificationPathDeferred(yaml, provenance);

        foreach (IArchitecturePolicyDocumentValidator validator in ArchitecturePolicyDocumentValidatorPipeline.All)
        {
            provenance.ResetValidationSubject();
            try
            {
                validator.Validate(document);
            }
            catch (InvalidOperationException exception)
            {
                Exception enriched = provenance.EnrichValidationException(exception);
                if (ReferenceEquals(enriched, exception))
                {
                    throw;
                }

                throw enriched;
            }
        }

        return document;
    }

    private ArchitecturePolicyRootPath? EnsureSelectedRootIsRegularFile(string policyPath)
    {
        if (_fileSystem is not ArchitectureFileSystem)
        {
            return null;
        }

        try
        {
            return _pathResolver.ResolveRoot(policyPath);
        }
        catch (ArchitecturePolicyImportException exception)
        {
            ArchitecturePolicySourceDescriptor unresolvedRoot =
                ArchitecturePolicyProvenanceFactory.CreateUnresolvedRootDescriptor(policyPath);
            throw ArchitecturePolicyDiagnosticFactory.EnrichRoot(
                exception,
                unresolvedRoot);
        }
    }

    // classification.path is schema-accepted but unimplemented (path-convention classification
    // depends on issue #171's source/declared-type fact discovery). Detected here, from the raw node
    // tree rather than the deliberately unbound C# model, so declaring it produces a visible,
    // deterministic diagnostic instead of pure silence — fires once per policy load, independent of
    // scanned types, so it shows up even for a policy with zero scanned types.
    private static ArchitectureClassificationPathDeferredNotice? DetectClassificationPathDeferred(
        string yaml,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, "classification", out YamlMappingNode? classification)
            || !TryGetChild(classification!, "path", out YamlNode? pathNode)
            || pathNode is not YamlSequenceNode pathSequence
            || pathSequence.Children.Count == 0)
        {
            return null;
        }

        string classificationPath = ArchitecturePolicyProvenancePath.AppendProperty(
            ArchitecturePolicyProvenancePath.Property("classification"), "path");
        ArchitecturePolicySourceLocation[] locations = provenance.Nodes
            .Where(entry => ArchitecturePolicyProvenancePath.IsDirectSequenceItem(entry.Key, classificationPath))
            .Select(entry => entry.Value)
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.EncounterOrdinal)
            .ToArray();
        return new ArchitectureClassificationPathDeferredNotice(pathSequence.Children.Count)
        {
            PolicyLocations = locations
        };
    }

    private static void ValidateRawLayerYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, "layers", out YamlMappingNode? layers))
        {
            return;
        }

        foreach ((YamlNode keyNode, YamlNode valueNode) in layers!.Children)
        {
            string layerName = ((YamlScalarNode)keyNode).Value ?? string.Empty;
            provenance.SetValidationSubject(ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property("layers"), layerName));
            if (valueNode is not YamlMappingNode layerNode)
            {
                continue;
            }

            ValidateLayerNodeKeys(layerNode, layerName);
            ValidateNamespaceValue(layerNode, layerName);
            ValidateLayerExcludeEntries(layerNode, layerName);

            bool hasNamespace = TryGetNonNullChild(layerNode, "namespace", out _);
            bool hasNamespaceSuffix = TryGetNonNullChild(layerNode, "namespace_suffix", out _);
            YamlNode? selectorNode = null;
            bool hasSelectorKey = TryGetChild(layerNode, "selector", out selectorNode);

            if (hasSelectorKey && IsExplicitNull(selectorNode))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' selector must be an object when declared.");
            }

            if (hasNamespaceSuffix && !hasNamespace)
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' namespace_suffix requires a non-empty namespace.");
            }

            if (selectorNode is not YamlMappingNode selectorMapping)
            {
                continue;
            }

            if (TryGetChild(selectorMapping, MetadataKey, out YamlNode? metadataNode) && IsExplicitNull(metadataNode))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' selector metadata must be an object when declared.");
            }

            foreach ((YamlNode selKeyNode, _) in selectorMapping.Children)
            {
                if (selKeyNode is YamlScalarNode selKeyScalar
                    && !string.Equals(selKeyScalar.Value, "role", StringComparison.Ordinal)
                    && !string.Equals(selKeyScalar.Value, MetadataKey, StringComparison.Ordinal)
                    && !string.Equals(selKeyScalar.Value, WhenKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Layer '{layerName}' selector contains unknown property '{selKeyScalar.Value}'.");
                }
            }
        }
    }

    private static void ValidateLayerNodeKeys(YamlMappingNode layerNode, string layerName)
    {
        foreach ((YamlNode keyNode, _) in layerNode.Children)
        {
            if (keyNode is YamlScalarNode scalar
                && !string.Equals(scalar.Value, "namespace", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "namespace_suffix", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "external", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "selector", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "exclude", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' contains unknown property '{scalar.Value}'.");
            }
        }
    }

    // Mirrors the selector-key check above: `exclude` entries accept only namespace/namespace_suffix
    // (the same shape a layer itself uses for inclusion). An unrecognized key such as a typo'd
    // "namespace_sufix" or an accidental "role" would otherwise be silently dropped by
    // IgnoreUnmatchedProperties(), leaving the exclusion inert without any signal to the author.
    private static void ValidateLayerExcludeEntries(YamlMappingNode layerNode, string layerName)
    {
        if (!TryGetChild(layerNode, "exclude", out YamlNode? excludeNode))
        {
            return;
        }

        if (excludeNode is not YamlSequenceNode excludeSequence)
        {
            throw new InvalidOperationException(
                $"Layer '{layerName}' exclude must be a list of entries.");
        }

        foreach (YamlNode entryNode in excludeSequence.Children)
        {
            if (entryNode is not YamlMappingNode entryMapping)
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' exclude entries must be objects with a 'namespace' key.");
            }

            bool hasNamespace = false;

            foreach ((YamlNode entryKeyNode, _) in entryMapping.Children)
            {
                if (entryKeyNode is not YamlScalarNode entryKeyScalar)
                {
                    continue;
                }

                if (string.Equals(entryKeyScalar.Value, "namespace", StringComparison.Ordinal))
                {
                    hasNamespace = true;
                    continue;
                }

                if (!string.Equals(entryKeyScalar.Value, "namespace_suffix", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Layer '{layerName}' exclude entry contains unknown property '{entryKeyScalar.Value}'.");
                }
            }

            if (!hasNamespace)
            {
                throw new InvalidOperationException(
                    $"Layer '{layerName}' exclude entry must declare 'namespace'.");
            }
        }
    }

    private static void ValidateNamespaceValue(YamlMappingNode layerNode, string layerName)
    {
        if (TryGetChild(layerNode, "namespace", out YamlNode? nsNode)
            && nsNode is YamlScalarNode nsScalar
            && (IsExplicitNull(nsScalar) || string.IsNullOrWhiteSpace(nsScalar.Value)))
        {
            throw new InvalidOperationException(
                $"Layer '{layerName}' namespace must be a non-empty string.");
        }
    }

    // ContextualContractValidator (Validators/) runs after deserialization and can only see what
    // IgnoreUnmatchedProperties() left behind - an unknown selector property (e.g. "metdata" typo'd
    // for "metadata") is silently dropped by deserialization, leaving ArchitectureContextSelector's
    // Metadata at its empty-dictionary default. That default is structurally valid (a role-only
    // selector is a legitimate, intentional shape), so no post-deserialization check can distinguish
    // "author wrote role-only on purpose" from "author's metadata typo silently vanished" - the
    // dictionary looks identical either way. For context_allow_only in particular, an unintentionally
    // role-only `allowed` selector silently broadens to match every type of that role (any metadata),
    // turning a metadata-scoped allow-list into a false-negative that admits cross-context references.
    // This raw-YAML pass, mirroring ValidateRawLayerYaml's selector-key check below, is the only place
    // that can still see the rejected property name before deserialization discards it.
    private static void ValidateRawContextualContractYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, ContractsKey, out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateContextualContractGroup(contracts!, "strict_context_dependencies", ForbiddenKey, provenance);
        ValidateContextualContractGroup(contracts!, "audit_context_dependencies", ForbiddenKey, provenance);
        ValidateContextualContractGroup(contracts!, "strict_context_allow_only", "allowed", provenance);
        ValidateContextualContractGroup(contracts!, "audit_context_allow_only", "allowed", provenance);
        ValidatePortBoundaryContractGroup(contracts!, "strict_port_boundaries", provenance);
        ValidatePortBoundaryContractGroup(contracts!, "audit_port_boundaries", provenance);
    }

    private static void ValidateRawSemanticCoverageYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, ContractsKey, out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateSemanticCoverageContractGroup(contracts!, "strict_coverage", provenance);
        ValidateSemanticCoverageContractGroup(contracts!, "audit_coverage", provenance);
    }

    private static void ValidateSemanticCoverageContractGroup(
        YamlMappingNode contracts,
        string groupKey,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        if (!TryGetChild(contracts, groupKey, out YamlNode? groupNode) || groupNode is not YamlSequenceNode contractsList)
        {
            return;
        }

        for (int index = 0; index < contractsList.Children.Count; index++)
        {
            if (contractsList.Children[index] is not YamlMappingNode contract)
            {
                continue;
            }

            provenance.SetValidationSubject(ContractPath(groupKey, index));
            if (!TryGetChild(contract, "scope", out YamlNode? scopeNode)
                || scopeNode is not YamlScalarNode scope
                || !string.Equals(scope.Value, "semantic_role", StringComparison.Ordinal)
                || !TryGetChild(contract, "exclude", out YamlNode? excludeNode)
                || excludeNode is not YamlSequenceNode exclusions)
            {
                continue;
            }

            string contractName = TryGetChild(contract, "name", out YamlNode? nameNode)
                                  && nameNode is YamlScalarNode name
                ? name.Value ?? UnnamedContractName
                : UnnamedContractName;
            foreach (YamlMappingNode exclusion in exclusions.Children.OfType<YamlMappingNode>())
            {
                ValidateKnownKeys(exclusion, contractName, "semantic coverage exclusion",
                    new[]
                    {
                        "namespace", "namespace_suffix", "project", "assembly", "contract_id", "between",
                        "role", "metadata", "reason"
                    });
            }
        }
    }

    private static readonly string[] _layoutFilesMatchingAllowedKeys =
        { "folder_segment", "namespace_segment", "file_name_suffix", "file_name_prefix", WhenKey };

    private static readonly string[] _layoutRequireMatchingInterfaceAllowedKeys = { "name_prefix" };

    private static readonly string[] _layoutConventionContractAllowedKeys =
    {
        "name", "id", "files_matching", "require_type_kind", "forbid_type_kind",
        "required_name_suffix", "required_name_prefix", "forbidden_name_suffix", "forbidden_name_prefix",
        "require_type_name_matches_file_name", "require_matching_interface", "ignored_violations", "reason"
    };

    // Mirrors ValidateRawContextualContractYaml's rationale: IgnoreUnmatchedProperties() would
    // otherwise silently drop a typo'd files_matching key (e.g. "folder_segments" for
    // "folder_segment"), leaving the selector looking like a legitimate-but-empty field instead of
    // failing the load. ValidateRawWhenFieldLocations (WhenFields.cs) separately enforces that `when`
    // may only appear on this exact node - this pass only checks the non-`when` field names.
    private static void ValidateRawLayoutConventionYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        if (stream.Documents.Count == 0
            || stream.Documents[0].RootNode is not YamlMappingNode root
            || !TryGetMappingChild(root, ContractsKey, out YamlMappingNode? contracts))
        {
            return;
        }

        ValidateLayoutConventionContractGroup(contracts!, "strict_layout_conventions", provenance);
        ValidateLayoutConventionContractGroup(contracts!, "audit_layout_conventions", provenance);
    }

    private static void ValidateLayoutConventionContractGroup(
        YamlMappingNode contracts, string groupKey, ArchitecturePolicyProvenanceIndex provenance)
    {
        if (!TryGetChild(contracts, groupKey, out YamlNode? groupNode) || groupNode is not YamlSequenceNode sequence)
        {
            return;
        }

        for (int index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is not YamlMappingNode contractNode)
            {
                continue;
            }

            provenance.SetValidationSubject(ContractPath(groupKey, index));
            string contractName = TryGetChild(contractNode, "name", out YamlNode? nameNode)
                && nameNode is YamlScalarNode nameScalar
                    ? nameScalar.Value ?? UnnamedContractName
                    : UnnamedContractName;

            // Top-level fields too: without this, a typo like "required_name_sufix" is silently
            // dropped by IgnoreUnmatchedProperties() for a monolithic (non-imported) policy - the
            // composed-policy path catches this via schema/dependencies.arch.schema.json's
            // additionalProperties: false, but that JSON-schema pass never runs for a monolithic
            // policy, so this raw-YAML check is the only place monolithic policies get the same
            // protection. Mirrors ValidatePortBoundaryContractNodeKeys's identical rationale.
            ValidateKnownKeys(contractNode, contractName, "layout convention contract", _layoutConventionContractAllowedKeys);

            if (TryGetChild(contractNode, "files_matching", out YamlNode? filesMatchingNode)
                && filesMatchingNode is YamlMappingNode filesMatchingMapping)
            {
                ValidateKnownKeys(
                    filesMatchingMapping, contractName, "files_matching", _layoutFilesMatchingAllowedKeys);
            }

            // Same rationale as files_matching above: require_matching_interface has exactly one
            // accepted key (name_prefix). Without this raw-YAML check, a typo like "name_prefx"
            // would be silently dropped by IgnoreUnmatchedProperties(), leaving NamePrefix null and
            // the contract quietly falling back to the default "I" prefix instead of failing to load.
            if (TryGetChild(contractNode, "require_matching_interface", out YamlNode? requireMatchingInterfaceNode)
                && requireMatchingInterfaceNode is YamlMappingNode requireMatchingInterfaceMapping)
            {
                ValidateKnownKeys(
                    requireMatchingInterfaceMapping, contractName, "require_matching_interface",
                    _layoutRequireMatchingInterfaceAllowedKeys);
            }
        }
    }

    private static void ValidateContextualContractGroup(
        YamlMappingNode contracts,
        string groupKey,
        string targetListKey,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        if (!TryGetChild(contracts, groupKey, out YamlNode? groupNode) || groupNode is not YamlSequenceNode sequence)
        {
            return;
        }

        for (int index = 0; index < sequence.Children.Count; index++)
        {
            YamlNode entryNode = sequence.Children[index];
            if (entryNode is not YamlMappingNode contractNode)
            {
                continue;
            }

            provenance.SetValidationSubject(ContractPath(groupKey, index));
            string contractName = TryGetChild(contractNode, "name", out YamlNode? nameNode)
                && nameNode is YamlScalarNode nameScalar
                    ? nameScalar.Value ?? UnnamedContractName
                    : UnnamedContractName;

            if (TryGetChild(contractNode, SourceKey, out YamlNode? sourceNode) && sourceNode is YamlMappingNode sourceMapping)
            {
                ValidateContextualSelectorNodeKeys(sourceMapping, contractName, SourceKey, allowWhen: true);
            }

            ValidateContextualSelectorListKeys(contractNode, contractName, targetListKey, allowWhen: true);
            ValidateContextualSelectorListKeys(contractNode, contractName, "exclude", allowWhen: true);
        }
    }

    private static void ValidatePortBoundaryContractGroup(
        YamlMappingNode contracts,
        string groupKey,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        if (!TryGetChild(contracts, groupKey, out YamlNode? groupNode) || groupNode is not YamlSequenceNode sequence) return;
        for (int index = 0; index < sequence.Children.Count; index++)
        {
            if (sequence.Children[index] is not YamlMappingNode entry)
            {
                continue;
            }

            provenance.SetValidationSubject(ContractPath(groupKey, index));
            string name = TryGetChild(entry, "name", out YamlNode? value) && value is YamlScalarNode scalar
                ? scalar.Value ?? UnnamedContractName : UnnamedContractName;
            ValidatePortBoundaryContractNodeKeys(entry, name);
            if (TryGetChild(entry, SourceKey, out YamlNode? source) && source is YamlMappingNode sourceMapping)
            {
                ValidateContextualSelectorNodeKeys(sourceMapping, name, SourceKey);
            }
            if (TryGetChild(entry, "target_context", out YamlNode? targetContext) && targetContext is YamlMappingNode targetMapping)
            {
                ValidateTargetContextNodeKeys(targetMapping, name);
            }
            ValidateContextualSelectorListKeys(entry, name, "allowed_seams");
            ValidateContextualSelectorListKeys(entry, name, "forbidden");
            ValidateContextualSelectorListKeys(entry, name, "exclude");
            ValidateAdapterBindings(entry, name);
        }
    }

    private static void ValidatePortBoundaryContractNodeKeys(YamlMappingNode node, string contractName)
    {
        string[] allowed = { "name", "id", "source", "target_context", "allowed_seams", "forbidden", "adapter_bindings", "exclude", "ignored_violations", "reason" };
        ValidateKnownKeys(node, contractName, "port-boundary contract", allowed);
    }

    private static void ValidateTargetContextNodeKeys(YamlMappingNode node, string contractName) =>
        ValidateKnownKeys(node, contractName, "target_context", _targetContextAllowedKeys);

    private static void ValidateAdapterBindings(YamlMappingNode contractNode, string contractName)
    {
        if (!TryGetChild(contractNode, "adapter_bindings", out YamlNode? bindingsNode) || bindingsNode is not YamlSequenceNode bindings) return;
        foreach (YamlMappingNode binding in bindings.Children.OfType<YamlMappingNode>())
        {
            ValidateKnownKeys(binding, contractName, "adapter_bindings entry", _adapterBindingAllowedKeys);
            foreach (string field in new[] { "adapter", "expected_port" })
            {
                if (TryGetChild(binding, field, out YamlNode? selector) && selector is YamlMappingNode mapping)
                    ValidateContextualSelectorNodeKeys(mapping, contractName, $"adapter_bindings.{field}");
            }
            ValidateContextualSelectorListKeys(binding, contractName, "allowed_contexts");
        }
    }

    private static void ValidateKnownKeys(YamlMappingNode node, string contractName, string location, IEnumerable<string> allowed)
    {
        foreach ((YamlNode keyNode, _) in node.Children)
        {
            if (keyNode is YamlScalarNode scalar && !allowed.Contains(scalar.Value, StringComparer.Ordinal))
                throw new InvalidOperationException($"Contextual contract '{contractName}' declares an unknown property '{scalar.Value}' on {location}.");
        }
    }

    private static void ValidateContextualSelectorListKeys(
        YamlMappingNode contractNode, string contractName, string listKey, bool allowWhen = false)
    {
        if (!TryGetChild(contractNode, listKey, out YamlNode? listNode) || listNode is not YamlSequenceNode listSequence)
        {
            return;
        }

        foreach (YamlNode itemNode in listSequence.Children)
        {
            if (itemNode is YamlMappingNode itemMapping)
            {
                ValidateContextualSelectorNodeKeys(itemMapping, contractName, listKey, allowWhen);
            }
        }
    }

    // allowWhen is scoped per call site (not per selector type): ArchitectureContextSelector is
    // reused by port-boundary/adapter-binding contracts, which openspec/specs/cel-policy-model's
    // closed first-wave `when` location list does not include. Only ValidateContextualContractGroup
    // (context_dependencies/context_allow_only) passes allowWhen: true. See
    // openspec/changes/core-cel-integration/design.md Decision D4.
    private static void ValidateContextualSelectorNodeKeys(
        YamlMappingNode selectorNode, string contractName, string fieldName, bool allowWhen = false)
    {
        foreach ((YamlNode keyNode, _) in selectorNode.Children)
        {
            if (keyNode is YamlScalarNode scalar
                && !string.Equals(scalar.Value, "role", StringComparison.Ordinal)
                && !string.Equals(scalar.Value, "metadata", StringComparison.Ordinal)
                && !(allowWhen && string.Equals(scalar.Value, WhenKey, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares an unknown property '{scalar.Value}' on its '{fieldName}' selector. " +
                    (allowWhen
                        ? "A contextual selector supports only 'role', 'metadata', and 'when'."
                        : "A contextual selector supports only 'role' and 'metadata'."));
            }
        }
    }

    private static bool TryGetMappingChild(YamlMappingNode parent, string key, out YamlMappingNode? child)
    {
        child = null;
        if (!TryGetChild(parent, key, out YamlNode? node) || node is not YamlMappingNode mapping)
        {
            return false;
        }

        child = mapping;
        return true;
    }

    private static string ContractPath(string groupKey, int index)
    {
        return ArchitecturePolicyProvenancePath.AppendIndex(
            ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property(ContractsKey), groupKey),
            index);
    }

    private static bool TryGetNonNullChild(YamlMappingNode parent, string key, out YamlNode? child)
    {
        child = null;
        return TryGetChild(parent, key, out child) && !IsExplicitNull(child);
    }

    private static bool TryGetChild(YamlMappingNode parent, string key, out YamlNode? child)
    {
        foreach ((YamlNode candidateKey, YamlNode candidateValue) in parent.Children)
        {
            if (candidateKey is YamlScalarNode scalar
                && string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                child = candidateValue;
                return true;
            }
        }

        child = null;
        return false;
    }

    private static bool IsExplicitNull(YamlNode? node)
    {
        return node is YamlScalarNode scalar
            && (scalar.Value is null
                || (scalar.Style == ScalarStyle.Plain
                    && string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase))
                || (scalar.Style == ScalarStyle.Plain
                    && string.Equals(scalar.Value, "~", StringComparison.Ordinal)));
    }

    public static string NormalizeToContractId(string name)
    {
        string normalized = name.ToLowerInvariant();
        normalized = normalized.Replace(" -> ", "-to-");
        normalized = NonAlphaNumDashPattern().Replace(normalized, "-");
        normalized = MultiDashPattern().Replace(normalized, "-");
        normalized = normalized.Trim('-');
        return normalized;
    }

    private static void AssignFallbackIds(ArchitectureContractDocument document)
    {
        foreach (IArchitectureContract contract in GetAllContracts(document).Where(c => string.IsNullOrEmpty(c.Id)))
        {
            contract.Id = NormalizeToContractId(contract.Name);
        }
    }

    [GeneratedRegex(@"[^a-z0-9-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumDashPattern();
    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultiDashPattern();

    private static IEnumerable<IArchitectureContract> GetAllContracts(ArchitectureContractDocument document)
    {
        return document.Contracts.AllStrict
            .Concat(document.Contracts.AllAudit)
            .Concat(document.Contracts.StrictLayerTemplates)
            .Concat(document.Contracts.AuditLayerTemplates);
    }
}
