using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDiagnosticMapperTests
{
    private static readonly string[] _ref1 = { "ref1" };
    private static readonly string[] _coreInternal = { "Core.Internal" };
    private static readonly string[] _api = { "_api" };

    [Test]
    public void FromViolation_PlainViolation_ReturnsDependencyDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", "contract-id", "Source.Type", "Forbidden.Namespace", _ref1);

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<DependencyDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.Dependency));
    }

    [Test]
    public void FromViolation_LayerFields_ReturnsDependencyDiagnosticWithLayerData()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "Source.Type", "protected layer 'Core'", _ref1)
        {
            Payload = new DependencyPayload(
                SourceLayer: "Web",
                TargetLayer: "Core",
                AllowedImporters: _api)
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<DependencyDiagnostic>());
        var dependency = (DependencyDiagnostic)diagnostic;
        Assert.That(dependency.SourceLayer, Is.EqualTo("Web"));
        Assert.That(dependency.TargetLayer, Is.EqualTo("Core"));
        Assert.That(dependency.AllowedImporters, Is.EquivalentTo(_api));
    }

    [Test]
    public void FromViolation_LayerFieldsWithMatchedNamespacePrefixes_PreservesBoth()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "Source.Type", "protected layer 'Core'", _ref1)
        {
            Payload = new DependencyPayload(
                SourceLayer: "Web",
                TargetLayer: "Core",
                AllowedImporters: _api),
            MatchedNamespacePrefixes = _coreInternal
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<DependencyDiagnostic>());
        var dependency = (DependencyDiagnostic)diagnostic;
        Assert.That(dependency.SourceLayer, Is.EqualTo("Web"));
        Assert.That(dependency.AllowedImporters, Is.EquivalentTo(_api));
        Assert.That(dependency.MatchedNamespacePrefixes, Is.EquivalentTo(_coreInternal));
    }

    [Test]
    public void FromViolation_ForbiddenExternalGroup_ReturnsExternalDependencyDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", "core-no-unity", "MyApp.Core.PlayerModel", "external dependency group 'unity_runtime'",
            new[] { "UnityEngine.Vector3" })
        {
            Payload = new ExternalDependencyPayload("unity_runtime")
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<ExternalDependencyDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.ExternalDependency));
        Assert.That(((ExternalDependencyDiagnostic)diagnostic).ForbiddenExternalGroup, Is.EqualTo("unity_runtime"));
    }

    [Test]
    public void FromViolation_TemplateAndContainerNamespace_ReturnsConfigurationDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "Source.Type", "Forbidden.Namespace", _ref1)
        {
            Payload = new ConfigurationPayload(
                TemplateName: "asmdef-template",
                ContainerNamespace: "MyApp.Modules")
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<ConfigurationDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.Configuration));
        var configuration = (ConfigurationDiagnostic)diagnostic;
        Assert.That(configuration.TemplateName, Is.EqualTo("asmdef-template"));
        Assert.That(configuration.ContainerNamespace, Is.EqualTo("MyApp.Modules"));
    }

    [Test]
    public void FromViolation_ProjectMetadataFields_ReturnsProjectMetadataDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", "project-metadata", "src/MyApp/MyApp.csproj", "required project property mismatch", _ref1)
        {
            Payload = new ProjectMetadataPayload(
                ProjectMetadataKind: "required_property",
                ProjectMetadataKey: "Nullable",
                ProjectMetadataExpectedValue: "enable",
                ProjectMetadataActualValue: "disable",
                ProjectMetadataSourcePath: "Directory.Build.props")
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<ProjectMetadataDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.ProjectMetadata));
        var projectMetadata = (ProjectMetadataDiagnostic)diagnostic;
        Assert.That(projectMetadata.ProjectMetadataKey, Is.EqualTo("Nullable"));
        Assert.That(projectMetadata.ProjectMetadataExpectedValue, Is.EqualTo("enable"));
        Assert.That(projectMetadata.ProjectMetadataActualValue, Is.EqualTo("disable"));
    }

    [Test]
    public void FromViolation_DependencyPaths_ReturnsConfigurationDiagnosticWithPaths()
    {
        var paths = new IReadOnlyCollection<string>[] { new[] { "A", "B" } };
        var violation = new ArchitectureViolation(
            "contract", null, "Source.Type", "Forbidden.Namespace", _ref1)
        {
            Payload = new ConfigurationPayload(DependencyPaths: paths)
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<ConfigurationDiagnostic>());
        Assert.That(((ConfigurationDiagnostic)diagnostic).DependencyPaths, Is.EqualTo(paths));
    }

    [Test]
    public void FromViolation_TypePlacementPayload_ReturnsTypePlacementDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "Source.Type", "expected-location", _ref1)
        {
            Payload = new TypePlacementPayload(
                ExpectedTypeLocation: "namespace:MyApp.Domain",
                ActualTypeLocation: "namespace:MyApp.Infra",
                ExpectedTypeName: "IFoo",
                ActualTypeName: "Foo")
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<TypePlacementDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.TypePlacement));
        var typePlacement = (TypePlacementDiagnostic)diagnostic;
        Assert.That(typePlacement.ExpectedTypeLocation, Is.EqualTo("namespace:MyApp.Domain"));
        Assert.That(typePlacement.ActualTypeLocation, Is.EqualTo("namespace:MyApp.Infra"));
        Assert.That(typePlacement.ExpectedTypeName, Is.EqualTo("IFoo"));
        Assert.That(typePlacement.ActualTypeName, Is.EqualTo("Foo"));
    }

    [Test]
    public void FromViolation_PublicApiSurfacePayload_ReturnsPublicApiSurfaceDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "MyApp.Public.Thing", "public API surface", new[] { "MyApp.Public.Thing.Method()" })
        {
            Payload = new PublicApiSurfacePayload(
                UndeclaredApiSignature: "MyApp.Public.Thing.Method()",
                ForbiddenPublicConstant: true,
                ApiAssemblyName: "MyApp",
                ApiVisibility: "public")
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<PublicApiSurfaceDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.PublicApiSurface));
        var publicApiSurface = (PublicApiSurfaceDiagnostic)diagnostic;
        Assert.That(publicApiSurface.UndeclaredApiSignature, Is.EqualTo("MyApp.Public.Thing.Method()"));
        Assert.That(publicApiSurface.ForbiddenPublicConstant, Is.True);
        Assert.That(publicApiSurface.ApiAssemblyName, Is.EqualTo("MyApp"));
        Assert.That(publicApiSurface.ApiVisibility, Is.EqualTo("public"));
    }

    [Test]
    public void FromViolation_InheritancePayload_ReturnsInheritanceDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "MyApp.Domain.Thing", "System.Object", new[] { "System.Object" })
        {
            Payload = new InheritancePayload(
                ForbiddenBaseType: "System.Object",
                InheritanceSourceSurface: "layers: [domain]")
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<InheritanceDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.Inheritance));
        var inheritance = (InheritanceDiagnostic)diagnostic;
        Assert.That(inheritance.ForbiddenBaseType, Is.EqualTo("System.Object"));
        Assert.That(inheritance.InheritanceSourceSurface, Is.EqualTo("layers: [domain]"));
    }

    [Test]
    public void FromViolation_CompositionPayload_ReturnsCompositionDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "MyApp.Domain.Thing", "Container.Resolve", new[] { "Container.Resolve" })
        {
            Payload = new CompositionPayload(
                SourceMember: "MyApp.Domain.Thing.DoWork()",
                MatchedForbiddenApi: "Container.Resolve",
                ExpectedCompositionBoundary: "composition root")
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<CompositionDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.Composition));
        var composition = (CompositionDiagnostic)diagnostic;
        Assert.That(composition.SourceMember, Is.EqualTo("MyApp.Domain.Thing.DoWork()"));
        Assert.That(composition.MatchedForbiddenApi, Is.EqualTo("Container.Resolve"));
        Assert.That(composition.ExpectedCompositionBoundary, Is.EqualTo("composition root"));
    }

    [Test]
    public void FromCycle_ReturnsCycleDiagnostic()
    {
        var diagnostic = ArchitectureDiagnosticMapper.FromCycle("A -> B -> A", "cycle-contract", "cycle-check");

        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.Cycle));
        Assert.That(diagnostic.Path, Is.EqualTo("A -> B -> A"));
        Assert.That(diagnostic.ContractName, Is.EqualTo("cycle-contract"));
        Assert.That(diagnostic.ContractId, Is.EqualTo("cycle-check"));
    }

    [Test]
    public void FromUnmatchedIgnore_ReturnsUnmatchedIgnoreDiagnostic()
    {
        var unmatched = new ArchitectureUnmatchedIgnoredViolation(
            "contract", "contract-id", 0, "Source.Type", "Forbidden.Ref", "stale ignore");

        var diagnostic = ArchitectureDiagnosticMapper.FromUnmatchedIgnore(unmatched);

        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.UnmatchedIgnore));
        Assert.That(diagnostic.IgnoreIndex, Is.EqualTo(0));
        Assert.That(diagnostic.SourceType, Is.EqualTo("Source.Type"));
        Assert.That(diagnostic.ForbiddenReference, Is.EqualTo("Forbidden.Ref"));
        Assert.That(diagnostic.Reason, Is.EqualTo("stale ignore"));
    }
}
