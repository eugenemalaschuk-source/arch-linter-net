using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayoutConventionsValidatorTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-layout-validator-test-{Guid.NewGuid():N}");
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

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string AssemblyName => typeof(LayoutConventionsValidatorTests).Assembly.GetName().Name!;

    [Test]
    public void Load_LayoutConventionContract_NoSelectorField_Throws()
    {
        string path = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_layout_conventions:
                - name: empty-selector
                  files_matching:
                    folder_segment: ""
                  require_type_kind: class
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path));
        Assert.That(exception!.Message, Does.Contain("no usable files_matching selector field"));
    }

    [Test]
    public void Load_LayoutConventionContract_NoExpectationField_Throws()
    {
        string path = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_layout_conventions:
                - name: no-expectation
                  files_matching:
                    folder_segment: Services
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path));
        Assert.That(exception!.Message, Does.Contain("no"));
        Assert.That(exception.Message, Does.Contain("expectation"));
    }

    [Test]
    public void Load_LayoutConventionContract_UnrecognizedTypeKind_Throws()
    {
        string path = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_layout_conventions:
                - name: bad-kind
                  files_matching:
                    folder_segment: Services
                  require_type_kind: not_a_real_kind
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path));
        Assert.That(exception!.Message, Does.Contain("not a recognized type kind"));
    }

    [Test]
    public void Load_LayoutConventionContract_InvalidWhenExpression_Throws()
    {
        string path = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_layout_conventions:
                - name: bad-when
                  files_matching:
                    folder_segment: Services
                    when: "subject.notARealMember"
                  require_type_kind: class
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path));
        Assert.That(exception!.Message, Does.Contain("failed to compile"));
    }

    [Test]
    public void Load_LayoutConventionContract_WhenOnNonFilesMatchingLocation_Throws()
    {
        string path = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_layout_conventions:
                - name: when-in-wrong-place
                  files_matching:
                    folder_segment: Services
                  require_type_kind: class
                  when: "true"
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path));
        Assert.That(exception!.Message, Does.Contain("approved expression locations"));
    }

    [Test]
    public void Load_LayoutConventionContract_TypoFolderSegmentsField_Throws()
    {
        string path = WritePolicy($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{AssemblyName}]
            contracts:
              strict_layout_conventions:
                - name: typo-field
                  files_matching:
                    folder_segments: Services
                  require_type_kind: class
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path));
        Assert.That(exception!.Message, Does.Contain("unknown property"));
    }
}
