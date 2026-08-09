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
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(("/contracts/strict", "contracts.strict"));

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
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(("/contracts/strict/0/id", "contracts.strict[0].id"));

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
            ArchitecturePolicyEffectiveSchemaValidator.Validate(
                Yaml,
                CreateProvenance(("/contracts/strict/0/id", "contracts.strict[0].id"))));
    }

    [Test]
    public void Validate_InvalidNamespaceLayer_ReportsTheScalarFailureWithoutSelectorAlternativeNoise()
    {
        const string Yaml = """
            version: 1
            name: Example
            layers:
              domain:
                namespace: [App.Domain]
            analysis:
              target_assemblies: [App]
            contracts:
              strict: []
            """;
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(
            ("/layers/domain/namespace", "layers.domain.namespace"));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("/layers/domain/namespace"));
            Assert.That(exception.Message, Does.Not.Contain("selector"));
            Assert.That(exception.Diagnostic!.Location!.YamlPath, Is.EqualTo("layers.domain.namespace"));
        });
    }

    [Test]
    public void Validate_InvalidDiscriminatedCoverageContract_ReportsSelectedVariantDeterministically()
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
              strict_coverage:
                - name: projects
                  scope: project
                  exclude: invalid
            """;
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(
            ("/contracts/strict_coverage/0/exclude", "contracts.strict_coverage[0].exclude"));

        ArchitecturePolicyImportException first = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;
        ArchitecturePolicyImportException second = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;

        Assert.Multiple(() =>
        {
            Assert.That(first.Message, Does.Contain("/contracts/strict_coverage/0/exclude"));
            Assert.That(first.Message, Is.EqualTo(second.Message));
            Assert.That(first.Diagnostic!.Location!.YamlPath, Is.EqualTo("contracts.strict_coverage[0].exclude"));
            Assert.That(second.Diagnostic!.Location, Is.EqualTo(first.Diagnostic.Location));
        });
    }

    [Test]
    public void Validate_ValidNamespaceLayer_DoesNotRequireSelector()
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
              strict: []
            """;

        Assert.DoesNotThrow(() =>
            ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, CreateProvenance()));
    }

    [Test]
    public void Validate_NestedScalarMapFailure_ReportsTheMapValueInsteadOfAnyOfAlternatives()
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
              strict: []
            classification:
              namespace:
                - namespace: App.Domain
                  role: domain
                  metadata:
                    bounded_context: [Sales]
            """;
        ArchitecturePolicyProvenanceIndex provenance = CreateProvenance(
            ("/classification/namespace/0/metadata/bounded_context", "classification.namespace[0].metadata.bounded_context"));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => ArchitecturePolicyEffectiveSchemaValidator.Validate(Yaml, provenance))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("/classification/namespace/0/metadata/bounded_context"));
            Assert.That(exception.Message, Does.Not.Contain("anyOf"));
            Assert.That(exception.Diagnostic!.Location!.YamlPath,
                Is.EqualTo("classification.namespace[0].metadata.bounded_context"));
        });
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
