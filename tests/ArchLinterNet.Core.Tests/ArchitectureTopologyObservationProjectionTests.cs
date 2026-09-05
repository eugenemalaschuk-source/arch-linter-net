using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureTopologyObservationProjectionTests
{
    [Test]
    public void Observers_PreserveSharedFactsWhileRetainingTheirDistinctIdentityContracts()
    {
        Assembly assembly = typeof(ArchitectureMetricMeasurement).Assembly;
        using var context = new ArchitectureAnalysisContext(
            Path.GetTempPath(),
            [assembly],
            Array.Empty<string>(),
            Array.Empty<string>());
        var session = new ArchitectureAnalysisSession(
            context,
            new ArchitectureContractDocument(),
            null,
            false,
            null);

        ArchitectureTopologyObservation validation = ArchitectureTopologyValidationObserver.Observe(session, "namespace");
        ArchitectureTopologyObservation metric = ArchitectureTopologyMetricObserver.Observe(session, "namespace");

        Assert.Multiple(() =>
        {
            Assert.That(
                validation.Subjects.Select(subject => $"{subject.Assembly}|{subject.Subject}"),
                Is.EqualTo(metric.Subjects.Select(subject => $"{subject.Assembly}|{subject.Subject}")));
            Assert.That(
                validation.Dependencies.Select(dependency => dependency.Witness),
                Is.EqualTo(metric.Dependencies.Select(dependency => dependency.Witness)));
            Assert.That(
                validation.Subjects.All(subject =>
                    !subject.Identity.Contains("canonical_assembly=", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                metric.Subjects.All(subject =>
                    subject.Identity.Contains("canonical_assembly=", StringComparison.Ordinal)),
                Is.True);
        });
    }
}
