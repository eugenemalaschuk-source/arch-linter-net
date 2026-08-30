using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
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
            Assert.That(requirement.DiagnosticFilter, Is.Null);
        });
    }

    [Test]
    public void Load_ValidDiagnosticFilter_ExposesTypedSelectorsAndSeverityModes()
    {
        ArchitectureContractDocument document = Load(MonolithicPolicy(ValidDeclarationWithFilter()));

        ArchitectureExternalEvidenceDiagnosticFilter filter =
            document.ExternalEvidence.Single().DiagnosticFilter!;
        Assert.Multiple(() =>
        {
            Assert.That(filter.RuleIds, Is.EqualTo(["SEC100", "SEC200"]));
            Assert.That(filter.RuleTags, Is.EqualTo(["security", "owasp"]));
            Assert.That(filter.Projects, Is.EqualTo(["App.Web"]));
            Assert.That(filter.PathPrefixes, Is.EqualTo(["src/"]));
            Assert.That(filter.Severity, Is.EqualTo(new Dictionary<string, string>
            {
                ["error"] = "strict",
                ["warning"] = "audit",
            }));
            Assert.That(filter.RequireMatches, Is.True);
        });
    }

    [Test]
    public void Load_DiagnosticFilterWithoutSeverity_IsRejected()
    {
        string declaration = ValidDeclaration().Replace(
            "require_scope: false",
            "require_scope: false\n    diagnostic_filter:\n      rule_ids: [SEC100]",
            StringComparison.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Does.Contain("diagnostic_filter must declare 'severity'"));
    }

    [TestCase("../src/")]
    [TestCase("/src/")]
    [TestCase("src\\web\\")]
    [TestCase("src//web/")]
    [TestCase("src/./web/")]
    [TestCase("src/*.cs")]
    public void Load_UnsafeDiagnosticPathPrefix_IsRejected(string pathPrefix)
    {
        string declaration = ValidDeclarationWithFilter().Replace("src/", pathPrefix, StringComparison.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Does.Contain("safe repository-relative slash-normalized path prefix"));
    }

    [Test]
    public void Load_DuplicateDiagnosticSelector_IsRejected()
    {
        string declaration = ValidDeclarationWithFilter().Replace(
            "rule_ids: [SEC100, SEC200]",
            "rule_ids: [SEC100, SEC100]",
            StringComparison.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Does.Contain("rule_ids declares duplicate value 'SEC100'"));
    }

    [TestCase("critical")]
    [TestCase("ERROR")]
    public void Load_UnsupportedDiagnosticSeverityKey_IsRejected(string severity)
    {
        string declaration = ValidDeclarationWithFilter().Replace(
            "error: strict",
            $"{severity}: strict",
            StringComparison.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Does.Contain("severity key").And.Contain("unsupported"));
    }

    [TestCase("critical")]
    [TestCase(" ")]
    public void Load_UnsupportedDiagnosticSeverityMode_IsRejected(string mode)
    {
        string declaration = ValidDeclarationWithFilter().Replace(
            "warning: audit",
            $"warning: '{mode}'",
            StringComparison.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Load(MonolithicPolicy(declaration)))!;

        Assert.That(exception.Message, Does.Contain("severity.warning").And.Contain("mode"));
    }

    [Test]
    public void Load_ImportedInvalidDiagnosticFilter_IsRejectedByEffectiveSchema()
    {
        string root = Path.Combine(_temporaryDirectory, "architecture", "root.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        File.WriteAllText(root, """
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
            """);
        File.WriteAllText(Path.Combine(_temporaryDirectory, "architecture", "evidence.yml"), $"""
            external_evidence:
            {Indent(ValidDeclarationWithFilter().Replace("src/", "../src/", StringComparison.Ordinal))}
            """);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => Load(root))!;

        Assert.That(exception.Message, Does.Contain("diagnostic_filter/path_prefixes/0"));
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

    private static string ValidDeclarationWithFilter() => """
          - id: static-analysis
            format: sarif
            required: true
            tool: Semgrep
            tool_version: "1.0"
            run: security
            require_repository: true
            require_revision: true
            require_scope: false
            diagnostic_filter:
              rule_ids: [SEC100, SEC200]
              rule_tags: [security, owasp]
              projects: [App.Web]
              path_prefixes: [src/]
              severity:
                error: strict
                warning: audit
              require_matches: true
        """;

    private static string Indent(string value) => string.Join(
        "\n",
        value.Split('\n').Select(line => "  " + line.TrimEnd('\r')));

    private static string FieldValue(string field) => field switch
    {
        "id" => "static-analysis",
        "tool" => "Semgrep",
        "run" => "security",
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
