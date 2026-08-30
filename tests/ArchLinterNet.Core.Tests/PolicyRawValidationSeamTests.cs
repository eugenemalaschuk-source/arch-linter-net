using System.Reflection;
using System.Text.RegularExpressions;
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
public sealed partial class PolicyRawValidationSeamTests
{
    private const string RepresentationModelNamespace = "YamlDotNet.RepresentationModel";

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // The representation-model surface a raw-node algorithm has to name somewhere in its source, even
    // when its own signature exposes none of it. Deliberately does not match YamlDotNet.Serialization
    // types: configuring the deserializer is a loader stage, walking the node tree is not.
    [GeneratedRegex(@"\b(YamlDotNet\.RepresentationModel|YamlStream|YamlDocument|YamlNode|YamlMappingNode|YamlSequenceNode|YamlScalarNode|YamlAliasNode)\b")]
    private static partial Regex RepresentationModelReference();

    // The order ArchitecturePolicyDocumentLoader.LoadCore invoked its raw checks in before they were
    // extracted. Contextual and port-boundary contracts were a single pass, contextual groups first.
    private static readonly string[] _documentedPipelineOrder =
    {
        nameof(RawLayerNodeValidator),
        nameof(RawContextualContractNodeValidator),
        nameof(RawPortBoundaryNodeValidator),
        nameof(RawSemanticCoverageNodeValidator),
        nameof(RawLayoutConventionNodeValidator),
        nameof(RawModuleContainerNodeValidator),
        nameof(RawLayerTemplateNodeValidator),
        nameof(RawTopologyNodeValidator),
        nameof(RawWhenFieldLocationValidator),
        nameof(RawHistoryAnalysisNodeValidator),
        nameof(RawExternalEvidenceNodeValidator),
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
            nameof(RawModuleContainerNodeValidator),
            LayerExtra: string.Empty,
            ContractsExtra:
                "  strict_module_containers:\n" +
                "    - name: modules\n" +
                "      container: App.Commands\n" +
                "      profile: cli_command\n" +
                "      profile_typo: cli_command\n",
            AnalysisExtra: string.Empty,
            ExpectedMessage:
                "Contextual contract 'modules' declares an unknown property 'profile_typo' on module container contract."),
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

    // The reintroduced anti-pattern this guards against is a method like the former
    // `ValidateRawLayerYaml(string yaml, ArchitecturePolicyProvenanceIndex provenance)`: its signature
    // mentions no node type at all, because it built the YamlStream and walked YamlMappingNode inside
    // its own body. Checking signatures alone would let exactly that back in, so the source of every
    // loader partial is checked for any representation-model type name.
    [Test]
    public void LoaderSource_DoesNotReferenceRawYamlNodeTypes()
    {
        string[] loaderSources = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "src", "ArchLinterNet.Core", "Contracts"),
            "ArchitecturePolicyDocumentLoader*.cs");
        Assert.That(loaderSources, Is.Not.Empty, "Loader source files were not found - fix the path in this guard.");

        string[] offenders = loaderSources
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, number) => (Line: line, Number: number + 1))
                .Where(entry => RepresentationModelReference().IsMatch(entry.Line))
                .Select(entry => $"{Path.GetFileName(path)}:{entry.Number}: {entry.Line.Trim()}"))
            .ToArray();

        Assert.That(offenders, Is.Empty,
            "Raw YAML node algorithms belong in Contracts/RawValidators, not on the policy document loader.");
    }

    // Compiled-form counterpart to the source guard, so the boundary survives a rename or a move of
    // the loader's files. Locals and nested compiler-generated types are covered too: a raw-node
    // algorithm hidden inside a method body or a lambda still materializes as a representation-model
    // local or captured field.
    [Test]
    public void LoaderType_DeclaresNoRawYamlNodeMembersOrLocals()
    {
        Type loader = typeof(ArchitecturePolicyDocumentLoader);
        string[] offenders = new[] { loader }
            .Concat(loader.GetNestedTypes(Declared))
            .SelectMany(RepresentationModelUsages)
            .ToArray();

        Assert.That(offenders, Is.Empty,
            "Raw YAML node algorithms belong in Contracts/RawValidators, not on the policy document loader.");
    }

    private static IEnumerable<string> RepresentationModelUsages(Type type)
    {
        IEnumerable<string> signatures = type.GetMethods(Declared).Cast<MethodBase>()
            .Concat(type.GetConstructors(Declared))
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Concat(method is MethodInfo info ? new[] { info.ReturnType } : Array.Empty<Type>())
                .Where(MentionsRepresentationModel)
                .Select(used => $"{type.Name}.{method.Name} signature: {used.Name}"));

        IEnumerable<string> locals = type.GetMethods(Declared).Cast<MethodBase>()
            .Concat(type.GetConstructors(Declared))
            .SelectMany(method => (method.GetMethodBody()?.LocalVariables ?? (IList<LocalVariableInfo>)Array.Empty<LocalVariableInfo>())
                .Select(local => local.LocalType)
                .Where(MentionsRepresentationModel)
                .Select(used => $"{type.Name}.{method.Name} local: {used.Name}"));

        IEnumerable<string> fields = type.GetFields(Declared)
            .Where(field => MentionsRepresentationModel(field.FieldType))
            .Select(field => $"{type.Name}.{field.Name} field: {field.FieldType.Name}");

        IEnumerable<string> properties = type.GetProperties(Declared)
            .Where(property => MentionsRepresentationModel(property.PropertyType))
            .Select(property => $"{type.Name}.{property.Name} property: {property.PropertyType.Name}");

        return signatures.Concat(locals).Concat(fields).Concat(properties);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && directory.GetFiles("ArchLinterNet.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repo root");
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
