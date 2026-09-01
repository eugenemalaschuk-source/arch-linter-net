using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Topology.Abstractions;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Topology;

/// <summary>
/// Produces canonical topology candidates from the normal validation observation projection.
/// This service does not evaluate or mutate topology policy; it only exposes facts from one
/// analysis session for review-oriented hosts.
/// </summary>
public sealed class ArchitectureTopologyCaptureService(
    IArchitectureValidationApplicationService validationApplicationService)
    : IArchitectureTopologyCaptureService
{
    private static readonly string[] _supportedSubjectKinds = ["type", "namespace", "project", "assembly"];

    public ArchitectureTopologyCaptureOutcome Capture(ArchitectureTopologyCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSubjectKind(request.SubjectKind);

        using ArchitectureAnalysisSnapshot snapshot = validationApplicationService.CreateSnapshot(
            ToSnapshotRequest(request));

        if (snapshot.Failed)
        {
            return new ArchitectureTopologyCaptureOutcome(
                request.SubjectKind,
                Array.Empty<ArchitectureTopologyCaptureFact>(),
                Array.Empty<ArchitectureTopologyCaptureRelationship>(),
                snapshot.RepositoryRoot,
                snapshot.GetCapturePolicyImportPaths(),
                snapshot.GetCaptureResolvedAssemblyPaths(),
                snapshot.GetCaptureDiscoveredProjectPaths(),
                snapshot.Preflight.Diagnostics,
                PreflightBlocked: true);
        }

        ArchitectureTopologyEvaluator.ValidationObservation observation =
            snapshot.CaptureTopologyObservation(request.SubjectKind);

        ArchitectureTopologyCaptureFact[] subjects = observation.Subjects
            .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
            .Select(subject => new ArchitectureTopologyCaptureFact(
                subject.Identity,
                request.SubjectKind,
                subject.Subject,
                subject.Project,
                subject.Assembly))
            .ToArray();
        ArchitectureTopologyCaptureRelationship[] relationships = observation.Dependencies
            .OrderBy(dependency => dependency.SourceIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetIdentity, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.Witness, StringComparer.Ordinal)
            .Select(dependency => new ArchitectureTopologyCaptureRelationship(
                dependency.SourceIdentity,
                dependency.TargetIdentity,
                dependency.Witness))
            .ToArray();

        request.CancellationToken.ThrowIfCancellationRequested();
        return new ArchitectureTopologyCaptureOutcome(
            request.SubjectKind,
            subjects,
            relationships,
            snapshot.RepositoryRoot,
            snapshot.GetCapturePolicyImportPaths(),
            snapshot.GetCaptureResolvedAssemblyPaths(),
            snapshot.GetCaptureDiscoveredProjectPaths(),
            Array.Empty<BuildStatePreflightDiagnostic>());
    }

    private static AnalysisSnapshotRequest ToSnapshotRequest(ArchitectureTopologyCaptureRequest request) => new()
    {
        PolicyPath = request.PolicyPath,
        ConditionSetName = request.ConditionSetName,
        PreprocessorSymbols = request.PreprocessorSymbols,
        IncludeAsmdefContracts = request.IncludeAsmdefContracts,
        PreparationMode = request.PreparationMode,
        NoRestore = request.NoRestore,
        RequestedConfiguration = request.RequestedConfiguration,
        RequestedTargetFramework = request.RequestedTargetFramework,
        RequestedPlatform = request.RequestedPlatform,
        RequestedRuntimeIdentifier = request.RequestedRuntimeIdentifier,
        MaxParallelism = request.MaxParallelism,
        CancellationToken = request.CancellationToken,
    };

    private static void ValidateSubjectKind(string subjectKind)
    {
        if (!_supportedSubjectKinds.Contains(subjectKind, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported topology subject kind '{subjectKind}'. Use 'type', 'namespace', 'project', or 'assembly'.",
                nameof(subjectKind));
        }
    }
}
