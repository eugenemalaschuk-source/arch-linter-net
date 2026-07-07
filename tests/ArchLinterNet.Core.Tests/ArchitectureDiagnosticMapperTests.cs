using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDiagnosticMapperTests
{
    [Test]
    public void FromViolation_PlainViolation_ReturnsDependencyDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", "contract-id", "Source.Type", "Forbidden.Namespace", new[] { "ref1" });

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<DependencyDiagnostic>());
        Assert.That(diagnostic.Kind, Is.EqualTo(ArchitectureDiagnosticKind.Dependency));
    }

    [Test]
    public void FromViolation_LayerFields_ReturnsDependencyDiagnosticWithLayerData()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "Source.Type", "protected layer 'Core'", new[] { "ref1" })
        {
            SourceLayer = "Web",
            TargetLayer = "Core",
            AllowedImporters = new[] { "Api" }
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<DependencyDiagnostic>());
        var dependency = (DependencyDiagnostic)diagnostic;
        Assert.That(dependency.SourceLayer, Is.EqualTo("Web"));
        Assert.That(dependency.TargetLayer, Is.EqualTo("Core"));
        Assert.That(dependency.AllowedImporters, Is.EquivalentTo(new[] { "Api" }));
    }

    [Test]
    public void FromViolation_LayerFieldsWithMatchedNamespacePrefixes_PreservesBoth()
    {
        var violation = new ArchitectureViolation(
            "contract", null, "Source.Type", "protected layer 'Core'", new[] { "ref1" })
        {
            SourceLayer = "Web",
            TargetLayer = "Core",
            AllowedImporters = new[] { "Api" },
            MatchedNamespacePrefixes = new[] { "Core.Internal" }
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<DependencyDiagnostic>());
        var dependency = (DependencyDiagnostic)diagnostic;
        Assert.That(dependency.SourceLayer, Is.EqualTo("Web"));
        Assert.That(dependency.AllowedImporters, Is.EquivalentTo(new[] { "Api" }));
        Assert.That(dependency.MatchedNamespacePrefixes, Is.EquivalentTo(new[] { "Core.Internal" }));
    }

    [Test]
    public void FromViolation_ForbiddenExternalGroup_ReturnsExternalDependencyDiagnostic()
    {
        var violation = new ArchitectureViolation(
            "contract", "core-no-unity", "MyApp.Core.PlayerModel", "external dependency group 'unity_runtime'",
            new[] { "UnityEngine.Vector3" })
        {
            ForbiddenExternalGroup = "unity_runtime"
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
            "contract", null, "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
        {
            TemplateName = "asmdef-template",
            ContainerNamespace = "MyApp.Modules"
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
            "contract", "project-metadata", "src/MyApp/MyApp.csproj", "required project property mismatch", new[] { "ref1" })
        {
            ProjectMetadataKind = "required_property",
            ProjectMetadataKey = "Nullable",
            ProjectMetadataExpectedValue = "enable",
            ProjectMetadataActualValue = "disable",
            ProjectMetadataSourcePath = "Directory.Build.props"
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
            "contract", null, "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
        {
            DependencyPaths = paths
        };

        var diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);

        Assert.That(diagnostic, Is.InstanceOf<ConfigurationDiagnostic>());
        Assert.That(((ConfigurationDiagnostic)diagnostic).DependencyPaths, Is.EqualTo(paths));
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
