using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.RawValidators;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Guards the pre-deserialization raw-validation boundary described by
// openspec/specs/policy-document-validation-pipeline: capability-specific raw YAML node algorithms
// belong to their own validator, the loader only orders the stage, and the stage's first-match-wins
// order and provenance evidence are load-bearing behavior.
[TestFixture]
public sealed class PolicyRawValidationSeamTests
{
    private const string RepresentationModelNamespace = "YamlDotNet.RepresentationModel";

    // The order ArchitecturePolicyDocumentLoader.LoadCore invoked its raw checks in before they were
    // extracted. Contextual and port-boundary contracts were a single pass, contextual groups first.
    private static readonly string[] _documentedPipelineOrder =
    {
        nameof(RawLayerNodeValidator),
        nameof(RawContextualContractNodeValidator),
        nameof(RawPortBoundaryNodeValidator),
        nameof(RawSemanticCoverageNodeValidator),
        nameof(RawLayoutConventionNodeValidator),
        nameof(RawLayerTemplateNodeValidator),
        nameof(RawWhenFieldLocationValidator),
    };

    private static readonly RawMalformation[] _malformationsInPipelineOrder =
    {
        new(
            nameof(RawLayerNodeValidator),
            LayerExtra: "    namespce: App.Oops\n",
            ContractsExtra: string.Empty,
            AnalysisExtra: string.Empty,
            ExpectedMessage: "Layer 'domain' contains unknown property 'namespce'."),
        new(
            nameof(RawContextualContractNodeValidator),
            LayerExtra: string.Empty,
            ContractsExtra:
                "  strict_context_dependencies:\n" +
                "    - name: ctx\n" +
                "      source:\n" +
                "        role: Adapter\n" +
                "        rle: Domain\n" +
                "      forbidden:\n" +
                "        - role: Domain\n",
            AnalysisExtra: string.Empty,
            ExpectedMessage:
                "Contextual contract 'ctx' declares an unknown property 'rle' on its 'source' selector. " +
                "A contextual selector supports only 'role', 'metadata', and 'when'."),
        new(
            nameof(RawPortBoundaryNodeValidator),
            LayerExtra: string.Empty,
            ContractsExtra:
                "  strict_port_boundaries:\n" +
                "    - name: port\n" +
                "      sorce:\n" +
                "        role: Adapter\n",
            AnalysisExtra: string.Empty,
            ExpectedMessage: "Contextual contract 'port' declares an unknown property 'sorce' on port-boundary contract."),
        new(
            nameof(RawSemanticCoverageNodeValidator),
            LayerExtra: string.Empty,
            ContractsExtra:
                "  strict_coverage:\n" +
                "    - name: cov\n" +
                "      scope: semantic_role\n" +
                "      exclude:\n" +
                "        - role: Adapter\n" +
                "          metdata:\n" +
                "            key: value\n",
            AnalysisExtra: string.Empty,
            ExpectedMessage: "Contextual contract 'cov' declares an unknown property 'metdata' on semantic coverage exclusion."),
        new(
            nameof(RawLayoutConventionNodeValidator),
            LayerExtra: string.Empty,
            ContractsExtra:
                "  strict_layout_conventions:\n" +
                "    - name: layout\n" +
                "      required_name_sufix: Service\n",
            AnalysisExtra: string.Empty,
            ExpectedMessage:
                "Contextual contract 'layout' declares an unknown property 'required_name_sufix' on layout convention contract."),
        new(
            nameof(RawLayerTemplateNodeValidator),
            LayerExtra: string.Empty,
            ContractsExtra:
                "  strict_layer_templates:\n" +
                "    - name: tpl\n" +
                "      containers: [App]\n" +
                "      layerz: []\n",
            AnalysisExtra: string.Empty,
            ExpectedMessage: "Contextual contract 'tpl' declares an unknown property 'layerz' on layer template contract."),
        new(
            nameof(RawWhenFieldLocationValidator),
            LayerExtra: string.Empty,
            ContractsExtra: string.Empty,
            AnalysisExtra: "  when: \"true\"\n",
            ExpectedMessage:
                "'analysis.when' is not one of the approved expression locations. " +
                "See openspec/specs/cel-policy-model/spec.md for the closed list of locations that may declare 'when'."),
    };

    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-raw-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    // A reintroduced capability-specific raw-node algorithm on the loader has to accept or return a
    // YamlDotNet representation-model node, so this fails the moment the switchboard starts growing
    // back - the self-architecture policy is namespace-scoped and cannot see inside a single type.
    [Test]
    public void Loader_DeclaresNoRawYamlNodeMembers()
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Type loader = typeof(ArchitecturePolicyDocumentLoader);

