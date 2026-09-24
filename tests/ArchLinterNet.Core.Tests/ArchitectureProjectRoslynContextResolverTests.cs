using System.Diagnostics;
using System.Threading;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("E2E")]
public sealed class ArchitectureProjectRoslynContextResolverTests
{
    private string _fixtureRoot = null!;
    private string _referencedProjectPath = null!;
    private string _consumerProjectPath = null!;
    private string _consumerSourceFilePath = null!;
    private string _explicitCompileProjectPath = null!;
    private string _explicitIncludedSourceFilePath = null!;
    private string _explicitExcludedSourceFilePath = null!;
    private string _singleOwnerLinkedProjectPath = null!;
    private string _singleOwnerLinkedSourceFilePath = null!;
    private string _sharedOwnerProjectAPath = null!;
    private string _sharedOwnerProjectBPath = null!;
    private string _multiOwnerLinkedSourceFilePath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-roslyn-context-{Guid.NewGuid():N}");

        string referencedDir = Path.Combine(_fixtureRoot, "Fixture.Referenced");
        Directory.CreateDirectory(referencedDir);
        _referencedProjectPath = Path.Combine(referencedDir, "Fixture.Referenced.csproj");
        File.WriteAllText(_referencedProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(referencedDir, "Widgets.cs"), """
            namespace Fixture.Referenced;

            public static class Widgets
            {
                public static void Build() { }
            }
            """);

        string consumerDir = Path.Combine(_fixtureRoot, "Fixture.Consumer");
        Directory.CreateDirectory(consumerDir);
        _consumerProjectPath = Path.Combine(consumerDir, "Fixture.Consumer.csproj");
        File.WriteAllText(_consumerProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Fixture.Referenced/Fixture.Referenced.csproj" />
              </ItemGroup>
              <Target Name="MutateUnownedOutputDuringDesignTimeBuild" BeforeTargets="Build">
                <WriteLinesToFile
                  File="$(MSBuildProjectDirectory)/bin/Debug/net10.0/Unrelated.dll"
                  Lines="changed-by-design-time-build"
                  Overwrite="true" />
              </Target>
            </Project>
            """);
        _consumerSourceFilePath = Path.Combine(consumerDir, "Caller.cs");
        File.WriteAllText(_consumerSourceFilePath, """
            namespace Fixture.Consumer;

            public class Caller
            {
                public void Run()
                {
                    Fixture.Referenced.Widgets.Build();
                }
            }
            """);

        RunDotnet(_fixtureRoot, "build", _consumerProjectPath);

        string explicitCompileDir = Path.Combine(_fixtureRoot, "Fixture.ExplicitCompile");
        Directory.CreateDirectory(Path.Combine(explicitCompileDir, "Sources"));
        _explicitCompileProjectPath = Path.Combine(explicitCompileDir, "Fixture.ExplicitCompile.csproj");
        File.WriteAllText(_explicitCompileProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Sources/**/*.cs" />
                <Compile Include="Explicit.cs" />
                <Compile Remove="Sources/Excluded.cs" />
              </ItemGroup>
            </Project>
            """);
        _explicitIncludedSourceFilePath = Path.Combine(explicitCompileDir, "Sources", "Included.cs");
        File.WriteAllText(_explicitIncludedSourceFilePath, """
            namespace Fixture.ExplicitCompile;

            public static class Included
            {
                public static string Value => "included";
            }
            """);
        _explicitExcludedSourceFilePath = Path.Combine(explicitCompileDir, "Sources", "Excluded.cs");
        File.WriteAllText(_explicitExcludedSourceFilePath, """
            namespace Fixture.ExplicitCompile;

            public static class Excluded
            {
                public static string Value => "excluded";
            }
            """);
        File.WriteAllText(Path.Combine(explicitCompileDir, "Explicit.cs"), """
            namespace Fixture.ExplicitCompile;

            public static class Explicit
            {
                public static string Value => "explicit";
            }
            """);
        RunDotnet(_fixtureRoot, "build", _explicitCompileProjectPath);

        string sharedDir = Path.Combine(_fixtureRoot, "Shared");
        Directory.CreateDirectory(sharedDir);
        _singleOwnerLinkedSourceFilePath = Path.Combine(sharedDir, "SingleOwner.cs");
        File.WriteAllText(_singleOwnerLinkedSourceFilePath, """
            namespace Fixture.Shared;

            public static class SingleOwner
            {
                public static string Value => "single-owner";
            }
            """);

