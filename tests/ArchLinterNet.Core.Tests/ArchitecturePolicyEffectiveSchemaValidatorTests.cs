using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyEffectiveSchemaValidatorTests
{
    [Test]
    public void Validate_SchemaFailure_UsesClosestProvenanceLocation()
    {
        const string Yaml = """
            version: 1
            name: Example
            layers:
              domain:
                namespace: App.Domain
            analysis:
              target_assemblies: [App]
            contracts:
              strict:
                - source: domain
                  forbidden: [application]
            """;
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance("contracts.strict");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;
        ArchitecturePolicySourceLocation location = exception.Diagnostic!.Location!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.SourceShape));
            Assert.That(location.SourcePath, Is.EqualTo("architecture/fragments/contracts.yml"));
            Assert.That(location.YamlPath, Is.EqualTo("contracts.strict"));
        });
    }

    [Test]
    public void Validate_NonStringContractId_UsesItsExactProvenanceLocation()
    {
        const string Yaml = """
            version: 1
            name: Example
            layers:
              domain:
                namespace: App.Domain
            analysis:
              target_assemblies: [App]
            contracts:
              strict:
                - id: 42
                  name: invalid-id
                  source: domain
                  forbidden: [application]
            """;
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance("contracts.strict[0].id");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;
        ArchitecturePolicySourceLocation location = exception.Diagnostic!.Location!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("non-empty string"));
            Assert.That(location.YamlPath, Is.EqualTo("contracts.strict[0].id"));
            Assert.That(location.SourcePath, Is.EqualTo("architecture/fragments/contracts.yml"));
        });
    }

    [Test]
    public void Validate_ExplicitStringContractId_RemainsValid()
    {
        const string Yaml = """
            version: 1
            name: Example
            layers:
              domain:
                namespace: App.Domain
            analysis:
              target_assemblies: [App]
            contracts:
              strict:
                - id: valid-id
                  name: valid-id
                  source: domain
                  forbidden: [application]
            """;

        Assert.DoesNotThrow(() =>
            ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, CreateProvenance("contracts.strict[0].id")));
    }

    private static ArchitecturePolicyProvenanceIndex CreateProvenance(params string[] paths)
    {
        var source = new ArchitecturePolicySourceDescriptor(
            "architecture/root.yml", "architecture/fragments/contracts.yml", ArchitecturePolicyDocumentRole.Fragment,
            1, "architecture/root.yml", "fragments/contracts.yml",
            ["architecture/root.yml", "architecture/fragments/contracts.yml"]);
        var nodes = new Dictionary<string, ArchitecturePolicySourceLocation>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            nodes[path] = new ArchitecturePolicySourceLocation(source, path, 4, 3, null, null);
        }

        return new ArchitecturePolicyProvenanceIndex([source], nodes);
    }
}
