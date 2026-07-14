using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyProvenanceTests
{
    private static readonly string[] _allowForbidPaths = { "architecture/allow.yml", "architecture/forbid.yml" };
    private static readonly string[] _cycleImportChain =
        { "architecture/root.yml", "architecture/a.yml", "architecture/nested/b.yml", "architecture/a.yml" };
    private static readonly string[] _cycleRelatedSourcePaths = { "architecture/a.yml" };
    private static readonly string[] _duplicateContractPaths = { "architecture/root.yml", "architecture/fragment.yml" };
    private static readonly string[] _fragmentContractImportChain =
        { "architecture/company-policy.yaml", "architecture/parts/domain.data" };
    private static readonly string[] _nestedMissingImportChain =
        { "architecture/root.yml", "architecture/a.yml", "nested/missing.yml" };

    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-provenance-{Guid.NewGuid():N}");
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
    public void Load_ImportedPolicy_BindsRootAndFragmentContractLocations()
    {
        string root = Write(
            "architecture/company-policy.yaml",
            RootYaml("parts/domain.data", DependencyContract("root-contract")));
        Write(
            "architecture/parts/domain.data",
            LayersFragment() + DependencyContract("fragment-contract"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        ArchitecturePolicySourceLocation rootLocation = document.Provenance.Nodes["/contracts/strict/0"];
        ArchitecturePolicySourceLocation fragmentLocation = document.Provenance.Nodes["/contracts/strict/1"];
        Assert.Multiple(() =>
        {
            Assert.That(rootLocation.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Root));
            Assert.That(rootLocation.SourcePath, Is.EqualTo("architecture/company-policy.yaml"));
            Assert.That(rootLocation.YamlPath, Is.EqualTo("contracts.strict[0]"));
            Assert.That(fragmentLocation.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Fragment));
            Assert.That(fragmentLocation.SourcePath, Is.EqualTo("architecture/parts/domain.data"));
            Assert.That(fragmentLocation.YamlPath, Is.EqualTo("contracts.strict[0]"));
            Assert.That(fragmentLocation.ContractFamily, Is.EqualTo("dependency"));
            Assert.That(fragmentLocation.ContractId, Is.EqualTo("fragment-contract"));
            Assert.That(fragmentLocation.Source.ImportChain,
                Is.EqualTo(_fragmentContractImportChain));
        });
    }

    [Test]
    public void Load_RootVersusFragmentConflict_CarriesBothTypedLocations()
    {
        string root = Write(
            "architecture/root.yml",
            RootYaml("fragment.yml", "layers:\n  domain:\n    namespace: Root.Domain\n"));
        Write("architecture/fragment.yml", "layers:\n  domain:\n    namespace: Fragment.Domain\n");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.CompositionConflict));
            Assert.That(exception.Diagnostic, Is.Not.Null);
            Assert.That(exception.Diagnostic!.Location!.SourcePath, Is.EqualTo("architecture/root.yml"));
            Assert.That(exception.Diagnostic.Location.YamlPath, Is.EqualTo("layers.domain"));
            Assert.That(exception.Diagnostic.RelatedLocations.Single().SourcePath,
                Is.EqualTo("architecture/fragment.yml"));
            Assert.That(exception.Diagnostic.RelatedLocations.Single().YamlPath, Is.EqualTo("layers.domain"));
        });
    }

    [Test]
    public void Load_ClassificationPathLocations_PreserveEncounterOrderForDoubleDigitIndices()
    {
        string root = Write("architecture/root.yml", RootYaml("classification.yml"));
        string entries = string.Join("\n", Enumerable.Range(0, 11)
            .Select(index => $"    - path_prefix: src/Area{index}\n      role: Layer{index}"));
        Write("architecture/classification.yml", LayersFragment() + $"classification:\n  path:\n{entries}\n");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);
        string[] paths = document.ClassificationPathDeferred!.PolicyLocations
            .Select(location => location.YamlPath)
            .ToArray();

        Assert.That(Array.IndexOf(paths, "classification.path[2]"), Is.LessThan(Array.IndexOf(paths, "classification.path[10]")));
    }

    [Test]
    public void Load_NestedMissingImport_CarriesRootBasedImportChain()
    {
        string root = Write("architecture/root.yml", RootYaml("a.yml"));
        Write("architecture/a.yml", "imports: [nested/missing.yml]\n");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.MissingFile));
            Assert.That(exception.Diagnostic!.Location!.SourcePath, Is.EqualTo("architecture/a.yml"));
            Assert.That(exception.Diagnostic.Location.YamlPath, Is.EqualTo("imports[0]"));
            Assert.That(exception.Diagnostic.ImportChain, Is.EqualTo(_nestedMissingImportChain));
        });
    }

    [Test]
    public void Load_ImportCycle_CarriesNestedChainAndBothEdges()
    {
        string root = Write("architecture/root.yml", RootYaml("a.yml"));
        Write("architecture/a.yml", "imports: [nested/b.yml]\n");
        Write("architecture/nested/b.yml", "imports: [../a.yml]\n");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.Cycle));
            Assert.That(exception.Diagnostic!.Location!.SourcePath, Is.EqualTo("architecture/nested/b.yml"));
            Assert.That(exception.Diagnostic.Location.YamlPath, Is.EqualTo("imports[0]"));
            Assert.That(exception.Diagnostic.RelatedLocations.Select(location => location.SourcePath),
                Is.EqualTo(_cycleRelatedSourcePaths));
            Assert.That(exception.Diagnostic.ImportChain, Is.EqualTo(_cycleImportChain));
        });
    }

    [Test]
    public void Load_DuplicateContractId_CarriesBothDefinitions()
    {
        string root = Write(
            "architecture/root.yml",
            RootYaml("fragment.yml", LayersFragment() + """
                contracts:
                  strict:
                    - id: duplicate-id
                      name: root rule
                      source: application
                      forbidden: [domain]
                """ + "\n"));
        Write("architecture/fragment.yml", """
            contracts:
              strict:
                - id: duplicate-id
                  name: fragment rule
                  source: application
                  forbidden: [domain]
            """);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;
        string[] paths = new[] { exception.Diagnostic!.Location! }
            .Concat(exception.Diagnostic.RelatedLocations)
            .OrderBy(location => location.SourceOrdinal)
            .Select(location => location.SourcePath)
            .ToArray();

        Assert.That(paths, Is.EqualTo(_duplicateContractPaths));
    }

    [Test]
    public void Load_InvalidFragmentValue_PointsToTheDeclaringFragment()
    {
        string root = Write("architecture/root.yml", RootYaml("fragment.yml"));
        Write("architecture/fragment.yml", "layers:\n  domain:\n    namespace: ''\n");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Diagnostic!.Location!.SourcePath, Is.EqualTo("architecture/fragment.yml"));
            Assert.That(exception.Diagnostic.Location.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Fragment));
            Assert.That(exception.Diagnostic.Location.YamlPath, Is.EqualTo("layers.domain.namespace"));
        });
    }

    [Test]
    public void Load_InvalidInlineRootValue_PointsToTheExplicitRootSource()
    {
        string root = Write(
            "architecture/arbitrary.policy",
            RootYaml("fragment.yml", "layers:\n  domain:\n    namespace: ''\n"));
        Write("architecture/fragment.yml", DependencyContract("fragment-contract"));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Diagnostic!.Location!.SourcePath, Is.EqualTo("architecture/arbitrary.policy"));
            Assert.That(exception.Diagnostic.Location.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Root));
            Assert.That(exception.Diagnostic.Location.YamlPath, Is.EqualTo("layers.domain.namespace"));
        });
    }

    [Test]
    public void Load_InvalidImportedContract_CarriesSemanticOwnerAndEffectiveId()
    {
        string root = Write("architecture/root.yml", RootYaml("packages.yml"));
        Write(
            "architecture/packages.yml",
            LayersFragment() + """
                packages:
                  forbidden_infra:
                    package_ids: [Microsoft.EntityFrameworkCore]
                contracts:
                  strict_package_dependency:
                    - name: transitive package rule
                      source: Missing
                      forbidden: [forbidden_infra]
                      reason: Unsupported depth.
                """);

        ArchitecturePolicyValidationException exception = Assert.Throws<ArchitecturePolicyValidationException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("source 'Missing'"));
            Assert.That(exception.Message, Does.Contain("root: architecture/root.yml"));
            Assert.That(exception.Diagnostic.Location!.SourcePath, Is.EqualTo("architecture/packages.yml"));
            Assert.That(exception.Diagnostic.Location.YamlPath,
                Is.EqualTo("contracts.strict_package_dependency[0]"));
            Assert.That(exception.Diagnostic.Location.ContractFamily, Is.EqualTo("package_dependency"));
            Assert.That(exception.Diagnostic.Location.ContractId, Is.EqualTo("transitive-package-rule"));
        });
    }

    [Test]
    public void CheckConfiguration_MissingLayer_CarriesReferencingFragmentLocation()
    {
        string root = Write("architecture/root.yml", RootYaml("contract.yml"));
        Write(
            "architecture/contract.yml",
            LayersFragment() + DependencyContract("missing-layer-rule", forbidden: "missing"));
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        ArchitecturePolicyValidationException exception = Assert.Throws<ArchitecturePolicyValidationException>(
            () => runner.CheckConfiguration())!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("unknown layer 'missing'"));
            Assert.That(exception.Diagnostic.Location!.SourcePath, Is.EqualTo("architecture/contract.yml"));
            Assert.That(exception.Diagnostic.Location.ContractId, Is.EqualTo("missing-layer-rule"));
        });
    }

    [Test]
    public void CheckPolicyConsistency_CrossFragmentConflict_CarriesBothContractLocations()
    {
        string root = Write("architecture/root.yml", RootYaml("allow.yml"));
        Write(
            "architecture/allow.yml",
            "imports: [forbid.yml]\n" + LayersFragment() + """
                contracts:
                  strict_allow_only:
                    - name: allow-domain-application
                      source: domain
                      allowed: [application]
                """);
        Write(
            "architecture/forbid.yml",
            DependencyContract("forbid-domain-application", "domain", "application"));
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        PolicyConsistencyDiagnostic finding = runner.CheckPolicyConsistency()
            .Single(candidate => candidate.CheckKind == "allow-forbid-conflict");
        string[] paths = new[] { finding.PolicyLocation! }
            .Concat(finding.RelatedPolicyLocations)
            .Select(location => location.SourcePath)
            .ToArray();

        Assert.That(paths, Is.EqualTo(_allowForbidPaths));
    }

    [Test]
    public void CheckPolicyConsistency_SameNameInDifferentFamily_UsesOnlyParticipantIds()
    {
        string root = Write(
            "architecture/root.yml",
            RootYaml("forbid.yml", LayersFragment() + """
                contracts:
                  strict_allow_only:
                    - id: allow-id
                      name: shared-name
                      source: domain
                      allowed: [application]
                  strict_layers:
                    - id: unrelated-layer-id
                      name: shared-name
                      layers: [domain, application]
                """ + "\n"));
        Write(
            "architecture/forbid.yml",
            """
            contracts:
              strict:
                - id: forbid-id
                  name: forbid-name
                  source: domain
                  forbidden: [application]
            """);
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        PolicyConsistencyDiagnostic finding = runner.CheckPolicyConsistency()
            .Single(candidate => candidate.CheckKind == "allow-forbid-conflict");
        string[] paths = new[] { finding.PolicyLocation! }
            .Concat(finding.RelatedPolicyLocations)
            .Select(location => location.YamlPath)
            .ToArray();

        Assert.That(paths, Is.EquivalentTo(new[]
        {
            "contracts.strict_allow_only[0]",
            "contracts.strict[0]",
        }));
    }

    [Test]
    public void UnmatchedIgnore_ResolvesNestedFragmentYamlPath()
    {
        string root = Write("architecture/root.yml", RootYaml("fragment.yml"));
        Write(
            "architecture/fragment.yml",
            LayersFragment() + """
                contracts:
                  strict:
                    - name: ignored rule
                      source: application
                      forbidden: [domain]
                      ignored_violations:
                        - source_type: App.Legacy
                          forbidden_reference: App.Domain
                          reason: migration
                """);
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);
        ArchitectureUnmatchedIgnoredViolation unmatched = document.Provenance.Enrich(
            new ArchitectureUnmatchedIgnoredViolation(
                "ignored rule", "ignored-rule", 0, "App.Legacy", "App.Domain", "migration")
            {
                ContractGroup = "strict"
            });

        UnmatchedIgnoreDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromUnmatchedIgnore(unmatched);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.PolicyLocation!.SourcePath, Is.EqualTo("architecture/fragment.yml"));
            Assert.That(diagnostic.PolicyLocation.YamlPath,
                Is.EqualTo("contracts.strict[0].ignored_violations[0]"));
        });
    }

    [Test]
    public void Load_MonolithicPolicy_PreservesBehaviorAndProvidesRootProvenance()
    {
        string root = Write("custom/location/policy.data", EffectiveRootYaml());

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        Assert.Multiple(() =>
        {
            Assert.That(document.Provenance.Sources, Has.Count.EqualTo(1));
            Assert.That(document.Provenance.RootSource!.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Root));
            Assert.That(document.Provenance.Nodes["/layers/domain"].Role,
                Is.EqualTo(ArchitecturePolicyDocumentRole.Root));
            Assert.That(document.Contracts.Strict, Is.Empty);
        });
    }

    [Test]
    public void Load_RenamedEquivalentGraph_ChangesPathsButNotSemanticProvenance()
    {
        ArchitectureContractDocument first = LoadEquivalent("first/architecture/root.yml", "part.yml");
        ArchitectureContractDocument second = LoadEquivalent("second/architecture/custom.policy", "arbitrary.data");

        ArchitecturePolicySourceLocation firstLocation = first.Provenance.Nodes["/contracts/strict/0"];
        ArchitecturePolicySourceLocation secondLocation = second.Provenance.Nodes["/contracts/strict/0"];
        Assert.Multiple(() =>
        {
            Assert.That(secondLocation.SourcePath, Is.Not.EqualTo(firstLocation.SourcePath));
            Assert.That(secondLocation.Role, Is.EqualTo(firstLocation.Role));
            Assert.That(secondLocation.YamlPath, Is.EqualTo(firstLocation.YamlPath));
            Assert.That(secondLocation.ContractFamily, Is.EqualTo(firstLocation.ContractFamily));
            Assert.That(secondLocation.ContractId, Is.EqualTo(firstLocation.ContractId));
        });
    }

    private ArchitectureContractDocument LoadEquivalent(string rootPath, string fragmentName)
    {
        string directory = Path.GetDirectoryName(rootPath)!.Replace('\\', '/');
        string root = Write(rootPath, RootYaml(fragmentName));
        Write($"{directory}/{fragmentName}", LayersFragment() + DependencyContract("same rule"));
        return new ArchitecturePolicyDocumentLoader().Load(root);
    }

    private ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            _temporaryDirectory,
            new[] { typeof(ArchitecturePolicyDocumentLoader).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private string Write(string relativePath, string content)
    {
        string path = Path.Combine(_temporaryDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string RootYaml(string import, string inline = "")
    {
        string yaml = $"version: 1\nname: Example\nimports:\n  - {import}\n{inline}";
        if (!inline.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Contains("analysis:"))
        {
            yaml += "analysis:\n  target_assemblies: [App]\n";
        }

        if (!inline.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Contains("contracts:"))
        {
            yaml += "contracts:\n  strict: []\n";
        }

        return yaml;
    }

    private static string LayersFragment()
    {
        return """
            layers:
              domain:
                namespace: App.Domain
              application:
                namespace: App.Application
            """ + "\n";
    }

    private static string DependencyContract(
        string name,
        string source = "application",
        string forbidden = "domain")
    {
        return $"contracts:\n  strict:\n    - name: {name}\n      source: {source}\n      forbidden: [{forbidden}]\n";
    }

    private static string EffectiveRootYaml()
    {
        return """
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
    }
}
