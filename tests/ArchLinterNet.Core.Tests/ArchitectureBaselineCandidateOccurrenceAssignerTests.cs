using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineCandidateOccurrenceAssignerTests
{
    [Test]
    public void Assign_DuplicateIdentitiesInSameContract_GetSequentialOccurrenceIndices()
    {
        var identity = new ArchitectureViolationIdentity(
            2, "method_body", "call", "my-rule", "MyApp.App", "MyApp.Service", null,
            "System", null, "Console.WriteLine(string)", 0);

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict_method_body", "my-rule", "MyApp.Service", "line 10: -> Console.WriteLine", identity),
            new("strict_method_body", "my-rule", "MyApp.Service", "line 20: -> Console.WriteLine", identity),
            new("strict_method_body", "my-rule", "MyApp.Service", "line 30: -> Console.WriteLine", identity),
        };

        IReadOnlyList<ArchitectureBaselineCandidate> assigned = ArchitectureBaselineCandidateOccurrenceAssigner.Assign(candidates);

        Assert.That(assigned.Select(c => c.Identity!.Occurrence), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void Assign_DistinctIdentities_EachStayAtOccurrenceZero()
    {
        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "rule", "Src.A", "Ref.A",
                new ArchitectureViolationIdentity(2, "strict", "dependency", "rule", null, "Src.A", null, null, null, "Ref.A", 0)),
            new("strict", "rule", "Src.B", "Ref.B",
                new ArchitectureViolationIdentity(2, "strict", "dependency", "rule", null, "Src.B", null, null, null, "Ref.B", 0)),
        };

        IReadOnlyList<ArchitectureBaselineCandidate> assigned = ArchitectureBaselineCandidateOccurrenceAssigner.Assign(candidates);

        Assert.That(assigned.Select(c => c.Identity!.Occurrence), Is.EqualTo(new[] { 0, 0 }));
    }

    [Test]
    public void Assign_CandidateWithoutIdentity_IsPassedThroughUnchanged()
    {
        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "rule", "Src.A", "Ref.A"),
        };

        IReadOnlyList<ArchitectureBaselineCandidate> assigned = ArchitectureBaselineCandidateOccurrenceAssigner.Assign(candidates);

        Assert.That(assigned[0].Identity, Is.Null);
    }

    [Test]
    public void Assign_SameIdentityDifferentContractIds_DoNotShareOccurrenceCounter()
    {
        var identity = new ArchitectureViolationIdentity(
            2, "strict", "dependency", "rule-a", null, "Src.A", null, null, null, "Ref.A", 0);

        var candidates = new List<ArchitectureBaselineCandidate>
        {
            new("strict", "rule-a", "Src.A", "Ref.A", identity),
            new("strict", "rule-b", "Src.A", "Ref.A", identity with { ContractId = "rule-b" }),
        };

        IReadOnlyList<ArchitectureBaselineCandidate> assigned = ArchitectureBaselineCandidateOccurrenceAssigner.Assign(candidates);

        Assert.That(assigned.Select(c => c.Identity!.Occurrence), Is.EqualTo(new[] { 0, 0 }));
    }
}
