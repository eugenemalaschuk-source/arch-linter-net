using System.Diagnostics;
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

        RunDotnet($"build \"{_consumerProjectPath}\"", _fixtureRoot);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_fixtureRoot))
        {
            Directory.Delete(_fixtureRoot, true);
        }
    }

    private static void RunDotnet(string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(psi)!;
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet {arguments}' failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
        }
    }

    [Test]
    public void Resolve_BuiltProjectWithProjectReference_SucceedsAndResolvesCrossProjectReference()
    {
        ArchitectureProjectRoslynResolution resolution =
            new ArchitectureProjectRoslynContextResolver().Resolve(_consumerProjectPath);

        Assert.That(resolution.Succeeded, Is.True, resolution.FailureReason);
        Assert.That(resolution.Context!.SourceFilePaths, Has.Some.Contains("Caller.cs"));
        Assert.That(resolution.Context!.ReferenceAssemblyPaths,
            Has.Some.Contains("Fixture.Referenced.dll"),
            "Expected the referenced project's build output to be among the resolved references.");
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
}
