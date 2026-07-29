using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    private static void ApplyDiagnosticSpecificCiFields(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        switch (diagnostic)
        {
            case DependencyDiagnostic dependency: ApplyDependencyCiFields(dependency, obj); break;
            case ExternalDependencyDiagnostic external: obj["forbidden_external_group"] = external.ForbiddenExternalGroup; break;
            case PackageDependencyDiagnostic package: obj["forbidden_package_group"] = package.ForbiddenPackageGroup; break;
            case PackageAllowOnlyDiagnostic package: obj["allowed_package_groups"] = package.AllowedPackageGroups.ToArray(); break;
            case FrameworkReferenceDiagnostic framework:
                obj["forbidden_framework_group"] = framework.ForbiddenFrameworkGroup;
                ApplyFrameworkReferenceEvidenceCiFields(framework.Evidence, obj); break;
            case FrameworkReferenceAllowOnlyDiagnostic framework:
                obj["allowed_framework_groups"] = framework.AllowedFrameworkGroups.ToArray();
                ApplyFrameworkReferenceEvidenceCiFields(framework.Evidence, obj); break;
            case TypePlacementDiagnostic typePlacement: ApplyTypePlacementCiFields(typePlacement, obj); break;
            case LayoutConventionDiagnostic layout: ApplyLayoutConventionCiFields(layout, obj); break;
            case PublicApiSurfaceDiagnostic api: ApplyPublicApiSurfaceCiFields(api, obj); break;
            case AttributeUsageDiagnostic attribute: ApplyAttributeUsageCiFields(attribute, obj); break;
            case InheritanceDiagnostic inheritance: ApplyInheritanceCiFields(inheritance, obj); break;
            case InterfaceImplementationDiagnostic implementation: ApplyInterfaceImplementationCiFields(implementation, obj); break;
            case CompositionDiagnostic composition: ApplyCompositionCiFields(composition, obj); break;
            case ProjectMetadataDiagnostic metadata: ApplyProjectMetadataCiFields(metadata, obj); break;
            case ConfigurationDiagnostic configuration: ApplyConfigurationCiFields(configuration, obj); break;
            case ContextDependencyDiagnostic context: ApplyContextDependencyCiFields(context, obj); break;
            case ContextAllowOnlyDiagnostic context: ApplyContextAllowOnlyCiFields(context, obj); break;
            case PortBoundaryDiagnostic boundary: ApplyPortBoundaryCiFields(boundary, obj); break;
            case CycleDiagnostic cycle: obj["path"] = cycle.Path; break;
            case BuildStatePreflightDiagnostic preflight: ApplyBuildStatePreflightCiFields(preflight, obj); break;
        }
    }

    private static object? SourceLocationForJson(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        LayoutConventionDiagnostic { MatchedFilePath: { } path } => new Dictionary<string, object?> { ["path"] = path },
        FrameworkReferenceDiagnostic { Evidence: { Count: > 0 } evidence } => new Dictionary<string, object?> { ["path"] = evidence.First().SourcePath },
        FrameworkReferenceAllowOnlyDiagnostic { Evidence: { Count: > 0 } evidence } => new Dictionary<string, object?> { ["path"] = evidence.First().SourcePath },
        ProjectMetadataDiagnostic { ProjectMetadataSourcePath: { } path } => new Dictionary<string, object?> { ["path"] = path },
        BuildStatePreflightDiagnostic { Evidence.ProjectPath: var path } => new Dictionary<string, object?> { ["path"] = path },
        _ => null,
    };

    private static void ApplyBuildStatePreflightCiFields(BuildStatePreflightDiagnostic preflight, Dictionary<string, object?> obj)
    {
        BuildStatePreflightEvidence evidence = preflight.Evidence;
        obj["state"] = StateToken(preflight.State); obj["project_path"] = evidence.ProjectPath;
        obj["assembly_name"] = evidence.AssemblyName; obj["requested_configuration"] = evidence.RequestedConfiguration;
        obj["observed_configuration"] = evidence.ObservedConfiguration; obj["requested_target_framework"] = evidence.RequestedTargetFramework;
        obj["observed_target_framework"] = evidence.ObservedTargetFramework; obj["expected_output_path"] = evidence.ExpectedOutputPath;
        obj["build_command"] = evidence.BuildCommand; obj["detail"] = evidence.Detail;
    }
}
