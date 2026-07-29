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
            case UnmatchedIgnoreDiagnostic unmatched:
                obj["ignore_index"] = unmatched.IgnoreIndex;
                obj["source_type"] = unmatched.SourceType;
                obj["forbidden_reference"] = unmatched.ForbiddenReference;
                obj["reason"] = unmatched.Reason;
                break;
            case PolicyConsistencyDiagnostic policy:
                obj["check_kind"] = policy.CheckKind;
                obj["reason"] = policy.Reason;
                obj["conflicting_contract_ids"] = policy.ConflictingContractIds.ToArray();
                obj["conflicting_contract_names"] = policy.ConflictingContractNames.ToArray();
                obj["layers"] = policy.Layers.ToArray();
                obj["representative_type"] = policy.RepresentativeType;
                break;
            case BaselineLifecycleDiagnostic baseline:
                obj["contract_group"] = baseline.ContractGroup;
                obj["source_type"] = baseline.SourceType;
                obj["forbidden_reference"] = baseline.ForbiddenReference;
                obj["reason"] = baseline.Reason;
                obj["issue"] = baseline.Issue;
                obj["disposition"] = BaselineEntryDispositionNames.WireName(baseline.Disposition);
                obj["suppresses"] = baseline.Suppresses;
                obj["identity"] = baseline.StructuredIdentity is null
                    ? null
                    : ArchitectureViolationIdentityJson.ToWireObject(baseline.StructuredIdentity);
                break;
            case ArchitecturePolicyErrorDiagnostic policyError:
                obj["diagnostic_kind"] = policyError.DiagnosticKind.ToString().ToLowerInvariant();
                obj["error_category"] = policyError.ErrorCategory;
                obj["import_chain"] = policyError.ImportChain;
                obj["message"] = policyError.Message;
                break;
        }
    }

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
