using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Resolution;
using Json.Schema;
using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Tests;

// Schema validation tests for the contextSelector/contextDependencyContract/contextAllowOnlyContract
// $defs added to schema/dependencies.arch.schema.json, per tasks.md 6.4. Mirrors the
// LoadSubSchema/Validate helper pattern in ArchitectureContractSchemaInstanceValidationTests.cs.
[TestFixture]
public sealed class ContextualContractSchemaTests
{
    private static string SchemaText()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string schemaPath = Path.Combine(repositoryRoot, "schema", "dependencies.arch.schema.json");
        return File.ReadAllText(schemaPath);
    }

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
            _ when int.TryParse(value, out int i) => i,
            _ when double.TryParse(value, out double d) => d,
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

    // --- contextSelector ---

    [Test]
    public void ContextSelector_RoleOnly_IsValid()
    {
        const string Yaml = "role: DomainLayer\n";

        Assert.That(Validate(Yaml, "contextSelector"), Is.True);
    }

    [Test]
    public void ContextSelector_MissingRole_IsRejected()
    {
        const string Yaml = "metadata:\n  domain: Sales\n";

        Assert.That(Validate(Yaml, "contextSelector"), Is.False);
    }

    [Test]
    public void ContextSelector_EmptyRole_IsRejected()
    {
        const string Yaml = "role: \"\"\n";

        Assert.That(Validate(Yaml, "contextSelector"), Is.False);
    }

    [Test]
    public void ContextSelector_UnknownProperty_IsRejected()
    {
        const string Yaml = "role: DomainLayer\nunknown: true\n";

        Assert.That(Validate(Yaml, "contextSelector"), Is.False);
    }

    [TestCase("metadata:\n  domain: Sales", true, TestName = "ExactScalar")]
    [TestCase("metadata:\n  domain: \"*\"", true, TestName = "AnyWildcard")]
    [TestCase("metadata:\n  domain: [Sales, Inventory]", true, TestName = "InList")]
    [TestCase("metadata:\n  domain: \"!{source.metadata.domain}\"", true, TestName = "NotEqualToSource")]
    // A string that does not match the not-equal-to-source pattern still falls through to the
    // "any other scalar -> exact literal" form, per the grammar's fixed-order fallback - it is a
    // valid (if unusual) exact-match literal, not a schema rejection.
    [TestCase("metadata:\n  domain: \"!{source.metadata}\"", true, TestName = "MalformedNotEqualToSourcePatternIsTreatedAsExactLiteral")]
    [TestCase("metadata:\n  domain: null", false, TestName = "NullValueRejected")]
    public void ContextSelector_MetadataValueForms(string metadataYaml, bool expectedValid)
    {
        string yaml = $"role: DomainLayer\n{metadataYaml}\n";

        Assert.That(Validate(yaml, "contextSelector"), Is.EqualTo(expectedValid));
    }

    // --- contextDependencyContract ---

    [Test]
    public void ContextDependencyContract_ValidDocument_IsAccepted()
    {
        const string Yaml = """
            name: sales-must-not-depend-on-inventory
            id: sales-no-inventory
            source:
              role: DomainLayer
              metadata:
                domain: Sales
            forbidden:
              - role: DomainLayer
                metadata:
                  domain: Inventory
            exclude:
              - role: SharedKernel
            reason: Bounded contexts must not depend on each other.
            """;

        Assert.That(Validate(Yaml, "contextDependencyContract"), Is.True);
    }

    [Test]
    public void ContextDependencyContract_SourceSelectorWithoutRole_IsRejected()
    {
        const string Yaml = """
            name: sales-must-not-depend-on-inventory
            source:
              metadata:
                domain: Sales
            forbidden:
              - role: DomainLayer
                metadata:
                  domain: Inventory
            """;

        Assert.That(Validate(Yaml, "contextDependencyContract"), Is.False);
    }

    [Test]
    public void ContextDependencyContract_ForbiddenSelectorWithoutRole_IsRejected()
    {
        const string Yaml = """
            name: sales-must-not-depend-on-inventory
            source:
              role: DomainLayer
            forbidden:
              - metadata:
                  domain: Inventory
            """;

        Assert.That(Validate(Yaml, "contextDependencyContract"), Is.False);
    }

    [Test]
    public void ContextDependencyContract_ExcludeSelectorWithoutRole_IsRejected()
    {
        const string Yaml = """
            name: sales-must-not-depend-on-inventory
            source:
              role: DomainLayer
            forbidden:
              - role: DomainLayer
            exclude:
              - metadata:
                  domain: SharedKernel
            """;

        Assert.That(Validate(Yaml, "contextDependencyContract"), Is.False);
    }

    [Test]
    public void ContextDependencyContract_MissingRequiredForbidden_IsRejected()
    {
        const string Yaml = """
            name: sales-must-not-depend-on-inventory
            source:
              role: DomainLayer
            """;

        Assert.That(Validate(Yaml, "contextDependencyContract"), Is.False);
    }

    [Test]
    public void ContextDependencyContract_EmptyForbiddenList_IsRejected()
    {
        const string Yaml = """
            name: sales-must-not-depend-on-inventory
            source:
              role: DomainLayer
            forbidden: []
            """;

        Assert.That(Validate(Yaml, "contextDependencyContract"), Is.False);
    }

    // --- contextAllowOnlyContract ---

    [Test]
    public void ContextAllowOnlyContract_ValidDocument_IsAccepted()
    {
        const string Yaml = """
            name: sales-allow-only
            source:
              role: DomainLayer
              metadata:
                domain: Sales
            allowed:
              - role: DomainLayer
                metadata:
                  domain: Sales
              - role: SharedKernel
            reason: Sales may depend only on its own context or the shared kernel.
            """;

        Assert.That(Validate(Yaml, "contextAllowOnlyContract"), Is.True);
    }

    [Test]
    public void ContextAllowOnlyContract_SourceSelectorWithoutRole_IsRejected()
    {
        const string Yaml = """
            name: sales-allow-only
            source:
              metadata:
                domain: Sales
            allowed:
              - role: DomainLayer
            """;

        Assert.That(Validate(Yaml, "contextAllowOnlyContract"), Is.False);
    }

    [Test]
    public void ContextAllowOnlyContract_AllowedSelectorWithoutRole_IsRejected()
    {
        const string Yaml = """
            name: sales-allow-only
            source:
              role: DomainLayer
            allowed:
              - metadata:
                  domain: Sales
            """;

        Assert.That(Validate(Yaml, "contextAllowOnlyContract"), Is.False);
    }

    [Test]
    public void ContextAllowOnlyContract_ExcludeSelectorWithoutRole_IsRejected()
    {
        const string Yaml = """
            name: sales-allow-only
            source:
              role: DomainLayer
            allowed:
              - role: DomainLayer
            exclude:
              - metadata:
                  domain: SharedKernel
            """;

        Assert.That(Validate(Yaml, "contextAllowOnlyContract"), Is.False);
    }

    [Test]
    public void ContextAllowOnlyContract_MissingRequiredAllowed_IsRejected()
    {
        const string Yaml = """
            name: sales-allow-only
            source:
              role: DomainLayer
            """;

        Assert.That(Validate(Yaml, "contextAllowOnlyContract"), Is.False);
    }

    // --- top-level contracts.* properties exist on the full document schema ---

    [TestCase("strict_context_dependencies")]
    [TestCase("audit_context_dependencies")]
    [TestCase("strict_context_allow_only")]
    [TestCase("audit_context_allow_only")]
    public void ContractsSchema_DeclaresNewFamilyProperty(string propertyName)
    {
        using JsonDocument document = JsonDocument.Parse(SchemaText());
        JsonElement contracts = document.RootElement.GetProperty("$defs").GetProperty("contracts").GetProperty("properties");

        Assert.That(contracts.TryGetProperty(propertyName, out _), Is.True);
    }
}
