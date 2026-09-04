using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    // Packed proof of the Unity-shaped topology-review path (issue #524's library/Unity synthetic
    // fixture requirement), reusing the same checked-in topology-review-unity fixture the in-process
    // TopologyReviewLifecycleAcceptanceTests already exercises, but through the immutable packed
    // candidate instead of the built-in-place CLI. It proves the Runtime/Gameplay/Editor asmdef
    // boundary is captured and reviewed, and that a required first-party subject the declared
    // topology stops mapping fails closed to unmapped -- not silently mapped or dropped.
    //
    // What this does NOT prove, and is scoped out of this scenario rather than faked: `validate`,
    // `health`, and `gate` cannot currently run against a pure asmdef/Unity subject at all --
    // `analysis.target_assemblies` unconditionally requires real assembly resolution regardless of
    // --ensure-built, and Unity assemblies are never produced by `dotnet build`. Editor-exposure
    // denial (the strict_asmdef `forbidden_editor_refs` contract), budget applicability, and the
    // Health/report/badge pipeline for a Unity-shaped candidate therefore remain a genuine product
    // gap, not a test-coverage gap -- forcing a "pass" here without that product capability would
    // recreate exactly the false-green pattern this shard exists to rule out.
    private static CheckpointScenarioResult AssertUnityTopologyPackedProof(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture unityFixture = AdoptionAcceptanceFixture.Create("topology-review-unity");
        MaterializeUnityAssemblies(unityFixture.Root);

        string capturePath = Path.Combine(unityFixture.Root, "unity-capture.json");
        CommandResult capture = candidate.RunTool(unityFixture.Root,
            "topology", "capture",
            "--policy", "capture.arch.yml",
            "--subject-kind", "assembly",
            "--format", "json",
            "--output", capturePath);
        Assert.That(capture.ExitCode, Is.EqualTo(0), $"v08-unity-topology-review (capture): {capture.CombinedOutput}");
        using (JsonDocument captureDocument = JsonDocument.Parse(File.ReadAllText(capturePath)))
        {
            Assert.That(captureDocument.RootElement.GetProperty("subjects").GetArrayLength(), Is.GreaterThan(0),
                "v08-unity-topology-review (capture) expected the Runtime/Gameplay/Editor asmdef boundary to be observed.");
        }

        string declaredDiffPath = Path.Combine(unityFixture.Root, "unity-declared-diff.json");
        CommandResult declaredDiff = candidate.RunTool(unityFixture.Root,
            "topology", "diff",
            "--policy", "declared.arch.yml",
            "--mode", "strict",
            "--format", "json",
            "--output", declaredDiffPath);
        Assert.That(declaredDiff.ExitCode, Is.EqualTo(0), $"v08-unity-topology-review (declared diff): {declaredDiff.CombinedOutput}");
        using (JsonDocument declaredDocument = JsonDocument.Parse(File.ReadAllText(declaredDiffPath)))
        {
            Assert.That(declaredDocument.RootElement.GetProperty("relational").GetArrayLength(), Is.GreaterThan(0),
                "v08-unity-topology-review (declared diff) expected the mapped Runtime/Gameplay/Editor relationships.");
            Assert.That(declaredDocument.RootElement.GetProperty("stale").GetProperty("nodes").GetArrayLength(), Is.GreaterThan(0),
                "v08-unity-topology-review (declared diff) expected the fixture's retired node to surface as stale-declaration evidence.");
        }

        // Mandatory negative proof, mirroring AssertTopologyUnmappedSubjectFailsClosed for the .NET
        // subject kind: a required first-party subject (the editor assembly) the declared topology
        // stops mapping must fail closed to genuinely unmapped, not silently pass.
        string unmappedDiffPath = Path.Combine(unityFixture.Root, "unity-unmapped-diff.json");
        CommandResult unmappedDiff = candidate.RunTool(unityFixture.Root,
            "topology", "diff",
            "--policy", "declared-unmapped.arch.yml",
            "--mode", "strict",
            "--format", "json",
            "--output", unmappedDiffPath);
        Assert.That(unmappedDiff.ExitCode, Is.EqualTo(0), $"v08-unity-topology-review (unmapped diff): {unmappedDiff.CombinedOutput}");
        using (JsonDocument unmappedDocument = JsonDocument.Parse(File.ReadAllText(unmappedDiffPath)))
        {
            Assert.That(unmappedDocument.RootElement.GetProperty("unmapped").GetArrayLength(), Is.GreaterThan(0),
                "v08-unity-topology-review (unmapped diff) expected the editor assembly to fail closed to unmapped once the declared topology stops covering it.");
        }

        return Passed("v08-unity-topology-review");
    }

    // Mirrors TopologyReviewLifecycleAcceptanceTests.MaterializeUnityAssemblies: the fixture's
    // checked-in .asmdef manifests declare a Library/ScriptAssemblies layout Unity itself would
    // produce, but no such Unity build runs here. Compile the fixture's own source files with Roslyn
    // into that exact expected path so --subject-kind assembly resolution has real assemblies to
    // observe, without requiring the Unity editor or any external toolchain.
    private static void MaterializeUnityAssemblies(string fixtureRoot)
    {
        string assets = Path.Combine(fixtureRoot, "Assets");
        string output = Path.Combine(fixtureRoot, "Library", "ScriptAssemblies");
        Directory.CreateDirectory(output);

        string runtime = CompileUnityAssembly(
            Path.Combine(output, "TopologyReview.Unity.Runtime.dll"),
            "TopologyReview.Unity.Runtime",
            Path.Combine(assets, "TopologyReview.Unity.Runtime", "RuntimeBootstrap.cs"));
        string gameplay = CompileUnityAssembly(
            Path.Combine(output, "TopologyReview.Unity.Gameplay.dll"),
            "TopologyReview.Unity.Gameplay",
            Path.Combine(assets, "TopologyReview.Unity.Gameplay", "GameplayController.cs"), runtime);
        _ = CompileUnityAssembly(
            Path.Combine(output, "TopologyReview.Unity.Editor.dll"),
            "TopologyReview.Unity.Editor",
            Path.Combine(assets, "TopologyReview.Unity.Editor", "GameplayInspector.cs"), gameplay);
    }

    private static string CompileUnityAssembly(
        string outputPath, string assemblyName, string sourcePath, params string[] referencedAssemblies)
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
                "Unity-style fixture assembly failed to compile: "
                + string.Join(Environment.NewLine, emitted.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        return outputPath;
    }
}
