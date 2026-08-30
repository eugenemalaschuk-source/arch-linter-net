using System.Text.Json;
using ArchLinterNet.Core.Contracts;
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
public sealed partial class SarifEvidenceReader(IArchitectureEvidenceFileSystem? fileSystem = null)
{
    private const string SupportedFormat = "sarif";
    private const string SupportedVersion = "2.1.0";
    private readonly IArchitectureEvidenceFileSystem _fileSystem = fileSystem ?? ArchitectureFileSystem.Real;

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
        ValidateRequirement(requirement);
        expectedContext ??= new SarifEvidenceAssessmentContext();
        limits ??= new SarifEvidenceLimits();

        if (artifact is null)
        {
            return CreateMissingArtifactResult(requirement);
        }

        PathResolution path = ResolveArtifactPath(repositoryRoot, artifact.Path);
        if (!path.IsSafe)
        {
            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.UnsafePath,
                "The evidence path is absolute, outside the repository, or crosses an unsafe filesystem indirection.");
        }

        return ReadResolvedArtifact(requirement, artifact, expectedContext, limits, path, cancellationToken);
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

    private SarifEvidenceReadResult ReadResolvedArtifact(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceArtifactReference artifact,
        SarifEvidenceAssessmentContext expectedContext,
        SarifEvidenceLimits limits,
        PathResolution path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ByteReadOutcome bytes = ReadBoundedBytes(path, limits.MaxArtifactBytes, cancellationToken);
        SarifEvidenceProvenance baseProvenance = new(
            requirement.Id,
            bytes.IsReadable || bytes.BytesRead > 0 ? path.RelativePath : null,
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

    private static void ValidateRequirement(ArchitectureExternalEvidenceRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement.Id))
        {
            throw new ArgumentException("An external evidence requirement id is required.", nameof(requirement));
        }

        if (!string.Equals(requirement.Format, SupportedFormat, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The bounded evidence reader supports only SARIF format.", nameof(requirement));
        }

        if (string.IsNullOrWhiteSpace(requirement.Tool))
        {
            throw new ArgumentException("An external evidence tool name is required.", nameof(requirement));
        }

        if (requirement.ToolVersion is not null && string.IsNullOrWhiteSpace(requirement.ToolVersion))
        {
            throw new ArgumentException(
                "An external evidence tool version must be non-blank when supplied.",
                nameof(requirement));
        }

        if (string.IsNullOrWhiteSpace(requirement.Run))
        {
            throw new ArgumentException("An external evidence run id is required.", nameof(requirement));
        }
    }

    private static SarifEvidenceReadResult ParseAndValidate(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceArtifactReference artifact,
        SarifEvidenceAssessmentContext expectedContext,
        SarifEvidenceLimits limits,
        byte[] bytes,
        SarifEvidenceProvenance baseProvenance,
        CancellationToken cancellationToken)
    {
        if (!TryParseDocument(bytes, out JsonDocument? document))
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
            if (!TryGetRuns(root, out JsonElement runs, out SarifEvidenceTrustStatus shapeStatus, out string shapeDetail))
            {
                return CreateResult(requirement.Id, shapeStatus, shapeDetail, baseProvenance);
            }

            SarifRunSelection selection = SelectMatchingRun(runs, requirement, limits, cancellationToken);
            if (selection.Status is not null)
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

    private static bool TryParseDocument(byte[] bytes, out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128,
                });
            return true;
        }
        catch (JsonException)
        {
            document = null;
            return false;
        }
    }

    private static bool TryGetRuns(
        JsonElement root,
        out JsonElement runs,
        out SarifEvidenceTrustStatus status,
        out string detail)
    {
        runs = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            status = SarifEvidenceTrustStatus.UnsupportedShape;
            detail = "The SARIF document root must be an object.";
            return false;
        }

        if (!root.TryGetProperty("version", out JsonElement version))
        {
            status = SarifEvidenceTrustStatus.UnsupportedVersion;
            detail = "The SARIF document does not declare a version.";
            return false;
        }

        if (version.ValueKind != JsonValueKind.String)
        {
            status = SarifEvidenceTrustStatus.UnsupportedShape;
            detail = "The SARIF version must be a string.";
            return false;
        }

        if (!string.Equals(version.GetString(), SupportedVersion, StringComparison.Ordinal))
        {
            status = SarifEvidenceTrustStatus.UnsupportedVersion;
            detail = "Only SARIF version 2.1.0 is supported.";
            return false;
        }

        if (!root.TryGetProperty("runs", out runs) || runs.ValueKind != JsonValueKind.Array)
        {
            status = SarifEvidenceTrustStatus.UnsupportedShape;
            detail = "The SARIF document must contain a runs array.";
            return false;
        }

        status = default;
        detail = string.Empty;
        return true;
    }

    private static SarifRunSelection SelectMatchingRun(
        JsonElement runs,
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceLimits limits,
        CancellationToken cancellationToken)
    {
        List<SarifRunCandidate> matches = [];
        int runCount = 0;
        foreach (JsonElement run in runs.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++runCount > limits.MaxRuns)
            {
                return SarifRunSelection.Failure(
                    SarifEvidenceTrustStatus.TooManyRuns,
                    "The SARIF document exceeds the configured run limit.");
            }

            if (!TryGetMatchingCandidate(run, requirement, out SarifRunCandidate? candidate, out SarifRunSelection? failure))
            {
                if (failure is not null)
                {
                    return failure.Value;
                }

                continue;
            }

            matches.Add(candidate!.Value);
        }

        return matches.Count switch
        {
            0 => SarifRunSelection.Failure(
                SarifEvidenceTrustStatus.MissingExpectedRun,
                "No SARIF run matched the configured tool and automation run identity."),
            1 => SarifRunSelection.Selected(matches[0]),
            _ => SarifRunSelection.Failure(
                SarifEvidenceTrustStatus.AmbiguousExpectedRun,
                "More than one SARIF run matched the configured tool and automation run identity.",
                matches[0]),
        };
    }

    private static bool TryGetMatchingCandidate(
        JsonElement run,
        ArchitectureExternalEvidenceRequirement requirement,
        out SarifRunCandidate? candidate,
        out SarifRunSelection? failure)
    {
        candidate = null;
        failure = null;
        if (run.ValueKind != JsonValueKind.Object)
        {
            failure = SarifRunSelection.Failure(
                SarifEvidenceTrustStatus.UnsupportedShape,
                "Every SARIF run must be an object.");
            return false;
        }

        if (!TryReadRunIdentity(run, out SarifRunCandidate parsed, out string? shapeError))
        {
            if (shapeError is not null)
            {
                failure = SarifRunSelection.Failure(SarifEvidenceTrustStatus.UnsupportedShape, shapeError);
            }

            return false;
        }

        if (!MatchesRequirement(parsed, requirement))
        {
            return false;
        }

        candidate = parsed;
        return true;
    }

    private static bool MatchesRequirement(
        SarifRunCandidate candidate,
        ArchitectureExternalEvidenceRequirement requirement)
    {
        return string.Equals(candidate.ToolName, requirement.Tool, StringComparison.Ordinal)
            && string.Equals(candidate.RunId, requirement.Run, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(requirement.ToolVersion)
                || string.Equals(candidate.ToolVersion, requirement.ToolVersion, StringComparison.Ordinal));
    }

    private static SarifEvidenceReadResult CreateSelectionFailureResult(
        string requirementId,
        SarifEvidenceProvenance baseProvenance,
        SarifRunSelection selection)
    {
        SarifEvidenceProvenance provenance = selection.Candidate is { } candidate
            ? WithRun(baseProvenance, candidate, null, null)
            : baseProvenance;
        return CreateResult(requirementId, selection.Status!.Value, selection.Detail!, provenance);
    }

    private static SarifEvidenceReadResult ValidateSelectedRun(
        ArchitectureExternalEvidenceRequirement requirement,
        SarifEvidenceArtifactReference artifact,
        SarifEvidenceAssessmentContext expectedContext,
        SarifEvidenceLimits limits,
        JsonElement root,
        SarifRunCandidate selected,
        SarifEvidenceProvenance baseProvenance,
        CancellationToken cancellationToken)
    {
        int? resultCount = ReadResultCount(selected.Run, limits, out SarifEvidenceTrustStatus? resultStatus, out string? resultDetail);
        SarifEvidenceProvenance selectedProvenance = WithRun(baseProvenance, selected, resultCount, null);
        if (resultStatus is not null)
        {
            return CreateResult(requirement.Id, resultStatus.Value, resultDetail!, selectedProvenance);
        }

        if (HasDuplicateProperties(root, cancellationToken))
        {
            return DuplicatePropertiesResult(requirement.Id, selectedProvenance);
        }

        _ = ReadExecutionState(selected.Run, out SarifEvidenceTrustStatus? executionStatus, out string? executionDetail);
        if (executionStatus is not null)
        {
            return CreateResult(requirement.Id, executionStatus.Value, executionDetail!, selectedProvenance);
        }

        ContextReadOutcome context = ReadContext(selected.Run, artifact, out SarifEvidenceTrustStatus? contextStatus, out string? contextDetail);
        SarifEvidenceProvenance contextProvenance = selectedProvenance with { Context = context.Context };
        if (contextStatus is not null)
        {
            return CreateResult(requirement.Id, contextStatus.Value, contextDetail!, contextProvenance);
        }

        SarifEvidenceTrustStatus? bindingStatus = ValidateBindings(
            requirement,
            expectedContext,
            context.Context,
            out string? bindingDetail);
        if (bindingStatus is not null)
        {
            return CreateResult(requirement.Id, bindingStatus.Value, bindingDetail!, contextProvenance);
        }

        if (!TryReadSourceDiagnostics(
                selected.Run,
                out IReadOnlyList<SarifEvidenceSourceDiagnostic> sourceDiagnostics,
                out string? sourceShapeDetail,
                cancellationToken))
        {
            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.UnsupportedShape,
                sourceShapeDetail!,
                contextProvenance);
        }

        SarifEvidenceAuthorizationSnapshot authorization = CaptureAuthorization(
            requirement,
            expectedContext,
            context.Context);

        return CreateResult(
            requirement.Id,
            SarifEvidenceTrustStatus.Valid,
            "The SARIF artifact contains one matching successful run bound to the assessment context.",
            contextProvenance,
            sourceDiagnostics,
            authorization);
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

    private readonly record struct ContextReadOutcome(SarifEvidenceResolvedContext Context);

    private readonly record struct SarifRunSelection(
        SarifRunCandidate? Candidate,
        SarifEvidenceTrustStatus? Status,
        string? Detail)
    {
        public static SarifRunSelection Selected(SarifRunCandidate candidate) => new(candidate, null, null);

        public static SarifRunSelection Failure(
            SarifEvidenceTrustStatus status,
            string detail,
            SarifRunCandidate? candidate = null) => new(candidate, status, detail);
    }
}
