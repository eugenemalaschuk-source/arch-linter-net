using System.Text.Json.Nodes;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Regressions for issue #471: on a composed policy the effective-schema failure must locate the
/// authored defect and must not resurface branches of composites that ultimately succeeded.
/// </summary>
[TestFixture]
public sealed class ArchitecturePolicyEffectiveSchemaValidatorComposedTests
{
    // One authored defect, several sibling contracts whose discriminated variants all evaluate.
    private const string CoverageRootsYaml = """
        version: 1
        name: Example
        layers:
          domain:
            namespace: App.Domain
        analysis:
          solution: App.slnx
          target_assemblies: [App]
        contracts:
          strict_coverage:
            - id: assemblies
              name: assemblies
              scope: assembly
              roots: [App]
              reason: roots is invalid for assembly coverage.
            - id: projects
              name: projects
              scope: project
              reason: Every discovered project is classified.
          strict_project_metadata:
            - id: nullable
              name: nullable
              projects: [src/App/App.csproj]
              required_properties:
                Nullable: enable
              reason: Production projects opt into nullable reference types.
        """;

    [Test]
    public void Validate_ComposedCoverageRootsDefect_LocatesTheAuthoredRootsEntry()
    {
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(
            ("/contracts/strict_coverage/0/roots", "contracts.strict_coverage[0].roots"),
            ("/contracts/strict_project_metadata/0/required_properties/Nullable",
                "contracts.strict_project_metadata[0].required_properties.Nullable"));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(CoverageRootsYaml, provenance))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("'roots' is not valid for assembly coverage"));
            Assert.That(exception.Diagnostic!.Location!.YamlPath,
                Is.EqualTo("contracts.strict_coverage[0].roots"),
                "The reported location must describe the reported message.");
        });
    }

    [Test]
    public void Validate_IndependentDefect_DoesNotReportInapplicableDiscriminatorBranches()
    {
        const string Yaml = """
            version: 1
            name: Example
            layers:
              domain:
                namespace: 42
            analysis:
              solution: App.slnx
              target_assemblies: [App]
            contracts:
              strict_coverage:
                - id: assemblies
                  name: assemblies
                  scope: assembly
                  reason: Every discovered assembly is classified.
                - id: projects
                  name: projects
                  scope: project
                  reason: Every discovered project is classified.
            """;
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(
            ("/layers/domain/namespace", "layers.domain.namespace"));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("/layers/domain/namespace"));
            Assert.That(exception.Message, Does.Not.Contain("/scope:"),
                "A coverage scope whose constant discriminator selects another variant is not a defect.");
            Assert.That(exception.Diagnostic!.Location!.YamlPath, Is.EqualTo("layers.domain.namespace"));
        });
    }

    [Test]
    public void Validate_SatisfiedAnyOfAlternative_IsNotReportedAsAMissingRequirement()
    {
        const string Yaml = """
            version: 1
            name: Example
            layers:
              domain:
                namespace: App.Domain
            analysis:
              target_assemblies: [App]
              projects: [src/App/App.csproj]
            contracts:
              strict:
                - id: broken
                  name: broken
                  source: domain
                  forbidden: 42
            """;
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(
            ("/contracts/strict/0/forbidden", "contracts.strict[0].forbidden"));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("/contracts/strict/0/forbidden"));
            Assert.That(exception.Message, Does.Not.Contain("solution"),
                "analysis satisfies its anyOf through 'projects'; the 'solution' alternative is not a defect.");
            Assert.That(exception.Diagnostic!.Location!.YamlPath, Is.EqualTo("contracts.strict[0].forbidden"));
        });
    }

    [Test]
    public void Validate_ValidComposedPolicy_RemainsValid()
    {
        const string Yaml = """
            version: 1
            name: Example
            layers:
              domain:
                namespace: App.Domain
            analysis:
              solution: App.slnx
              target_assemblies: [App]
            contracts:
              strict_coverage:
                - id: assemblies
                  name: assemblies
                  scope: assembly
                  reason: Every discovered assembly is classified.
            """;

        Assert.That(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, CreateProvenance()),
            Throws.Nothing);
    }

    // The policy schema only reaches a few of these combinations through its own alternatives
    // (a scalar map value failing string/boolean/number/integer). The rest guard the same rule for
    // any future typed alternative, so the matrix is asserted directly.
    [TestCase("\"string\"", "\"App.Domain\"", false)]
    [TestCase("\"string\"", "true", true)]
    [TestCase("\"string\"", "7", true)]
    [TestCase("\"string\"", "7.5", true)]
    [TestCase("\"string\"", "{}", true)]
    [TestCase("\"string\"", "[]", true)]
    [TestCase("\"boolean\"", "true", false)]
    [TestCase("\"boolean\"", "\"App.Domain\"", true)]
    [TestCase("\"integer\"", "7", false)]
    [TestCase("\"integer\"", "7.5", true)]
    [TestCase("\"number\"", "7", false)]
    [TestCase("\"number\"", "7.5", false)]
    [TestCase("\"object\"", "{}", false)]
    [TestCase("\"object\"", "[]", true)]
    [TestCase("\"array\"", "[]", false)]
    [TestCase("\"array\"", "{}", true)]
    [TestCase("[\"string\", \"boolean\"]", "true", false)]
    [TestCase("[\"string\", \"boolean\"]", "7", true)]
    public void DeclaresIncompatibleType_MatchesTheInstanceJsonType(
        string declaredType, string instanceJson, bool expectedIncompatible)
    {
        var schema = (JsonObject)JsonNode.Parse($$"""{"type": {{declaredType}}}""")!;
        JsonNode instance = JsonNode.Parse(instanceJson)!;

        bool incompatible = ArchitecturePolicyEffectiveSchemaValidator.DeclaresIncompatibleType(schema, instance);

        Assert.That(incompatible, Is.EqualTo(expectedIncompatible));
    }

    [Test]
    public void DeclaresIncompatibleType_SchemaWithoutDeclaredType_NeverSuppressesAnAlternative()
    {
        var schema = (JsonObject)JsonNode.Parse("""{"required": ["name"]}""")!;

        Assert.That(
            ArchitecturePolicyEffectiveSchemaValidator.DeclaresIncompatibleType(schema, JsonNode.Parse("7")!),
            Is.False);
    }

    private static ArchitecturePolicyProvenanceIndex CreateProvenance(
        params (string EffectivePath, string YamlPath)[] paths)
    {
        var source = new ArchitecturePolicySourceDescriptor(
            "architecture/root.yml", "architecture/fragments/contracts.yml", ArchitecturePolicyDocumentRole.Fragment,
            1, "architecture/root.yml", "fragments/contracts.yml",
            ["architecture/root.yml", "architecture/fragments/contracts.yml"]);
        var nodes = new Dictionary<string, ArchitecturePolicySourceLocation>(StringComparer.Ordinal);
        foreach ((string effectivePath, string yamlPath) in paths)
        {
            nodes[effectivePath] = new ArchitecturePolicySourceLocation(source, yamlPath, 4, 3, null, null);
        }

        return new ArchitecturePolicyProvenanceIndex([source], nodes);
    }
}
