using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

using CandidatePackageFeed = CheckpointBReleaseGateTests.CandidatePackageFeed;
using CheckpointScenarioResult = CheckpointBReleaseGateTests.CheckpointScenarioResult;

/// <summary>
/// Owns the v0.8 full-cycle packed-CLI acceptance scenario: the release proof for the one-tool
/// v0.8 workflow, composing the <c>validate</c>/<c>topology</c>/<c>measure</c>,
/// policy-weakening/gate/external-evidence, health-matrix, Unity-shaped, and reporting phase
/// groups against one mutated <c>modular-consumer</c> fixture checkout. The NUnit entrypoint in
/// <c>CheckpointBReleaseGateTests.V08FullCycle.cs</c> only selects and runs this scenario and
/// asserts/publishes its result; it owns none of the orchestration itself.
/// </summary>
internal sealed class CheckpointBV08FullCycleScenario(CandidatePackageFeed candidate)
{
    // The composed scenario legitimately runs dozens of separately bounded CLI phases. Release
    // evidence on macOS x64 measured ~4m25 on an ordinary pass and 5m before cancellation under
    // load; 7m keeps a bounded scenario guard while each child process remains independently
    // limited by CheckpointBProcessRunner.ProcessCompletionTimeout (2m).
    internal const int WatchdogMs = 420_000;

    private readonly CandidatePackageFeed _candidate = candidate;

