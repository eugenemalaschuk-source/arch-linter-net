using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CheckpointBRestoreReuseTests
{
    [Test]
    public void SuccessfulEnsureBuiltIsReusedOnlyForTheSameFixtureRoot()
    {
        var restoreReuse = new CheckpointBRestoreReuse();
        string firstRoot = Path.Combine(Path.GetTempPath(), "checkpoint-b-first-root");
        string secondRoot = Path.Combine(Path.GetTempPath(), "checkpoint-b-second-root");
        string[] ensureBuilt = ["validate", "--ensure-built"];
        string[] policyCheck = ["policy", "check"];

        Assert.That(restoreReuse.PrepareArguments(firstRoot, ensureBuilt), Is.EqualTo(ensureBuilt),
            "The first --ensure-built command for a root must retain restore.");
        restoreReuse.RecordSuccessfulEnsureBuilt(firstRoot, ensureBuilt, exitCode: 0);

        Assert.Multiple(() =>
        {
            Assert.That(restoreReuse.PrepareArguments(firstRoot, ensureBuilt),
                Is.EqualTo(new[] { "validate", "--ensure-built", "--no-restore" }),
                "A later --ensure-built command for the same root must skip only redundant restore.");
            Assert.That(restoreReuse.PrepareArguments(secondRoot, ensureBuilt), Is.EqualTo(ensureBuilt),
                "A different fixture root must still perform its first restore.");
            Assert.That(restoreReuse.PrepareArguments(firstRoot, policyCheck), Is.EqualTo(policyCheck),
                "A command without --ensure-built must not be changed.");
        });
    }

    [Test]
    public void FailedEnsureBuiltDoesNotEnableRestoreReuse()
    {
        var restoreReuse = new CheckpointBRestoreReuse();
        string root = Path.Combine(Path.GetTempPath(), "checkpoint-b-failed-root");
        string[] ensureBuilt = ["validate", "--ensure-built"];

        restoreReuse.RecordSuccessfulEnsureBuilt(root, ensureBuilt, exitCode: 1);

        Assert.That(restoreReuse.PrepareArguments(root, ensureBuilt), Is.EqualTo(ensureBuilt));
    }
}
