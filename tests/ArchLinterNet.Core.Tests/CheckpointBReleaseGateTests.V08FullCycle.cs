using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    [Test]
    [CancelAfter(CheckpointBV08FullCycleScenario.WatchdogMs)]
    public void PackedCandidate_V08FullCycle()
    {
        CandidatePackageFeed candidate = Candidate;
        candidate.WriteShardEvidence("v08-full-cycle", new CheckpointBV08FullCycleScenario(candidate).Run());
    }
}
