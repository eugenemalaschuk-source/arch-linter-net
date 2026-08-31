using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSurfaceExposurePolicyTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-contract-surface-exposure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public void Load_ValidStrictAndAuditControls_BindsAllBoundedFields()
    {
        string path = WritePolicy($"""
            version: 1
            name: Contract surface exposure
            analysis:
              target_assemblies: [{TestAssemblyName()}]
            contracts:
              strict_contract_surface_exposure:
                - id: strict-exposure
                  name: Strict exposure
                  source:
                    assemblies: [{TestAssemblyName()}]
                    types_matching:
                      role: ApiContract
                  forbidden:
                    - namespace: Example.Domain
                      name_suffix: Entity
                  reason: Keep domain entities out of API contracts.
              audit_contract_surface_exposure:
                - id: audit-exposure
                  name: Audit exposure
                  source:
                    projects:
                      - src/Example.Api/Example.Api.csproj
                  forbidden:
                    - has_attribute: Example.ForbiddenAttribute
                  ignored_violations: []
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictContractSurfaceExposure, Has.Count.EqualTo(1));
            Assert.That(document.Contracts.AuditContractSurfaceExposure, Has.Count.EqualTo(1));
            Assert.That(document.Contracts.StrictContractSurfaceExposure[0].Id, Is.EqualTo("strict-exposure"));
            Assert.That(document.Contracts.StrictContractSurfaceExposure[0].Source.Assemblies,
                Is.EqualTo(new[] { TestAssemblyName() }));
            Assert.That(document.Contracts.StrictContractSurfaceExposure[0].Source.TypesMatching!.Role,
                Is.EqualTo("ApiContract"));
            Assert.That(document.Contracts.StrictContractSurfaceExposure[0].Forbidden[0].Namespace,
                Is.EqualTo("Example.Domain"));
            Assert.That(document.Contracts.AuditContractSurfaceExposure[0].Source.Projects,
                Is.EqualTo(new[] { "src/Example.Api/Example.Api.csproj" }));
        });
    }

    [TestCase("id: missing-source\nname: Missing source\nsource: {}\nforbidden:\n  - role: Entity\n", "no usable source selector")]
    [TestCase("id: empty-forbidden\nname: Empty forbidden\nsource:\n  assemblies: [Example.Api]\nforbidden: []\n", "non-empty 'forbidden'")]
    [TestCase("id: empty-selector\nname: Empty selector\nsource:\n  types_matching: {}\nforbidden:\n  - role: Entity\n", "empty or unbounded")]
    [TestCase("id: unknown-selector\nname: Unknown selector\nsource:\n  types_matching:\n    regex: Entity\nforbidden:\n  - role: Entity\n", "unknown property 'regex'")]
    [TestCase("id: blank-assembly\nname: Blank assembly\nsource:\n  assemblies: [' ']\nforbidden:\n  - role: Entity\n", "blank")]
    [TestCase("name: missing-id\nsource:\n  assemblies: [Example.Api]\nforbidden:\n  - role: Entity\n", "non-blank 'id'")]
    public void Load_MalformedControl_FailsClosed(string contractYaml, string expectedMessage)
    {
        string path = WritePolicy($"""
            version: 1
            name: Invalid contract surface exposure
            analysis:
              target_assemblies: [Example.Api]
            contracts:
              strict_contract_surface_exposure:
                - {contractYaml.Replace("\n", "\n      ")}
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [Test]
    public void Load_UnknownReferencedPublicApiSurface_FailsClosed()
    {
        string path = WritePolicy($"""
            version: 1
            name: Invalid public API reference
            analysis:
              target_assemblies: [{TestAssemblyName()}]
            contracts:
              strict_contract_surface_exposure:
                - id: exposure
                  name: Exposure
                  source:
                    public_api_surface: does-not-exist
                  forbidden:
                    - role: Entity
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain("unknown public API surface"));
    }

    [Test]
    public void Load_PublicApiReference_IsAcceptedWhenIdentityIsUnambiguous()
    {
        string path = WritePolicy($"""
            version: 1
            name: Referenced public API
            analysis:
              target_assemblies: [{TestAssemblyName()}]
            contracts:
              strict_public_api_surface:
                - id: reviewed-api
                  name: Reviewed API
                  assemblies: [{TestAssemblyName()}]
              strict_contract_surface_exposure:
                - id: exposure
                  name: Exposure
                  source:
                    public_api_surface: reviewed-api
                  forbidden:
                    - role: Entity
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.StrictContractSurfaceExposure.Single().Source.PublicApiSurface,
            Is.EqualTo("reviewed-api"));
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string TestAssemblyName() => typeof(ContractSurfaceExposurePolicyTests).Assembly.GetName().Name!;
}
