using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Validators;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

/// <summary>
/// Reads one explicitly supplied repository-local SARIF artifact and validates its trust boundary.
/// </summary>
/// <remarks>
/// This reader deliberately does not execute analyzers, select diagnostics, contact producer APIs,
/// or project findings. It only establishes that bounded bytes contain one successful, matching
/// SARIF 2.1.0 run and that the run is explicitly bound to the requested assessment context.
/// </remarks>
/// <remarks>Creates a reader using the supplied verified evidence-file capability.</remarks>
public sealed class SarifEvidenceReader
{
    private readonly SarifEvidenceArtifactReader _artifactReader;
    private readonly SarifEvidenceDocumentReader _documentReader = new();
    private readonly SarifEvidenceContextReader _contextReader = new();
    private readonly SarifEvidenceSourceProjectionReader _sourceProjectionReader = new();

    public SarifEvidenceReader(IArchitectureEvidenceFileSystem? fileSystem = null)
    {
        _artifactReader = new SarifEvidenceArtifactReader(fileSystem ?? ArchitectureFileSystem.Real);
    }

    /// <summary>
    /// Reads and trust-validates one declared artifact. Trust failures are returned as values;
    /// null arguments and invalid bounds are programming errors and throw.
    /// </summary>
    public SarifEvidenceReadResult Read(
        ArchitectureExternalEvidenceRequirement requirement,
        string repositoryRoot,
        SarifEvidenceArtifactReference? artifact,
        SarifEvidenceAssessmentContext? expectedContext = null,
        SarifEvidenceLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ValidateRepositoryRoot(repositoryRoot);
        SarifEvidenceDocumentReader.ValidateRequirement(requirement);
        expectedContext ??= new SarifEvidenceAssessmentContext();
        limits ??= new SarifEvidenceLimits();

        if (artifact is null)
        {
            return CreateMissingArtifactResult(requirement);
        }

        SarifEvidenceArtifactReadOutcome bytes = _artifactReader.Read(
            repositoryRoot,
            artifact.Path,
            limits.MaxArtifactBytes,
            cancellationToken);
        if (!bytes.IsReadable
            && bytes.Failure == ArtifactReadFailure.Unsafe
            && bytes.RelativePath is null)
        {
            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.UnsafePath,
                "The evidence path is absolute, outside the repository, or crosses an unsafe filesystem indirection.");
        }

        SarifEvidenceProvenance baseProvenance = new(
            requirement.Id,
            bytes.IsReadable || bytes.BytesRead > 0 ? bytes.RelativePath : null,
            bytes.IsReadable || bytes.BytesRead > 0 ? bytes.Sha256 : null,
            null,
            null,
            null,
            null,
            null);

        if (!bytes.IsReadable)
        {
            return CreateReadFailureResult(requirement, bytes.Failure, baseProvenance);
        }