        string[] offenders = loader.GetMethods(Declared)
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType)
                .Where(MentionsRepresentationModel)
                .Select(type => $"{method.Name}: {type.Name}"))
            .Concat(loader.GetFields(Declared)
                .Where(field => MentionsRepresentationModel(field.FieldType))
                .Select(field => $"{field.Name}: {field.FieldType.Name}"))
            .Concat(loader.GetProperties(Declared)
                .Where(property => MentionsRepresentationModel(property.PropertyType))
                .Select(property => $"{property.Name}: {property.PropertyType.Name}"))
            .ToArray();

        Assert.That(offenders, Is.Empty,
            "Raw YAML node validation belongs in Contracts/RawValidators, not on the policy document loader.");
    }

    [Test]
    public void RawValidatorPipeline_RegistersEveryRawValidatorExactlyOnce()
    {
        Type[] implementations = typeof(IArchitecturePolicyRawDocumentValidator).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(IArchitecturePolicyRawDocumentValidator).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        Type[] registered = ArchitecturePolicyRawDocumentValidatorPipeline.All
            .Select(validator => validator.GetType())
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.That(registered, Is.EqualTo(implementations),
            "Every raw validator must be reachable from the pipeline exactly once.");
    }

    [Test]
    public void RawValidatorPipeline_PreservesDocumentedOrder()
    {
        string[] actual = ArchitecturePolicyRawDocumentValidatorPipeline.All
            .Select(validator => validator.GetType().Name)
            .ToArray();

        Assert.That(actual, Is.EqualTo(_documentedPipelineOrder));
    }

    [Test]
    public void Load_SingleRawMalformation_ReportsThatValidatorsDiagnostic(
        [Range(0, 6)] int malformationIndex)
    {
        RawMalformation malformation = _malformationsInPipelineOrder[malformationIndex];
        string policy = WritePolicy("architecture/dependencies.arch.yml", BuildPolicy(malformation));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(policy))!;

        Assert.That(exception.Message, Is.EqualTo(malformation.ExpectedMessage));
    }

    // First-match-wins: raw validation throws eagerly, so an earlier validator's diagnostic must keep
    // winning over a later one's for a policy that is invalid in both respects.
    [Test]
    public void Load_TwoRawMalformations_ReportsTheEarlierPipelineStage(
        [Range(0, 5)] int earlierIndex)
    {
        RawMalformation earlier = _malformationsInPipelineOrder[earlierIndex];
        RawMalformation later = _malformationsInPipelineOrder[earlierIndex + 1];
        string policy = WritePolicy("architecture/dependencies.arch.yml", BuildPolicy(earlier, later));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(policy))!;

        Assert.That(exception.Message, Is.EqualTo(earlier.ExpectedMessage));
    }

    // A monolithic policy has no import provenance to enrich with, so the raw diagnostic surfaces
    // unwrapped and location-free.
    [Test]
    public void Load_MalformedMonolithicRoot_ReportsUnenrichedDiagnostic()
    {
        string policy = WritePolicy(
            "architecture/dependencies.arch.yml",
            BuildPolicy(_malformationsInPipelineOrder[0]));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(policy))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.InstanceOf<ArchitecturePolicyValidationException>());
            Assert.That(exception.Message, Is.EqualTo("Layer 'domain' contains unknown property 'namespce'."));
        });
    }

    // The composed-policy path runs effective-schema validation before raw validation, so this uses a
    // shape the JSON schema accepts (a non-empty but blank namespace string) and only the raw layer
    // validator rejects. The validation subject each raw validator sets is what carries the authored
    // fragment location into the diagnostic.
    [Test]
    public void Load_MalformedImportedFragment_ReportsAuthoredFragmentLocation()
    {
        string root = WritePolicy(
            "architecture/root.yml",
            "version: 1\nname: Example\nimports:\n  - fragment.yml\nanalysis:\n  target_assemblies: [App]\ncontracts:\n  strict: []\n");
        WritePolicy("architecture/fragment.yml", "layers:\n  domain:\n    namespace: \"   \"\n");

        ArchitecturePolicyValidationException exception = Assert.Throws<ArchitecturePolicyValidationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.StartWith("Layer 'domain' namespace must be a non-empty string."));
            Assert.That(exception.Diagnostic.Location, Is.Not.Null);
            Assert.That(exception.Diagnostic.Location!.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Fragment));
            Assert.That(exception.Diagnostic.Location.SourcePath, Is.EqualTo("architecture/fragment.yml"));
            Assert.That(exception.Diagnostic.Location.YamlPath, Is.EqualTo("layers.domain"));
        });
    }

    private static bool MentionsRepresentationModel(Type type)
    {
        if (string.Equals(type.Namespace, RepresentationModelNamespace, StringComparison.Ordinal))
        {
            return true;
        }

        Type? element = type.HasElementType ? type.GetElementType() : null;
        return (element is not null && MentionsRepresentationModel(element))
            || type.GetGenericArguments().Any(MentionsRepresentationModel);
    }

    private static string BuildPolicy(params RawMalformation[] malformations)
    {
        string layers = "layers:\n  domain:\n    namespace: App.Domain\n"
            + string.Concat(malformations.Select(malformation => malformation.LayerExtra));
        string contracts = "contracts:\n  strict: []\n"
            + string.Concat(malformations.Select(malformation => malformation.ContractsExtra));
        string analysis = "analysis:\n  target_assemblies: [App]\n"
            + string.Concat(malformations.Select(malformation => malformation.AnalysisExtra));
        return $"version: 1\nname: Example\n{layers}{contracts}{analysis}";
    }

    private string WritePolicy(string relativePath, string content)
    {
        string path = Path.Combine(_temporaryDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed record RawMalformation(
        string Name,
        string LayerExtra,
        string ContractsExtra,
        string AnalysisExtra,
        string ExpectedMessage);
}
