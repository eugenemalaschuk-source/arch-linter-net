using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
internal sealed class ResidualSonarHelperTests
{
    [TestCase(ArchitectureTopologyAssemblyEndpointBinding.Bound, false, null)]
    [TestCase(ArchitectureTopologyAssemblyEndpointBinding.Missing, true, ArchitectureApplicabilityReasonCodes.UnmappedSubject)]
    [TestCase(ArchitectureTopologyAssemblyEndpointBinding.Ambiguous, true, ArchitectureApplicabilityReasonCodes.AmbiguousSubject)]
    public void OtherBindingStop_RecordsOnlyUnusableBindings(
        ArchitectureTopologyAssemblyEndpointBinding binding, bool stops, string? reason)
    {
        var reasons = new List<string>();

        bool result = ArchitectureTopologyMetricCalculator.TryRecordOtherBindingStop(binding, reasons);

        Assert.That(result, Is.EqualTo(stops));
        Assert.That(reasons, reason is null ? Is.Empty : Is.EqualTo(new[] { reason }));
    }

    [TestCase(ArchitectureTopologyEvaluator.Disposition.Mapped, false, null)]
    [TestCase(ArchitectureTopologyEvaluator.Disposition.ReviewedOutOfScope, true, null)]
    [TestCase(ArchitectureTopologyEvaluator.Disposition.Unmapped, true, ArchitectureApplicabilityReasonCodes.UnmappedSubject)]
    [TestCase(ArchitectureTopologyEvaluator.Disposition.Ambiguous, true, ArchitectureApplicabilityReasonCodes.AmbiguousSubject)]
    public void OtherDispositionStop_MatchesEachDisposition(
        ArchitectureTopologyEvaluator.Disposition disposition, bool stops, string? reason)
    {
        var reasons = new List<string>();

        bool result = ArchitectureTopologyMetricCalculator.TryRecordOtherDispositionStop(disposition, reasons);

        Assert.That(result, Is.EqualTo(stops));
        Assert.That(reasons, reason is null ? Is.Empty : Is.EqualTo(new[] { reason }));
    }

    [Test]
    public void WaiverExpiry_WithoutExpiryKeepsTheDateHorizon()
    {
        var horizon = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var reasons = new List<ArchitectureHealthPublicationEvidenceReason>();

        DateTimeOffset result = ArchitectureHealthPublicationEvidenceProjector.ApplyWaiverExpiry(
            Waiver(new DateOnly(2026, 9, 9), null), horizon, reasons);

        Assert.That(result, Is.EqualTo(horizon));
        Assert.That(reasons, Is.Empty);
    }

    [Test]
    public void WaiverExpiry_EarlierExpiryShrinksTheHorizon()
    {
        var horizon = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var reasons = new List<ArchitectureHealthPublicationEvidenceReason>();

        DateTimeOffset result = ArchitectureHealthPublicationEvidenceProjector.ApplyWaiverExpiry(
            Waiver(new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 12)), horizon, reasons);

        Assert.That(result, Is.EqualTo(new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero)));
        Assert.That(reasons, Is.Empty);
    }

    [Test]
    public void WaiverExpiry_LaterExpiryKeepsTheDateHorizon()
    {
        var horizon = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var reasons = new List<ArchitectureHealthPublicationEvidenceReason>();

        DateTimeOffset result = ArchitectureHealthPublicationEvidenceProjector.ApplyWaiverExpiry(
            Waiver(new DateOnly(2026, 9, 9), new DateOnly(2026, 12, 1)), horizon, reasons);

        Assert.That(result, Is.EqualTo(horizon));
        Assert.That(reasons, Is.Empty);
    }

    [Test]
    public void WaiverExpiry_ExpiredBeforeEvaluationIsReported()
    {
        var horizon = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var reasons = new List<ArchitectureHealthPublicationEvidenceReason>();

        DateTimeOffset result = ArchitectureHealthPublicationEvidenceProjector.ApplyWaiverExpiry(
            Waiver(new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 1)), horizon, reasons);

        Assert.That(result, Is.EqualTo(horizon));
        Assert.That(reasons.Select(reason => reason.Code), Is.EqualTo(new[] { "expired_waiver" }));
    }

    [Test]
    public void WaiverExpiry_ExpiredStateIsReportedEvenWithFutureExpiry()
    {
        var horizon = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var reasons = new List<ArchitectureHealthPublicationEvidenceReason>();

        ArchitectureHealthPublicationEvidenceProjector.ApplyWaiverExpiry(
            Waiver(new DateOnly(2026, 9, 9), new DateOnly(2026, 12, 1)) with { State = "expired" }, horizon, reasons);

        Assert.That(reasons.Select(reason => reason.Code), Is.EqualTo(new[] { "expired_waiver" }));
    }

    [Test]
    public void WaiverExpiry_UnrepresentableExpiryIsInvalid()
    {
        var horizon = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
        var reasons = new List<ArchitectureHealthPublicationEvidenceReason>();

        DateTimeOffset result = ArchitectureHealthPublicationEvidenceProjector.ApplyWaiverExpiry(
            Waiver(new DateOnly(2026, 9, 9), DateOnly.MaxValue), horizon, reasons);

        Assert.That(result, Is.EqualTo(horizon));
        Assert.That(reasons.Select(reason => reason.Code), Is.EqualTo(new[] { "invalid_evaluation_date" }));
    }

    private static ArchitectureWaiverLifecycleRecord Waiver(DateOnly evaluationDate, DateOnly? expires) =>
        new("waiver", "active", "contract", "contract", "group", "type", "forbidden", null, "reason", "owner", "issue",
            evaluationDate, expires, evaluationDate, true);
}
