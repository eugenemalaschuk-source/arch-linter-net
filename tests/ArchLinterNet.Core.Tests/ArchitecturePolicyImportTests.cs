using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyImportTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-imports-{Guid.NewGuid():N}");
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
    public void Load_RootInlineAndNestedFragments_ComposesDepthFirstPreOrder()
    {
        string root = Write("architecture/company-policy.yaml", RootYaml("parts/a.data", RootContract("root")));
        Write("architecture/parts/a.data", FragmentContract("a", "nested/c.yml"));
        Write("architecture/parts/nested/c.yml", LayersFragment() + "\n" + FragmentContract("c"));
        Write("architecture/parts/b.yml", FragmentContract("unused"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        Assert.That(document.Contracts.Strict.Select(contract => contract.Name), Is.EqualTo(new[] { "root", "a", "c" }));
    }

    [Test]
    public void Load_RecommendedAndArbitraryNames_ProduceEquivalentModels()
    {
        string recommended = Write("recommended/architecture/arch.yml", RootYaml("policy/layers.arch.yml"));
        Write("recommended/architecture/policy/layers.arch.yml", LayersFragment());
        string arbitrary = Write("alternative/config/company-policy.yaml", RootYaml("pieces/domain.data"));
        Write("alternative/config/pieces/domain.data", LayersFragment());

        ArchitectureContractDocument first = new ArchitecturePolicyDocumentLoader().Load(recommended);
        ArchitectureContractDocument second = new ArchitecturePolicyDocumentLoader().Load(arbitrary);

        Assert.That(second.Layers.Keys, Is.EqualTo(first.Layers.Keys));
        Assert.That(second.Analysis.Configuration, Is.EqualTo(first.Analysis.Configuration));
        Assert.That(second.Contracts.AllStrict.Select(contract => contract.Id),
            Is.EqualTo(first.Contracts.AllStrict.Select(contract => contract.Id)));
    }

    [Test]
    public void Load_ImportedAndMonolithicPolicies_AreBehaviorallyEquivalent()
    {
        string imported = Write("imported/architecture/arch.yml", RootYaml("parts.yml"));
        Write("imported/architecture/parts.yml", LayersFragment());
        string monolithic = Write("monolithic/custom.yml", EffectiveRootYaml());

        ArchitectureContractDocument composed = new ArchitecturePolicyDocumentLoader().Load(imported);
        ArchitectureContractDocument single = new ArchitecturePolicyDocumentLoader().Load(monolithic);

        Assert.Multiple(() =>
        {
            Assert.That(composed.Name, Is.EqualTo(single.Name));
            Assert.That(composed.Layers.Keys, Is.EqualTo(single.Layers.Keys));
            Assert.That(composed.Analysis.TargetAssemblies, Is.EqualTo(single.Analysis.TargetAssemblies));
            Assert.That(composed.Contracts.AllStrict.Select(contract => contract.Name),
                Is.EqualTo(single.Contracts.AllStrict.Select(contract => contract.Name)));
        });
    }

    [Test]
    public void Load_FragmentRoleComesFromReachability_NotFilename()
    {
        string root = Write("architecture/not-arch.txt", RootYaml("fragment.yaml"));
        Write("architecture/fragment.yaml", LayersFragment());

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        Assert.That(document.Layers, Contains.Key("domain"));
    }

    [Test]
    public void Load_ImportedContractWithExplicitId_PreservesId()
    {
        string root = Write("architecture/root.yml", RootYaml("fragment.yml"));
        Write(
            "architecture/fragment.yml",
            LayersFragment() + "\ncontracts:\n  strict:\n    - id: explicit-id\n      name: explicit contract\n      source: application\n      forbidden: [domain]\n");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        Assert.That(document.Contracts.Strict.Single().Id, Is.EqualTo("explicit-id"));
    }

    [Test]
    public void Load_RepeatedRuns_PreserveComposedOrdering()
    {
        string root = Write("architecture/root.yml", RootYaml("a.yml", RootContract("root")));
        Write("architecture/a.yml", FragmentContract("a", "c.yml"));
        Write("architecture/c.yml", LayersFragment() + "\n" + FragmentContract("c"));

        string[] first = new ArchitecturePolicyDocumentLoader().Load(root).Contracts.Strict.Select(c => c.Name).ToArray();
        string[] second = new ArchitecturePolicyDocumentLoader().Load(root).Contracts.Strict.Select(c => c.Name).ToArray();

        Assert.That(second, Is.EqualTo(first));
    }

    [TestCase("/etc/policy.yml")]
    [TestCase("C:/policy.yml")]
    [TestCase("parts\\layer.yml")]
    [TestCase("parts/*.yml")]
    [TestCase("${POLICY}.yml")]
    [TestCase("NUL.yml")]
    [TestCase("parts//layer.yml")]
    public void Load_NonPortableImport_ExposesPortablePathCategory(string importPath)
    {
        string root = Write("architecture/root.yml", RootYaml(importPath));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.PortablePath));
    }

    [Test]
    public void Load_MissingImport_ExposesMissingFileCategory()
    {
        string root = Write("architecture/root.yml", RootYaml("missing.yml"));

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.MissingFile));
    }

    [Test]
    public void Load_PathCaseMismatch_ExposesCaseCategory()
    {
        string root = Write("architecture/root.yml", RootYaml("Layer.yml"));
        Write("architecture/layer.yml", LayersFragment());

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.PathCaseMismatch));
    }

    [Test]
    public void Load_PathOutsideBoundary_ExposesBoundaryCategory()
    {
        string root = Write("config/root.yml", RootYaml("../outside.yml"));
        Write("outside.yml", LayersFragment());

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.OutOfBoundary));
    }

    [Test]
    public void Load_CycleAndCompletedDuplicate_ExposeDistinctCategories()
    {
        string cycleRoot = Write("cycle/architecture/root.yml", RootYaml("a.yml"));
        Write("cycle/architecture/a.yml", "imports: [root.yml]\n");
        string duplicateRoot = Write("duplicate/architecture/root.yml", RootYaml("a.yml", importsSuffix: "  - ./a.yml\n"));
        Write("duplicate/architecture/a.yml", LayersFragment());

        ArchitecturePolicyImportException cycle = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(cycleRoot))!;
        ArchitecturePolicyImportException duplicate = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(duplicateRoot))!;

        Assert.Multiple(() =>
        {
            Assert.That(cycle.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.Cycle));
            Assert.That(duplicate.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.DuplicateImport));
        });
    }

    [Test]
    public void Load_FragmentWithRootField_ExposesSourceShapeCategory()
    {
        string root = Write("architecture/root.yml", RootYaml("fragment.yml"));
        Write("architecture/fragment.yml", "version: 1\nlayers:\n  domain:\n    namespace: App.Domain\n");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.SourceShape));
    }

    [Test]
    public void Load_MalformedImportedYaml_ExposesSourceShapeCategory()
    {
        string root = Write("architecture/root.yml", RootYaml("fragment.yml"));
        Write("architecture/fragment.yml", "layers: [unterminated");

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.SourceShape));
    }

    [TestCase("layers")]
    [TestCase("singleton")]
    [TestCase("contract-id")]
    public void Load_CompositionConflict_ExposesConflictCategory(string conflict)
    {
        string rootContent = conflict switch
        {
            "layers" => RootYaml("fragment.yml", "layers:\n  domain:\n    namespace: App.Domain\n"),
            "singleton" => RootYaml("fragment.yml", "analysis:\n  configuration: Debug\n"),
            _ => RootYaml("fragment.yml", RootContract("same-id"))
        };
        string fragmentContent = conflict switch
        {
            "layers" => LayersFragment(),
            "singleton" => "analysis:\n  configuration: Debug\n",
            _ => FragmentContract("same-id")
        };
        string root = Write("architecture/root.yml", rootContent);
        Write("architecture/fragment.yml", fragmentContent);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.CompositionConflict));
    }

    [Test]
    public void Load_ImportDepthBeyondSixteen_ExposesGraphLimitCategory()
    {
        string root = Write("architecture/root.yml", RootYaml("f1.yml"));
        for (int index = 1; index <= 16; index++)
        {
            Write($"architecture/f{index}.yml", $"imports: [f{index + 1}.yml]\n");
        }

        Write("architecture/f17.yml", LayersFragment());

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.GraphLimit));
    }

    [Test]
    public void Load_ImportFileCountBeyondTwoHundredFiftySix_ExposesGraphLimitBeforeRead()
    {
        string imports = string.Join('\n', Enumerable.Range(1, 256).Select(index => $"  - f{index}.yml"));
        string root = Write(
            "architecture/root.yml",
            $"version: 1\nname: Example\nimports:\n{imports}\nanalysis: {{}}\ncontracts: {{}}\n");
        for (int index = 1; index <= 255; index++)
        {
            Write($"architecture/f{index}.yml", $"layers:\n  layer{index}:\n    namespace: App.Layer{index}\n");
        }

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.GraphLimit));
    }

    [Test]
    public void Load_ClassificationPathAcrossSources_AggregatesDeferredCount()
    {
        string root = Write("architecture/root.yml", RootYaml("fragment.yml", "classification:\n  path:\n    - path_prefix: src/\n      role: Root\n"));
        Write("architecture/fragment.yml", "layers:\n  domain:\n    namespace: App.Domain\nclassification:\n  path:\n    - path_prefix: one/\n      role: One\n    - path_prefix: two/\n      role: Two\n");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        Assert.That(document.ClassificationPathDeferred?.DeclaredEntryCount, Is.EqualTo(3));
    }

    [TestCase("docs/internal/policy-import-examples/recommended/architecture/arch.yml")]
    [TestCase("docs/internal/policy-import-examples/alternative/config/policy.custom.yaml")]
    public void Load_ApprovedDesignExample_Succeeds(string relativePath)
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(path));
    }

    private string Write(string relativePath, string content)
    {
        string path = Path.Combine(_temporaryDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string RootYaml(string import, string inline = "", string importsSuffix = "")
    {
        string yaml = $"version: 1\nname: Example\nimports:\n  - {import}\n{importsSuffix}{inline}";
        if (!HasTopLevel(inline, "analysis:"))
        {
            yaml += "analysis:\n  target_assemblies: [App]\n";
        }

        if (!HasTopLevel(inline, "contracts:"))
        {
            yaml += "contracts:\n  strict: []\n";
        }

        return yaml;
    }

    private static bool HasTopLevel(string yaml, string field)
    {
        return yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Contains(field, StringComparer.Ordinal);
    }

    private static string EffectiveRootYaml()
    {
        return """
            version: 1
            name: Example
            layers:
              domain:
                namespace: App.Domain
              application:
                namespace: App.Application
            analysis:
              target_assemblies: [App]
            contracts:
              strict: []
            """;
    }

    private static string LayersFragment()
    {
        return """
            layers:
              domain:
                namespace: App.Domain
              application:
                namespace: App.Application
            """;
    }

    private static string RootContract(string name)
    {
        return $"contracts:\n  strict:\n    - name: {name}\n      source: application\n      forbidden: [domain]\n";
    }

    private static string FragmentContract(string name, string? nestedImport = null)
    {
        string imports = nestedImport is null ? string.Empty : $"imports: [{nestedImport}]\n";
        return $"{imports}contracts:\n  strict:\n    - name: {name}\n      source: application\n      forbidden: [domain]\n";
    }
}
