using System.Text.Json;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CycleRecorderProfileIntegrationTests
{
    [Test]
    public void ProfiledCycleRuns_PreserveResultsAndReportStrictAuditEmptyAndDisabledWorkloads()
    {
        string policyPath = WriteCyclePolicy();
        try
        {
            ArchitectureValidationBuilder builder = new(policyPath);

            ArchitectureValidationResult unprofiledStrict = builder.ValidateStrict();
            ArchitectureValidationResult profiledStrict = new ArchitectureValidationBuilder(policyPath)
                .WithProfile()
                .ValidateStrict();
            ArchitectureValidationResult unprofiledAudit = new ArchitectureValidationBuilder(policyPath)
                .ValidateAudit();
            ArchitectureValidationResult profiledAudit = new ArchitectureValidationBuilder(policyPath)
                .WithProfile()
                .ValidateAudit();
            ArchitectureValidationResult disabledRecording = new ArchitectureValidationBuilder(policyPath)
                .WithProfile()
                .WithUnmatchedIgnoredViolationsPolicy()
                .ValidateStrict();
            ArchitectureValidationResult timingOnly = new ArchitectureValidationBuilder(policyPath)
                .WithTimings()
                .ValidateStrict();

            AssertEquivalent(unprofiledStrict, profiledStrict);
            AssertEquivalent(unprofiledAudit, profiledAudit);

            AnalysisProfileCycleRecorderMeasurements strictRecorder = profiledStrict.Profile!.Measurements!.CycleRecorder!;
            AnalysisProfileCycleRecorderInvocation strictCycle = strictRecorder.Invocations.Single(
                invocation => invocation.CandidateEvidenceCount > 0);
            AnalysisProfileCycleRecorderInvocation strictEmpty = strictRecorder.Invocations.Single(
                invocation => invocation.CandidateEvidenceCount == 0);
            AnalysisProfileCycleRecorderInvocation auditCycle = profiledAudit.Profile!.Measurements!.CycleRecorder!
                .Invocations.Single();
            AnalysisProfileCycleRecorderInvocation disabledCycle = disabledRecording.Profile!.Measurements!.CycleRecorder!
                .Invocations.Single(invocation => invocation.CandidateEvidenceCount > 0);
            string cycleRecorderJson = JsonSerializer.Serialize(strictRecorder);

            Assert.Multiple(() =>
            {
                Assert.That(strictRecorder.InvocationCount, Is.EqualTo(2));
                Assert.That(strictRecorder.EnabledInvocationCount, Is.EqualTo(2));
                Assert.That(strictRecorder.EmptyEvidenceInvocationCount, Is.EqualTo(1));
                Assert.That(strictCycle.Mode, Is.EqualTo("strict"));
                Assert.That(strictCycle.Vertices, Is.EqualTo(2));
                Assert.That(strictCycle.Edges, Is.EqualTo(2));
                Assert.That(strictCycle.CandidateEvidenceCount, Is.EqualTo(8));
                Assert.That(strictCycle.UniqueCandidateEdgePairs, Is.EqualTo(2));
                Assert.That(strictCycle.EmittedCandidateCount, Is.EqualTo(8));
                Assert.That(strictCycle.Work.ComponentIndexBuildCount, Is.EqualTo(1));
                Assert.That(strictCycle.Work.PreparedVertices, Is.EqualTo(2));
                Assert.That(strictCycle.Work.PreparedEdges, Is.EqualTo(2));
                Assert.That(strictEmpty.Work.ComponentIndexBuildCount, Is.Zero);
                Assert.That(strictEmpty.Work.CandidateEvidenceChecks, Is.Zero);
                Assert.That(auditCycle.Mode, Is.EqualTo("audit"));
                Assert.That(auditCycle.EmittedCandidateCount, Is.EqualTo(8));
                Assert.That(disabledCycle.RecordingEnabled, Is.False);
                Assert.That(disabledCycle.CandidateEvidenceCount, Is.EqualTo(8));
                Assert.That(disabledCycle.EmittedCandidateCount, Is.Zero);
                Assert.That(disabledCycle.Work.ComponentIndexBuildCount, Is.Zero);
                Assert.That(disabledCycle.Work.CandidateEvidenceChecks, Is.Zero);
                Assert.That(timingOnly.Timing!.CycleRecorderObservations, Is.Empty);
                Assert.That(cycleRecorderJson, Does.Not.Contain("strict-cycle-recorder-profile"));
                Assert.That(cycleRecorderJson, Does.Not.Contain("audit-cycle-recorder-profile"));
                Assert.That(cycleRecorderJson, Does.Not.Contain("CycleRecorderFixtures"));
                Assert.That(cycleRecorderJson, Does.Not.Contain("LeftNode"));
                Assert.That(cycleRecorderJson, Does.Not.Contain("RightNode"));
            });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, recursive: true);
        }
    }

    private static void AssertEquivalent(
        ArchitectureValidationResult expected,
        ArchitectureValidationResult actual)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.Passed, Is.EqualTo(expected.Passed));
            Assert.That(actual.Violations, Is.EqualTo(expected.Violations));
            Assert.That(actual.Cycles, Is.EqualTo(expected.Cycles));
            Assert.That(JsonSerializer.Serialize(actual.CycleFindings), Is.EqualTo(JsonSerializer.Serialize(expected.CycleFindings)));
            Assert.That(JsonSerializer.Serialize(actual.Findings), Is.EqualTo(JsonSerializer.Serialize(expected.Findings)));
        });
    }

    private static string WriteCyclePolicy()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-cycle-recorder-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Cycle recorder profile integration

            layers:
              left:
                namespace: ArchLinterNet.Core.Tests.CycleRecorderFixtures.Left
              right:
                namespace: ArchLinterNet.Core.Tests.CycleRecorderFixtures.Right
              isolated:
                namespace: ArchLinterNet.Core.Tests.CycleRecorderFixtures.Isolated

            contracts:
              strict_cycles:
                - id: strict-cycle-recorder-profile
                  name: strict-cycle-recorder-profile
                  layers: [left, right]
                  reason: Exercise a real inter-layer cycle.
                - id: strict-empty-recorder-profile
                  name: strict-empty-recorder-profile
                  layers: [left, isolated]
                  reason: Exercise a recorder call with no candidate evidence.
              audit_cycles:
                - id: audit-cycle-recorder-profile
                  name: audit-cycle-recorder-profile
                  layers: [left, right]
                  reason: Exercise an audit inter-layer cycle.

            analysis:
              unmatched_ignored_violations: off
              target_assemblies: [ArchLinterNet.Core.Tests]
            """);
        return policyPath;
    }
}