        string singleOwnerDir = Path.Combine(_fixtureRoot, "Fixture.SingleOwner");
        Directory.CreateDirectory(singleOwnerDir);
        _singleOwnerLinkedProjectPath = Path.Combine(singleOwnerDir, "Fixture.SingleOwner.csproj");
        File.WriteAllText(_singleOwnerLinkedProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="../Shared/SingleOwner.cs" Link="Linked/SingleOwner.cs" />
              </ItemGroup>
            </Project>
            """);
        RunDotnet(_fixtureRoot, "build", _singleOwnerLinkedProjectPath);

        _multiOwnerLinkedSourceFilePath = Path.Combine(sharedDir, "MultiOwner.cs");
        File.WriteAllText(_multiOwnerLinkedSourceFilePath, """
            namespace Fixture.Shared;

            public static class MultiOwner
            {
                public static string Value => "multi-owner";
            }
            """);

        _sharedOwnerProjectAPath = CreateLinkedOwnerProject("Fixture.SharedOwnerA");
        _sharedOwnerProjectBPath = CreateLinkedOwnerProject("Fixture.SharedOwnerB");
        RunDotnet(_fixtureRoot, "build", _sharedOwnerProjectAPath);
        RunDotnet(_fixtureRoot, "build", _sharedOwnerProjectBPath);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_fixtureRoot))
        {
            Directory.Delete(_fixtureRoot, true);
        }
    }

    private static void RunDotnet(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet' failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
        }
    }

    [Test]
    public void Resolve_BuiltProjectWithProjectReference_SucceedsAndResolvesCrossProjectReference()
    {
        ArchitectureProjectRoslynResolution resolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(_consumerProjectPath);

        string? referencedAssemblyPath = resolution.Context?.ReferenceAssemblyPaths
            .FirstOrDefault(path => path.Contains("Fixture.Referenced.dll", StringComparison.Ordinal));
        string projectIntermediateDirectory = Path.Combine(Path.GetDirectoryName(_consumerProjectPath)!, "obj");
        string[] generatedCleanFiles = Directory.GetFiles(
            projectIntermediateDirectory, "ArchLinterNet.DesignTime.*.FileListAbsolute.txt", SearchOption.AllDirectories);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Succeeded, Is.True, resolution.FailureReason);
            Assert.That(ContainsSourceFile(resolution.Context!.SourceFilePaths, _consumerSourceFilePath), Is.True,
                "The ordinary project-owned source file must come from evaluated Compile items.");
            Assert.That(referencedAssemblyPath, Is.Not.Null,
                "Expected the referenced project's build output to be among the resolved references.");
            Assert.That(File.Exists(referencedAssemblyPath), Is.True,
                "The referenced project assembly must remain available after Resolve returns.");
            Assert.That(generatedCleanFiles, Is.Empty,
                "Design-time clean manifests must be removed after resolution.");
        });
    }

    [Test]
    public void Resolve_SourceFilePaths_HonorExplicitCompileGlobIncludeAndExclude()
    {
        ArchitectureProjectRoslynResolution resolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(_explicitCompileProjectPath);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Succeeded, Is.True, resolution.FailureReason);
            Assert.That(ContainsSourceFile(resolution.Context!.SourceFilePaths, _explicitIncludedSourceFilePath), Is.True,
                "A source file selected by the explicit Compile glob must be resolved.");
            Assert.That(ContainsSourceFile(
                resolution.Context.SourceFilePaths,
                Path.Combine(Path.GetDirectoryName(_explicitCompileProjectPath)!, "Explicit.cs")), Is.True,
                "A source file selected by an explicit Compile Include must be resolved.");
            Assert.That(ContainsSourceFile(resolution.Context.SourceFilePaths, _explicitExcludedSourceFilePath), Is.False,
                "A source file removed from the evaluated Compile items must not be resolved.");
        });
    }

    [Test]
    public void Resolve_SourceFilePaths_MapSingleOwnerLinkedFileToIncludingProject()
    {
        ArchitectureProjectRoslynResolution resolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(_singleOwnerLinkedProjectPath);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Succeeded, Is.True, resolution.FailureReason);
            Assert.That(ContainsSourceFile(resolution.Context!.SourceFilePaths, _singleOwnerLinkedSourceFilePath), Is.True,
                "The linked source file must be resolved from the including project's evaluated Compile item.");
        });
    }

    [Test]
    public void Resolve_SourceFilePaths_MapSharedLinkedFileToEveryIncludingProject()
    {
        ArchitectureProjectRoslynResolution projectAResolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(_sharedOwnerProjectAPath);
        ArchitectureProjectRoslynResolution projectBResolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(_sharedOwnerProjectBPath);

        Assert.Multiple(() =>
        {
            Assert.That(projectAResolution.Succeeded, Is.True, projectAResolution.FailureReason);
            Assert.That(projectBResolution.Succeeded, Is.True, projectBResolution.FailureReason);
            Assert.That(ContainsSourceFile(projectAResolution.Context!.SourceFilePaths, _multiOwnerLinkedSourceFilePath), Is.True,
                "The shared source file must be resolved for the first including project.");
            Assert.That(ContainsSourceFile(projectBResolution.Context!.SourceFilePaths, _multiOwnerLinkedSourceFilePath), Is.True,
                "The shared source file must be resolved for the second including project.");
        });
    }

    [Test]
    public void Resolve_BuiltProject_PreservesPrimaryOutputs()
    {
        string outputDirectory = Path.Combine(
            Path.GetDirectoryName(_consumerProjectPath)!, "bin", "Debug", "net10.0");
        string assemblyPath = Path.Combine(outputDirectory, "Fixture.Consumer.dll");
        string pdbPath = Path.Combine(outputDirectory, "Fixture.Consumer.pdb");
        byte[] assemblyBefore = File.ReadAllBytes(assemblyPath);
        byte[] pdbBefore = File.ReadAllBytes(pdbPath);

        ArchitectureProjectRoslynResolution resolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(_consumerProjectPath);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Succeeded, Is.True, resolution.FailureReason);
            Assert.That(File.ReadAllBytes(assemblyPath), Is.EqualTo(assemblyBefore));
            Assert.That(File.ReadAllBytes(pdbPath), Is.EqualTo(pdbBefore));
        });
    }

    [Test]
    public void Resolve_BuiltProject_DoesNotExposeTornOutputsOrRestoreUnownedFiles()
    {
        string outputDirectory = Path.Combine(
            Path.GetDirectoryName(_consumerProjectPath)!, "bin", "Debug", "net10.0");
        string assemblyPath = Path.Combine(outputDirectory, "Fixture.Consumer.dll");
        string pdbPath = Path.Combine(outputDirectory, "Fixture.Consumer.pdb");
        string unownedPath = Path.Combine(outputDirectory, "Unrelated.dll");
        byte[] assemblyBefore = File.ReadAllBytes(assemblyPath);
        byte[] pdbBefore = File.ReadAllBytes(pdbPath);
        File.WriteAllText(unownedPath, "before-design-time-build");

        using CancellationTokenSource cancellation = new();
        using ManualResetEventSlim readerStarted = new();
        Exception? readerFailure = null;
        Task reader = Task.Run(() =>
        {
            readerStarted.Set();
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    if (!File.ReadAllBytes(assemblyPath).AsSpan().SequenceEqual(assemblyBefore)
                        || !File.ReadAllBytes(pdbPath).AsSpan().SequenceEqual(pdbBefore))
                    {
                        Interlocked.CompareExchange(ref readerFailure,
                            new InvalidOperationException("A primary output changed while project context was resolving."), null);
                        return;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Interlocked.CompareExchange(ref readerFailure, ex, null);
                    return;
                }
            }
        });

        readerStarted.Wait();
        ArchitectureProjectRoslynResolution resolution;
        try
        {
            resolution = new ArchitectureProjectRoslynContextResolver().Resolve(_consumerProjectPath);
        }
        finally
        {
            cancellation.Cancel();
            reader.GetAwaiter().GetResult();
        }

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Succeeded, Is.True, resolution.FailureReason);
            Assert.That(readerFailure, Is.Null, readerFailure?.ToString());
            Assert.That(File.ReadAllBytes(assemblyPath), Is.EqualTo(assemblyBefore));
            Assert.That(File.ReadAllBytes(pdbPath), Is.EqualTo(pdbBefore));
            Assert.That(File.ReadAllText(unownedPath), Does.Contain("changed-by-design-time-build"));
        });
    }

    [Test]
    public void Resolve_ProjectNeverRestored_FailsWithReason()
    {
        string notRestoredDir = Path.Combine(_fixtureRoot, "Fixture.NotRestored");
        Directory.CreateDirectory(notRestoredDir);
        string projectPath = Path.Combine(notRestoredDir, "Fixture.NotRestored.csproj");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(notRestoredDir, "Empty.cs"), "namespace Fixture.NotRestored;\n");

        ArchitectureProjectRoslynResolution resolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(projectPath);

        Assert.That(resolution.Succeeded, Is.False);
        Assert.That(resolution.FailureReason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Resolve_ProjectFileDoesNotExist_FailsWithReason()
    {
        ArchitectureProjectRoslynResolution resolution = new ArchitectureProjectRoslynContextResolver()
            .Resolve(Path.Combine(_fixtureRoot, "DoesNotExist", "Missing.csproj"));

        Assert.That(resolution.Succeeded, Is.False);
        Assert.That(resolution.FailureReason, Does.Contain("does not exist"));
    }

    [Test]
    public void Resolve_PreCancelledToken_PropagatesCancellationInsteadOfReturningEvaluationFailure()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new ArchitectureProjectRoslynContextResolver()
            .Resolve(_consumerProjectPath, cts.Token));
    }

    private string CreateLinkedOwnerProject(string projectName)
    {
        string projectDirectory = Path.Combine(_fixtureRoot, projectName);
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, $"{projectName}.csproj");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="../Shared/MultiOwner.cs" Link="Linked/MultiOwner.cs" />
              </ItemGroup>
            </Project>
            """);
        return projectPath;
    }

    private static bool ContainsSourceFile(IReadOnlyList<string> sourceFiles, string expectedPath)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string normalizedExpectedPath = Path.GetFullPath(expectedPath);

        return sourceFiles.Any(sourceFile => string.Equals(
            Path.GetFullPath(sourceFile), normalizedExpectedPath, comparison));
    }
}
