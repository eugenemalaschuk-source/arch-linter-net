using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureProjectDiscoveryTests
{
    private static readonly string[] FixtureAssembly = { "Fixture.Assembly" };
    private static readonly string[] BinDebugNet10 = { "bin/Debug/net10.0" };
    private static readonly string[] SrcFixture = { "src/Fixture" };

    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, true);
        }
    }

    [Test]
    public void ProjectDiscoveryResult_FourArgPositionalConstructorAndDeconstruct_StillCompile()
    {
        // ProjectDiscoveryResult.DiscoveredProjects is an init-only property, not a fifth
        // positional parameter, specifically so the pre-existing 4-arg positional constructor
        // and 4-value Deconstruct (both part of the public API before project/assembly
        // coverage existed) remain source- and binary-compatible.
        ProjectDiscoveryResult result = new(
            FixtureAssembly,
            BinDebugNet10,
            SrcFixture,
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>());

        (IReadOnlyCollection<string> targetAssemblyNames,
            IReadOnlyCollection<string> assemblySearchPaths,
            IReadOnlyCollection<string> sourceRoots,
            IReadOnlyCollection<ArchitectureProjectDiscoveryDiagnostic> diagnostics) = result;

        Assert.That(targetAssemblyNames, Is.EquivalentTo(FixtureAssembly));
        Assert.That(assemblySearchPaths, Is.EquivalentTo(BinDebugNet10));
        Assert.That(sourceRoots, Is.EquivalentTo(SrcFixture));
        Assert.That(diagnostics, Is.Empty);
        Assert.That(result.DiscoveredProjects, Is.Empty);
    }

    [Test]
    public void ResolveFromDocument_NoDiscoveryConfigured_ReturnsEmptyResult()
    {
        var document = new ArchitectureContractDocument { Analysis = new ArchitectureAnalysisConfiguration() };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.Empty);
        Assert.That(result.AssemblySearchPaths, Is.Empty);
        Assert.That(result.SourceRoots, Is.Empty);
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ResolveFromDocument_ExplicitTargetAssembliesSet_DiscoveryStillRunsButDoesNotOverride()
    {
        string projectDir = CreateProject("Sample", "net9.0", buildOutputFrameworks: ["net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "Explicit.Assembly" },
                Projects = new List<string> { Path.Combine(projectDir, "Sample.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        // Discovery itself always reports what it found; the factory layer (not this method) decides precedence.
        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "Sample" }));
    }

    [Test]
    public void ResolveFromDocument_ExplicitProject_PopulatesDiscoveredProjectsWithPathAndAssemblyName()
    {
        string projectDir = CreateProject("Sample", "net9.0", buildOutputFrameworks: ["net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "Sample.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        ArchitectureDiscoveredProject discoveredProject = result.DiscoveredProjects.Single();
        Assert.That(discoveredProject.AssemblyName, Is.EqualTo("Sample"));
        Assert.That(discoveredProject.Path, Does.EndWith("Sample.csproj"));
        Assert.That(discoveredProject.TargetFrameworks, Is.EquivalentTo(new[] { "net9.0" }));
    }

    [Test]
    public void ResolveFromDocument_MultiTargetedProject_DiscoveredProjectListsAllTargetFrameworks()
    {
        string projectDir = CreateProject("MultiTarget", "net8.0;net9.0", buildOutputFrameworks: ["net8.0", "net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MultiTarget.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        ArchitectureDiscoveredProject discoveredProject = result.DiscoveredProjects.Single();
        Assert.That(discoveredProject.TargetFrameworks, Is.EquivalentTo(new[] { "net8.0", "net9.0" }));
    }

    [Test]
    public void ResolveFromDocument_SlnxSolution_DiscoversProjectAndOutput()
    {
        string projectDir = CreateProject("Foo", "net9.0", buildOutputFrameworks: ["net9.0"]);
        string slnxPath = Path.Combine(_repoRoot, "Test.slnx");
        File.WriteAllText(slnxPath, $"""
            <Solution>
              <Project Path="{Path.GetRelativePath(_repoRoot, Path.Combine(projectDir, "Foo.csproj"))}" />
            </Solution>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration { Solution = "Test.slnx" }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "Foo" }));
        Assert.That(result.AssemblySearchPaths.Single(), Does.Contain(Path.Combine("bin", "Debug", "net9.0")));
    }

    [Test]
    public void ResolveFromDocument_ClassicSlnSolution_SkipsSolutionFolderEntries()
    {
        string projectDir = CreateProject("Bar", "net9.0", buildOutputFrameworks: ["net9.0"]);
        string relativeProjectPath = Path.GetRelativePath(_repoRoot, Path.Combine(projectDir, "Bar.csproj"));
        string slnPath = Path.Combine(_repoRoot, "Test.sln");
        string slnContent = "Microsoft Visual Studio Solution File, Format Version 12.00\n" +
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Bar\", \"" + relativeProjectPath + "\", \"{11111111-1111-1111-1111-111111111111}\"\n" +
            "EndProject\n" +
            "Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"SolutionFolder\", \"SolutionFolder\", \"{22222222-2222-2222-2222-222222222222}\"\n" +
            "EndProject\n";
        File.WriteAllText(slnPath, slnContent);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration { Solution = "Test.sln" }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "Bar" }));
    }

    [Test]
    public void ResolveFromDocument_ExplicitProjectsList_DiscoversOutputAndSourceRoot()
    {
        string projectDir = CreateProject("Baz", "net9.0", buildOutputFrameworks: ["net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "Baz.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "Baz" }));
        Assert.That(result.SourceRoots.Single(), Is.EqualTo(Path.GetRelativePath(_repoRoot, projectDir).Replace('\\', '/')));
    }

    [Test]
    public void ResolveFromDocument_MissingBuildOutput_ProducesDiagnostic()
    {
        string projectDir = CreateProject("NoOutput", "net9.0", buildOutputFrameworks: []);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "NoOutput.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.Empty);
        Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo("missing project build output"));
    }

    [Test]
    public void ResolveFromDocument_MultiTargetAmbiguousOutput_ProducesDiagnostic()
    {
        string projectDir = CreateProject("MultiTarget", "net8.0;net9.0", buildOutputFrameworks: ["net8.0", "net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MultiTarget.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.Empty);
        Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo("ambiguous project build output"));
    }

    [Test]
    public void ResolveFromDocument_MultiTargetSingleResolvedOutput_Succeeds()
    {
        string projectDir = CreateProject("MultiTarget", "net8.0;net9.0", buildOutputFrameworks: ["net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MultiTarget.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "MultiTarget" }));
        Assert.That(result.AssemblySearchPaths.Single(), Does.Contain(Path.Combine("bin", "Debug", "net9.0")));
    }

    [Test]
    public void ResolveFromDocument_MultiTargetWithOverride_SelectsOverriddenFramework()
    {
        string projectDir = CreateProject("MultiTarget", "net8.0;net9.0", buildOutputFrameworks: ["net8.0", "net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MultiTarget.csproj") },
                TargetFramework = "net8.0"
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.AssemblySearchPaths.Single(), Does.Contain(Path.Combine("bin", "Debug", "net8.0")));
    }

    [Test]
    public void ResolveFromDocument_MissingSolutionFile_ProducesDiagnostic()
    {
        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration { Solution = "DoesNotExist.slnx" }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo("missing solution file"));
    }

    [Test]
    public void ResolveFromDocument_StaleBuildOutput_ProducesDiagnostic()
    {
        string projectDir = CreateProject("Stale", "net9.0", buildOutputFrameworks: ["net9.0"]);

        string dllPath = Path.Combine(projectDir, "bin", "Debug", "net9.0", "Stale.dll");
        File.SetLastWriteTimeUtc(dllPath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        string sourceFile = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(sourceFile, "// changed after the build");
        File.SetLastWriteTimeUtc(sourceFile, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "Stale.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.Empty);
        Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo("stale project build output"));
    }

    [Test]
    public void ResolveFromDocument_FreshBuildOutput_NoStaleDiagnostic()
    {
        // CreateProject writes the .csproj first, then the build output a moment later,
        // mirroring a real build — so the output is naturally newer than its sources.
        string projectDir = CreateProject("Fresh", "net9.0", buildOutputFrameworks: ["net9.0"]);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "Fresh.csproj") }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "Fresh" }));
    }

    [Test]
    public void ResolveFromDocument_MalformedClassicSln_ProducesUnparsableDiagnostic()
    {
        string slnPath = Path.Combine(_repoRoot, "Garbage.sln");
        File.WriteAllText(slnPath, "this is not a solution file\njust garbage text\n");

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration { Solution = "Garbage.sln" }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.Empty);
        Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo("unparsable solution file"));
    }

    [Test]
    public void ResolveFromDocument_ValidSlnWithNoProjectEntries_ProducesNoProjectsDiagnostic()
    {
        string slnPath = Path.Combine(_repoRoot, "Empty.sln");
        File.WriteAllText(slnPath, "Microsoft Visual Studio Solution File, Format Version 12.00\n");

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration { Solution = "Empty.sln" }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.Empty);
        Assert.That(result.Diagnostics.Single().Kind, Is.EqualTo("no C# projects discovered"));
    }

    [Test]
    public void ResolveFromDocument_ProjectIncludeExclude_FiltersDiscoveredProjects()
    {
        string includedDir = CreateProject("Included", "net9.0", buildOutputFrameworks: ["net9.0"], subdirectory: "src/Included");
        string excludedDir = CreateProject("Excluded", "net9.0", buildOutputFrameworks: ["net9.0"], subdirectory: "src/Excluded");

        string slnxPath = Path.Combine(_repoRoot, "Test.slnx");
        File.WriteAllText(slnxPath, $"""
            <Solution>
              <Project Path="{Path.GetRelativePath(_repoRoot, Path.Combine(includedDir, "Included.csproj")).Replace('\\', '/')}" />
              <Project Path="{Path.GetRelativePath(_repoRoot, Path.Combine(excludedDir, "Excluded.csproj")).Replace('\\', '/')}" />
            </Solution>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Solution = "Test.slnx",
                ProjectExclude = new List<string> { "src/Excluded/**" }
            }
        };

        ProjectDiscoveryResult result = new ArchitectureProjectDiscoveryService().ResolveFromDocument(document, _repoRoot);

        Assert.That(result.TargetAssemblyNames, Is.EquivalentTo(new[] { "Included" }));
    }

    private string CreateProject(
        string assemblyName,
        string targetFrameworksProperty,
        IReadOnlyList<string> buildOutputFrameworks,
        string? subdirectory = null)
    {
        string projectDir = Path.Combine(_repoRoot, subdirectory ?? assemblyName);
        Directory.CreateDirectory(projectDir);

        string targetFrameworkElement = targetFrameworksProperty.Contains(';')
            ? $"<TargetFrameworks>{targetFrameworksProperty}</TargetFrameworks>"
            : $"<TargetFramework>{targetFrameworksProperty}</TargetFramework>";

        File.WriteAllText(Path.Combine(projectDir, $"{assemblyName}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {targetFrameworkElement}
              </PropertyGroup>
            </Project>
            """);

        foreach (string framework in buildOutputFrameworks)
        {
            string outputDir = Path.Combine(projectDir, "bin", "Debug", framework);
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, $"{assemblyName}.dll"), string.Empty);
        }

        return projectDir;
    }
}
