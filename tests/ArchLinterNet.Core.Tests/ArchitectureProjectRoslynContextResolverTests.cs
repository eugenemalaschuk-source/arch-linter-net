using System.Diagnostics;
using System.Threading;
using ArchLinterNet.Core.Discovery;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureProjectRoslynContextResolverTests
{
    private string _fixtureRoot = null!;
    private string _referencedProjectPath = null!;
    private string _consumerProjectPath = null!;

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
        File.WriteAllText(Path.Combine(consumerDir, "Caller.cs"), """
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
            Assert.That(resolution.Context!.SourceFilePaths, Has.Some.Contains("Caller.cs"));
            Assert.That(referencedAssemblyPath, Is.Not.Null,
                "Expected the referenced project's build output to be among the resolved references.");
            Assert.That(File.Exists(referencedAssemblyPath), Is.True,
                "The referenced project assembly must remain available after Resolve returns.");
            Assert.That(generatedCleanFiles, Is.Empty,
                "Design-time clean manifests must be removed after resolution.");
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
}
