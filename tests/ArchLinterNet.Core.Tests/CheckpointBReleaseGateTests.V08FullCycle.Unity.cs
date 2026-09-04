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
    // Materializing the fixture's own source into Library/ScriptAssemblies via Roslyn (see
    // MaterializeUnityAssemblies) is not just for topology capture: once those assemblies exist,
    // `validate`/`health`/`gate` resolve analysis.target_assemblies against them exactly like any
    // other packed target, matching what TopologyReviewLifecycleAcceptanceTests.
    // AssertVerifyMatchesOrdinaryValidation already proves in-process. AssertUnityEditorExposureRejection
    // and AssertUnityHealthReportRouting below exercise that same materialized shape through the
    // packed candidate for the remaining shape-specific boundary and the canonical Health/report path.
    private static CheckpointScenarioResult AssertUnityTopologyPackedProof(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture unityFixture = AdoptionAcceptanceFixture.Create("topology-review-unity");
        MaterializeUnityAssemblies(unityFixture.Root);

        string capturePath = Path.Combine(unityFixture.Root, "unity-capture.json");
        CommandResult capture = candidate.RunToolWithReusedRestore(unityFixture.Root,
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
        CommandResult declaredDiff = candidate.RunToolWithReusedRestore(unityFixture.Root,
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
        CommandResult unmappedDiff = candidate.RunToolWithReusedRestore(unityFixture.Root,
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

    // Mandatory negative proof: a runtime-layer asmdef that starts referencing an editor-only asmdef
    // must be rejected by the strict_asmdef `forbidden_editor_refs` contract (contracts.strict_asmdef
    // in declared.arch.yml, id unity-runtime-no-editor) -- runtime/public-surface exposure of
    // editor-only types is exactly what this contract exists to forbid. Paired with a clean-fixture
    // check proving the same contract does not fire spuriously against the checked-in, unmutated
    // asmdefs.
    private static CheckpointScenarioResult AssertUnityEditorExposureRejection(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture cleanFixture = AdoptionAcceptanceFixture.Create("topology-review-unity");
        MaterializeUnityAssemblies(cleanFixture.Root);
        CommandResult clean = candidate.RunToolWithReusedRestore(cleanFixture.Root,
            "--policy", "declared.arch.yml",
            "--mode", "strict",
            "--contract", "unity-runtime-no-editor",
            "--format", "json");
        using (JsonDocument cleanDocument = JsonDocument.Parse(clean.StandardOutput))
        {
            bool cleanHasExposureViolation = cleanDocument.RootElement.GetProperty("violations").EnumerateArray()
                .Any(violation => violation.GetProperty("contract_id").GetString() == "unity-runtime-no-editor");
            Assert.That(cleanHasExposureViolation, Is.False,
                $"v08-unity-editor-exposure-rejection (clean) expected the checked-in asmdefs to declare no editor "
                + $"reference from Runtime/Gameplay: {clean.CombinedOutput}");
        }

        using AdoptionAcceptanceFixture mutatedFixture = AdoptionAcceptanceFixture.Create("topology-review-unity");
        MaterializeUnityAssemblies(mutatedFixture.Root);
        string runtimeAsmdefPath = Path.Combine(
            mutatedFixture.Root, "Assets", "TopologyReview.Unity.Runtime", "TopologyReview.Unity.Runtime.asmdef");
        string mutatedAsmdef = File.ReadAllText(runtimeAsmdefPath)
            .Replace("\"references\": []", "\"references\": [\"TopologyReview.Unity.Editor\"]", StringComparison.Ordinal);
        Assert.That(mutatedAsmdef, Does.Contain("TopologyReview.Unity.Editor"),
            "Diagnostic: the Runtime asmdef's empty references array no longer matches the expected shape to mutate.");
        File.WriteAllText(runtimeAsmdefPath, mutatedAsmdef);

        CommandResult mutated = candidate.RunToolWithReusedRestore(mutatedFixture.Root,
            "--policy", "declared.arch.yml",
            "--mode", "strict",
            "--contract", "unity-runtime-no-editor",
            "--format", "json");
        using (JsonDocument mutatedDocument = JsonDocument.Parse(mutated.StandardOutput))
        {
            JsonElement? exposureViolation = mutatedDocument.RootElement.GetProperty("violations").EnumerateArray()
                .Where(violation => violation.GetProperty("contract_id").GetString() == "unity-runtime-no-editor")
                .Select(violation => (JsonElement?)violation)
                .FirstOrDefault();
            Assert.That(exposureViolation, Is.Not.Null,
                $"v08-unity-editor-exposure-rejection (mutated) expected the Runtime -> Editor asmdef reference to be "
                + $"rejected: {mutated.CombinedOutput}");
            Assert.Multiple(() =>
            {
                Assert.That(exposureViolation!.Value.GetProperty("source").GetString(), Is.EqualTo("TopologyReview.Unity.Runtime"));
                Assert.That(exposureViolation.Value.GetProperty("forbidden_references").EnumerateArray()
                        .Select(reference => reference.GetString()),
                    Does.Contain("TopologyReview.Unity.Editor"));
            });
        }

        return Passed("v08-unity-editor-exposure-rejection");
    }

    // Proves the Unity-shaped candidate routes through the same canonical Health/report/badge
    // pipeline every other v0.8 shape uses -- issue #524's explicit requirement, previously
    // unproven because this scenario never materialized real assemblies before calling
    // `health`/`report pr`/`badge` (a test-setup gap, not a product one: `analysis.target_assemblies`
    // resolves against Library/ScriptAssemblies exactly like any other packed target once those
    // assemblies exist).
    private static CheckpointScenarioResult AssertUnityHealthReportRouting(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture unityFixture = AdoptionAcceptanceFixture.Create("topology-review-unity");
        MaterializeUnityAssemblies(unityFixture.Root);

        string baselinePath = Path.Combine(unityFixture.Root, "unity-health-baseline.arch.yml");
        File.WriteAllText(baselinePath, V08FullCycleFragmentContent.EmptyBaseline);
        string healthPath = Path.Combine(unityFixture.Root, "unity-health.json");

        CommandResult health = candidate.RunToolWithReusedRestore(unityFixture.Root,
            "health",
            "--policy", "declared.arch.yml",
            "--baseline", baselinePath,
            "--mode", "strict",
            "--format", "json",
            "--execution-context", "v08-unity-topology-review");
        File.WriteAllText(healthPath, health.StandardOutput);

        using (JsonDocument healthDocument = JsonDocument.Parse(health.StandardOutput))
        {
            // declared.arch.yml deliberately carries a stale topology node/edge (see the fixture's
            // README): the declared_topology dimension is a required control that cannot be fully
            // assessed while that staleness stands, so the canonical Health category is unassessable
            // here -- a real, deterministic outcome of routing this exact policy through Health, not
            // an arbitrary placeholder.
            Assert.That(healthDocument.RootElement.GetProperty("health").GetString(), Is.EqualTo("unassessable"),
                $"v08-unity-health-report-routing (health): {health.StandardOutput}");
            Assert.That(healthDocument.RootElement.TryGetProperty("report_evidence", out _), Is.True,
                $"v08-unity-health-report-routing expected report_evidence to be populated: {health.StandardOutput}");
        }

        string badgePath = Path.Combine(unityFixture.Root, "unity-badge.json");
        CommandResult badge = candidate.RunToolWithReusedRestore(unityFixture.Root,
            "badge", "architecture-health",
            "--input", healthPath,
            "--output", badgePath);
        using (JsonDocument badgeDocument = JsonDocument.Parse(File.ReadAllText(badgePath)))
        {
            string badgeMessage = badgeDocument.RootElement.GetProperty("message").GetString() ?? string.Empty;
            Assert.That(badgeMessage, Does.StartWith("UNASSESSABLE"),
                $"v08-unity-health-report-routing (badge) expected the badge to carry the same canonical category: {badgeMessage}");
        }

        // `report pr` also requires --change; a trivial base==current snapshot pair is enough since
        // this scenario proves routing, not a bounded delta.
        string snapshotPath = Path.Combine(unityFixture.Root, "unity-snapshot.json");
        CommandResult snapshot = candidate.RunToolWithReusedRestore(unityFixture.Root,
            "change", "snapshot",
            "--policy", "declared.arch.yml",
            "--mode", "strict",
            "--output", snapshotPath);
        Assert.That(snapshot.ExitCode, Is.EqualTo(0), $"v08-unity-health-report-routing (change snapshot): {snapshot.CombinedOutput}");

        string changeReportPath = Path.Combine(unityFixture.Root, "unity-change.json");
        CommandResult changeReport = candidate.RunToolWithReusedRestore(unityFixture.Root,
            "change", "report",
            "--base", snapshotPath,
            "--current", snapshotPath,
            "--execution-context", "v08-unity-topology-review",
            "--format", "json",
            "--output", changeReportPath);
        Assert.That(changeReport.ExitCode, Is.EqualTo(0), $"v08-unity-health-report-routing (change report): {changeReport.CombinedOutput}");

        string reportPath = Path.Combine(unityFixture.Root, "unity-report.md");
        CommandResult report = candidate.RunToolWithReusedRestore(unityFixture.Root,
            "report", "pr",
            "--health", healthPath,
            "--change", changeReportPath,
            "--output", reportPath);
        Assert.That(report.ExitCode, Is.EqualTo(0), $"v08-unity-health-report-routing (report pr): {report.CombinedOutput}");
        Assert.That(File.ReadAllText(reportPath), Is.Not.Empty, "v08-unity-health-report-routing (report pr)");

        return Passed("v08-unity-health-report-routing");
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
