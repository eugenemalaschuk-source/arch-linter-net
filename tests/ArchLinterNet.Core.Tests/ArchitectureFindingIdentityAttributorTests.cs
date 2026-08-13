using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Identity attribution driven directly, with no session, no policy document and no assembly
// loading: the component takes a candidate log, a cursor into it and the violations one contract
// reported (issue #452). ArchitectureAnalysisSessionFindingIdentityTests still pins the same
// contract end-to-end through a real session; these tests pin the algorithm itself — candidate
// ordering, per-reference selection, single consumption and the cursor's bracket.
[TestFixture]
public sealed class ArchitectureFindingIdentityAttributorTests
{
    private const string ContractId = "contract-1";

    private static readonly string[] _expectedReportedOrder = { "Serilog.Log", "Newtonsoft.Json.JsonConvert" };

    [Test]
    public void Attach_SelectsOneCandidatePerReportedReference_InReportedOrder()
    {
        List<ArchitectureBaselineCandidate> log = new()
        {
            Candidate("Acme.Consumer", "Newtonsoft.Json.JsonConvert"),
            Candidate("Acme.Consumer", "Serilog.Log"),
        };

        ArchitectureViolation violation = Violation(
            "Acme.Consumer", "Serilog.Log", "Newtonsoft.Json.JsonConvert");

        IReadOnlyList<ArchitectureViolation> attached =
            ArchitectureFindingIdentityAttributor.Attach(log, cursor: 0, new[] { violation });

        // Reported-reference order, not candidate-log order: the log records JsonConvert first, so
        // asserting both the identities and the recorded references against this one expectation is
        // what pins the pairing rather than a coincidence.
        Assert.That(attached, Has.Count.EqualTo(1));
        Assert.That(attached[0].Identities.Select(i => i.TargetMember), Is.EqualTo(_expectedReportedOrder));
        Assert.That(attached[0].IdentityReferences, Is.EqualTo(_expectedReportedOrder));
        Assert.That(attached[0].Identity, Is.EqualTo(attached[0].Identities.First()));
    }

    [Test]
    public void Attach_ConsumesEachCandidateAtMostOnceAcrossViolations()
    {
        List<ArchitectureBaselineCandidate> log = new()
        {
            Candidate("Acme.A", "Serilog.Log"),
            Candidate("Acme.A", "Serilog.Log"),
        };

        ArchitectureViolation first = Violation("Acme.A", "Serilog.Log");
        ArchitectureViolation second = Violation("Acme.A", "Serilog.Log");

        IReadOnlyList<ArchitectureViolation> attached =
            ArchitectureFindingIdentityAttributor.Attach(log, cursor: 0, new[] { first, second });

        Assert.That(attached[0].Identity, Is.Not.Null);
        Assert.That(attached[1].Identity, Is.Not.Null);
        Assert.That(attached[0].Identity!.Occurrence, Is.EqualTo(0));
        Assert.That(attached[1].Identity!.Occurrence, Is.EqualTo(1));
    }

    // The cursor is what keeps one contract's attribution from consuming a previous contract's
    // candidates, which is why the executor snapshots it before each contract runs.
    [Test]
    public void Attach_IgnoresCandidatesRecordedBeforeTheCursor()
    {
        List<ArchitectureBaselineCandidate> log = new()
        {
            Candidate("Acme.A", "Serilog.Log"),
        };

        IReadOnlyList<ArchitectureViolation> attached = ArchitectureFindingIdentityAttributor.Attach(
            log, cursor: log.Count, new[] { Violation("Acme.A", "Serilog.Log") });

        Assert.That(attached[0].Identity, Is.Null);
        Assert.That(attached[0].Identities, Is.Empty);
    }

