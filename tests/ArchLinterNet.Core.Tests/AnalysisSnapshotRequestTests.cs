using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #375: ForMode/FromValidationRequest are hand-written field-by-field mappers between
// AnalysisSnapshotRequest and ValidationRequest — every field, including CancellationToken, must
// survive the round trip or a caller building one from the other silently loses it.
[TestFixture]
public sealed class AnalysisSnapshotRequestTests
{
    [Test]
    public void ForMode_CarriesCancellationTokenIntoValidationRequest()
    {
        using CancellationTokenSource cts = new();
        var snapshotRequest = new AnalysisSnapshotRequest
        {
            PolicyPath = "policy.yml",
            CancellationToken = cts.Token,
        };

        ValidationRequest validationRequest = snapshotRequest.ForMode("strict");

        Assert.That(validationRequest.CancellationToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public void FromValidationRequest_CarriesCancellationTokenIntoAnalysisSnapshotRequest()
    {
        using CancellationTokenSource cts = new();
        var validationRequest = new ValidationRequest
        {
            PolicyPath = "policy.yml",
            Mode = "strict",
            CancellationToken = cts.Token,
        };

        AnalysisSnapshotRequest snapshotRequest = AnalysisSnapshotRequest.FromValidationRequest(validationRequest);

        Assert.That(snapshotRequest.CancellationToken, Is.EqualTo(cts.Token));
    }
}
