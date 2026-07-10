using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Lightweight regression coverage for schema/dependencies.arch.schema.json using System.Text.Json
// (no new package dependency): asserts the assembly_dependency/assembly_allow_only contract groups
// and their dependency_depth "direct"-only enum are present, so a future schema edit can't silently
// drop them without a test noticing.
[TestFixture]
public sealed class ArchitectureContractSchemaTests
{
    private static JsonElement LoadSchema()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string schemaPath = Path.Combine(repositoryRoot, "schema", "dependencies.arch.schema.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        return document.RootElement.Clone();
    }

    [Test]
    public void Schema_IsValidJson()
    {
        Assert.DoesNotThrow(() => LoadSchema());
    }

    [TestCase("strict_assembly_dependency")]
    [TestCase("audit_assembly_dependency")]
    [TestCase("strict_assembly_allow_only")]
    [TestCase("audit_assembly_allow_only")]
    public void Schema_ContractsSection_DeclaresAssemblyDependencyFamilies(string propertyName)
    {
        JsonElement schema = LoadSchema();
        JsonElement contracts = schema.GetProperty("$defs").GetProperty("contracts").GetProperty("properties");

        Assert.That(contracts.TryGetProperty(propertyName, out _), Is.True,
            $"contracts.{propertyName} must be declared in the schema's contracts.properties.");
    }

    [TestCase("assemblyDependencyContract")]
    [TestCase("assemblyAllowOnlyContract")]
    public void Schema_AssemblyContractDefs_RestrictDependencyDepthToDirectOnly(string defName)
    {
        JsonElement schema = LoadSchema();
        JsonElement dependencyDepth = schema.GetProperty("$defs").GetProperty(defName).GetProperty("properties").GetProperty("dependency_depth");

        string[] enumValues = dependencyDepth.GetProperty("enum").EnumerateArray().Select(v => v.GetString()!).ToArray();

        Assert.That(enumValues, Is.EqualTo(new[] { "direct" }),
            $"{defName}.dependency_depth must only accept 'direct' until transitive assembly-reference-path resolution ships.");
        Assert.That(dependencyDepth.GetProperty("default").GetString(), Is.EqualTo("direct"));
    }

    [Test]
    public void Schema_AssemblyDependencyContract_RequiresNameSourceAndForbidden()
    {
        JsonElement schema = LoadSchema();
        JsonElement required = schema.GetProperty("$defs").GetProperty("assemblyDependencyContract").GetProperty("required");

        Assert.That(required.EnumerateArray().Select(v => v.GetString()), Is.EquivalentTo(new[] { "name", "source", "forbidden" }));
    }

    [Test]
    public void Schema_AssemblyAllowOnlyContract_RequiresNameSourceAndAllowed()
    {
        JsonElement schema = LoadSchema();
        JsonElement required = schema.GetProperty("$defs").GetProperty("assemblyAllowOnlyContract").GetProperty("required");

        Assert.That(required.EnumerateArray().Select(v => v.GetString()), Is.EquivalentTo(new[] { "name", "source", "allowed" }));
    }

