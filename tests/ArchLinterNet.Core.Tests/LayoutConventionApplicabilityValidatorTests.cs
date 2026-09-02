using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayoutConventionApplicabilityValidatorTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-layout-applicability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public void Load_ApplicabilityInventory_UnknownSameModeConvention_Throws()
    {
        string path = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Test]
              source_roots: [src]
            contracts:
              strict_layout_conventions:
                - id: services
                  name: services
                  files_matching:
                    folder_segment: Services
                  require_type_kind: class
              strict_layout_convention_applicability:
                - id: inventory
                  name: inventory
                  scope: src
                  expected_folders:
                    - id: services
                      path: Services
                      convention_id: absent
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain("unknown same-mode layout convention id 'absent'"));
    }

    [Test]
    public void Load_ApplicabilityInventory_ScopeOutsideSourceRoots_Throws()
    {
        string path = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Test]
              source_roots: [src]
            contracts:
              strict_layout_conventions:
                - id: services
                  name: services
                  files_matching:
                    folder_segment: Services
                  require_type_kind: class
              strict_layout_convention_applicability:
                - id: inventory
                  name: inventory
                  scope: tests
                  expected_folders:
                    - id: services
                      path: Services
                      convention_id: services
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain("must be under a configured analysis.source_roots entry"));
    }

    [Test]
    public void Load_ApplicabilityInventory_UnknownFolderProperty_Throws()
    {
        string path = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Test]
              source_roots: [src]
            contracts:
              strict_layout_conventions:
                - id: services
                  name: services
                  files_matching:
                    folder_segment: Services
                  require_type_kind: class
              strict_layout_convention_applicability:
                - id: inventory
                  name: inventory
                  scope: src
                  expected_folders:
                    - id: services
                      path: Services
                      convention: services
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(path))!;

        Assert.That(exception.Message, Does.Contain("unknown property 'convention'"));
    }

    [Test]
    public void Load_ComposedApplicabilityInventory_AtConfiguredRepositoryRoot_PassesSchemaValidation()
    {
        string path = WritePolicy("""
            version: 1
            name: Test
            imports: [fragment.yml]
            layers: {}
            analysis:
              target_assemblies: [Test]
              source_roots: [.]
            contracts:
              strict_layout_conventions:
                - id: services
                  name: services
                  files_matching:
                    folder_segment: Services
                  require_type_kind: class
              strict_layout_convention_applicability:
                - id: inventory
                  name: inventory
                  scope: .
                  expected_folders:
                    - id: root
                      path: .
                      convention_id: services
            """);
        File.WriteAllText(Path.Combine(_tempDir, "fragment.yml"), "contracts: {}\n");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.StrictLayoutConventionApplicability.Single().Scope, Is.EqualTo("."));
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }
}
