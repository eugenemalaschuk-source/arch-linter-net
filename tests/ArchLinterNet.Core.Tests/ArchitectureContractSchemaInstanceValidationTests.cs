using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Tests;

// Real positive/negative instance validation for schema/dependencies.arch.schema.json, using
// JsonSchema.Net against actual YAML documents (converted to JSON via YamlDotNet's JSON-compatible
// emitter). ArchitectureContractSchemaTests only asserts schema *structure* (that a $def/property
// exists); these tests assert schema *behavior* - that specific valid and invalid policy snippets
// are accepted/rejected as the semantic-classification-model design requires.
[TestFixture]
public sealed class ArchitectureContractSchemaInstanceValidationTests
{
    private static string SchemaText()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string schemaPath = Path.Combine(repositoryRoot, "schema", "dependencies.arch.schema.json");
        return File.ReadAllText(schemaPath);
    }

    private static JsonSchema LoadSchema() => JsonSchema.FromText(SchemaText());

    // Wraps a single named $def in a standalone schema that carries the full document's $defs
    // alongside it, so cross-references between $defs (e.g. classification -> selector) still
    // resolve, without relying on JsonSchema.Net's global SchemaRegistry or the document's $id.
    private static JsonSchema LoadSubSchema(string defName)
    {
        using JsonDocument document = JsonDocument.Parse(SchemaText());
        JsonElement defs = document.RootElement.GetProperty("$defs");

        var wrapper = new JsonObject
        {
            ["$defs"] = JsonNode.Parse(defs.GetRawText()),
            ["$ref"] = $"#/$defs/{defName}"
        };

        return JsonSchema.FromText(wrapper.ToJsonString());
    }

    // YamlDotNet's Deserialize<object> returns every scalar as a string (no YAML-1.1-core-schema
    // type inference), which would make every "version: 1"/"external: true" instance fail schema
    // validation for the wrong reason. Walk the representation-model tree directly instead, so
    // plain scalars are resolved to bool/int/double/null the way the policy loader's own YamlDotNet
    // configuration resolves them.
    private static JsonNode? ToJsonNode(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count == 0 ? null : ConvertNode(stream.Documents[0].RootNode);
    }

    private static JsonNode? ConvertNode(YamlNode node) => node switch
    {
        YamlScalarNode scalar => ConvertScalar(scalar),
        YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ConvertNode).ToArray()),
        YamlMappingNode mapping => new JsonObject(mapping.Children.Select(kv =>
            new KeyValuePair<string, JsonNode?>(((YamlScalarNode)kv.Key).Value ?? string.Empty, ConvertNode(kv.Value)))),
        _ => throw new NotSupportedException($"Unsupported YAML node type: {node.GetType()}")
    };

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        string? value = scalar.Value;
        if (scalar.Style != YamlDotNet.Core.ScalarStyle.Plain || value is null)
        {
            return value;
        }

        return value switch
        {
            "null" or "~" or "" => null,
            "true" => true,
            "false" => false,
            _ when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) => i,
            _ when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) => d,
            _ => value
        };
    }

    private static bool Validate(string yaml, string subDefinition)
    {
        JsonNode? instance = ToJsonNode(yaml);
        JsonSchema subSchema = LoadSubSchema(subDefinition);
        EvaluationResults results = subSchema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });
        return results.IsValid;
    }

    [TestCase("[yaml_override, type_attribute, assembly_attribute, inheritance, namespace, path]", true)]
    [TestCase("[yaml_override, namespace]", true)]
    [TestCase("[namespace]", true)]
    [TestCase("[namespace, type_attribute]", false, TestName = "Precedence_RejectsReorderedTiers")]
    [TestCase("[namespace, namespace]", false, TestName = "Precedence_RejectsDuplicateEntries")]
    [TestCase("[]", false, TestName = "Precedence_RejectsEmptyList")]
    public void Classification_Precedence_EnforcesFixedOrderedSubsequence(string precedenceYaml, bool expectedValid)
    {
        string yaml = $"precedence: {precedenceYaml}";

        bool isValid = Validate(yaml, "classification");

        Assert.That(isValid, Is.EqualTo(expectedValid), $"precedence: {precedenceYaml}");
    }

    [Test]
    public void ClassificationOverride_TypeScopedWithoutReason_IsValid()
    {
        const string Yaml = "type: MyApp.Order\nrole: ApplicationLayer\n";

        Assert.That(Validate(Yaml, "classificationOverride"), Is.True);
    }

    [Test]
    public void ClassificationOverride_NamespaceScopedWithReason_IsValid()
    {
        const string Yaml = "namespace: MyApp.Legacy\nrole: Unclassified\nreason: Reviewed quarterly.\n";

        Assert.That(Validate(Yaml, "classificationOverride"), Is.True);
    }

    [Test]
    public void ClassificationOverride_NamespaceScopedWithoutReason_IsRejected()
    {
        const string Yaml = "namespace: MyApp.Legacy\nrole: Unclassified\n";

        Assert.That(Validate(Yaml, "classificationOverride"), Is.False);
    }

    [Test]
    public void ClassificationOverride_CombiningTypeAndNamespaceWithoutReason_CannotBypassRequiredReason()
    {
        const string Yaml = "type: MyApp.Order\nnamespace: MyApp.Legacy\nrole: ApplicationLayer\n";

        Assert.That(Validate(Yaml, "classificationOverride"), Is.False,
            "A broad 'namespace' scope declared alongside 'type' must not bypass the required reason " +
            "by matching the narrow-scope branch instead.");
    }

    [Test]
    public void ClassificationOverride_CombiningTypeAndNamespaceWithReason_IsStillRejected()
    {
        const string Yaml = "type: MyApp.Order\nnamespace: MyApp.Legacy\nrole: ApplicationLayer\nreason: r\n";

        Assert.That(Validate(Yaml, "classificationOverride"), Is.False,
            "Scopes are mutually exclusive; combining them is rejected even when 'reason' is present.");
    }

    [Test]
    public void ClassificationOverride_NoScopeAtAll_IsRejected()
    {
        const string Yaml = "role: ApplicationLayer\n";

        Assert.That(Validate(Yaml, "classificationOverride"), Is.False);
    }

    [Test]
    public void ClassificationExclusion_SingleScopeWithReason_IsValid()
    {
        const string Yaml = "namespace_suffix: Generated\nreason: Generated code is exempt.\n";

        Assert.That(Validate(Yaml, "classificationExclusion"), Is.True);
    }

    [Test]
    public void ClassificationExclusion_WithoutReason_IsRejected()
    {
        const string Yaml = "namespace_suffix: Generated\n";

        Assert.That(Validate(Yaml, "classificationExclusion"), Is.False);
    }

    [Test]
    public void ClassificationExclusion_CombiningTypeAndNamespace_IsRejected()
    {
        const string Yaml = "type: MyApp.Order\nnamespace: MyApp.Legacy\nreason: r\n";

        Assert.That(Validate(Yaml, "classificationExclusion"), Is.False);
    }

    [Test]
    public void Layer_SelectorOnly_IsValid()
    {
        const string Yaml = "selector:\n  role: DomainLayer\n";

        Assert.That(Validate(Yaml, "layer"), Is.True);
    }

    [Test]
    public void Layer_NamespaceOnly_IsStillValid()
    {
        const string Yaml = "namespace: MyApp.Domain\n";

        Assert.That(Validate(Yaml, "layer"), Is.True);
    }

    [Test]
    public void Layer_NeitherNamespaceNorSelector_IsRejected()
    {
        const string Yaml = "external: true\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [TestCase("samples/policies/modular-monolith.yml")]
    [TestCase("samples/policies/unity-asmdef-boundaries.yml")]
    [TestCase("samples/policies/basic-clean-architecture.yml")]
    public void SamplePolicy_ValidatesAgainstFullSchema(string relativePath)
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string policyPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        JsonSchema schema = LoadSchema();
        JsonNode? instance = ToJsonNode(File.ReadAllText(policyPath));

        EvaluationResults results = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.That(results.IsValid, Is.True,
            string.Join(Environment.NewLine, results.Details.Where(d => !d.IsValid).Select(d => $"{d.InstanceLocation}: {string.Join(';', d.Errors?.Values ?? [])}")));
    }
}
