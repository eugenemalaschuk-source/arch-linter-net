using System.Diagnostics;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("Integration")]
public sealed class EnsureBuiltNonDestructiveIntegrationTests
{
    private string _fixtureRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-ensure-built-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_fixtureRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_fixtureRoot))
        {
            Directory.Delete(_fixtureRoot, true);
        }
    }

    [Test]
    [CancelAfter(180_000)]
    public void CliEnsureBuilt_PreservesCompiledPrimaryOutputs()
    {
        CompiledFixture fixture = CreateAndBuildFixture();
        ArtifactBytes before = fixture.ReadPrimaryOutputs();
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string cliProjectPath = Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "ArchLinterNet.Cli.csproj");

        CommandResult result = RunDotnet(fixture.Root,
            "run", "--project", cliProjectPath, "--no-build", "--",
            "--policy", fixture.PolicyPath,
            "--mode", "strict",
            "--ensure-built",
            "--configuration", "Debug");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Zero, result.CombinedOutput);
            AssertPrimaryOutputsUnchanged(before, fixture.ReadPrimaryOutputs());
        });
    }

    [Test]
    [CancelAfter(180_000)]
    public void TestingApiEnsureBuilt_SequentialValidationsPreserveCompiledPrimaryOutputs()
    {
        CompiledFixture fixture = CreateAndBuildFixture();
        ArtifactBytes before = fixture.ReadPrimaryOutputs();
        ArchitectureValidationBuilder builder = ArchitectureAssertions.FromPolicy(fixture.PolicyPath)
            .WithEnsureBuilt(configuration: "Debug");

        ArchitectureValidationResult first = builder.ValidateStrict();
        ArchitectureValidationResult second = builder.ValidateStrict();

        Assert.Multiple(() =>
        {
            Assert.That(first.Passed, Is.True, string.Join("; ", first.Violations));
            Assert.That(second.Passed, Is.True, string.Join("; ", second.Violations));
            AssertPrimaryOutputsUnchanged(before, fixture.ReadPrimaryOutputs());
        });
    }

    private CompiledFixture CreateAndBuildFixture()
    {
        const string AssemblyName = "EnsureBuiltFixture";
        string projectDirectory = Path.Combine(_fixtureRoot, "src", AssemblyName);
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, $"{AssemblyName}.csproj");
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectDirectory, "Marker.cs"), """
            namespace EnsureBuiltFixture;

            public sealed class Marker
            {
                public void Run() { }
            }
            """);

        string architectureDirectory = Path.Combine(_fixtureRoot, "architecture");
        Directory.CreateDirectory(architectureDirectory);
        string policyPath = Path.Combine(architectureDirectory, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Ensure-built output preservation

            layers:
              fixture:
                namespace: EnsureBuiltFixture

            analysis:
              target_assemblies: [EnsureBuiltFixture]
              projects: ["src/EnsureBuiltFixture/EnsureBuiltFixture.csproj"]

            contracts:
              strict_method_body:
                - name: evaluate-project-context
                  source: fixture
                  forbidden_calls: [System.Console.WriteLine]
            """);

        CommandResult build = RunDotnet(_fixtureRoot, "build", projectPath, "--nologo");
        Assert.That(build.ExitCode, Is.Zero, build.CombinedOutput);
        return new CompiledFixture(_fixtureRoot, policyPath, projectDirectory, AssemblyName);
    }

    private static CommandResult RunDotnet(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutput, standardError);
        return new CommandResult(process.ExitCode, standardOutput.Result, standardError.Result);
    }

    private static void AssertPrimaryOutputsUnchanged(ArtifactBytes before, ArtifactBytes after)
    {
        Assert.That(after.Assembly, Is.EqualTo(before.Assembly), "The selected assembly bytes changed.");
        Assert.That(after.Pdb, Is.EqualTo(before.Pdb), "The selected PDB bytes changed.");
    }

    private sealed record CompiledFixture(string Root, string PolicyPath, string ProjectDirectory, string AssemblyName)
    {
        public ArtifactBytes ReadPrimaryOutputs()
        {
            string outputDirectory = Path.Combine(ProjectDirectory, "bin", "Debug", "net10.0");
            string assemblyPath = Path.Combine(outputDirectory, $"{AssemblyName}.dll");
            string pdbPath = Path.Combine(outputDirectory, $"{AssemblyName}.pdb");
            return new ArtifactBytes(File.ReadAllBytes(assemblyPath), File.ReadAllBytes(pdbPath));
        }
    }

    private sealed record ArtifactBytes(byte[] Assembly, byte[] Pdb);

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => StandardOutput + StandardError;
    }
}
