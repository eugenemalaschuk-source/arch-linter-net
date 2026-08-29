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
/// <remarks>Creates a reader using the supplied file-system seam.</remarks>
public sealed partial class SarifEvidenceReader(IArchitectureFileSystem? fileSystem = null)
{
    private const string SupportedFormat = "sarif";
    private const string SupportedVersion = "2.1.0";
    private readonly IArchitectureFileSystem _fileSystem = fileSystem ?? ArchitectureFileSystem.Real;

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
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("A repository root is required.", nameof(repositoryRoot));
        }

        ValidateRequirement(requirement);
        expectedContext ??= new SarifEvidenceAssessmentContext();
        limits ??= new SarifEvidenceLimits();

        string requirementId = requirement.Id;
        if (artifact is null)
        {
            return CreateResult(
                requirementId,
                requirement.Required
                    ? SarifEvidenceTrustStatus.MissingRequiredInput
                    : SarifEvidenceTrustStatus.OptionalNotConfigured,
                requirement.Required
                    ? "The required external evidence artifact was not supplied."
                    : "The optional external evidence artifact was not configured.");
        }

        PathResolution path = ResolveArtifactPath(repositoryRoot, artifact.Path);
        if (!path.IsSafe)
        {
            return CreateResult(
                requirementId,
                SarifEvidenceTrustStatus.UnsafePath,
                "The evidence path is absolute, outside the repository, or crosses an unsafe filesystem indirection.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        ByteReadOutcome bytes = ReadBoundedBytes(path, limits.MaxArtifactBytes, cancellationToken);
        SarifEvidenceProvenance baseProvenance = new(
            requirementId,
            bytes.IsReadable || bytes.BytesRead > 0 ? path.RelativePath : null,
            bytes.IsReadable || bytes.BytesRead > 0 ? bytes.Sha256 : null,
            null,
            null,
            null,
            null,
            null);

        if (!bytes.IsReadable)
        {
            SarifEvidenceTrustStatus status = bytes.Failure switch
            {
                ArtifactReadFailure.Missing => requirement.Required
                    ? SarifEvidenceTrustStatus.MissingRequiredInput
                    : SarifEvidenceTrustStatus.MissingOptionalInput,
                ArtifactReadFailure.Unsafe => SarifEvidenceTrustStatus.UnsafePath,
                _ => SarifEvidenceTrustStatus.UnreadableInput,
            };
            return CreateResult(
                requirementId,
                status,
                bytes.Failure == ArtifactReadFailure.Unsafe
                    ? "The evidence path is not a repository-local regular file or changed while it was opened."
                    : "The evidence artifact could not be read.",
                baseProvenance);
        }

        if (bytes.ExceededLimit)
        {
            return CreateResult(
                requirementId,
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
        JsonDocument document;
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
        }
        catch (JsonException)
        {
            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.MalformedInput,
                "The evidence artifact is not valid JSON.",
                baseProvenance);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.UnsupportedShape,
                    "The SARIF document root must be an object.",
                    baseProvenance);
            }

            if (HasDuplicateProperties(root))
            {
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.UnsupportedShape,
                    "The SARIF document contains duplicate JSON object properties.",
                    baseProvenance);
            }

            if (!root.TryGetProperty("version", out JsonElement version))
            {
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.UnsupportedVersion,
                    "The SARIF document does not declare a version.",
                    baseProvenance);
            }

            if (version.ValueKind != JsonValueKind.String)
            {
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.UnsupportedShape,
                    "The SARIF version must be a string.",
                    baseProvenance);
            }

            if (!string.Equals(version.GetString(), SupportedVersion, StringComparison.Ordinal))
            {
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.UnsupportedVersion,
                    "Only SARIF version 2.1.0 is supported.",
                    baseProvenance);
            }

            if (!root.TryGetProperty("runs", out JsonElement runs)
                || runs.ValueKind != JsonValueKind.Array)
            {
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.UnsupportedShape,
                    "The SARIF document must contain a runs array.",
                    baseProvenance);
            }

            List<SarifRunCandidate> matches = [];
            int runCount = 0;
            foreach (JsonElement run in runs.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                runCount++;
                if (runCount > limits.MaxRuns)
                {
                    return CreateResult(
                        requirement.Id,
                        SarifEvidenceTrustStatus.TooManyRuns,
                        "The SARIF document exceeds the configured run limit.",
                        baseProvenance);
                }

                if (run.ValueKind != JsonValueKind.Object)
                {
                    return CreateResult(
                        requirement.Id,
                        SarifEvidenceTrustStatus.UnsupportedShape,
                        "Every SARIF run must be an object.",
                        baseProvenance);
                }

                if (!TryReadRunIdentity(run, out SarifRunCandidate candidate, out string? shapeError))
                {
                    if (shapeError is not null)
                    {
                        return CreateResult(
                            requirement.Id,
                            SarifEvidenceTrustStatus.UnsupportedShape,
                            shapeError,
                            baseProvenance);
                    }

                    continue;
                }

                if (!string.Equals(candidate.ToolName, requirement.Tool, StringComparison.Ordinal)
                    || !string.Equals(candidate.RunId, requirement.Run, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(requirement.ToolVersion)
                        && !string.Equals(candidate.ToolVersion, requirement.ToolVersion, StringComparison.Ordinal)))
                {
                    continue;
                }

                matches.Add(candidate);
            }

            if (matches.Count == 0)
            {
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.MissingExpectedRun,
                    "No SARIF run matched the configured tool and automation run identity.",
                    baseProvenance);
            }

            if (matches.Count > 1)
            {
                SarifRunCandidate first = matches[0];
                return CreateResult(
                    requirement.Id,
                    SarifEvidenceTrustStatus.AmbiguousExpectedRun,
                    "More than one SARIF run matched the configured tool and automation run identity.",
                    WithRun(baseProvenance, first, null, null));
            }

            SarifRunCandidate selected = matches[0];
            int? resultCount = ReadResultCount(selected.Run, limits, out SarifEvidenceTrustStatus? resultStatus, out string? resultDetail);
            SarifEvidenceProvenance selectedProvenance = WithRun(
                baseProvenance,
                selected,
                resultCount,
                null);
            if (resultStatus is not null)
            {
                return CreateResult(requirement.Id, resultStatus.Value, resultDetail!, selectedProvenance);
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

            return CreateResult(
                requirement.Id,
                SarifEvidenceTrustStatus.Valid,
                "The SARIF artifact contains one matching successful run bound to the assessment context.",
                contextProvenance);
        }
    }

    private static SarifEvidenceReadResult CreateResult(
        string logicalId,
        SarifEvidenceTrustStatus status,
        string detail,
        SarifEvidenceProvenance? provenance = null)
    {
        provenance ??= new SarifEvidenceProvenance(logicalId, null, null, null, null, null, null, null);
        return new SarifEvidenceReadResult(status, ReasonCode(status), detail, provenance);
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
}
