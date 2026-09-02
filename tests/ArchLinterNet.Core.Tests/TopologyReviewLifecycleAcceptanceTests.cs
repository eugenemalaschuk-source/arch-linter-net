using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// These are deliberately process-level acceptance tests. Unit tests keep the renderers and fake
// runtimes fast; this fixture proves the shipped CLI drives real analysis for both .NET and the
// Unity-shaped Library/ScriptAssemblies layout without modifying a reviewed input.
[TestFixture]
[NonParallelizable]
public sealed class TopologyReviewLifecycleAcceptanceTests
{
    private AdoptionAcceptanceFixture _dotnetFixture = null!;
    private AdoptionAcceptanceFixture _unityFixture = null!;

    [OneTimeSetUp]
    public void SetUpFixtures()
    {
        Assert.That(File.Exists(CliDllPath()), Is.True, $"CLI not built at {CliDllPath()}");

        _dotnetFixture = AdoptionAcceptanceFixture.Create("topology-review-dotnet");
        BuildDotNetFixture(_dotnetFixture);

        _unityFixture = AdoptionAcceptanceFixture.Create("topology-review-unity");
        MaterializeUnityAssemblies(_unityFixture.Root);
    }

    [OneTimeTearDown]
    public void TearDownFixtures()
    {
        _dotnetFixture?.Dispose();
        _unityFixture?.Dispose();
    }

    [Test]
    public void DotNetCaptureAndDiff_ExerciseTheRealLifecycle()
    {
        AssertCaptureAndDiff(new FixtureCase("dotnet", _dotnetFixture, needsBuildStatePreparation: true));
    }

    [Test]
    public void DotNetVerify_PreservesStrictAndAuditLifecycleSemantics()
    {
        AssertVerify(new FixtureCase("dotnet", _dotnetFixture, needsBuildStatePreparation: true));
    }

    [Test]
    public void UnityCaptureDiffAndVerify_ExerciseTheRealLifecycle()
    {
        FixtureCase fixtureCase = new("unity", _unityFixture, needsBuildStatePreparation: false);
        AssertCaptureAndDiff(fixtureCase);
        AssertVerify(fixtureCase);
    }

    private void AssertCaptureAndDiff(FixtureCase fixtureCase)
    {
        IReadOnlyList<string> hashesBefore = HashConsumedSourceInputs(fixtureCase.Fixture.Root);

        CliRun firstCapture = RunCli(fixtureCase,
            "topology", "capture", "--policy", fixtureCase.Policy("capture.arch.yml"),
            "--subject-kind", "assembly", "--format", "json");
        CliRun secondCapture = RunCli(fixtureCase,
            "topology", "capture", "--policy", fixtureCase.Policy("capture.arch.yml"),
            "--subject-kind", "assembly", "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(firstCapture.ExitCode, Is.EqualTo(0), firstCapture.Describe());
            Assert.That(secondCapture.ExitCode, Is.EqualTo(0), secondCapture.Describe());
            Assert.That(secondCapture.StandardOutput, Is.EqualTo(firstCapture.StandardOutput),
                "unchanged capture input must produce identical JSON bytes");
        });
        using (JsonDocument captureDocument = JsonDocument.Parse(firstCapture.StandardOutput))
        {
            Assert.Multiple(() =>
            {
                Assert.That(captureDocument.RootElement.GetProperty("kind").GetString(), Is.EqualTo("topology-capture"));
                Assert.That(captureDocument.RootElement.GetProperty("subjects").GetArrayLength(), Is.GreaterThan(0));
                Assert.That(captureDocument.RootElement.GetProperty("relationships").GetArrayLength(), Is.GreaterThan(0));
            });
        }

