using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ExternalEvidencePolicyTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-external-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Load_ValidDeclaration_ExposesTypedRequirement()
    {
        ArchitectureContractDocument document = Load(MonolithicPolicy(ValidDeclaration()));

        ArchitectureExternalEvidenceRequirement requirement = document.ExternalEvidence.Single();
        Assert.Multiple(() =>
        {
            Assert.That(requirement.Id, Is.EqualTo("static-analysis"));
            Assert.That(requirement.Format, Is.EqualTo("sarif"));
            Assert.That(requirement.Required, Is.True);
            Assert.That(requirement.Tool, Is.EqualTo("Semgrep"));
            Assert.That(requirement.ToolVersion, Is.EqualTo("1.0"));
            Assert.That(requirement.Run, Is.EqualTo("security"));
            Assert.That(requirement.RequireRepository, Is.True);
            Assert.That(requirement.RequireRevision, Is.True);
            Assert.That(requirement.RequireScope, Is.False);
        });
    }

    [Test]
    public void Load_WithoutExternalEvidence_PreservesEmptyDefault()
    {
        ArchitectureContractDocument document = Load(MonolithicPolicy(string.Empty));

        Assert.That(document.ExternalEvidence, Is.Empty);
    }

    [Test]
    public void Load_DuplicateIds_IsRejected()
    {
        string declarations = ValidDeclaration() + Environment.NewLine + ValidDeclaration();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declarations)))!;

        Assert.That(exception.Message, Does.Contain("duplicate id 'static-analysis'"));
    }

    [TestCase("", "non-blank")]
    [TestCase("xml", "exactly 'sarif'")]
    public void Load_InvalidFormat_IsRejected(string format, string expectedMessage)
    {
        string declaration = ValidDeclaration().Replace("format: sarif", $"format: {format}", StringComparison.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [TestCase("id", "external_evidence[0].id must be a non-blank string.")]
    [TestCase("tool", "external_evidence[0].tool must be a non-blank string.")]
    [TestCase("run", "external_evidence[0].run must be a non-blank string.")]
    public void Load_BlankIdentity_IsRejected(string field, string expectedMessage)
    {
        string declaration = ValidDeclaration().Replace($"{field}: {FieldValue(field)}", $"{field}: '  '", StringComparison.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void Load_UnknownNestedProperty_IsRejectedWithDeclarationContext()
    {
        string declaration = ValidDeclaration() + Environment.NewLine + "    unexpected: true\n";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Is.EqualTo(
            "external_evidence entry 0 contains unknown property 'unexpected'."));
    }

    [Test]
    public void Load_ImportedDeclarations_AreAppendedInSourceOrder()
    {
        string root = Path.Combine(_temporaryDirectory, "architecture", "root.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        File.WriteAllText(root, $"""
            version: 1
            name: Example
            imports: [evidence.yml]
            layers:
              domain:
                namespace: App.Domain
            analysis:
              target_assemblies: []
            contracts:
              strict: []
            external_evidence:
            {Indent(ValidDeclaration())}
            """);
        File.WriteAllText(Path.Combine(_temporaryDirectory, "architecture", "evidence.yml"), $"""
            external_evidence:
            {Indent(ValidDeclaration().Replace("static-analysis", "fragment-analysis", StringComparison.Ordinal))}
            """);

        ArchitectureContractDocument document = Load(root);

        Assert.That(document.ExternalEvidence.Select(requirement => requirement.Id),
            Is.EqualTo(["static-analysis", "fragment-analysis"]));
    }

    private ArchitectureContractDocument Load(string policyOrYaml)
    {
        if (File.Exists(policyOrYaml))
        {
            return new ArchitecturePolicyDocumentLoader().Load(policyOrYaml);
        }

        string path = Path.Combine(_temporaryDirectory, "architecture", "root.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, policyOrYaml);
        return new ArchitecturePolicyDocumentLoader().Load(path);
    }

    private static string MonolithicPolicy(string declarations) => $"""
        version: 1
        name: Example
        layers:
          domain:
            namespace: App.Domain
        analysis:
          target_assemblies: []
        contracts:
          strict: []
        """ + (string.IsNullOrEmpty(declarations)
            ? string.Empty
            : $"\nexternal_evidence:\n{Indent(declarations)}");

    private static string ValidDeclaration() => """
          - id: static-analysis
            format: sarif
            required: true
            tool: Semgrep
            tool_version: "1.0"
            run: security
            require_repository: true
            require_revision: true
            require_scope: false
        """;

    private static string Indent(string value) => string.Join(
        Environment.NewLine,
        value.Split(Environment.NewLine).Select(line => "  " + line));

    private static string FieldValue(string field) => field switch
    {
        "id" => "static-analysis",
        "tool" => "Semgrep",
        "run" => "security",
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
