using System.Collections;
using System.Globalization;
using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext.Abstractions;

namespace ArchLinterNet.Core.PolicyContext;

/// <summary>Projects the already-loaded effective policy into compact agent context.</summary>
public sealed class ArchitecturePolicyContextApplicationService(IArchitecturePolicyDocumentLoader policyDocumentLoader)
    : IArchitecturePolicyContextApplicationService
{
    private const string ContextKind = "architecture-policy-context";
    private static readonly string[] _referenceProperties =
    [
        "Source", "Sources", "SourceSets", "ExcludedSources", "ExcludedSourceSets", "Forbidden", "Allowed",
        "AllowedTypes", "Layers", "Protected", "AllowedImporters", "SourceLayers", "MustResideInLayers",
        "AllowedOnlyInLayers", "ForbiddenInLayers", "Container", "Projects", "Assemblies",
    ];

    private static readonly string[] _guidance =
    [
        "Inspect the policy facts and current code before choosing a role, layer, or boundary.",
        "Prefer a narrow, schema-backed code or policy change; do not invent policy fields.",
        "Do not use broad ignores, overrides, or shared helpers to bypass a boundary.",
        "Treat uncovered, stale, or ambiguous architecture facts as review work, not permission to bypass policy.",
        "Run normal architecture validation after the change; this export is not a validation result.",
        "Keep human review for exceptions and policy changes.",
    ];

    /// <inheritdoc />
    public ArchitecturePolicyContextExport Export(ArchitecturePolicyContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.PolicyPath))
        {
            throw new ArgumentException("A policy path is required.", nameof(request));
        }

        ArchitectureContractDocument document = LoadDocument(request.PolicyPath);
        ArchitectureContractCatalog catalog = ArchitectureContractCatalog.Build(document);

        IReadOnlyList<ArchitecturePolicyContextClassification> classification = ProjectClassification(document.Classification);
        IReadOnlyList<ArchitecturePolicyContextContract> contracts = ProjectContracts(catalog, document.Provenance);
        IReadOnlyList<ArchitecturePolicyContextLayer> layers = ProjectLayers(document);

        return new ArchitecturePolicyContextExport(
            SchemaVersion: 1,
            Kind: ContextKind,
            Policy: new ArchitecturePolicyContextPolicy(
                document.Name,
                document.Version,
                PortablePath(document.Provenance.RootSource?.RootPath ?? string.Empty),
                document.Provenance.HasImports),
            Sources: document.Provenance.Sources
                .OrderBy(source => source.SourceOrdinal)
                .Select(ProjectSource)
                .ToArray(),
            Layers: layers,
            Contracts: contracts,
            Classification: classification,
            SemanticRoles: CollectRoles(layers, classification, contracts),
            Contexts: CollectContexts(layers, classification, contracts),
            SourceSets: document.SourceExpansion.Sets
                .OrderBy(sourceSet => sourceSet.Name, StringComparer.Ordinal)
                .Select(sourceSet => new ArchitecturePolicyContextSourceSet(
                    sourceSet.Name,
                    sourceSet.Kind.ToString().ToLowerInvariant(),
                    sourceSet.ResolvedSources.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    sourceSet.Optional,
                    sourceSet.Reason,
                    ProjectProvenance(sourceSet.PolicyLocation)))
                .ToArray(),
            Exceptions: ProjectExceptions(document, catalog, contracts),
            Guidance: _guidance);
    }

    private ArchitectureContractDocument LoadDocument(string policyPath)
    {
        try
        {
            return policyDocumentLoader.Load(policyPath);
        }
        catch (ArchitecturePolicyImportException exception)
        {
            throw new ArchitecturePolicyLoadException(
                exception.Message,
                exception.Diagnostic,
                exception.Category.ToString().ToLowerInvariant(),
                exception);
        }
    }

    private static ArchitecturePolicyContextSource ProjectSource(ArchitecturePolicySourceDescriptor source) => new(
        PortablePath(source.SourcePath),
        source.Role.ToString().ToLowerInvariant(),
        source.SourceOrdinal,
        PortablePathOrNull(source.DeclaringSourcePath),
        PortablePathOrNull(source.AuthoredImportPath),
        source.ImportChain.Select(PortablePath).ToArray());

    private static IReadOnlyList<ArchitecturePolicyContextLayer> ProjectLayers(ArchitectureContractDocument document)
    {
        return document.Layers
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ArchitecturePolicyContextLayer(
                item.Key,
                item.Value.Namespace,
                item.Value.NamespaceSuffix,
                item.Value.External,
                item.Value.Selector is null ? null : ProjectSelector("layer", item.Value.Selector),
                item.Value.Exclude
                    .Select(exclusion => new ArchitecturePolicyContextException(
                        "layer",
                        item.Key,
                        "exclude",
                        JoinNonEmpty(exclusion.Namespace, exclusion.NamespaceSuffix),
                        null))
                    .OrderBy(exclusion => exclusion.Details, StringComparer.Ordinal)
                    .ToArray(),
                ProjectProvenance(document.Provenance.LocationForLayer(item.Key))))
            .ToArray();
    }

    private static IReadOnlyList<ArchitecturePolicyContextContract> ProjectContracts(
        ArchitectureContractCatalog catalog,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        return catalog.Descriptors
            .Select(descriptor => new ArchitecturePolicyContextContract(
                descriptor.Mode,
                descriptor.Family,
                descriptor.Id ?? descriptor.Name,
                descriptor.Name,
                descriptor.AuthoredId,
                ReadReason(descriptor.Contract),
                ProjectReferences(descriptor.Contract),
                ProjectSelectors(descriptor.Contract),
                ProjectAdapterBindings(descriptor.Contract),
                ProjectExclusionSelectors(descriptor.Contract),
                descriptor.Contract is ArchitectureCoverageContract coverage ? [coverage.Scope] : Array.Empty<string>(),
                ProjectProvenance(provenance.LocationFor(descriptor.Contract))))
            .ToArray();
    }

    private static IReadOnlyList<ArchitecturePolicyContextClassification> ProjectClassification(
        ArchitectureClassificationConfiguration classification)
    {
        List<ArchitecturePolicyContextClassification> projected = new();
        projected.AddRange(classification.Attributes.Select(mapping => new ArchitecturePolicyContextClassification(
            "attribute", mapping.Attribute, mapping.Role, ProjectMetadata(mapping.Metadata))));
        projected.AddRange(classification.AssemblyAttributes.Select(mapping => new ArchitecturePolicyContextClassification(
            "assembly_attribute", mapping.Attribute, mapping.Role, ProjectMetadata(mapping.Metadata))));
        projected.AddRange(classification.Inheritance.Select(mapping => new ArchitecturePolicyContextClassification(
            "inheritance", mapping.BaseType, mapping.Role, ProjectMetadata(mapping.Metadata))));
        projected.AddRange(classification.Namespace.Select(mapping => new ArchitecturePolicyContextClassification(
            "namespace", JoinNonEmpty(mapping.Namespace, mapping.NamespaceSuffix), mapping.Role, ProjectMetadata(mapping.Metadata))));
        return projected.ToArray();
    }

    private static IReadOnlyList<ArchitecturePolicyContextReference> ProjectReferences(IArchitectureContract contract)
    {
        List<ArchitecturePolicyContextReference> references = new();
        Type type = contract.GetType();
        foreach (string propertyName in _referenceProperties)
        {
            PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(contract) is not object value)
            {
                continue;
            }

            string[] values = StringValues(value).ToArray();
            if (values.Length > 0)
            {
                references.Add(new ArchitecturePolicyContextReference(ToSnakeCase(propertyName), values));
            }
        }

        return references;
    }

    private static IReadOnlyList<ArchitecturePolicyContextSelector> ProjectSelectors(IArchitectureContract contract)
    {
        return contract switch
        {
            ArchitectureContextDependencyContract value =>
            [
                ProjectSelector("source", value.Source),
                .. value.Forbidden.Select(selector => ProjectSelector("forbidden", selector)),
            ],
            ArchitectureContextAllowOnlyContract value =>
            [
                ProjectSelector("source", value.Source),
                .. value.Allowed.Select(selector => ProjectSelector("allowed", selector)),
            ],
            ArchitecturePortBoundaryContract value =>
            [
                ProjectSelector("source", value.Source),
                new ArchitecturePolicyContextSelector("target_context", string.Empty, ProjectMetadata(value.TargetContext.Metadata), null),
                .. value.AllowedSeams.Select(selector => ProjectSelector("allowed_seam", selector)),
                .. value.Forbidden.Select(selector => ProjectSelector("forbidden", selector)),
            ],
            _ => Array.Empty<ArchitecturePolicyContextSelector>(),
        };
    }

    private static IReadOnlyList<ArchitecturePolicyContextAdapterBinding> ProjectAdapterBindings(
        IArchitectureContract contract)
    {
        return contract is ArchitecturePortBoundaryContract value
            ? value.AdapterBindings.Select(binding => new ArchitecturePolicyContextAdapterBinding(
                ProjectSelector("adapter", binding.Adapter),
                ProjectSelector("expected_port", binding.ExpectedPort),
                binding.AllowedContexts.Select(context => ProjectSelector("allowed_context", context)).ToArray()))
                .ToArray()
            : Array.Empty<ArchitecturePolicyContextAdapterBinding>();
    }

    private static IReadOnlyList<ArchitecturePolicyContextSelector> ProjectExclusionSelectors(IArchitectureContract contract)
    {
        return contract switch
        {
            ArchitectureContextDependencyContract value => value.Exclude.Select(selector => ProjectSelector("exclude", selector)).ToArray(),
            ArchitectureContextAllowOnlyContract value => value.Exclude.Select(selector => ProjectSelector("exclude", selector)).ToArray(),
            ArchitecturePortBoundaryContract value => value.Exclude.Select(selector => ProjectSelector("exclude", selector)).ToArray(),
            _ => Array.Empty<ArchitecturePolicyContextSelector>(),
        };
    }

    private static ArchitecturePolicyContextSelector ProjectSelector(string kind, ArchitectureLayerSelector selector) => new(
        kind,
        selector.Role,
        ProjectMetadata(selector.Metadata),
        selector.When);

    private static ArchitecturePolicyContextSelector ProjectSelector(string kind, ArchitectureContextSelector selector) => new(
        kind,
        selector.Role,
        ProjectMetadata(selector.Metadata),
        selector.When);

    private static IReadOnlyList<string> CollectRoles(
        IReadOnlyList<ArchitecturePolicyContextLayer> layers,
        IReadOnlyList<ArchitecturePolicyContextClassification> classification,
        IReadOnlyList<ArchitecturePolicyContextContract> contracts)
    {
        return layers.Select(layer => layer.Selector?.Role)
            .Concat(classification.Select(item => item.Role))
            .Concat(contracts.SelectMany(ContractSelectors).Select(selector => selector.Role))
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ArchitecturePolicyContextValue> CollectContexts(
        IReadOnlyList<ArchitecturePolicyContextLayer> layers,
        IReadOnlyList<ArchitecturePolicyContextClassification> classification,
        IReadOnlyList<ArchitecturePolicyContextContract> contracts)
    {
        IEnumerable<IReadOnlyDictionary<string, string>> metadata = layers
            .Where(layer => layer.Selector is not null)
            .Select(layer => layer.Selector!.Metadata)
            .Concat(classification.Select(item => item.Metadata))
            .Concat(contracts.SelectMany(ContractSelectors).Select(selector => selector.Metadata));

        return metadata.SelectMany(values => values)
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ArchitecturePolicyContextValue(
                group.Key,
                group.Select(item => item.Value).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    private static IEnumerable<ArchitecturePolicyContextSelector> ContractSelectors(
        ArchitecturePolicyContextContract contract)
    {
        return contract.Selectors
            .Concat(contract.AdapterBindings.Select(binding => binding.Adapter))
            .Concat(contract.AdapterBindings.Select(binding => binding.ExpectedPort))
            .Concat(contract.AdapterBindings.SelectMany(binding => binding.AllowedContexts));
    }

    private static IReadOnlyList<ArchitecturePolicyContextException> ProjectExceptions(
        ArchitectureContractDocument document,
        ArchitectureContractCatalog catalog,
        IReadOnlyList<ArchitecturePolicyContextContract> contracts)
    {
        List<ArchitecturePolicyContextException> exceptions = document.Layers
            .SelectMany(layer => layer.Value.Exclude.Select(exclusion => new ArchitecturePolicyContextException(
                "layer", layer.Key, "exclude", JoinNonEmpty(exclusion.Namespace, exclusion.NamespaceSuffix), null)))
            .ToList();

        foreach (ArchitecturePolicyContextContract contract in contracts)
        {
            exceptions.AddRange(contract.Exclusions.Select(selector => new ArchitecturePolicyContextException(
                "contract", contract.Id, "exclude", DescribeSelector(selector), contract.Reason)));
        }

        foreach (ArchitectureContractDescriptor descriptor in catalog.Descriptors)
        {
            string subject = descriptor.Id ?? descriptor.Name;
            exceptions.AddRange(ReadIgnoredViolations(descriptor.Contract).Select(ignored => new ArchitecturePolicyContextException(
                "contract",
                subject,
                "ignored_violation",
                JoinNonEmpty(ignored.SourceType, ignored.ForbiddenReference),
                ignored.Reason)));

            if (descriptor.Contract is ArchitectureSourceExpandableContractBase expandable)
            {
                exceptions.AddRange(expandable.ExcludedSources.Select(source => new ArchitecturePolicyContextException(
                    "contract", subject, "exclude_source", source, expandable.Reason)));
                exceptions.AddRange(expandable.ExcludedSourceSets.Select(sourceSet => new ArchitecturePolicyContextException(
                    "contract", subject, "exclude_source_set", sourceSet, expandable.Reason)));
            }

            if (descriptor.Contract is ArchitectureCoverageContract coverage)
            {
                exceptions.AddRange(coverage.Exclude.Select(exclusion => new ArchitecturePolicyContextException(
                    "coverage",
                    subject,
                    "exclude",
                    JoinNonEmpty(exclusion.Namespace, exclusion.NamespaceSuffix, exclusion.Project, exclusion.Assembly, exclusion.ContractId,
                        exclusion.Role),
                    exclusion.Reason)));
            }
        }

        return exceptions
            .OrderBy(exceptionItem => exceptionItem.Scope, StringComparer.Ordinal)
            .ThenBy(exceptionItem => exceptionItem.Subject, StringComparer.Ordinal)
            .ThenBy(exceptionItem => exceptionItem.Kind, StringComparer.Ordinal)
            .ThenBy(exceptionItem => exceptionItem.Details, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ProjectMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        return metadata.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => DisplayValue(item.Value), StringComparer.Ordinal);
    }

    private static string? ReadReason(IArchitectureContract contract) =>
        contract.GetType().GetProperty("Reason", BindingFlags.Instance | BindingFlags.Public)?.GetValue(contract) as string;

    private static IEnumerable<ArchitectureIgnoredViolation> ReadIgnoredViolations(IArchitectureContract contract) =>
        contract.GetType().GetProperty("IgnoredViolations", BindingFlags.Instance | BindingFlags.Public)?.GetValue(contract)
            as IEnumerable<ArchitectureIgnoredViolation> ?? Array.Empty<ArchitectureIgnoredViolation>();

    private static IEnumerable<string> StringValues(object value)
    {
        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : [text];
        }

        if (value is IEnumerable values)
        {
            return values.Cast<object?>().OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        }

        return Array.Empty<string>();
    }

    private static string DisplayValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => text,
            IEnumerable values => "[" + string.Join(", ", values.Cast<object?>().Select(DisplayValue)) + "]",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static ArchitecturePolicyContextProvenance? ProjectProvenance(ArchitecturePolicySourceLocation? location)
    {
        return location is null
            ? null
            : new ArchitecturePolicyContextProvenance(
                PortablePath(location.SourcePath),
                PortablePath(location.RootPath),
                location.Role.ToString().ToLowerInvariant(),
                location.YamlPath,
                location.SourceOrdinal);
    }

    private static string DescribeSelector(ArchitecturePolicyContextSelector selector) =>
        JoinNonEmpty(selector.Role, string.Join(", ", selector.Metadata.Select(item => $"{item.Key}={item.Value}")), selector.When ?? string.Empty);

    private static string JoinNonEmpty(params string[] values) => string.Join("; ", values.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string ToSnakeCase(string value) => string.Concat(value.Select((character, index) =>
        index > 0 && char.IsUpper(character) ? "_" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));

    private static string? PortablePathOrNull(string? path) => path is null ? null : PortablePath(path);

    private static string PortablePath(string path) => Path.IsPathRooted(path) ? "[redacted]" : path.Replace('\\', '/');
}
