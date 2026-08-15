using System.Text.Json;
using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Review finding #1's closed-set converter (AnalysisCacheDiagnosticPayloadConverter) enumerates
// all 18 concrete IArchitectureDiagnosticPayload record types in one switch. Each arm is its own
// branch/line for coverage purposes and for proving the converter actually round-trips that type's
// own fields (not just falls through to a shared default), so every type gets its own case here
// rather than one parameterized case reusing a single shape.
[TestFixture]
public sealed class AnalysisCacheDiagnosticPayloadConverterTests
{
    private static readonly string[] _value = { "group-a" };
    private static readonly string[] _value1 = { "MyApp.Application" };
    private static readonly string[] _value2 = { "group-a", "group-b" };
    private static T RoundTrip<T>(T payload) where T : IArchitectureDiagnosticPayload
    {
        string json = JsonSerializer.Serialize<IArchitectureDiagnosticPayload>(payload, AnalysisCacheJson.Options);
        IArchitectureDiagnosticPayload? result = JsonSerializer.Deserialize<IArchitectureDiagnosticPayload>(json, AnalysisCacheJson.Options);
        Assert.That(result, Is.InstanceOf<T>());
        return (T)result!;
    }

    [Test]
    public void RoundTrips_FrameworkReferenceAllowOnlyPayload()
    {
        FrameworkReferenceAllowOnlyPayload original = new(_value);
        FrameworkReferenceAllowOnlyPayload result = RoundTrip(original);
        Assert.That(result.AllowedFrameworkGroups, Is.EqualTo(original.AllowedFrameworkGroups));
    }

    [Test]
    public void RoundTrips_PackageDependencyPayload()
    {
        PackageDependencyPayload original = new("forbidden-group");
        PackageDependencyPayload result = RoundTrip(original);
        Assert.That(result.ForbiddenPackageGroup, Is.EqualTo(original.ForbiddenPackageGroup));
    }

    [Test]
    public void RoundTrips_FrameworkReferencePayload()
    {
        FrameworkReferencePayload original = new("forbidden-framework-group");
        FrameworkReferencePayload result = RoundTrip(original);
        Assert.That(result.ForbiddenFrameworkGroup, Is.EqualTo(original.ForbiddenFrameworkGroup));
    }

    [Test]
    public void RoundTrips_ConfigurationPayload()
    {
        ConfigurationPayload original = new("template", "container-ns");
        ConfigurationPayload result = RoundTrip(original);
        Assert.That(result.TemplateName, Is.EqualTo(original.TemplateName));
        Assert.That(result.ContainerNamespace, Is.EqualTo(original.ContainerNamespace));
    }

    [Test]
    public void RoundTrips_ProjectMetadataPayload()
    {
        ProjectMetadataPayload original = new("kind", "key", "expected", "actual", "path");
        ProjectMetadataPayload result = RoundTrip(original);
        Assert.That(result.ProjectMetadataKey, Is.EqualTo(original.ProjectMetadataKey));
        Assert.That(result.ProjectMetadataSourcePath, Is.EqualTo(original.ProjectMetadataSourcePath));
    }

    [Test]
    public void RoundTrips_CompositionPayload()
    {
        CompositionPayload original = new("member", "api", "assembly", "boundary");
        CompositionPayload result = RoundTrip(original);
        Assert.That(result.SourceMember, Is.EqualTo(original.SourceMember));
        Assert.That(result.ExpectedCompositionBoundary, Is.EqualTo(original.ExpectedCompositionBoundary));
    }

    [Test]
    public void RoundTrips_TypePlacementPayload()
    {
        TypePlacementPayload original = new("expected-loc", "actual-loc", "expected-name", "actual-name");
        TypePlacementPayload result = RoundTrip(original);
        Assert.That(result.ExpectedTypeLocation, Is.EqualTo(original.ExpectedTypeLocation));
        Assert.That(result.ActualTypeName, Is.EqualTo(original.ActualTypeName));
    }

    [Test]
    public void RoundTrips_ExternalDependencyPayload()
    {
        ExternalDependencyPayload original = new("forbidden-external-group");
        ExternalDependencyPayload result = RoundTrip(original);
        Assert.That(result.ForbiddenExternalGroup, Is.EqualTo(original.ForbiddenExternalGroup));
    }

    [Test]
    public void RoundTrips_DependencyPayload()
    {
        DependencyPayload original = new("Domain", "Infrastructure", _value1);
        DependencyPayload result = RoundTrip(original);
        Assert.That(result.SourceLayer, Is.EqualTo(original.SourceLayer));
        Assert.That(result.AllowedImporters, Is.EqualTo(original.AllowedImporters));
    }

