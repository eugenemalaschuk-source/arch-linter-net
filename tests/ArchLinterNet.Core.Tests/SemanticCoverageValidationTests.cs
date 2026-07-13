using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SemanticCoverageValidationTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-semantic-coverage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public void SemanticRoleCoverage_ValidRootAndExclusion_Loads()
    {
        ArchitectureContractDocument document = Load("""
            roots:
              - namespace: MyApp
            exclude:
              - role: GeneratedRole
                metadata:
                  kind: generated
                reason: Generated code is governed elsewhere.
            """);

        Assert.That(document.Contracts.StrictCoverage.Single().Scope, Is.EqualTo("semantic_role"));
    }

    [TestCase("roots:\n  - include: [src]\n", "without a non-empty namespace")]
    [TestCase("roots:\n  - namespace: MyApp\n    include: [src]\n", "using include/exclude discovery fields")]
    [TestCase("exclude:\n  - reason: Missing role.\n", "without a non-empty role matcher")]
    [TestCase("exclude:\n  - role: DomainLayer\n", "without a non-empty reason")]
    [TestCase("exclude:\n  - role: DomainLayer\n    namespace: MyApp\n    reason: Wrong matcher.\n", "using a non-semantic matcher")]
    [TestCase("between: [[a, b]]\n", "cannot declare 'between' or 'contract_ids'")]
    [TestCase("contract_ids: [other]\n", "cannot declare 'between' or 'contract_ids'")]
    public void SemanticRoleCoverage_InvalidShape_IsRejected(string fragment, string expectedMessage)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load(fragment))!;

        Assert.That(exception.Message, Does.Contain(expectedMessage));
    }

    [Test]
    public void SemanticRoleCoverage_UnknownExclusionKey_IsRejectedBeforeDeserialization()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load("""
            exclude:
              - role: DomainLayer
                metdata:
                  domain: Sales
                reason: Typo must not broaden this exclusion.
            """))!;

        Assert.That(exception.Message, Does.Contain("unknown property 'metdata'"));
    }

    private ArchitectureContractDocument Load(string contractFragment)
    {
        string path = Path.Combine(_tempDirectory, "dependencies.arch.yml");
        File.WriteAllText(path, """
            version: 1
            name: Semantic coverage validation
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Core.Tests]
            contracts:
              strict_coverage:
                - id: semantic-coverage
                  name: semantic-coverage
                  scope: semantic_role
            """ + Environment.NewLine + Indent(contractFragment, 6) + "      reason: Semantic coverage must be explicit.\n");

        return new ArchitecturePolicyDocumentLoader().Load(path);
    }

    private static string Indent(string text, int spaces)
    {
        string prefix = new(' ', spaces);
        return string.Join(Environment.NewLine, text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => prefix + line)) + Environment.NewLine;
    }
}
