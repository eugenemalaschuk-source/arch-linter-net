using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Contracts.RawValidators;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

// Load orchestrator: sequences the policy-load stages and owns the cross-stage concerns
// (cancellation, exception enrichment, provenance validation-subject lifecycle). Each stage's
// algorithm lives in its own type - raw YAML node checks in RawValidators/, post-deserialization
// family checks in Validators/, composition in PolicyImports/ - so adding a rule to a capability
// does not mean extending a central method here.
public sealed partial class ArchitecturePolicyDocumentLoader : IArchitecturePolicyDocumentLoader
{
    private readonly IArchitectureFileSystem _fileSystem;
    private readonly IArchitecturePolicyPathResolver _pathResolver;
    private readonly ArchitecturePolicyImportGraphResolver _importResolver;

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
        _importResolver = new ArchitecturePolicyImportGraphResolver(fileSystem, pathResolver);
    }

    public ArchitectureContractDocument Load(string policyPath)
    {
        return LoadCore(policyPath, validateEffectiveSchema: false, CancellationToken.None);
    }

    public ArchitectureContractDocument Load(string policyPath, CancellationToken cancellationToken)
    {
        return LoadCore(policyPath, validateEffectiveSchema: false, cancellationToken);
    }

    private ArchitectureContractDocument LoadCore(
        string policyPath, bool validateEffectiveSchema, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArchitecturePolicyRootPath? resolvedRoot = EnsureSelectedRootIsRegularFile(policyPath);
        ArchitecturePolicySourceDescriptor rootDescriptor = ResolveRootDescriptor(policyPath, resolvedRoot);

        (string yaml, ArchitecturePolicyProvenanceIndex provenance) =
            LoadYamlAndProvenance(policyPath, resolvedRoot, rootDescriptor, validateEffectiveSchema, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // The finally here must stay outside RunWithEnrichedExceptions: enrichment reads the
            // provenance index's current validation subject, so it has to run (inside that helper's
            // catch) before this reset clears it. The subject is sticky across raw validators by
            // design - each one points it at the node it is checking and the whole stage is reset
            // once, here.
            RunWithEnrichedExceptions(provenance, () => ValidateRawYaml(yaml, provenance));
        }
        finally
        {
            provenance.ResetValidationSubject();
        }

        ArchitectureContractDocument document = DeserializeDocument(yaml);

        ArchitectureContractFallbackIds.Assign(document);
        document.Provenance = provenance;
        provenance.Bind(document);
        document.ClassificationPathDeferred = RawClassificationPathDeferredDetector.Detect(yaml, provenance);

        // Reviewed API snapshots are resolved before the validator pipeline so that a contract's
        // declared surface is complete by the time any validator inspects it.
        PolicyCheckSnapshotValidation.Resolve(
            document, policyPath, _fileSystem, validateEffectiveSchema);

        // Source sets expand after provenance binding (so expanded instances can be aliased onto
        // their authored location) and before validation (so every validator, and everything
        // downstream, sees ordinary single-source contracts).
        RunWithEnrichedExceptions(provenance, () => ArchitectureSourceSetExpander.Expand(document));

        foreach (IArchitecturePolicyDocumentValidator validator in ArchitecturePolicyDocumentValidatorPipeline.All)
        {
            provenance.ResetValidationSubject();
            RunWithEnrichedExceptions(provenance, () => validator.Validate(document));
        }

        return document;
    }

    // Raw node checks run against the effective YAML - the root document, or the composed result of
    // an import graph - after effective-schema validation and before deserialization, because
    // IgnoreUnmatchedProperties() erases the unknown keys they exist to reject.
    private static void ValidateRawYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        ArchitecturePolicyRawDocument rawDocument = ArchitecturePolicyRawDocument.Parse(yaml, provenance);
        foreach (IArchitecturePolicyRawDocumentValidator validator in ArchitecturePolicyRawDocumentValidatorPipeline.All)
        {
            validator.Validate(rawDocument);
        }
    }

    private ArchitecturePolicySourceDescriptor ResolveRootDescriptor(
        string policyPath, ArchitecturePolicyRootPath? resolvedRoot)
    {
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

        return rootDescriptor;
    }

    private (string Yaml, ArchitecturePolicyProvenanceIndex Provenance) LoadYamlAndProvenance(
        string policyPath,
        ArchitecturePolicyRootPath? resolvedRoot,
        ArchitecturePolicySourceDescriptor rootDescriptor,
        bool validateEffectiveSchema,
        CancellationToken cancellationToken)
    {
        string yaml = ArchitecturePolicySourceReader.ReadAllText(
            _fileSystem,
            resolvedRoot?.PhysicalPath ?? policyPath,
            rootDescriptor.SourcePath,
            resolvedRoot?.FileIdentity,
            ArchitecturePolicyDiagnosticFactory.Location(rootDescriptor),
            rootDescriptor.ImportChain);

        if (!ArchitecturePolicySourceParser.ContainsImports(yaml, rootDescriptor))
        {
            ArchitecturePolicyProvenanceIndex monolithicProvenance =
                ArchitecturePolicyProvenanceFactory.CreateMonolithic(rootDescriptor, policyPath, yaml);
            if (validateEffectiveSchema)
            {
                ArchitecturePolicyEffectiveSchemaValidator.Validate(yaml, monolithicProvenance);
            }

            return (yaml, monolithicProvenance);
        }

        IReadOnlyList<ArchitecturePolicySource> sources = resolvedRoot is null
            ? _importResolver.Resolve(policyPath, yaml, cancellationToken)
            : _importResolver.Resolve(resolvedRoot, rootDescriptor, yaml, cancellationToken);
        ArchitecturePolicyCompositionResult composition = new ArchitecturePolicyDocumentComposer().Compose(sources);
        ArchitecturePolicyEffectiveSchemaValidator.Validate(composition.Yaml, composition.Provenance);
        return (composition.Yaml, composition.Provenance);
    }

    private static ArchitectureContractDocument DeserializeDocument(string yaml)
    {
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                new ArchitectureClassificationMetadataScalarNodeDeserializer(),
                syntax => syntax.Before<YamlDotNet.Serialization.NodeDeserializers.ScalarNodeDeserializer>())
            .Build();

        return deserializer.Deserialize<ArchitectureContractDocument>(yaml)
            ?? throw new InvalidOperationException("Failed to deserialize architecture contract YAML.");
    }

    // Every raw-validation, expansion, and validator-pipeline step reports an InvalidOperationException
    // enriched with the offending policy location - centralized here so LoadCore's own control flow
    // stays flat instead of repeating this try/catch/enrich shape at each call site.
    private static void RunWithEnrichedExceptions(ArchitecturePolicyProvenanceIndex provenance, Action action)
    {
        try
        {
            action();
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

    public static string NormalizeToContractId(string name)
    {
        string normalized = name.ToLowerInvariant();
        normalized = normalized.Replace(" -> ", "-to-");
        normalized = NonAlphaNumDashPattern().Replace(normalized, "-");
        normalized = MultiDashPattern().Replace(normalized, "-");
        normalized = normalized.Trim('-');
        return normalized;
    }

    [GeneratedRegex(@"[^a-z0-9-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphaNumDashPattern();
    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultiDashPattern();
}