    // Composition findings report one API per violation and never walk reported references to pair
    // them, so every match is taken and no reference attribution is recorded.
    [Test]
    public void Attach_CompositionPayload_TakesEveryMatchWithoutReferenceAttribution()
    {
        List<ArchitectureBaselineCandidate> log = new()
        {
            Candidate("Acme.A", "System.Console.WriteLine"),
            Candidate("Acme.A", "System.Console.WriteLine"),
        };

        ArchitectureViolation violation = Violation("Acme.A", "System.Console.WriteLine") with
        {
            Payload = new CompositionPayload(
                MatchedForbiddenApi: "System.Console.WriteLine",
                SourceMember: "Acme.A.Run",
                SourceAssembly: "Acme",
                ExpectedCompositionBoundary: "layers: [host]")
        };

        IReadOnlyList<ArchitectureViolation> attached =
            ArchitectureFindingIdentityAttributor.Attach(log, cursor: 0, new[] { violation });

        Assert.That(attached[0].Identities, Has.Count.EqualTo(2));
        Assert.That(attached[0].IdentityReferences, Is.Empty);
    }

    // A reported reference carrying a version/detail suffix ('<identity>@...' or '<identity> ...')
    // still resolves to the identity it was derived from — package and framework findings depend on
    // this, and it is the reason attribution is not a plain string equality.
    [Test]
    public void Attach_MatchesASuffixedReportedReferenceToItsIdentity()
    {
        List<ArchitectureBaselineCandidate> log = new()
        {
            Candidate("Acme.Web", "Newtonsoft.Json"),
        };

        ArchitectureViolation violation = Violation("Acme.Web", "Newtonsoft.Json@13.0.3");

        IReadOnlyList<ArchitectureViolation> attached =
            ArchitectureFindingIdentityAttributor.Attach(log, cursor: 0, new[] { violation });

        Assert.That(attached[0].Identity, Is.Not.Null);
        Assert.That(attached[0].Identity!.TargetMember, Is.EqualTo("Newtonsoft.Json"));
    }

    [Test]
    public void Attach_NoMatchingCandidate_LeavesTheViolationUnchanged()
    {
        List<ArchitectureBaselineCandidate> log = new()
        {
            Candidate("Acme.Other", "Serilog.Log"),
        };

        ArchitectureViolation violation = Violation("Acme.A", "Serilog.Log");

        IReadOnlyList<ArchitectureViolation> attached =
            ArchitectureFindingIdentityAttributor.Attach(log, cursor: 0, new[] { violation });

        Assert.That(attached[0], Is.SameAs(violation));
    }

    private static ArchitectureBaselineCandidate Candidate(string sourceType, string forbiddenReference)
    {
        return new ArchitectureBaselineCandidate(
            "strict_external",
            ContractId,
            sourceType,
            forbiddenReference,
            new ArchitectureViolationIdentity(
                ArchitectureViolationIdentity.CurrentVersion,
                "external",
                "external_dependency",
                ContractId,
                SourceAssembly: "Acme",
                SourceType: sourceType,
                SourceMember: null,
                TargetAssembly: null,
                TargetType: forbiddenReference,
                TargetMember: forbiddenReference,
                Occurrence: OccurrenceFor(sourceType, forbiddenReference)));
    }

    // Mirrors ArchitectureContractExecutionContext's live, unconditional occurrence counting: a
    // repeated (source, reference) pair gets the next index, so duplicate candidates stay distinct.
    private static readonly Dictionary<(string, string), int> _occurrences = new();

    private static int OccurrenceFor(string sourceType, string forbiddenReference)
    {
        (string, string) key = (sourceType, forbiddenReference);
        int occurrence = _occurrences.TryGetValue(key, out int current) ? current : 0;
        _occurrences[key] = occurrence + 1;
        return occurrence;
    }

    [SetUp]
    public void ResetOccurrenceCounters()
    {
        _occurrences.Clear();
    }

    private static ArchitectureViolation Violation(string sourceType, params string[] forbiddenReferences)
    {
        return new ArchitectureViolation(
            "external contract", ContractId, sourceType, "external group 'json'", forbiddenReferences);
    }
}
