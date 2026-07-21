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
    public void CoverageContract_SemanticRole_AcceptsReasonedRoleExclusion()
    {
        const string Yaml = """
            name: semantic-role-coverage
            scope: semantic_role
            reason: Semantic facts must be governed.
            roots:
              - namespace: MyApp
            exclude:
              - role: GeneratedRole
                metadata:
                  kind: generated
                reason: Generated code is governed elsewhere.
            """;

        Assert.That(Validate(Yaml, "coverageContract"), Is.True);
    }

    [Test]
    public void CoverageContract_SemanticRole_RejectsExclusionWithoutRole()
    {
        const string Yaml = """
            name: semantic-role-coverage
            scope: semantic_role
            reason: Semantic facts must be governed.
            exclude:
              - reason: Missing role.
            """;

        Assert.That(Validate(Yaml, "coverageContract"), Is.False);
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
    public void Layer_SelectorOnlyWithoutNamespace_IsValid()
    {
        const string Yaml = "selector:\n  role: DomainLayer\n";

        Assert.That(Validate(Yaml, "layer"), Is.True);
    }

    [Test]
    public void Layer_NamespaceWithSelector_IsValid()
    {
        const string Yaml = "namespace: MyApp.Domain\nselector:\n  role: DomainLayer\n";

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

    [Test]
    public void Layer_SelectorExplicitNull_IsRejected()
    {
        const string Yaml = "namespace: MyApp.Domain\nselector: null\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [Test]
    public void Layer_SelectorMetadataExplicitNull_IsRejected()
    {
        const string Yaml = "selector:\n  role: DomainLayer\n  metadata: null\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [Test]
    public void Layer_SelectorOnlyWithNamespaceSuffix_IsRejected()
    {
        const string Yaml = "namespace_suffix: Generated\nselector:\n  role: DomainLayer\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [Test]
    public void Layer_SelectorMetadataEmptyString_IsRejected()
    {
        const string Yaml = "selector:\n  role: DomainLayer\n  metadata:\n    domain: \"\"\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [Test]
    public void Layer_WithExclude_IsValid()
    {
        const string Yaml = "namespace: Product.Modules.*\nexclude:\n  - namespace: Product.Modules.*.Infrastructure\n  - namespace: Product.Modules.*.Persistence\n";

        Assert.That(Validate(Yaml, "layer"), Is.True);
    }

    [Test]
    public void Layer_ExcludeEntryWithNamespaceSuffix_IsValid()
    {
        const string Yaml = "namespace: Product.Modules.*\nexclude:\n  - namespace: Product.Modules\n    namespace_suffix: Generated\n";

        Assert.That(Validate(Yaml, "layer"), Is.True);
    }

    [Test]
    public void Layer_ExcludeEntryMissingNamespace_IsRejected()
    {
        const string Yaml = "namespace: Product.Modules.*\nexclude:\n  - namespace_suffix: Generated\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [Test]
    public void Layer_ExcludeEntryUnknownProperty_IsRejected()
    {
        const string Yaml = "namespace: Product.Modules.*\nexclude:\n  - namespace: Product.Modules.Infrastructure\n    role: DomainLayer\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [Test]
    public void Layer_ExcludeOnSelectorOnlyLayer_IsRejected()
    {
        // Regression for PR #384 review: exclude entries are namespace-based and have nothing to
        // subtract from on a purely role/metadata-matched (selector-only) layer.
        const string Yaml = "selector:\n  role: DomainLayer\nexclude:\n  - namespace: MyApp.Domain.Generated\n";

        Assert.That(Validate(Yaml, "layer"), Is.False);
    }

    [Test]
    public void Layer_WithoutExclude_IsStillValid()
    {
        const string Yaml = "namespace: Product.Modules.*\n";

        Assert.That(Validate(Yaml, "layer"), Is.True);
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

    // A review round found that design.md's/the docs page's illustrative ```yaml Markdown code
    // blocks had drifted from the schema (a selector-only layer slipped past several earlier review
    // rounds because only the schema-validated sample-policy files above were tested, never the
    // Markdown prose itself). These tests extract every ```yaml fenced block from the design record
    // and the public docs pages and validate their classification, layer, coverage, and contextual-
    // contract fragments against the corresponding schema $defs, so Markdown snippets can't silently
    // diverge from the schema again. Blocks are partial policy fragments, so each fragment is
    // validated against its own $def rather than the full root schema.
    [TestCase("openspec/changes/archive/2026-07-10-design-semantic-classification-model/design.md")]
    [TestCase("docs/policy-format/semantic-classification.md")]
    [TestCase("docs/ai/semantic-role-governance.md")]
    public void MarkdownYamlBlocks_ClassificationAndLayerFragmentsValidateAgainstSchema(string relativePath)
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string markdownPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string markdown = File.ReadAllText(markdownPath);

        List<string> yamlBlocks = ExtractYamlCodeBlocks(markdown);
        Assert.That(yamlBlocks, Is.Not.Empty, $"{relativePath} is expected to contain at least one ```yaml code block.");

        var failures = new List<string>();

        for (int blockIndex = 0; blockIndex < yamlBlocks.Count; blockIndex++)
        {
            JsonNode? instance = ToJsonNode(yamlBlocks[blockIndex]);
            if (instance is not JsonObject root)
            {
                continue;
            }

            if (root.TryGetPropertyValue("classification", out JsonNode? classification) && classification is not null)
            {
                CollectFailures(classification, "classification", $"block {blockIndex}", failures);
            }

            if (root.TryGetPropertyValue("layers", out JsonNode? layers) && layers is JsonObject layerMap)
            {
                foreach ((string layerName, JsonNode? layer) in layerMap)
                {
                    if (layer is not null)
                    {
                        CollectFailures(layer, "layer", $"block {blockIndex}, layer '{layerName}'", failures);
                    }
                }
            }

            if (root.TryGetPropertyValue("contracts", out JsonNode? contracts) && contracts is JsonObject contractGroups)
            {
                ValidateContractGroup(contractGroups, "strict_context_dependencies", "contextDependencyContract", $"block {blockIndex}", failures);
                ValidateContractGroup(contractGroups, "audit_context_dependencies", "contextDependencyContract", $"block {blockIndex}", failures);
                ValidateContractGroup(contractGroups, "strict_context_allow_only", "contextAllowOnlyContract", $"block {blockIndex}", failures);
                ValidateContractGroup(contractGroups, "audit_context_allow_only", "contextAllowOnlyContract", $"block {blockIndex}", failures);
                ValidateContractGroup(contractGroups, "strict_coverage", "coverageContract", $"block {blockIndex}", failures);
                ValidateContractGroup(contractGroups, "audit_coverage", "coverageContract", $"block {blockIndex}", failures);
            }
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
    }

    private static void ValidateContractGroup(
        JsonObject contractGroups,
        string groupName,
        string definitionName,
        string location,
        List<string> failures)
    {
        if (!contractGroups.TryGetPropertyValue(groupName, out JsonNode? group))
        {
            return;
        }

        if (group is not JsonArray contracts)
        {
            failures.Add($"{location}, contracts.{groupName}: expected an array of contract definitions.");
            return;
        }

        for (int contractIndex = 0; contractIndex < contracts.Count; contractIndex++)
        {
            if (contracts[contractIndex] is JsonNode contract)
            {
                CollectFailures(contract, definitionName, $"{location}, contracts.{groupName}[{contractIndex}]", failures);
            }
        }
    }

    [Test]
    public void MarkdownYamlBlocks_ReportsContextualContractGroupWithNonArrayValue()
    {
        JsonObject contracts = (JsonObject)ToJsonNode("""
            strict_context_dependencies:
              name: invalid-contract-container
            """)!;
        var failures = new List<string>();

        ValidateContractGroup(
            contracts,
            "strict_context_dependencies",
            "contextDependencyContract",
            "test block",
            failures);

        Assert.That(failures, Has.One.Contains("expected an array of contract definitions"));
    }

    private static void CollectFailures(JsonNode instance, string defName, string location, List<string> failures)
    {
        JsonSchema subSchema = LoadSubSchema(defName);
        EvaluationResults results = subSchema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (!results.IsValid)
        {
            string errors = string.Join(';', results.Details.Where(d => !d.IsValid).SelectMany(d => d.Errors?.Values ?? []));
            failures.Add($"{location} ($defs/{defName}): {errors}");
        }
    }

    private static List<string> ExtractYamlCodeBlocks(string markdown)
    {
        var blocks = new List<string>();
        string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
        var current = new List<string>();
        bool inYamlBlock = false;

        foreach (string line in lines)
        {
            if (!inYamlBlock && line.Trim() == "```yaml")
            {
                inYamlBlock = true;
                current.Clear();
                continue;
            }

            if (inYamlBlock && line.Trim() == "```")
            {
                inYamlBlock = false;
                blocks.Add(string.Join('\n', current));
                continue;
            }

            if (inYamlBlock)
            {
                current.Add(line);
            }
        }

        return blocks;
    }
}