    [Test]
    public void RoundTrips_PortBoundaryPayload()
    {
        PortBoundaryPayload original = new(
            "source-role", null, "target-role", null, "evidence-kind", "expected-seam", "remediation-hint");
        PortBoundaryPayload result = RoundTrip(original);
        Assert.That(result.SourceRole, Is.EqualTo(original.SourceRole));
        Assert.That(result.EvidenceKind, Is.EqualTo(original.EvidenceKind));
        Assert.That(result.RemediationHint, Is.EqualTo(original.RemediationHint));
    }

    [Test]
    public void RoundTrips_AttributeUsagePayload()
    {
        AttributeUsagePayload original = new("attribute", "kind", "expected-loc", "actual-loc");
        AttributeUsagePayload result = RoundTrip(original);
        Assert.That(result.MatchedAttribute, Is.EqualTo(original.MatchedAttribute));
        Assert.That(result.ActualAttributeLocation, Is.EqualTo(original.ActualAttributeLocation));
    }

    [Test]
    public void RoundTrips_InheritancePayload()
    {
        InheritancePayload original = new("BaseType", "surface");
        InheritancePayload result = RoundTrip(original);
        Assert.That(result.ForbiddenBaseType, Is.EqualTo(original.ForbiddenBaseType));
        Assert.That(result.InheritanceSourceSurface, Is.EqualTo(original.InheritanceSourceSurface));
    }

    [Test]
    public void RoundTrips_InterfaceImplementationPayload()
    {
        InterfaceImplementationPayload original = new("IInterface", "kind", "expected-loc", "actual-loc");
        InterfaceImplementationPayload result = RoundTrip(original);
        Assert.That(result.MatchedInterface, Is.EqualTo(original.MatchedInterface));
        Assert.That(result.ActualImplementationLocation, Is.EqualTo(original.ActualImplementationLocation));
    }

    [Test]
    public void RoundTrips_LayoutConventionPayload()
    {
        LayoutConventionPayload original = new(
            "file.cs", "class", "record", "Foo", "Bar", "counterpart", DataUnavailable: true);
        LayoutConventionPayload result = RoundTrip(original);
        Assert.That(result.MatchedFilePath, Is.EqualTo(original.MatchedFilePath));
        Assert.That(result.DataUnavailable, Is.EqualTo(original.DataUnavailable));
    }

    [Test]
    public void RoundTrips_ContextAllowOnlyPayload()
    {
        ContextAllowOnlyPayload original = new("source-role", null, "target-role", null, "selector");
        ContextAllowOnlyPayload result = RoundTrip(original);
        Assert.That(result.SourceRole, Is.EqualTo(original.SourceRole));
        Assert.That(result.MatchedSelector, Is.EqualTo(original.MatchedSelector));
    }

    [Test]
    public void RoundTrips_ContextDependencyPayload()
    {
        ContextDependencyPayload original = new("source-role", null, "target-role", null, "selector");
        ContextDependencyPayload result = RoundTrip(original);
        Assert.That(result.SourceRole, Is.EqualTo(original.SourceRole));
        Assert.That(result.TargetRole, Is.EqualTo(original.TargetRole));
    }

    [Test]
    public void RoundTrips_PackageAllowOnlyPayload()
    {
        PackageAllowOnlyPayload original = new(_value2);
        PackageAllowOnlyPayload result = RoundTrip(original);
        Assert.That(result.AllowedPackageGroups, Is.EqualTo(original.AllowedPackageGroups));
    }

    [Test]
    public void RoundTrips_PublicApiSurfacePayload()
    {
        PublicApiSurfacePayload original = new("signature", true, "assembly", "public", "delta", "previous");
        PublicApiSurfacePayload result = RoundTrip(original);
        Assert.That(result.UndeclaredApiSignature, Is.EqualTo(original.UndeclaredApiSignature));
        Assert.That(result.PreviousApiSignature, Is.EqualTo(original.PreviousApiSignature));
    }

    [Test]
    public void Read_MissingKindDiscriminator_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<IArchitectureDiagnosticPayload>("{\"value\":{}}", AnalysisCacheJson.Options));
    }

    [Test]
    public void Read_MissingValueObject_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<IArchitectureDiagnosticPayload>(
                "{\"$kind\":\"DependencyPayload\"}", AnalysisCacheJson.Options));
    }

    [Test]
    public void Read_UnrecognizedKind_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<IArchitectureDiagnosticPayload>(
                "{\"$kind\":\"SomeUnknownPayload\",\"value\":{}}", AnalysisCacheJson.Options));
    }
}
