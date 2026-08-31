using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class VersionedContractSurfaceIsolationPolicyTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"arch-linter-versioned-isolation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() =>
        Directory.Delete(_directory, recursive: true);

    [Test]
    public void Load_ValidStrictAndAuditControls_BindsAllFields()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Versioned isolation
            analysis:
              target_assemblies: []
            contracts:
              strict_versioned_contract_surface_isolation:
                - id: strict-isolation
                  name: Strict isolation
                  surfaces:
                    - id: api-v1
                      types_matching:
                        namespace: Product.Api.V1
                    - id: domain-v1
                      types_matching:
                        role: Entity
                  source_surface: api-v1
                  forbidden_surfaces: [domain-v1]
                  reason: Keep versions isolated.
              audit_versioned_contract_surface_isolation:
                - id: audit-isolation
                  name: Audit isolation
                  surfaces:
                    - id: api-v2
                      types_matching:
                        name_suffix: Contract
                    - id: implementation-v2
                      types_matching:
                        implements_interface: Product.Api.IV2
                  source_surface: api-v2
                  forbidden_surfaces: [implementation-v2]
                  ignored_violations: []
            """);

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictVersionedContractSurfaceIsolation, Has.Count.EqualTo(1));
            Assert.That(document.Contracts.AuditVersionedContractSurfaceIsolation, Has.Count.EqualTo(1));
            Assert.That(document.Contracts.StrictVersionedContractSurfaceIsolation[0].Surfaces[0].TypesMatching.Namespace,
                Is.EqualTo("Product.Api.V1"));
            Assert.That(document.Contracts.AuditVersionedContractSurfaceIsolation[0].ForbiddenSurfaces,
                Is.EqualTo(new[] { "implementation-v2" }));
        });
    }

    [TestCase("missing, domain-v1", "unknown source surface")]
    [TestCase("domain-v1, missing", "unknown forbidden surface")]
    [TestCase("api-v1, api-v1", "cannot forbid its source surface")]
    public void Load_InvalidSurfaceReferences_FailsClosed(string forbidden, string expected)
    {
        string source = forbidden.StartsWith("missing", StringComparison.OrdinalIgnoreCase) ? "missing" : "api-v1";
        string path = Write($"""
            version: 1
            name: Invalid isolation
            analysis:
              target_assemblies: []
            contracts:
              strict_versioned_contract_surface_isolation:
                - id: isolation
                  name: Isolation
                  surfaces:
                    - id: api-v1
                      types_matching:
                        role: ApiContract
                    - id: domain-v1
                      types_matching:
                        role: Entity
                  source_surface: {source}
                  forbidden_surfaces: [{forbidden}]
            """);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path))!;
        Assert.That(error.Message, Does.Contain(expected));
    }

    [TestCase("api-v1", "duplicate surface ID")]
    [TestCase("API-V1", "duplicate surface ID")]
    public void Load_DuplicateSurfaceIdsIncludingCaseOnly_FailsClosed(string secondId, string expected)
    {
        string path = Write($"""
            version: 1
            name: Duplicate surfaces
            analysis:
              target_assemblies: []
            contracts:
              strict_versioned_contract_surface_isolation:
                - id: isolation
                  name: Isolation
                  surfaces:
                    - id: api-v1
                      types_matching:
                        role: ApiContract
                    - id: {secondId}
                      types_matching:
                        role: Entity
                  source_surface: api-v1
                  forbidden_surfaces: [domain-v1]
            """);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path))!;
        Assert.That(error.Message, Does.Contain(expected));
    }

    [TestCase("types_matching: {}", "empty or unbounded")]
    [TestCase("types_matching: { regex: Entity }", "unknown property 'regex'")]
    [TestCase("types_matching: { role: ' ' }", "blank")]
    public void Load_InvalidSelector_FailsClosed(string selector, string expected)
    {
        string path = Write($"""
            version: 1
            name: Invalid selector
            analysis:
              target_assemblies: []
            contracts:
              strict_versioned_contract_surface_isolation:
                - id: isolation
                  name: Isolation
                  surfaces:
                    - id: api-v1
                      {selector}
                    - id: domain-v1
                      types_matching:
                        role: Entity
                  source_surface: api-v1
                  forbidden_surfaces: [domain-v1]
            """);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path))!;
        Assert.That(error.Message, Does.Contain(expected));
    }

    private ArchitectureContractDocument Load(string yaml) => new ArchitecturePolicyDocumentLoader().Load(Write(yaml));

    private string Write(string yaml)
    {
        string path = Path.Combine(_directory, "policy.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }
}

[TestFixture]
public sealed class VersionedContractSurfaceIsolationSchemaTests
{
    [Test]
    public void Schema_DeclaresClosedVersionedIsolationContractShape()
    {
        string root = new ArchitectureRepositoryRootResolver().Resolve();
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "schema", "dependencies.arch.schema.json")));
        JsonElement defs = schema.RootElement.GetProperty("$defs");
        JsonElement contracts = defs.GetProperty("contracts").GetProperty("properties");
        JsonElement contract = defs.GetProperty("versionedContractSurfaceIsolationContract");
        JsonElement surface = defs.GetProperty("versionedContractSurfaceIsolationSurface");

        Assert.Multiple(() =>
        {
            Assert.That(contracts.GetProperty("strict_versioned_contract_surface_isolation").GetProperty("items")
                .GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/versionedContractSurfaceIsolationContract"));
            Assert.That(contracts.GetProperty("audit_versioned_contract_surface_isolation").GetProperty("items")
                .GetProperty("$ref").GetString(), Is.EqualTo("#/$defs/versionedContractSurfaceIsolationContract"));
            Assert.That(contract.GetProperty("unevaluatedProperties").GetBoolean(), Is.False);
            Assert.That(surface.GetProperty("additionalProperties").GetBoolean(), Is.False);
            Assert.That(surface.GetProperty("required").EnumerateArray().Select(v => v.GetString()),
                Is.EquivalentTo(["id", "types_matching"]));
            Assert.That(contract.GetProperty("allOf")[1].GetProperty("required").EnumerateArray().Select(v => v.GetString()),
                Is.EquivalentTo(["id", "name", "surfaces", "source_surface", "forbidden_surfaces"]));
        });
    }
}