        CliRun declaredDiff = RunCli(fixtureCase,
            "topology", "diff", "--policy", fixtureCase.Policy("declared.arch.yml"),
            "--mode", "strict", "--format", "json");
        CliRun structuralDiff = RunCli(fixtureCase,
            "topology", "diff", "--policy", fixtureCase.Policy("declared-structural.arch.yml"),
            "--mode", "strict", "--format", "json");
        CliRun unmappedDiff = RunCli(fixtureCase,
            "topology", "diff", "--policy", fixtureCase.Policy("declared-unmapped.arch.yml"),
            "--mode", "strict", "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(declaredDiff.ExitCode, Is.EqualTo(0), declaredDiff.Describe());
            Assert.That(structuralDiff.ExitCode, Is.EqualTo(0), structuralDiff.Describe());
            Assert.That(unmappedDiff.ExitCode, Is.EqualTo(0), unmappedDiff.Describe());
        });
        using (JsonDocument declaredDocument = JsonDocument.Parse(declaredDiff.StandardOutput))
        using (JsonDocument structuralDocument = JsonDocument.Parse(structuralDiff.StandardOutput))
        using (JsonDocument unmappedDocument = JsonDocument.Parse(unmappedDiff.StandardOutput))
        {
            Assert.Multiple(() =>
            {
                Assert.That(structuralDocument.RootElement.GetProperty("structural").GetArrayLength(), Is.GreaterThan(0));
                Assert.That(declaredDocument.RootElement.GetProperty("relational").GetArrayLength(), Is.GreaterThan(0));
                Assert.That(declaredDocument.RootElement.GetProperty("stale").GetProperty("nodes").GetArrayLength(), Is.GreaterThan(0));
                Assert.That(unmappedDocument.RootElement.GetProperty("unmapped").GetArrayLength(), Is.GreaterThan(0));
            });
        }

        Assert.That(HashConsumedSourceInputs(fixtureCase.Fixture.Root), Is.EqualTo(hashesBefore),
            "capture and diff must not rewrite policies, imports, asmdefs, or source inputs");
    }

    private void AssertVerify(FixtureCase fixtureCase)
    {
        IReadOnlyList<string> hashesBefore = HashConsumedSourceInputs(fixtureCase.Fixture.Root);
        AssertVerifyMatchesOrdinaryValidation(fixtureCase, "strict");
        AssertVerifyMatchesOrdinaryValidation(fixtureCase, "audit");
        Assert.That(HashConsumedSourceInputs(fixtureCase.Fixture.Root), Is.EqualTo(hashesBefore),
            "verify must not rewrite policies, imports, asmdefs, or source inputs");
    }

    private void AssertVerifyMatchesOrdinaryValidation(FixtureCase fixtureCase, string mode)
    {
        string policy = fixtureCase.Policy("declared.arch.yml");
        CliRun ordinary = RunCli(fixtureCase,
            "--policy", policy, "--mode", mode, "--format", "json");
        CliRun topology = RunCli(fixtureCase,
            "topology", "verify", "--policy", policy, "--mode", mode, "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(topology.ExitCode, Is.EqualTo(ordinary.ExitCode),
                $"topology verify must preserve ordinary {mode} exit semantics.{Environment.NewLine}" +
                topology.Describe() + Environment.NewLine + ordinary.Describe());
            Assert.That(topology.StandardOutput, Does.Contain("declared-topology"));
        });
    }

    private static void MaterializeUnityAssemblies(string fixtureRoot)
    {
        string assets = Path.Combine(fixtureRoot, "Assets");
        string output = Path.Combine(fixtureRoot, "Library", "ScriptAssemblies");
        Directory.CreateDirectory(output);

        string runtime = CompileAssembly(
            Path.Combine(output, "TopologyReview.Unity.Runtime.dll"),
            "TopologyReview.Unity.Runtime",
            Path.Combine(assets, "TopologyReview.Unity.Runtime", "RuntimeBootstrap.cs"));
        string gameplay = CompileAssembly(
            Path.Combine(output, "TopologyReview.Unity.Gameplay.dll"),
            "TopologyReview.Unity.Gameplay",
            Path.Combine(assets, "TopologyReview.Unity.Gameplay", "GameplayController.cs"), runtime);
        _ = CompileAssembly(
            Path.Combine(output, "TopologyReview.Unity.Editor.dll"),
            "TopologyReview.Unity.Editor",
            Path.Combine(assets, "TopologyReview.Unity.Editor", "GameplayInspector.cs"), gameplay);
    }

    private static void BuildDotNetFixture(AdoptionAcceptanceFixture fixture)
    {
        string solutionPath = Path.Combine(fixture.Root, "TopologyReview.slnx");
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = fixture.Root,
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(solutionPath);
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("--maxcpucount:1");

        using Process process = Process.Start(startInfo)!;
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $".NET topology fixture failed to build.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
        }
    }

    private static string CompileAssembly(
        string outputPath,
        string assemblyName,
        string sourcePath,
        params string[] referencedAssemblies)
    {
        SyntaxTree source = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath));
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        ];
        references.AddRange(referencedAssemblies.Select(path => MetadataReference.CreateFromFile(path)));
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [source],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using FileStream stream = File.Create(outputPath);
        Microsoft.CodeAnalysis.Emit.EmitResult emitted = compilation.Emit(stream);
        if (!emitted.Success)
        {
            throw new InvalidOperationException(
                "Unity-style fixture assembly failed to compile: " +
                string.Join(Environment.NewLine, emitted.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        return outputPath;
    }

    private static IReadOnlyList<string> HashConsumedSourceInputs(string root)
    {
        return Directory.EnumerateFiles(root, "*.yml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => $"{Path.GetRelativePath(root, path)}:{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))}")
            .ToArray();
    }

    private static CliRun RunCli(FixtureCase fixtureCase, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = fixtureCase.Fixture.Root,
        };
        startInfo.ArgumentList.Add(CliDllPath());
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (fixtureCase.NeedsBuildStatePreparation)
        {
            startInfo.ArgumentList.Add("--ensure-built");
            startInfo.ArgumentList.Add("--no-restore");
        }

        using Process process = Process.Start(startInfo)!;
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliRun(process.ExitCode, standardOutput, standardError);
    }

    private static string CliDllPath() => Path.Combine(
        new ArchitectureRepositoryRootResolver().Resolve(),
        "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");

    private sealed class FixtureCase(string name, AdoptionAcceptanceFixture fixture, bool needsBuildStatePreparation)
    {
        public string Name { get; } = name;

        public AdoptionAcceptanceFixture Fixture { get; } = fixture;

        public bool NeedsBuildStatePreparation { get; } = needsBuildStatePreparation;

        public string Policy(string fileName) => Path.Combine(Fixture.Root, fileName);

        public override string ToString() => Name;
    }

    private sealed record CliRun(int ExitCode, string StandardOutput, string StandardError)
    {
        public string Describe() =>
            $"exit={ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{StandardOutput}" +
            $"{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}";
    }
}