    [Test]
    public void Schema_ProjectMetadataPropertyMaps_AcceptScalarValues()
    {
        JsonElement schema = LoadSchema();
        JsonElement projectMetadata = schema.GetProperty("$defs").GetProperty("projectMetadataContract");
        JsonElement properties = projectMetadata.GetProperty("allOf")[1].GetProperty("properties");

        Assert.That(properties.GetProperty("required_properties").GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/scalarMap"));
        Assert.That(properties.GetProperty("forbidden_properties").GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/scalarMap"));

        JsonElement scalarValueAnyOf = schema.GetProperty("$defs").GetProperty("scalarValue").GetProperty("anyOf");
        string[] acceptedTypes = scalarValueAnyOf.EnumerateArray()
            .Select(option => option.GetProperty("type").GetString()!)
            .ToArray();

        Assert.That(acceptedTypes, Is.SupersetOf(new[] { "string", "boolean", "number" }));
    }

    [Test]
    public void Schema_ProjectMetadataExpectations_RequireNonEmptyValues()
    {
        JsonElement schema = LoadSchema();
        JsonElement anyOf = schema.GetProperty("$defs")
            .GetProperty("projectMetadataContract")
            .GetProperty("allOf")[1]
            .GetProperty("anyOf");

        Assert.That(anyOf[0].GetProperty("properties").GetProperty("required_properties").GetProperty("minProperties").GetInt32(), Is.EqualTo(1));
        Assert.That(anyOf[1].GetProperty("properties").GetProperty("forbidden_properties").GetProperty("minProperties").GetInt32(), Is.EqualTo(1));
        Assert.That(anyOf[2].GetProperty("required")[0].GetString(), Is.EqualTo("allowed_friend_assemblies"));
        Assert.That(anyOf[3].GetProperty("properties").GetProperty("forbidden_project_references").GetProperty("minItems").GetInt32(), Is.EqualTo(1));
    }

    // Regression coverage for the semantic-classification-model design
    // (openspec/changes/design-semantic-classification-model): schema acceptance only,
    // no extraction/matching engine exists yet. See design.md for the reviewed shape.
    [Test]
    public void Schema_Root_DeclaresClassificationSection()
    {
        JsonElement schema = LoadSchema();
        JsonElement rootProperties = schema.GetProperty("properties");

        Assert.That(rootProperties.TryGetProperty("classification", out JsonElement classificationRef), Is.True);
        Assert.That(classificationRef.GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/classification"));
    }

    [TestCase("attributeClassificationEntry")]
    [TestCase("assemblyAttributeClassificationEntry")]
    [TestCase("inheritanceClassificationEntry")]
    [TestCase("namespaceClassificationEntry")]
    [TestCase("pathClassificationEntry")]
    [TestCase("classificationOverride")]
    [TestCase("classificationExclusion")]
    [TestCase("selector")]
    public void Schema_DeclaresClassificationSubShapes(string defName)
    {
        JsonElement schema = LoadSchema();
        Assert.That(schema.GetProperty("$defs").TryGetProperty(defName, out _), Is.True,
            $"$defs.{defName} must be declared in the schema.");
    }

    [Test]
    public void Schema_Classification_PrecedenceIsFixedSixSourceEnum()
    {
        JsonElement schema = LoadSchema();
        JsonElement precedence = schema.GetProperty("$defs").GetProperty("classification").GetProperty("properties").GetProperty("precedence");
        JsonElement itemEnum = precedence.GetProperty("items").GetProperty("enum");

        string[] enumValues = itemEnum.EnumerateArray().Select(v => v.GetString()!).ToArray();

        Assert.That(enumValues, Is.EqualTo(new[]
        {
            "yaml_override", "type_attribute", "assembly_attribute", "inheritance", "namespace", "path"
        }));
    }

    [Test]
    public void Schema_Layer_AcceptsSelectorAsAlternativeToNamespace()
    {
        JsonElement schema = LoadSchema();
        JsonElement layer = schema.GetProperty("$defs").GetProperty("layer");

        Assert.That(layer.GetProperty("properties").TryGetProperty("selector", out JsonElement selectorProperty), Is.True);
        Assert.That(selectorProperty.GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/selector"));

        JsonElement anyOf = layer.GetProperty("anyOf");
        Assert.That(anyOf[0].GetProperty("required")[0].GetString(), Is.EqualTo("namespace"));
        Assert.That(anyOf[1].GetProperty("required")[0].GetString(), Is.EqualTo("selector"));
    }

    [Test]
    public void Schema_ClassificationExclusion_AlwaysRequiresReason()
    {
        JsonElement schema = LoadSchema();
        JsonElement exclusion = schema.GetProperty("$defs").GetProperty("classificationExclusion");

        JsonElement required = exclusion.GetProperty("required");
        Assert.That(required.EnumerateArray().Select(v => v.GetString()), Contains.Item("reason"));
    }

    [Test]
    public void Schema_ClassificationOverride_RequiresReasonOnlyForBroadScope()
    {
        JsonElement schema = LoadSchema();
        JsonElement classificationOverride = schema.GetProperty("$defs").GetProperty("classificationOverride");

        JsonElement anyOf = classificationOverride.GetProperty("anyOf");
        Assert.That(anyOf[0].GetProperty("required").EnumerateArray().Select(v => v.GetString()),
            Is.EquivalentTo(new[] { "type" }),
            "A type-scoped override must not require 'reason'.");
        Assert.That(anyOf[1].GetProperty("required").EnumerateArray().Select(v => v.GetString()),
            Is.EquivalentTo(new[] { "namespace", "reason" }),
            "A namespace-scoped override must require 'reason'.");
        Assert.That(anyOf[2].GetProperty("required").EnumerateArray().Select(v => v.GetString()),
            Is.EquivalentTo(new[] { "namespace_suffix", "reason" }),
            "A namespace_suffix-scoped override must require 'reason'.");
    }
}
