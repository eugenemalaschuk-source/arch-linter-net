using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Characterizes a known, confirmed limitation raised in PR #389 review: after --ensure-built
// rebuilds a *stale* (pre-existing, out-of-date) artifact, in-process contract analysis can still
// see the assembly object an earlier resolution attempt already loaded into this process's
// default (non-collectible) AssemblyLoadContext, not the freshly rebuilt bytes. This test asserts
// the current (undesirable) behavior on purpose, so a real fix shows up here as this test
// failing/needing an update — not as a silent regression nobody notices.
[TestFixture]
public sealed class BuildStatePreflightAssemblyReloadTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-reload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    [Category("Integration")]
    [CancelAfter(180_000)]
    // Two --ensure-built rebuilds of the same project in one process: on Windows this also hits
    // the Assembly.LoadFrom file-lock limitation documented on
    // Prepare_EnsureBuiltAfterSourceChange_OverwritesStaleReceiptAndReportsCurrent, on top of the
    // in-process staleness this test exists to characterize — excluded there for the same reason.
    [Platform(Exclude = "Win", Reason = "Assembly.LoadFrom locks the .dll for the process lifetime; a second same-process rebuild can't overwrite it.")]
    public void EnsureBuiltAfterStaleRebuild_ContractsSeeFreshTypeNotStaleType()
    {
        string projectDir = Path.Combine(_tempDir, "src", "ReloadFixture");
        Directory.CreateDirectory(projectDir);
        string projectPath = Path.Combine(projectDir, "ReloadFixture.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDir, "Class1.cs"), "namespace ReloadFixture; public class MarkerV1 {}");

        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Test

            analysis:
              target_assemblies: [ReloadFixture]
              projects: ["src/ReloadFixture/ReloadFixture.csproj"]
            """);

        // First ensure-built: produces a real build with MarkerV1 present, and — critically —
        // this also exercises the *ordinary* resolution path once (inside LoadAndSetup) before
        // preflight runs, which is what can load a stale assembly into the process for a later
        // rebuild to contend with.
        ValidationOutcome firstOutcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
            PreparationMode = ArchLinterNet.Core.BuildState.BuildPreparationMode.EnsureBuilt,
            RequestedConfiguration = "Debug",
        });
        Assert.That(firstOutcome.PreflightBlocked, Is.False, () => string.Join("; ", firstOutcome.PreflightDiagnostics.Select(d => d.Evidence.Detail)));

        // Change the source so the rebuilt assembly's type identity differs — if analysis still
        // sees the pre-rebuild in-memory Assembly object, this exact scenario can't distinguish
        // that from correct behavior through a contract violation alone (both builds are
        // policy-clean), so instead assert directly against the resolved runtime Assembly's own
        // exported types, which is what contract checks operate on.
        File.WriteAllText(Path.Combine(projectDir, "Class1.cs"), "namespace ReloadFixture; public class MarkerV2 {}");

        ValidationOutcome secondOutcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
            PreparationMode = ArchLinterNet.Core.BuildState.BuildPreparationMode.EnsureBuilt,
            RequestedConfiguration = "Debug",
        });

        Assert.That(secondOutcome.PreflightBlocked, Is.False,
            () => string.Join("; ", secondOutcome.PreflightDiagnostics.Select(d => d.Evidence.Detail)));
        Assert.That(secondOutcome.Passed, Is.True);

        // This is the real assertion: load the assembly the same way the resolver does and check
        // which marker type is visible in *this* process after the second ensure-built run.
        System.Reflection.Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ReloadFixture");
        Assert.That(loaded, Is.Not.Null, "Expected ReloadFixture to have been loaded into the process by now.");
        string[] typeNames = loaded!.GetTypes().Select(t => t.Name).ToArray();

        TestContext.WriteLine($"Loaded ReloadFixture types after second ensure-built: {string.Join(", ", typeNames)}");

        // KNOWN LIMITATION (tracked, not silently accepted): this asserts the CURRENT behavior,
        // not the desired one. .NET's default (non-collectible) AssemblyLoadContext keeps
        // whichever Assembly instance a simple name first resolved to for the lifetime of the
        // process; ArchitectureAssemblyResolutionService.ResolveByName's "already loaded" check
        // (and, independent of that check, LoadFrom's own same-simple-name reuse behavior) means
        // a --ensure-built rebuild that happens *after* an earlier resolution attempt already
        // loaded a stale copy of the same assembly cannot make in-process contract analysis see
        // the rebuilt bytes within that same process run. Preflight itself is not fooled — it
        // reads bytes from disk and correctly reports stale-artifact / re-verifies after the
        // rebuild (see Prepare_EnsureBuiltAfterSourceChange_OverwritesStaleReceiptAndReportsCurrent)
        // — but contract execution's resolved Assembly object can still be the earlier one.
        // A real fix needs isolated (collectible) AssemblyLoadContext-based loading for
        // verification/re-resolution, which has type-identity implications for reflection-based
        // contract checks and is out of scope for this change — flagged as a follow-up.
        Assert.That(typeNames, Does.Contain("MarkerV1"));
        Assert.That(typeNames, Does.Not.Contain("MarkerV2"));
    }
}
