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
}