    internal IReadOnlyList<CheckpointScenarioResult> Run()
    {
        var scenarios = new List<CheckpointScenarioResult>();
        var phaseTrace = new CheckpointBPhaseTrace();
        var runner = new CheckpointBV08ToolRunner(_candidate, phaseTrace);
        var validation = new CheckpointBV08ValidationPhases(runner);
        var policyWeakening = new CheckpointBV08PolicyWeakeningPhases(runner);
        var healthMatrix = new CheckpointBV08HealthMatrixPhases(runner);
        var unity = new CheckpointBV08UnityPhases(runner);
        var reporting = new CheckpointBV08ReportingPhases(runner);

        using GitVersionedAdoptionFixture fixture = GitVersionedAdoptionFixture.Create("modular-consumer");
        fixture.Commit("base");

        string baseDir = Path.Combine(Path.GetTempPath(), $"arch-linter-v08-base-{Guid.NewGuid():N}");
        CopyDirectoryExcludingGit(fixture.Root, baseDir);

        ApplyV08CurrentMutations(fixture.Root);
        string currentSha = fixture.Commit("current");

        try
        {
            scenarios.Add(AssertPolicyCheck(runner, fixture.Root));

            string validSarifPath = Path.Combine(fixture.Root, "evidence", "v08-static-analysis.sarif");
            CheckpointBV08ReportingPhases.WriteSarif(validSarifPath, executionSuccessful: true, resultMessages: []);
            using (JsonDocument sanityCheck = JsonDocument.Parse(File.ReadAllBytes(validSarifPath)))
            {
                Assert.That(sanityCheck.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object),
                    $"Diagnostic: written SARIF at '{validSarifPath}' did not parse. Content: {File.ReadAllText(validSarifPath)}");
            }

            (CheckpointScenarioResult validateScenario, string validateJson, string strictValidateSarifPath) =
                validation.AssertValidateStrictAudit(
                    fixture.Root, validSarifPath, CheckpointBV08EvidenceIdentity.Repository, currentSha, CheckpointBV08EvidenceIdentity.Scope);
            scenarios.Add(validateScenario);
            scenarios.Add(validation.AssertRecursiveExposureEvidence(validateJson));
            scenarios.Add(validation.AssertTopologyCaptureDiffVerify(fixture.Root, currentSha));
            scenarios.Add(validation.AssertTopologyUnmappedSubjectFailsClosed(fixture.Root, currentSha));
            scenarios.Add(validation.AssertMeasureAndBudget(fixture.Root, validateJson));

            string basePolicyContext = Path.Combine(fixture.Root, "v08-policy-base.json");
            string currentPolicyContext = Path.Combine(fixture.Root, "v08-policy-current.json");
            runner.AssertPolicyContext(baseDir, basePolicyContext);
            runner.AssertPolicyContext(fixture.Root, currentPolicyContext);
            scenarios.Add(policyWeakening.AssertPolicyWeakeningAndGate(fixture.Root, basePolicyContext, currentPolicyContext));

            (string parityBasePolicy, string parityCurrentPolicy) = WritePolicyContextParityPolicies(baseDir, fixture.Root);
            string parityBaseContext = Path.Combine(baseDir, "v08-policy-parity-base.json");
            string parityCurrentContext = Path.Combine(fixture.Root, "v08-policy-parity-current.json");
            runner.AssertPolicyContext(baseDir, parityBaseContext, parityBasePolicy);
            runner.AssertPolicyContext(fixture.Root, parityCurrentContext, parityCurrentPolicy);
            string parityBaseline = Path.Combine(fixture.Root, "v08-policy-parity-baseline.arch.yml");
            File.WriteAllText(parityBaseline, V08FullCycleFragmentContent.EmptyBaseline);
            scenarios.Add(runner.AssertExternalTestingPolicyContextConsumer(
                parityCurrentPolicy, parityBaseline, parityBaseContext, parityCurrentContext));

            scenarios.Add(policyWeakening.AssertExternalEvidenceBinding(fixture.Root, validSarifPath, currentSha));

            string baseSnapshot = Path.Combine(fixture.Root, "v08-architecture-base.json");
            string currentSnapshot = Path.Combine(fixture.Root, "v08-architecture-current.json");
            string changeReportPath = Path.Combine(fixture.Root, "v08-architecture-change.json");
            scenarios.Add(policyWeakening.AssertChangeSnapshotAndReport(
                baseDir, fixture.Root, baseSnapshot, currentSnapshot, changeReportPath));

            string healthPath = Path.Combine(fixture.Root, "v08-architecture-health.json");
            scenarios.Add(healthMatrix.AssertHealthMatrix(baseDir, fixture.Root, validSarifPath, currentSha, healthPath));
            scenarios.Add(healthMatrix.AssertHealthMatrixAdvisoryDegrading(baseDir));

            (CheckpointScenarioResult reportScenario, string reportPath) =
                reporting.AssertReportPr(fixture.Root, healthPath, changeReportPath);
            scenarios.Add(reportScenario);
            (CheckpointScenarioResult badgeScenario, string badgePath) = reporting.AssertBadge(fixture.Root, healthPath);
            scenarios.Add(badgeScenario);
            scenarios.Add(reporting.AssertProjectionParity(
                fixture.Root, validateJson, strictValidateSarifPath, healthPath, reportPath, badgePath));
            scenarios.Add(unity.AssertUnityTopologyPackedProof());
            scenarios.Add(unity.AssertUnityEditorExposureRejection());
            scenarios.Add(unity.AssertUnityHealthReportRouting());
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

        string policyPath = CheckpointBV08ToolRunner.DependenciesPath(root);
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

    private static (string BasePolicyPath, string CurrentPolicyPath) WritePolicyContextParityPolicies(
        string baseRoot,
        string currentRoot)
    {
        string basePolicyPath = CheckpointBV08ToolRunner.DependenciesPath(baseRoot);
        string currentFragmentPath = Path.Combine(currentRoot, "fragments", "v08-policy-context-current.yml");
        string moduleContractsPath = Path.Combine(currentRoot, "fragments", "module-contracts.yml");
        string currentFragment = File.ReadAllText(moduleContractsPath).Replace(
            "strict_assembly_allow_only:",
            "audit_assembly_allow_only:",
            StringComparison.Ordinal);
        File.WriteAllText(currentFragmentPath, currentFragment);

        string currentPolicyPath = Path.Combine(currentRoot, "v08-policy-context-current.arch.yml");
        string currentPolicy = File.ReadAllText(CheckpointBV08ToolRunner.DependenciesPath(currentRoot)).Replace(
            "  - fragments/module-contracts.yml",
            "  - fragments/v08-policy-context-current.yml",
            StringComparison.Ordinal);
        File.WriteAllText(currentPolicyPath, currentPolicy);
        return (basePolicyPath, currentPolicyPath);
    }

    private static CheckpointScenarioResult AssertPolicyCheck(CheckpointBV08ToolRunner runner, string root)
    {
        CheckpointBReleaseGateTests.CommandResult result = runner.RunToolWithReusedRestore(
            root, "policy", "check", "--policy", CheckpointBV08ToolRunner.DependenciesPath(root), "--format", "json");
        Assert.That(result.ExitCode, Is.EqualTo(0), $"v08-policy-check: {result.CombinedOutput}");
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object), "v08-policy-check");
        return CheckpointBReleaseGateTests.Passed("v08-policy-check");
    }

    internal static void CopyDirectoryExcludingGit(string source, string destination)
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

    internal static void DeleteDirectoryEventually(string path)
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
}
