using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CoverageScopePolicyValidationTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-coverage-scopes-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    [TestCase("project")]
    [TestCase("assembly")]
    public void Load_DirectDiscoveryWideCoverageWithRoots_RejectsTheInvalidField(string scope)
    {
        string policy = Write("direct.yml", $"""
            version: 1
            name: Invalid {scope} coverage
            layers:
              domain:
                namespace: App.Domain
            analysis:
              projects: [src/App/App.csproj]
            contracts:
              strict_coverage:
                - name: {scope}-coverage
                  scope: {scope}
                  roots:
                    - namespace: App
                  reason: Every discovered unit must be governed.
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(policy))!;

        Assert.That(exception.Message, Does.Contain($"{char.ToUpperInvariant(scope[0])}{scope[1..]} coverage contract")
            .And.Contain("cannot declare 'roots'"));
    }

    [Test]
    public void Load_ImportedAllIssueCoverageScopesWithoutDiscoveryRoots_Succeeds()
    {
        string root = Write("architecture/root.yml", """
            version: 1
            name: Imported all coverage scopes
            imports: [coverage.yml]
            layers:
              domain:
                namespace: App.Domain
              application:
                namespace: App.Application
            analysis:
              projects: [src/App/App.csproj]
            contracts:
              strict:
                - id: application-does-not-depend-on-domain
                  name: application-does-not-depend-on-domain
                  source: application
                  forbidden: [domain]
            """);
        Write("architecture/coverage.yml", CoverageContracts());

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(root));
    }

    [TestCase("strict_coverage", "project")]
    [TestCase("strict_coverage", "assembly")]
    [TestCase("audit_coverage", "project")]
    [TestCase("audit_coverage", "assembly")]
    public void Load_ImportedDiscoveryWideCoverageWithRoots_RejectsTheInvalidField(
        string coverageGroup,
        string scope)
    {
        string root = Write("architecture/root.yml", """
            version: 1
            name: Imported invalid coverage
            imports: [coverage.yml]
            layers:
              domain:
                namespace: App.Domain
            analysis:
              projects: [src/App/App.csproj]
            contracts:
              strict: []
            """);
        Write("architecture/coverage.yml", $"""
            contracts:
              {coverageGroup}:
                - name: {scope}-coverage
                  scope: {scope}
                  roots:
                    - namespace: App
                  reason: Every discovered unit must be governed.
            """);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Message, Does.Contain("effective policy schema").And.Contain("roots"));
    }

    [Test]
    public void Load_ImportedNamespaceCoverageWithoutRoots_UsesTheGenericSchemaDiagnostic()
    {
        string root = Write("architecture/root.yml", """
            version: 1
            name: Imported invalid namespace coverage
            imports: [coverage.yml]
            layers:
              domain:
                namespace: App.Domain
            analysis:
              projects: [src/App/App.csproj]
            contracts:
              strict: []
            """);
        Write("architecture/coverage.yml", """
            contracts:
              strict_coverage:
                - name: namespace-coverage
                  scope: namespace
                  reason: Every application namespace must be governed.
            """);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Message, Does.Contain("effective policy schema")
            .And.Not.Contain("that scope classifies all discovered units"));
    }

    private string Write(string relativePath, string content)
    {
        string path = Path.Combine(_temporaryDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string CoverageContracts() => """
        contracts:
          strict_coverage:
            - id: namespace-coverage
              name: namespace-coverage
              scope: namespace
              roots:
                - namespace: App
              reason: Every application namespace must be governed.
            - id: project-coverage
              name: project-coverage
              scope: project
              reason: Every discovered project must be governed.
            - id: assembly-coverage
              name: assembly-coverage
              scope: assembly
              reason: Every resolved assembly must be governed.
            - id: dependency-edge-coverage
              name: dependency-edge-coverage
              scope: dependency_edge
              between:
                - [application, domain]
              reason: Every observed layer edge must be governed.
            - id: rule-input-coverage
              name: rule-input-coverage
              scope: rule_input
              contract_ids: [application-does-not-depend-on-domain]
              reason: Every selected rule input must be meaningful.
        """;
}
