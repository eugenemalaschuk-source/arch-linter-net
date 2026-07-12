using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ProjectMetadataDiscoveryTests
{
    private static readonly string[] _myAppTests = ["MyApp.Tests"];
    private static readonly string[] _myAppTools = ["MyApp.Tools"];
    private static readonly string[] _testProjectReference = ["tests/MyApp.Tests/MyApp.Tests.csproj"];
    private static readonly string[] _targetFrameworks = ["net8.0", "net10.0"];
    private static readonly (string PackageId, string? Version)[] _packageReferences =
    [
        ("Central.Package", "2.3.4"),
        ("Inline.Package", "1.0.0"),
        ("Missing.Package", null)
    ];
    private static readonly Assembly[] _testAssembly = [typeof(ProjectMetadataDiscoveryTests).Assembly];
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-project-metadata-{Guid.NewGuid():N}");
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
    public void Discovery_ParsesInheritedPropertiesFriendAssembliesAndProjectReferences()
    {
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>
            """);

        string testsDir = Path.Combine(_repoRoot, "tests", "MyApp.Tests");
        Directory.CreateDirectory(testsDir);
        File.WriteAllText(Path.Combine(testsDir, "MyApp.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <InternalsVisibleTo Include="MyApp.Tests" />
                <ProjectReference Include="../../tests/MyApp.Tests/MyApp.Tests.csproj" />
              </ItemGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.Properties["Nullable"].Value, Is.EqualTo("enable"));
        Assert.That(project.Properties["TreatWarningsAsErrors"].Value, Is.EqualTo("true"));
        Assert.That(project.Properties["TreatWarningsAsErrors"].SourcePath, Is.EqualTo("Directory.Build.props"));
        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName), Is.EqualTo(_myAppTests));
        Assert.That(project.ProjectReferences.Select(entry => entry.Path), Is.EqualTo(_testProjectReference));
    }

    [Test]
    public void Discovery_ParsesProjectReferenceWithBackslashSeparators()
    {
        string testsDir = Path.Combine(_repoRoot, "tests", "MyApp.Tests");
        Directory.CreateDirectory(testsDir);
        File.WriteAllText(Path.Combine(testsDir, "MyApp.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(projectDir);
        string csprojContent = string.Join(Environment.NewLine,
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "  <PropertyGroup>",
            "    <TargetFramework>net10.0</TargetFramework>",
            "  </PropertyGroup>",
            "  <ItemGroup>",
            "    <ProjectReference Include=\"..\\..\\tests\\MyApp.Tests\\MyApp.Tests.csproj\" />",
            "  </ItemGroup>",
            "</Project>",
            "");

        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), csprojContent);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.ProjectReferences.Select(entry => entry.Path),
            Is.EqualTo(_testProjectReference));
    }

    [Test]
    public void Discovery_ParsesCentralVersionsMultiTargetingAndSourceFriendAttributes()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Packages.props"), """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Central.Package" Version="2.3.4" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectDir, "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;
            [assembly: InternalsVisibleTo("MyApp.Tools")]
            namespace MyApp;
            [assembly: InternalsVisibleTo("MyApp.MemberTools", AllInternalsVisible = true)]
            public class Marker { }
            """);
        string projectPath = Path.Combine(projectDir, "MyApp.csproj");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName> MyApp.Custom </AssemblyName>
                <TargetFrameworks> net8.0; net10.0 </TargetFrameworks>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Central.Package" />
                <PackageReference Include="Inline.Package" Version="1.0.0" />
                <PackageReference Include="Missing.Package" />
              </ItemGroup>
            </Project>
            """);

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(new ArchitectureContractDocument
            {
                Analysis = new ArchitectureAnalysisConfiguration
                {
                    Projects = new List<string> { projectPath }
                }
            }, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects.Single();

        Assert.That(project.AssemblyName, Is.EqualTo("MyApp.Custom"));
        Assert.That(project.TargetFrameworks, Is.EqualTo(_targetFrameworks));
        Assert.That(project.PackageReferences.Select(reference => (reference.PackageId, reference.Version)), Is.EquivalentTo(_packageReferences));
        Assert.That(project.FriendAssemblies.Select(friend => friend.AssemblyName), Is.EquivalentTo(_myAppTools));
    }

    [Test]
    public void Discovery_BackslashProjectReference_IsDetectedByForbiddenContract()
    {
        string testsDir = Path.Combine(_repoRoot, "tests", "MyApp.Tests");
        Directory.CreateDirectory(testsDir);
        File.WriteAllText(Path.Combine(testsDir, "MyApp.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(projectDir);
        string csprojPath = Path.Combine(projectDir, "MyApp.csproj");
        string csprojContent = string.Join(Environment.NewLine,
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "  <PropertyGroup>",
            "    <TargetFramework>net10.0</TargetFramework>",
            "  </PropertyGroup>",
            "  <ItemGroup>",
            "    <ProjectReference Include=\"..\\..\\tests\\MyApp.Tests\\MyApp.Tests.csproj\" />",
            "  </ItemGroup>",
            "</Project>",
            "");

        File.WriteAllText(csprojPath, csprojContent);

        ArchitectureProjectMetadataContract contract = new()
        {
            Id = "forbidden-refs",
            Name = "forbidden-refs",
            Projects = new List<string> { "src/MyApp/MyApp.csproj" },
            ForbiddenProjectReferences = new List<string> { "tests/**/*.csproj" }
        };

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { csprojPath }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract> { contract }
            }
        };

        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false);
        ArchitectureAnalysisContext context = new(
            _repoRoot,
            _testAssembly,
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
        ArchitectureContractRunner runner = new(context, document);

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataKind, Is.EqualTo("project_reference"));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue,
            Is.EqualTo("tests/MyApp.Tests/MyApp.Tests.csproj"));
    }

    [Test]
    public void NestedDirectoryBuildProps_WithoutExplicitImport_DoesNotMergeRootProperties()
    {
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <IsPackable>true</IsPackable>
              </PropertyGroup>
            </Project>
            """);

        string srcDir = Path.Combine(_repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        string projectDir = Path.Combine(srcDir, "MyLib");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "MyLib.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyLib.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.Properties.TryGetValue("Nullable", out ArchitectureDiscoveredProjectProperty? nullable), Is.True);
        Assert.That(nullable!.Value, Is.EqualTo("enable"));
        Assert.That(project.Properties.TryGetValue("IsPackable", out ArchitectureDiscoveredProjectProperty? _), Is.False,
            "Root Directory.Build.props must not merge when an intermediate Directory.Build.props exists without explicit Import.");
    }

    [Test]
    public void NestedDirectoryBuildProps_ContractRequiringRootProperty_ReportsViolation()
    {
        File.WriteAllText(Path.Combine(_repoRoot, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <IsPackable>true</IsPackable>
              </PropertyGroup>
            </Project>
            """);

        string srcDir = Path.Combine(_repoRoot, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        string projectDir = Path.Combine(srcDir, "MyLib");
        Directory.CreateDirectory(projectDir);
        string csprojPath = Path.Combine(projectDir, "MyLib.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        ArchitectureProjectMetadataContract contract = new()
        {
            Id = "packable",
            Name = "packable",
            Projects = new List<string> { "src/MyLib/MyLib.csproj" },
            RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsPackable"] = "true"
            }
        };

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { csprojPath }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract> { contract }
            }
        };

        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false);
        ArchitectureAnalysisContext context = new(
            _repoRoot,
            _testAssembly,
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
        ArchitectureContractRunner runner = new(context, document);

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataKind, Is.EqualTo("required_property"));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataKey, Is.EqualTo("IsPackable"));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue, Is.EqualTo(null));
    }

    [Test]
    public void Discovery_ParsesSourceLevelInternalsVisibleToAndContractFlagsForbiddenFriendAssembly()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(Path.Combine(projectDir, "Properties"));
        File.WriteAllText(Path.Combine(projectDir, "Properties", "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo("MyApp.Tools")]
            """);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        ArchitectureProjectMetadataContract contract = new()
        {
            Id = "friend-assemblies",
            Name = "friend-assemblies",
            Projects = new List<string> { "src/MyApp/MyApp.csproj" },
            AllowedFriendAssemblies = new List<string> { "MyApp.Tests" }
        };
        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract> { contract }
            }
        };

        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false);
        ArchitectureDiscoveredProject project = discovery.DiscoveredProjects.Single();
        ArchitectureAnalysisContext context = new(
            _repoRoot,
            _testAssembly,
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
        ArchitectureContractRunner runner = new(context, document);

        List<ArchitectureViolation> violations = runner.Session.CheckProjectMetadataContract(contract);

        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName), Is.EqualTo(_myAppTools));
        Assert.That(project.FriendAssemblies.Single().SourcePath, Is.EqualTo("src/MyApp/Properties/AssemblyInfo.cs"));
        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataKind, Is.EqualTo("friend_assembly"));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataActualValue, Is.EqualTo("MyApp.Tools"));
        Assert.That((violations[0].Payload as ProjectMetadataPayload)?.ProjectMetadataSourcePath, Is.EqualTo("src/MyApp/Properties/AssemblyInfo.cs"));
    }

    [Test]
    public void Discovery_SourceLevelInternalsVisibleTo_DoesNotMatchCommentedDeclarations()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(Path.Combine(projectDir, "Properties"));
        File.WriteAllText(Path.Combine(projectDir, "Properties", "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;

            // [assembly: InternalsVisibleTo("MyApp.Tools")]
            [assembly: InternalsVisibleTo("MyApp.Tests")]
            """);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName),
            Is.EqualTo(_myAppTests));
        Assert.That(project.FriendAssemblies.Any(entry => entry.AssemblyName == "MyApp.Tools"), Is.False,
            "Commented InternalsVisibleTo declaration must not be treated as an actual friend assembly.");
    }

    [Test]
    public void Discovery_SourceLevelInternalsVisibleTo_DoesNotMatchBlockCommentedDeclarations()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(Path.Combine(projectDir, "Properties"));
        File.WriteAllText(Path.Combine(projectDir, "Properties", "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;

            /* [assembly: InternalsVisibleTo("MyApp.Tools")] */
            [assembly: InternalsVisibleTo("MyApp.Tests")]
            """);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName),
            Is.EqualTo(_myAppTests));
        Assert.That(project.FriendAssemblies.Any(entry => entry.AssemblyName == "MyApp.Tools"), Is.False,
            "Block-commented InternalsVisibleTo declaration must not be treated as an actual friend assembly.");
    }

    [Test]
    public void Discovery_SourceLevelInternalsVisibleTo_DoesNotMatchMultiLineBlockComment()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(Path.Combine(projectDir, "Properties"));
        File.WriteAllText(Path.Combine(projectDir, "Properties", "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;

            /*
               Previously we used:
               [assembly: InternalsVisibleTo("MyApp.Tools")]
            */
            [assembly: InternalsVisibleTo("MyApp.Tests")]
            """);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName),
            Is.EqualTo(_myAppTests));
        Assert.That(project.FriendAssemblies.Any(entry => entry.AssemblyName == "MyApp.Tools"), Is.False,
            "Multi-line block-commented InternalsVisibleTo declaration must not be treated as an actual friend assembly.");
    }

    [Test]
    public void Discovery_SourceLevelInternalsVisibleTo_IgnoresPreprocessorDisabledDeclarations()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(Path.Combine(projectDir, "Properties"));
        File.WriteAllText(Path.Combine(projectDir, "Properties", "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;

            #if false
            [assembly: InternalsVisibleTo("MyApp.Tools")]
            #endif
            [assembly: InternalsVisibleTo("MyApp.Tests")]
            """);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName),
            Is.EqualTo(_myAppTests));
        Assert.That(project.FriendAssemblies.Any(entry => entry.AssemblyName == "MyApp.Tools"), Is.False,
            "InternalsVisibleTo inside #if false must not be treated as an actual friend assembly.");
    }

    [Test]
    public void Discovery_SourceLevelInternalsVisibleTo_IgnoresStringLiteralContainingPattern()
    {
        string projectDir = Path.Combine(_repoRoot, "src", "MyApp");
        Directory.CreateDirectory(Path.Combine(projectDir, "Properties"));
        File.WriteAllText(Path.Combine(projectDir, "Properties", "AssemblyInfo.cs"), """
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo("MyApp.Tests")]
            internal static class DocExample
            {
                public static string GetMessage()
                {
                    return "Tests may reference friend assemblies via InternalsVisibleTo.";
                }
            }
            """);
        File.WriteAllText(Path.Combine(projectDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "MyApp.csproj") }
            }
        };

        ArchitectureDiscoveredProject project = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false)
            .DiscoveredProjects
            .Single();

        Assert.That(project.FriendAssemblies.Select(entry => entry.AssemblyName),
            Is.EqualTo(_myAppTests));
    }
}
