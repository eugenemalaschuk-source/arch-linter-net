using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    // Synthetic, deliberately-fictitious evidence-context identity for the v0.8 full-cycle
    // external-SARIF binding proof; never a real repository.
    private const string V08EvidenceRepository = "https://example.test/synthetic/v08-full-cycle";
    private const string V08EvidenceScope = "checkpoint-b-v08";

    // SarifEvidenceReader.FileSystem.cs deliberately treats a rooted path (or one containing ':')
    // as unsafe and refuses to read it -- evidence paths are always resolved relative to the
    // analyzed repository root. The --external-evidence "path=" binding must therefore use this
    // repository-relative form, never the absolute validSarifPath used for File I/O.
    private const string V08EvidenceRelativePath = "evidence/v08-static-analysis.sarif";

    // The composed scenario legitimately runs dozens of separately bounded CLI phases. Release
    // evidence on macOS x64 measured ~4m25 on an ordinary pass and 5m before cancellation under
    // load; 7m keeps a bounded scenario guard while each child process remains independently
    // limited by CheckpointBProcessRunner.ProcessCompletionTimeout (2m).
    private const int V08FullCycleWatchdogMs = 420_000;

    [Test]
    [CancelAfter(V08FullCycleWatchdogMs)]
    public void PackedCandidate_V08FullCycle()
    {
        CandidatePackageFeed candidate = Candidate;
        candidate.WriteShardEvidence("v08-full-cycle", AssertV08FullCycle(candidate));
    }

    private static List<CheckpointScenarioResult> AssertV08FullCycle(CandidatePackageFeed candidate)
    {
        var scenarios = new List<CheckpointScenarioResult>();
        var phaseTrace = new CheckpointBPhaseTrace();
        using GitVersionedAdoptionFixture fixture = GitVersionedAdoptionFixture.Create("modular-consumer");
        fixture.Commit("base");

        string baseDir = Path.Combine(Path.GetTempPath(), $"arch-linter-v08-base-{Guid.NewGuid():N}");
        CopyDirectoryExcludingGit(fixture.Root, baseDir);

        ApplyV08CurrentMutations(fixture.Root);
        string currentSha = fixture.Commit("current");

        try
        {
            using (candidate.BeginPhaseTrace(phaseTrace))
            {
                scenarios.Add(AssertPolicyCheck(candidate, fixture.Root));

                string validSarifPath = Path.Combine(fixture.Root, "evidence", "v08-static-analysis.sarif");
                WriteSarif(validSarifPath, executionSuccessful: true, resultMessages: []);
                using (JsonDocument sanityCheck = JsonDocument.Parse(File.ReadAllBytes(validSarifPath)))
                {
                    Assert.That(sanityCheck.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object),
                        $"Diagnostic: written SARIF at '{validSarifPath}' did not parse. Content: {File.ReadAllText(validSarifPath)}");
                }

                (CheckpointScenarioResult validateScenario, string validateJson, string strictValidateSarifPath) =
                    AssertValidateStrictAudit(
                        candidate, fixture.Root, validSarifPath, V08EvidenceRepository, currentSha, V08EvidenceScope);
                scenarios.Add(validateScenario);
                scenarios.Add(AssertRecursiveExposureEvidence(validateJson));
                scenarios.Add(AssertTopologyCaptureDiffVerify(candidate, fixture.Root, currentSha));
                scenarios.Add(AssertTopologyUnmappedSubjectFailsClosed(candidate, fixture.Root, currentSha));
                scenarios.Add(AssertMeasureAndBudget(candidate, fixture.Root, validateJson));

                string basePolicyContext = Path.Combine(fixture.Root, "v08-policy-base.json");
                string currentPolicyContext = Path.Combine(fixture.Root, "v08-policy-current.json");
                AssertPolicyContext(candidate, baseDir, basePolicyContext);
                AssertPolicyContext(candidate, fixture.Root, currentPolicyContext);
                scenarios.Add(AssertPolicyWeakeningAndGate(candidate, fixture.Root, basePolicyContext, currentPolicyContext));

                scenarios.Add(AssertExternalEvidenceBinding(candidate, fixture.Root, validSarifPath, currentSha));

                string baseSnapshot = Path.Combine(fixture.Root, "v08-architecture-base.json");
                string currentSnapshot = Path.Combine(fixture.Root, "v08-architecture-current.json");
                string changeReportPath = Path.Combine(fixture.Root, "v08-architecture-change.json");
                scenarios.Add(AssertChangeSnapshotAndReport(
                    candidate, baseDir, fixture.Root, baseSnapshot, currentSnapshot, changeReportPath));

                string healthPath = Path.Combine(fixture.Root, "v08-architecture-health.json");
                scenarios.Add(AssertHealthMatrix(
                    candidate, baseDir, fixture.Root, validSarifPath, currentSha, healthPath));
                scenarios.Add(AssertHealthMatrixAdvisoryDegrading(candidate, baseDir));

                (CheckpointScenarioResult reportScenario, string reportPath) =
                    AssertReportPr(candidate, fixture.Root, healthPath, changeReportPath);
                scenarios.Add(reportScenario);
                (CheckpointScenarioResult badgeScenario, string badgePath) = AssertBadge(candidate, fixture.Root, healthPath);
                scenarios.Add(badgeScenario);
                scenarios.Add(AssertProjectionParity(
                    candidate, fixture.Root, validateJson, strictValidateSarifPath, healthPath, reportPath, badgePath));
                scenarios.Add(AssertUnityTopologyPackedProof(candidate));
                scenarios.Add(AssertUnityEditorExposureRejection(candidate));
                scenarios.Add(AssertUnityHealthReportRouting(candidate));
            }
        }
        catch (OperationCanceledException)
        {
            TestContext.Out.WriteLine(phaseTrace.FormatCancellation());
            throw;
        }
        finally
        {
            DeleteDirectoryEventually(baseDir);
        }

        TestContext.Out.WriteLine(phaseTrace.FormatCompleted());
        return scenarios;
    }

    private static void ApplyV08CurrentMutations(string root)
    {
        string fragmentPath = Path.Combine(root, "fragments", "v08-full-cycle.yml");
        string fragment = string.Join(
            Environment.NewLine + Environment.NewLine,
            V08FullCycleFragmentContent.TopologyAndMetrics,
            V08FullCycleFragmentContent.Contracts,
            V08FullCycleFragmentContent.ExternalEvidence);
        File.WriteAllText(fragmentPath, fragment);

        string policyPath = DependenciesPath(root);
        string policy = File.ReadAllText(policyPath);
        const string ImportsMarker = "imports:";
        int importsIndex = policy.IndexOf(ImportsMarker, StringComparison.Ordinal);
        if (importsIndex < 0)
        {
            throw new InvalidOperationException($"'{policyPath}' has no imports: block to extend.");
        }

        int insertAt = importsIndex + ImportsMarker.Length;
        policy = policy.Insert(insertAt, $"{Environment.NewLine}  - fragments/v08-full-cycle.yml");
        File.WriteAllText(policyPath, policy);

        string internalDirectory = Path.Combine(root, "src", "Synthetic.Modules.M01", "Internal");
        Directory.CreateDirectory(internalDirectory);
        File.WriteAllText(
            Path.Combine(internalDirectory, "ModuleInternalState.cs"),
            V08FullCycleFragmentContent.ModuleInternalStateSource);
        File.WriteAllText(
            Path.Combine(root, "src", "Synthetic.Modules.M01", "ModuleContracts.cs"),
            V08FullCycleFragmentContent.ModuleContractsSource);
    }

    private static string DependenciesPath(string root) => Path.Combine(root, "dependencies.arch.yml");

    private static void DeleteDirectoryEventually(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leaked temporary fixture must never fail an otherwise passing test.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CopyDirectoryExcludingGit(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (name is ".git" or "bin" or "obj")
            {
                continue;
            }

            CopyDirectoryExcludingGit(directory, Path.Combine(destination, name));
        }
    }

    private static CheckpointScenarioResult AssertPolicyCheck(CandidatePackageFeed candidate, string root)
    {
        CommandResult result = candidate.RunToolWithReusedRestore(root, "policy", "check", "--policy", DependenciesPath(root), "--format", "json");
        Assert.That(result.ExitCode, Is.EqualTo(0), $"v08-policy-check: {result.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object), "v08-policy-check");
        return Passed("v08-policy-check");
    }
}
