using System.Collections.ObjectModel;

namespace ArchLinterNet.Core.Model;

/// <summary>Immutable diagnostic-filter terms captured when an external SARIF artifact is trusted.</summary>
public sealed record SarifExternalDiagnosticFilterAuthorization
{
    /// <summary>Creates a detached, deterministic copy of one diagnostic filter.</summary>
    internal SarifExternalDiagnosticFilterAuthorization(
        IEnumerable<string>? ruleIds,
        IEnumerable<string>? ruleTags,
        IEnumerable<string>? projects,
        IEnumerable<string>? pathPrefixes,
        IEnumerable<KeyValuePair<string, string>>? severity,
        bool requireMatches)
    {
        RuleIds = CopyValues(ruleIds);
        RuleTags = CopyValues(ruleTags);
        Projects = CopyValues(projects);
        PathPrefixes = CopyValues(pathPrefixes);
        Severity = new ReadOnlyDictionary<string, string>(severity is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : severity
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        RequireMatches = requireMatches;
    }

    /// <summary>Exact source rule identifiers accepted by the captured filter.</summary>
    public IReadOnlyList<string> RuleIds { get; }

    /// <summary>Exact driver-rule tags accepted by the captured filter.</summary>
    public IReadOnlyList<string> RuleTags { get; }

    /// <summary>Exact source project values accepted by the captured filter.</summary>
    public IReadOnlyList<string> Projects { get; }

    /// <summary>Repository-relative source path prefixes accepted by the captured filter.</summary>
    public IReadOnlyList<string> PathPrefixes { get; }

    /// <summary>Captured source-severity to governance-mode mappings.</summary>
    public IReadOnlyDictionary<string, string> Severity { get; }

    /// <summary>Whether each configured filter value must match the trusted evidence group.</summary>
    public bool RequireMatches { get; }

    private static IReadOnlyList<string> CopyValues(IEnumerable<string>? values) => Array.AsReadOnly(
        (values ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray());
}

/// <summary>Immutable policy and assessment authorization captured by the SARIF trust reader.</summary>
public sealed record SarifEvidenceAuthorizationSnapshot
{
    /// <summary>Creates the detached authorization facts for one trusted external-evidence read.</summary>
    internal SarifEvidenceAuthorizationSnapshot(
        string logicalId,
        string tool,
        string? toolVersion,
        string run,
        bool requireRepository,
        bool requireRevision,
        bool requireScope,
        SarifEvidenceAssessmentContext assessmentContext,
        SarifExternalDiagnosticFilterAuthorization? diagnosticFilter,
        SarifEvidenceResolvedContext? validatedContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(run);
        ArgumentNullException.ThrowIfNull(assessmentContext);
        LogicalId = logicalId;
        Tool = tool;
        ToolVersion = toolVersion;
        Run = run;
        RequireRepository = requireRepository;
        RequireRevision = requireRevision;
        RequireScope = requireScope;
        AssessmentContext = new SarifEvidenceAssessmentContext(
            assessmentContext.Repository,
            assessmentContext.Revision,
            assessmentContext.Scope);
        DiagnosticFilter = diagnosticFilter;
        ValidatedContext = validatedContext;
    }

    /// <summary>Policy logical evidence identifier used by the trust reader.</summary>
    public string LogicalId { get; }

    /// <summary>Exact producer tool identity authorized by the trust reader.</summary>
    public string Tool { get; }

    /// <summary>Optional exact producer tool version authorized by the trust reader.</summary>
    public string? ToolVersion { get; }

    /// <summary>Exact producer run identity authorized by the trust reader.</summary>
    public string Run { get; }

    /// <summary>Whether repository binding was required for the trusted read.</summary>
    public bool RequireRepository { get; }

    /// <summary>Whether revision binding was required for the trusted read.</summary>
    public bool RequireRevision { get; }

    /// <summary>Whether scope binding was required for the trusted read.</summary>
    public bool RequireScope { get; }

    /// <summary>Assessment context against which the artifact was validated.</summary>
    public SarifEvidenceAssessmentContext AssessmentContext { get; }

    /// <summary>Detached diagnostic filter that was authorized at read time, when configured.</summary>
    public SarifExternalDiagnosticFilterAuthorization? DiagnosticFilter { get; }

    /// <summary>Resolved artifact context accepted by the trust reader, when the read was valid.</summary>
    public SarifEvidenceResolvedContext? ValidatedContext { get; }

    internal string GroupIdentity => string.Join(
        "\u001f",
        [
            LogicalId,
            Tool,
            ToolVersion ?? string.Empty,
            Run,
            RequireRepository ? "true" : "false",
            RequireRevision ? "true" : "false",
            RequireScope ? "true" : "false",
            AssessmentContext.Repository ?? string.Empty,
            AssessmentContext.Revision ?? string.Empty,
            AssessmentContext.Scope ?? string.Empty,
            ValidatedContext?.Repository ?? string.Empty,
            ValidatedContext?.Revision ?? string.Empty,
            ValidatedContext?.Scope ?? string.Empty,
            CreateFilterIdentity(DiagnosticFilter),
        ]);

    private static string CreateFilterIdentity(SarifExternalDiagnosticFilterAuthorization? filter)
    {
        if (filter is null)
        {
            return string.Empty;
        }

        IEnumerable<string> parts = filter.RuleIds
            .Append("|")
            .Concat(filter.RuleTags)
            .Append("|")
            .Concat(filter.Projects)
            .Append("|")
            .Concat(filter.PathPrefixes)
            .Append("|")
            .Concat(filter.Severity.Select(pair => pair.Key + "=" + pair.Value))
            .Append(filter.RequireMatches ? "true" : "false");
        return string.Join("\u001e", parts);
    }
}
