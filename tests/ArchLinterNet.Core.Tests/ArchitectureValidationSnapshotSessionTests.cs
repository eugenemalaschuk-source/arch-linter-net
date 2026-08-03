using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Testing API surface for issue #363: ArchitectureValidationBuilder.CreateSnapshot() lets a test
// explicitly own one ArchitectureAnalysisSnapshot across multiple strict/audit assertions.
[TestFixture]
public sealed class ArchitectureValidationSnapshotSessionTests
{
    private static string WritePolicy(string yaml)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-snapshot-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, yaml);
        return policyPath;
    }

    private static string CreateHarmlessPolicyPath()
    {
        return WritePolicy("""
            version: 1
            name: Test

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution

            analysis:
              target_assemblies: [ArchLinterNet.Core]
            """);
    }

    [Test]
    public void CreateSnapshot_ValidatesStrictAndAudit_SharesOneSnapshotAndDisposesDeterministically()
    {
        string policyPath = CreateHarmlessPolicyPath();
        var builder = new ArchitectureValidationBuilder(policyPath);

        using ArchitectureValidationSnapshotSession session = builder.CreateSnapshot();
        ArchitectureValidationResult strict = session.ValidateStrict();
        ArchitectureValidationResult audit = session.ValidateAudit();

        Assert.Multiple(() =>
        {
            Assert.That(strict.Passed, Is.True);
            Assert.That(audit.Passed, Is.True);
            Assert.That(session.Counters.PolicyCompositions, Is.EqualTo(1));
            Assert.That(session.Counters.ProjectGraphEvaluations, Is.EqualTo(1));
            Assert.That(session.Counters.ModesEvaluated, Is.EqualTo(2));
        });
    }

    [Test]
    public void ValidateStrictAndValidateAudit_OnBuilderDirectly_RemainIndependentRuns()
    {
        string policyPath = CreateHarmlessPolicyPath();
        var builder = new ArchitectureValidationBuilder(policyPath);

        ArchitectureValidationResult strict = builder.ValidateStrict();
        ArchitectureValidationResult audit = builder.ValidateAudit();

        Assert.Multiple(() =>
        {
            Assert.That(strict.Passed, Is.True);
            Assert.That(audit.Passed, Is.True);
        });
    }

    // Issue #375: WithCancellation gives the Testing API the same cancellation semantics as the
    // CLI — a caller-supplied token that has already been cancelled must stop ValidateStrict()/
    // CreateSnapshot() with OperationCanceledException rather than running to completion.
    [Test]
    public void WithCancellation_AlreadyCancelledToken_ValidateStrictThrowsOperationCanceled()
    {
        string policyPath = CreateHarmlessPolicyPath();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        var builder = new ArchitectureValidationBuilder(policyPath).WithCancellation(cts.Token);

        Assert.Throws<OperationCanceledException>(() => builder.ValidateStrict());
    }

    [Test]
    public void WithCancellation_AlreadyCancelledToken_CreateSnapshotThrowsOperationCanceled()
    {
        string policyPath = CreateHarmlessPolicyPath();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        var builder = new ArchitectureValidationBuilder(policyPath).WithCancellation(cts.Token);

        Assert.Throws<OperationCanceledException>(() => builder.CreateSnapshot());
    }

    // Issue #406: WithEnsureBuilt/WithNoRestore feed the same platform/runtime-identifier
    // preparation request as the CLI's --ensure-built --platform/--runtime-identifier options
    // (see ValidateCommandHandler.Execution.cs). No project_patterns are configured here, so
    // preflight has nothing to evaluate against and the run proceeds unblocked — this only proves
    // the requested platform/runtime identifier flow through to preparation without error.
    [Test]
    public void WithEnsureBuilt_PlatformAndRuntimeIdentifier_FlowThroughToValidation()
    {
        string policyPath = CreateHarmlessPolicyPath();
        var builder = new ArchitectureValidationBuilder(policyPath)
            .WithEnsureBuilt(configuration: "Debug", targetFramework: "net10.0", platform: "AnyCPU", runtimeIdentifier: "linux-x64")
            .WithNoRestore();

        ArchitectureValidationResult result = builder.ValidateStrict();

        Assert.That(result.Passed, Is.True);
    }
}