        if (bytes.ExceededLimit)
        {
            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.ArtifactTooLarge,
                "The evidence artifact exceeds the configured byte limit.",
                baseProvenance);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ParseAndValidate(
            requirement,
            artifact,
            expectedContext,
            limits,
            bytes.Data,
            baseProvenance,
            cancellationToken);
    }

    private static void ValidateRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("A repository root is required.", nameof(repositoryRoot));
        }
    }

    private static SarifEvidenceReadResult CreateMissingArtifactResult(
        ArchitectureExternalEvidenceRequirement requirement)
    {
        return requirement.Required
            ? CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.MissingRequiredInput,
                "The required external evidence artifact was not supplied.")
            : CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.OptionalNotConfigured,
                "The optional external evidence artifact was not configured.");
    }

    private static SarifEvidenceReadResult CreateReadFailureResult(
        ArchitectureExternalEvidenceRequirement requirement,
        ArtifactReadFailure failure,
        SarifEvidenceProvenance provenance)
    {
        return CreateResult(
            requirement.Id,
            GetReadFailureStatus(requirement.Required, failure),
            failure == ArtifactReadFailure.Unsafe
                ? "The evidence path is not a repository-local regular file or changed while it was opened."
                : "The evidence artifact could not be read.",
            provenance);
    }

    private static SarifEvidenceTrustStatus GetReadFailureStatus(bool required, ArtifactReadFailure failure)
    {
        if (failure == ArtifactReadFailure.Missing)
        {
            return required
                ? SarifEvidenceTrustStatus.MissingRequiredInput
                : SarifEvidenceTrustStatus.MissingOptionalInput;
        }

        return failure == ArtifactReadFailure.Unsafe
            ? SarifEvidenceTrustStatus.UnsafePath
            : SarifEvidenceTrustStatus.UnreadableInput;
    }

    private SarifEvidenceReadResult ParseAndValidate(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceArtifactReference artifact,
        SarifEvidenceAssessmentContext expectedContext,
        SarifEvidenceLimits limits,
        byte[] bytes,
        SarifEvidenceProvenance baseProvenance,
        CancellationToken cancellationToken)
    {
        if (!_documentReader.TryParseDocument(bytes, out JsonDocument? document))
        {
            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.MalformedInput,
                "The evidence artifact is not valid JSON.",
                baseProvenance);
        }

        using (JsonDocument parsedDocument = document!)
        {
            JsonElement root = parsedDocument.RootElement;
            if (!_documentReader.TryGetRuns(root, out JsonElement runs, out SarifEvidenceDocumentFailure? shapeFailure, out string shapeDetail))
            {
                return CreateResult(requirement.Id, MapDocumentFailure(shapeFailure!.Value), shapeDetail, baseProvenance);
            }

            SarifRunSelection selection = _documentReader.SelectMatchingRun(
                runs,
                requirement,
                limits,
                cancellationToken);
            if (selection.Failure is not null)
            {
                return CreateSelectionFailureResult(requirement.Id, baseProvenance, selection);
            }

            return ValidateSelectedRun(
                requirement,
                artifact,
                expectedContext,
                limits,
                root,
                selection.Candidate!.Value,
                baseProvenance,
                cancellationToken);
        }
    }

    private SarifEvidenceReadResult CreateSelectionFailureResult(
        string requirementId,
        SarifEvidenceProvenance baseProvenance,
        SarifRunSelection selection)
    {
        SarifEvidenceProvenance provenance = selection.Candidate is { } candidate
            ? WithRun(baseProvenance, candidate, null, null)
            : baseProvenance;
        return CreateResult(requirementId, MapDocumentFailure(selection.Failure!.Value), selection.Detail!, provenance);
    }

    private SarifEvidenceReadResult ValidateSelectedRun(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceArtifactReference artifact,
        SarifEvidenceAssessmentContext expectedContext,
        SarifEvidenceLimits limits,
        JsonElement root,
        SarifRunCandidate selected,
        SarifEvidenceProvenance baseProvenance,
        CancellationToken cancellationToken)
    {
        int? resultCount = _documentReader.ReadResultCount(
            selected.Run,
            limits,
            out SarifEvidenceDocumentFailure? resultFailure,
            out string? resultDetail);
        SarifEvidenceProvenance selectedProvenance = WithRun(baseProvenance, selected, resultCount, null);
        if (resultFailure is not null)
        {
            return CreateResult(requirement.Id, MapDocumentFailure(resultFailure.Value), resultDetail!, selectedProvenance);
        }

        if (_documentReader.HasDuplicateProperties(root, cancellationToken))
        {
            return DuplicatePropertiesResult(requirement.Id, selectedProvenance);
        }

        _documentReader.ReadExecutionState(
            selected.Run,
            out SarifEvidenceDocumentFailure? executionFailure,
            out string? executionDetail);
        if (executionFailure is not null)
        {
            return CreateResult(requirement.Id, MapDocumentFailure(executionFailure.Value), executionDetail!, selectedProvenance);
        }

        ContextReadOutcome context = _contextReader.ReadContext(selected.Run, artifact);
        SarifEvidenceProvenance contextProvenance = selectedProvenance with { Context = context.Context };
        if (context.Failure is not null)
        {
            return CreateResult(requirement.Id, MapContextFailure(context.Failure.Value), context.Detail!, contextProvenance);
        }

        ContextBindingFailure? bindingFailure = _contextReader.ValidateBindings(
            requirement,
            expectedContext,
            context.Context,
            out string? bindingDetail);
        if (bindingFailure is not null)
        {
            return CreateResult(requirement.Id, MapBindingFailure(bindingFailure.Value), bindingDetail!, contextProvenance);
        }

        IReadOnlyList<SarifEvidenceSourceDiagnostic> sourceDiagnostics = Array.Empty<SarifEvidenceSourceDiagnostic>();
        SarifEvidenceAuthorizationSnapshot? authorization = null;
        if (requirement.DiagnosticFilter is not null
            && !_sourceProjectionReader.TryReadSourceDiagnostics(
                selected.Run,
                out sourceDiagnostics,
                out string? sourceShapeDetail,
                cancellationToken))
        {
            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.UnsupportedShape,
                sourceShapeDetail!,
                contextProvenance);
        }

        if (requirement.DiagnosticFilter is not null)
        {
            authorization = CaptureAuthorization(
                requirement,
                expectedContext,
                context.Context);
        }

        return CreateResult(
            requirement.Id,
            SarifEvidenceTrustStatus.Valid,
            "The SARIF artifact contains one matching successful run bound to the assessment context.",
            contextProvenance,
            sourceDiagnostics,
            authorization);
    }

    private static SarifEvidenceTrustStatus MapDocumentFailure(SarifEvidenceDocumentFailure failure)
    {
        return failure switch
        {
            SarifEvidenceDocumentFailure.UnsupportedVersion => SarifEvidenceTrustStatus.UnsupportedVersion,
            SarifEvidenceDocumentFailure.UnsupportedShape => SarifEvidenceTrustStatus.UnsupportedShape,
            SarifEvidenceDocumentFailure.MissingExpectedRun => SarifEvidenceTrustStatus.MissingExpectedRun,
            SarifEvidenceDocumentFailure.AmbiguousExpectedRun => SarifEvidenceTrustStatus.AmbiguousExpectedRun,
            SarifEvidenceDocumentFailure.FailedExecution => SarifEvidenceTrustStatus.FailedExecution,
            SarifEvidenceDocumentFailure.IncompleteExecution => SarifEvidenceTrustStatus.IncompleteExecution,
            SarifEvidenceDocumentFailure.TooManyRuns => SarifEvidenceTrustStatus.TooManyRuns,
            SarifEvidenceDocumentFailure.TooManyResults => SarifEvidenceTrustStatus.TooManyResults,
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
        };
    }

    private static SarifEvidenceTrustStatus MapContextFailure(ContextReadFailure failure)
    {
        return failure switch
        {
            ContextReadFailure.UnsupportedShape => SarifEvidenceTrustStatus.UnsupportedShape,
            ContextReadFailure.WrongLogicalId => SarifEvidenceTrustStatus.WrongLogicalId,
            ContextReadFailure.ConflictingContext => SarifEvidenceTrustStatus.ConflictingContext,
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
        };
    }

    private static SarifEvidenceTrustStatus MapBindingFailure(ContextBindingFailure failure)
    {
        return failure switch
        {
            ContextBindingFailure.MissingLogicalId => SarifEvidenceTrustStatus.MissingLogicalId,
            ContextBindingFailure.WrongLogicalId => SarifEvidenceTrustStatus.WrongLogicalId,
            ContextBindingFailure.MissingRepository => SarifEvidenceTrustStatus.MissingRepository,
            ContextBindingFailure.WrongRepository => SarifEvidenceTrustStatus.WrongRepository,
            ContextBindingFailure.MissingRevision => SarifEvidenceTrustStatus.MissingRevision,
            ContextBindingFailure.WrongRevision => SarifEvidenceTrustStatus.WrongRevision,
            ContextBindingFailure.MissingScope => SarifEvidenceTrustStatus.MissingScope,
            ContextBindingFailure.WrongScope => SarifEvidenceTrustStatus.WrongScope,
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
        };
    }

    private static SarifEvidenceReadResult CreateResult(
        string logicalId,
        SarifEvidenceTrustStatus status,
        string detail,
        SarifEvidenceProvenance? provenance = null,
        IReadOnlyList<SarifEvidenceSourceDiagnostic>? sourceDiagnostics = null,
        SarifEvidenceAuthorizationSnapshot? authorization = null)
    {
        provenance ??= new SarifEvidenceProvenance(logicalId, null, null, null, null, null, null, null);
        return new SarifEvidenceReadResult(
            status,
            ReasonCode(status),
            detail,
            provenance,
            sourceDiagnostics,
            authorization);
    }

    private static SarifEvidenceAuthorizationSnapshot CaptureAuthorization(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceAssessmentContext assessmentContext,
        SarifEvidenceResolvedContext validatedContext)
    {
        ArchitectureExternalEvidenceDiagnosticFilter? filter = requirement.DiagnosticFilter;
        ValidateDiagnosticFilterBounds(filter);
        return new SarifEvidenceAuthorizationSnapshot(
            requirement.Id,
            requirement.Tool,
            requirement.ToolVersion,
            requirement.Run,
            requirement.RequireRepository,
            requirement.RequireRevision,
            requirement.RequireScope,
            assessmentContext,
            filter is null
                ? null
                : new SarifExternalDiagnosticFilterAuthorization(
                    filter.RuleIds,
                    filter.RuleTags,
                    filter.Projects,
                    filter.PathPrefixes,
                    filter.Severity,
                    filter.RequireMatches),
            validatedContext);
    }

    private static void ValidateDiagnosticFilterBounds(ArchitectureExternalEvidenceDiagnosticFilter? filter)
    {
        if (filter is null)
        {
            return;
        }

        ValidateDiagnosticFilterBound("rule_ids", filter.RuleIds?.Count ?? 0);
        ValidateDiagnosticFilterBound("rule_tags", filter.RuleTags?.Count ?? 0);
        ValidateDiagnosticFilterBound("projects", filter.Projects?.Count ?? 0);
        ValidateDiagnosticFilterBound("path_prefixes", filter.PathPrefixes?.Count ?? 0);
        if (filter.Severity?.Count > ExternalDiagnosticFilterRules.SupportedSeverities.Length)
        {
            throw new ArgumentException(
                "The external-evidence diagnostic_filter.severity map exceeds the supported source-severity bound.",
                nameof(filter));
        }
    }

    private static void ValidateDiagnosticFilterBound(string name, int count)
    {
        if (count > ExternalDiagnosticFilterRules.MaxValuesPerSelector)
        {
            throw new ArgumentException(
                $"The external-evidence diagnostic_filter.{name} list exceeds the " +
                $"{ExternalDiagnosticFilterRules.MaxValuesPerSelector}-value bound.",
                name);
        }
    }

    private static SarifEvidenceReadResult DuplicatePropertiesResult(
        string logicalId,
        SarifEvidenceProvenance provenance)
    {
        return CreateResult(
            logicalId,
            SarifEvidenceTrustStatus.UnsupportedShape,
            "The SARIF document contains duplicate JSON object properties.",
            provenance);
    }

    private static SarifEvidenceProvenance WithRun(
        SarifEvidenceProvenance provenance,
        SarifRunCandidate candidate,
        int? resultCount,
        SarifEvidenceResolvedContext? context)
    {
        return provenance with
        {
            ToolName = candidate.ToolName,
            ToolVersion = candidate.ToolVersion,
            RunId = candidate.RunId,
            ResultCount = resultCount,
            Context = context,
        };
    }

    private static string ReasonCode(SarifEvidenceTrustStatus status)
    {
        return status switch
        {
            SarifEvidenceTrustStatus.Valid => "valid",
            SarifEvidenceTrustStatus.OptionalNotConfigured => "optional_not_configured",
            SarifEvidenceTrustStatus.MissingRequiredInput => "missing_required_input",
            SarifEvidenceTrustStatus.MissingOptionalInput => "missing_optional_input",
            SarifEvidenceTrustStatus.UnreadableInput => "unreadable_external_input",
            SarifEvidenceTrustStatus.UnsafePath => "unsafe_external_path",
            SarifEvidenceTrustStatus.ArtifactTooLarge => "external_input_limit_exceeded",
            SarifEvidenceTrustStatus.MalformedInput => "malformed_external_input",
            SarifEvidenceTrustStatus.UnsupportedVersion => "unsupported_external_version",
            SarifEvidenceTrustStatus.UnsupportedShape => "unsupported_external_shape",
            SarifEvidenceTrustStatus.MissingExpectedRun => "missing_expected_run",
            SarifEvidenceTrustStatus.AmbiguousExpectedRun => "ambiguous_expected_run",
            SarifEvidenceTrustStatus.FailedExecution => "failed_external_execution",
            SarifEvidenceTrustStatus.IncompleteExecution => "incomplete_external_execution",
            SarifEvidenceTrustStatus.MissingLogicalId => "missing_external_evidence_identity",
            SarifEvidenceTrustStatus.WrongLogicalId => "wrong_external_evidence_identity",
            SarifEvidenceTrustStatus.MissingRepository => "missing_external_repository",
            SarifEvidenceTrustStatus.WrongRepository => "wrong_external_repository",
            SarifEvidenceTrustStatus.MissingRevision => "missing_external_revision",
            SarifEvidenceTrustStatus.WrongRevision => "wrong_external_revision",
            SarifEvidenceTrustStatus.MissingScope => "missing_external_scope",
            SarifEvidenceTrustStatus.WrongScope => "wrong_external_scope",
            SarifEvidenceTrustStatus.ConflictingContext => "conflicting_external_context",
            SarifEvidenceTrustStatus.TooManyRuns => "external_input_limit_exceeded",
            SarifEvidenceTrustStatus.TooManyResults => "external_input_limit_exceeded",
            _ => "unassessable_external_input",
        };
    }
}
