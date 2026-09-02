using System.CommandLine;
using System.CommandLine.Parsing;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Commands.Validate.Application;

internal sealed class ValidateCommandDefinition(ValidateCommandHandler handler)
{
    private const string StrictMode = "strict";
    private const string AuditMode = "audit";
    private const string HumanFormat = "human";

    public const string HelpText =
        """
        arch-linter-net — architecture contract linter for .NET

        Usage:
          arch-linter-net [options]
          arch-linter-net baseline generate --config <path> --output <path> [options]
          arch-linter-net graph [options]
          arch-linter-net topology capture|diff|verify [options]
          arch-linter-net explain --source <id> --target <id> [options]

        Validate Options:
          -p, --policy <path>   Path to YAML contract file
                                (default: architecture/dependencies.arch.yml)
          -m, --mode <mode>     Validation mode: strict or audit (default: strict)
              --strict          Shortcut for --mode strict
              --audit           Shortcut for --mode audit
              --contract <id>   Run only the contract with the given ID (may be repeated)
              --condition-set <name>
                                Use a named condition set from analysis.condition_sets
                                to control conditional compilation symbols during
                                Roslyn source analysis (default: policy default_condition_set,
                                otherwise empty symbol set)
              --baseline <path> Path to baseline file to merge with policy ignores
              --timings         Print phase-level timing report to stderr
              --profile <dest>  Write a machine-readable analysis-profile/v1 JSON
                                document to stdout, stderr, or a file path.
                                Independent of --timings/--report.
              --cache <dest>    Opt into the persistent analysis-cache/v1 (disabled by
                                default). "auto" uses the platform user-cache
                                namespace; any other value is a caller-selected
                                directory, validated for safe containment.
              --max-parallelism <n>
                                Bound the degree of parallel assembly/fact scanning
                                (default: max(1, min(processor count, 4))). Must be
                                a positive integer; 1 is a fully supported
                                sequential mode.
              --waiver-evaluation-date <yyyy-MM-dd>
                                Evaluate waiver expiry against this UTC calendar date;
                                useful for reproducible CI boundary checks.
              --external-evidence <binding>
                                Bind a declared external_evidence requirement to a
                                repository-local SARIF artifact. Repeatable, one binding
                                per occurrence: id=<id>,path=<path>[,repository=<value>]
                                [,revision=<value>][,scope=<value>]. The repository/
                                revision/scope fields are the producer/CI context for
                                that one artifact; see --evidence-repository etc. for
                                the current assessment context.
              --evidence-repository <value>
                                Current repository identity for external_evidence
                                context binding.
              --evidence-revision <value>
                                Current source revision for external_evidence context
                                binding.
              --evidence-scope <value>
                                Current assessment scope for external_evidence context
                                binding.
              --ensure-built    Build the selected project graph once, verify it via an
                                ArchLinterNet build receipt, then validate (never implicit;
                                opt-in only)
              --no-restore      Fail closed with a restore-required diagnostic instead of
                                restoring; combine with --ensure-built to build offline
              --configuration <name>
                                Requested build configuration for build-state preflight
                                (e.g. Debug, Release)
              --framework <tfm> Requested target framework for build-state preflight
              --platform <platform> Requested platform for build-state preflight
              --runtime <rid>     Requested runtime identifier for build-state preflight
          -f, --format <fmt>    Stdout output format: human, json, or sarif
                                (default: human). See --report for additional
                                output destinations.
                                sarif covers violations, cycles, and build-state
                                preflight findings; coverage, unmatched-ignore, and
                                policy-consistency findings can still fail the run
                                (exit code 1) without appearing in SARIF results —
                                use --format json to see those
              --json            Shortcut for --format json
              --report <val>    Additional output sink in format=destination
                                form. Destination is stdout, stderr, or a file
                                path. Repeatable: --report json=ci.json
                                --report sarif=ci.sarif
          -h, --help            Show this help message
          -v, --version         Show version

        Exit codes:
          0   All contracts passed
          1   One or more contracts failed
          2   Runtime error (invalid arguments, file not found, etc.)
        """;

    public RootCommand CreateRootCommand()
    {
        RootCommand command = new("arch-linter-net");
        RemoveBuiltInRootOptions(command);

        Option<string> policyOption = CreateOption("--policy", "architecture/dependencies.arch.yml");
        policyOption.Aliases.Add("-p");

        Option<string> modeOption = CreateOption("--mode", StrictMode);
        modeOption.Aliases.Add("-m");

        Option<string> formatOption = CreateOption("--format", HumanFormat);
        formatOption.Aliases.Add("-f");

        Option<string[]> contractOption = new("--contract");
        Option<string> conditionSetOption = new("--condition-set");
        Option<string> baselineOption = new("--baseline");
        Option<bool> strictOption = new("--strict");
        Option<bool> auditOption = new("--audit");
        Option<bool> jsonOption = new("--json");
        Option<string[]> reportOption = new("--report") { AllowMultipleArgumentsPerToken = true };
        Option<bool> timingsOption = new("--timings");
        Option<string> profileOption = new("--profile");
        Option<string> cacheOption = new("--cache");
        Option<int?> maxParallelismOption = new("--max-parallelism");
        Option<string> waiverEvaluationDateOption = new("--waiver-evaluation-date");
        Option<string[]> externalEvidenceOption = new("--external-evidence") { AllowMultipleArgumentsPerToken = true };
        Option<string> evidenceRepositoryOption = new("--evidence-repository");
        Option<string> evidenceRevisionOption = new("--evidence-revision");
        Option<string> evidenceScopeOption = new("--evidence-scope");
        Option<bool> ensureBuiltOption = new("--ensure-built");
        Option<bool> noRestoreOption = new("--no-restore");
        Option<string> configurationOption = new("--configuration");
        Option<string> targetFrameworkOption = new("--framework");
        Option<string> platformOption = new("--platform");
        Option<string> runtimeIdentifierOption = new("--runtime");
        Option<bool> helpOption = new("--help");
        helpOption.Aliases.Add("-h");
        Option<bool> versionOption = new("--version");
        versionOption.Aliases.Add("-v");

        command.Options.Add(policyOption);
        command.Options.Add(modeOption);
        command.Options.Add(formatOption);
        command.Options.Add(contractOption);
        command.Options.Add(conditionSetOption);
        command.Options.Add(baselineOption);
        command.Options.Add(strictOption);
        command.Options.Add(auditOption);
        command.Options.Add(jsonOption);
        command.Options.Add(reportOption);
        command.Options.Add(timingsOption);
        command.Options.Add(profileOption);
        command.Options.Add(cacheOption);
        command.Options.Add(maxParallelismOption);
        command.Options.Add(waiverEvaluationDateOption);
        command.Options.Add(externalEvidenceOption);
        command.Options.Add(evidenceRepositoryOption);
        command.Options.Add(evidenceRevisionOption);
        command.Options.Add(evidenceScopeOption);
        command.Options.Add(ensureBuiltOption);
        command.Options.Add(noRestoreOption);
        command.Options.Add(configurationOption);
        command.Options.Add(targetFrameworkOption);
        command.Options.Add(platformOption);
        command.Options.Add(runtimeIdentifierOption);
        command.Options.Add(helpOption);
        command.Options.Add(versionOption);

        command.SetAction(parseResult => handler.Execute(MapOptions(
            parseResult,
            policyOption,
            contractOption,
            conditionSetOption,
            baselineOption,
            reportOption,
            timingsOption,
            profileOption,
            cacheOption,
            maxParallelismOption,
            waiverEvaluationDateOption,
            externalEvidenceOption,
            evidenceRepositoryOption,
            evidenceRevisionOption,
            evidenceScopeOption,
            ensureBuiltOption,
            noRestoreOption,
            configurationOption,
            targetFrameworkOption,
            platformOption,
            runtimeIdentifierOption,
            helpOption,
            versionOption)));

        return command;
    }

    private static void RemoveBuiltInRootOptions(RootCommand command)
    {
        Option? helpOption = command.Options.SingleOrDefault(static option => option.Name == "help");
        if (helpOption is not null)
        {
            command.Options.Remove(helpOption);
        }

        Option? versionOption = command.Options.SingleOrDefault(static option => option.Name == "version");
        if (versionOption is not null)
        {
            command.Options.Remove(versionOption);
        }
    }

    private static ValidateCommandOptions MapOptions( // NOSONAR: individual Option<T> parameters are required by the System.CommandLine API pattern; grouping into a single definitions object would add indirection without eliminating any field
        ParseResult parseResult,
        Option<string> policyOption,
        Option<string[]> contractOption,
        Option<string> conditionSetOption,
        Option<string> baselineOption,
        Option<string[]> reportOption,
        Option<bool> timingsOption,
        Option<string> profileOption,
        Option<string> cacheOption,
        Option<int?> maxParallelismOption,
        Option<string> waiverEvaluationDateOption,
        Option<string[]> externalEvidenceOption,
        Option<string> evidenceRepositoryOption,
        Option<string> evidenceRevisionOption,
        Option<string> evidenceScopeOption,
        Option<bool> ensureBuiltOption,
        Option<bool> noRestoreOption,
        Option<string> configurationOption,
        Option<string> targetFrameworkOption,
        Option<string> platformOption,
        Option<string> runtimeIdentifierOption,
        Option<bool> helpOption,
        Option<bool> versionOption)
    {
        string mode = ResolveMode(parseResult);
        string format = ResolveFormat(parseResult, out bool isFormatExplicit);
        IReadOnlyList<ReportSink> additionalSinks = Array.Empty<ReportSink>();
        string? reportParseError = null;

        try
        {
            additionalSinks = ParseReportSinks(parseResult.GetValue(reportOption));
        }
        catch (InvalidOperationException ex)
        {
            reportParseError = ex.Message;
        }

        IReadOnlyList<SarifEvidenceArtifactReference> externalEvidenceArtifacts =
            Array.Empty<SarifEvidenceArtifactReference>();
        string? externalEvidenceParseError = null;
        try
        {
            externalEvidenceArtifacts = ExternalEvidenceCommandSupport.ParseBindings(
                parseResult.GetValue(externalEvidenceOption));
        }
        catch (InvalidOperationException ex)
        {
            externalEvidenceParseError = ex.Message;
        }

        SarifEvidenceAssessmentContext? externalEvidenceAssessmentContext = ExternalEvidenceCommandSupport.ResolveAssessmentContext(
            parseResult.GetValue(evidenceRepositoryOption),
            parseResult.GetValue(evidenceRevisionOption),
            parseResult.GetValue(evidenceScopeOption));

        return new ValidateCommandOptions(
            parseResult.GetValue(policyOption) ?? "architecture/dependencies.arch.yml",
            mode,
            format,
            parseResult.GetValue(contractOption) ?? Array.Empty<string>(),
            parseResult.GetValue(conditionSetOption),
            parseResult.GetValue(timingsOption),
            parseResult.GetValue(baselineOption),
            parseResult.GetValue(helpOption),
            parseResult.GetValue(versionOption),
            parseResult.GetValue(ensureBuiltOption),
            parseResult.GetValue(noRestoreOption),
            parseResult.GetValue(configurationOption),
            parseResult.GetValue(targetFrameworkOption),
            parseResult.GetValue(platformOption),
            parseResult.GetValue(runtimeIdentifierOption))
        {
            IsFormatExplicit = isFormatExplicit,
            AdditionalSinks = additionalSinks,
            ReportParseError = reportParseError,
            ProfileDestination = parseResult.GetValue(profileOption),
            CacheDestination = parseResult.GetValue(cacheOption),
            MaxParallelism = parseResult.GetValue(maxParallelismOption),
            WaiverEvaluationDate = parseResult.GetValue(waiverEvaluationDateOption),
            ExternalEvidenceArtifacts = externalEvidenceArtifacts,
            ExternalEvidenceAssessmentContext = externalEvidenceAssessmentContext,
            ExternalEvidenceParseError = externalEvidenceParseError,
        };
    }

    internal static SarifEvidenceAssessmentContext? ResolveExternalEvidenceAssessmentContext(
        string? repository, string? revision, string? scope)
    {
        return repository is null && revision is null && scope is null
            ? null
            : new SarifEvidenceAssessmentContext(repository, revision, scope);
    }

    // One occurrence = one binding: id=<id>,path=<path>[,repository=<v>][,revision=<v>][,scope=<v>].
    // Mirrors ParseReportSinks' key=value structured-option shape. Bindings are matched to declared
    // external_evidence requirements by id, not position, so multiple occurrences remain
    // order-independent (see ArchitectureExternalEvidenceBinder).
    internal static IReadOnlyList<SarifEvidenceArtifactReference> ParseExternalEvidenceBindings(
        string[]? rawValues)
    {
        if (rawValues is null || rawValues.Length == 0)
        {
            return Array.Empty<SarifEvidenceArtifactReference>();
        }

        List<SarifEvidenceArtifactReference> artifacts = new(rawValues.Length);
        HashSet<string> seenIds = new(StringComparer.Ordinal);
        foreach (string raw in rawValues)
        {
            Dictionary<string, string> fields = ParseExternalEvidenceFields(raw);
            if (!fields.TryGetValue("id", out string? id) || string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence value: '{raw}'. Missing required 'id'.");
            }

            if (!fields.TryGetValue("path", out string? path) || string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence value: '{raw}'. Missing required 'path'.");
            }

            if (!seenIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate --external-evidence binding for id '{id}'.");
            }

            fields.TryGetValue("repository", out string? repository);
            fields.TryGetValue("revision", out string? revision);
            fields.TryGetValue("scope", out string? scope);
            SarifEvidenceProducerContext? producer = repository is null && revision is null && scope is null
                ? null
                : new SarifEvidenceProducerContext(repository, revision, scope);
            artifacts.Add(new SarifEvidenceArtifactReference(path, id, producer));
        }

        return artifacts;
    }

    private static Dictionary<string, string> ParseExternalEvidenceFields(string raw)
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (string segment in raw.Split(','))
        {
            int eqIndex = segment.IndexOf('=');
            if (eqIndex <= 0 || eqIndex >= segment.Length - 1)
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence value: '{raw}'. Use " +
                    "id=<id>,path=<path>[,repository=<value>][,revision=<value>][,scope=<value>].");
            }

            string key = segment[..eqIndex];
            string value = segment[(eqIndex + 1)..];
            if (key is not ("id" or "path" or "repository" or "revision" or "scope"))
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence key '{key}' in '{raw}'. " +
                    "Supported keys: id, path, repository, revision, scope.");
            }

            if (!fields.TryAdd(key, value))
            {
                throw new InvalidOperationException(
                    $"Duplicate key '{key}' in --external-evidence value '{raw}'.");
            }
        }

        return fields;
}
    private static string ResolveMode(ParseResult parseResult)
    {
        string mode = StrictMode;
        bool expectModeValue = false;

        foreach (string token in EnumerateTokenValues(parseResult))
        {
            if (expectModeValue)
            {
                expectModeValue = false;
                mode = NormalizeModeOrPreserve(token);
                continue;
            }

            if (IsOption(token, "--mode", "-m"))
            {
                expectModeValue = true;
                continue;
            }

            if (IsOption(token, "--strict"))
            {
                mode = StrictMode;
                continue;
            }

            if (IsOption(token, "--audit"))
            {
                mode = AuditMode;
            }
        }

        return mode;
    }

    private static string ResolveFormat(ParseResult parseResult, out bool isExplicit)
    {
        string format = HumanFormat;
        isExplicit = false;
        bool expectFormatValue = false;

        foreach (string token in EnumerateTokenValues(parseResult))
        {
            if (expectFormatValue)
            {
                expectFormatValue = false;
                format = NormalizeFormatOrPreserve(token);
                continue;
            }

            if (IsOption(token, "--format", "-f"))
            {
                expectFormatValue = true;
                isExplicit = true;
                continue;
            }

            if (IsOption(token, "--json"))
            {
                format = "json";
                isExplicit = true;
            }
        }

        return format;
    }

    private static IEnumerable<string> EnumerateTokenValues(ParseResult parseResult)
    {
        return parseResult.Tokens.Select(static token => token.Value);
    }

    private static string NormalizeModeOrPreserve(string token)
    {
        if (string.Equals(token, AuditMode, StringComparison.OrdinalIgnoreCase))
        {
            return AuditMode;
        }

        if (string.Equals(token, StrictMode, StringComparison.OrdinalIgnoreCase))
        {
            return StrictMode;
        }

        return token;
    }

    private static string NormalizeFormatOrPreserve(string token)
    {
        if (string.Equals(token, "json", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        if (string.Equals(token, "sarif", StringComparison.OrdinalIgnoreCase))
        {
            return "sarif";
        }

        if (string.Equals(token, HumanFormat, StringComparison.OrdinalIgnoreCase))
        {
            return HumanFormat;
        }

        return token;
    }

    private static IReadOnlyList<ReportSink> ParseReportSinks(string[]? rawValues)
    {
        if (rawValues is null || rawValues.Length == 0)
        {
            return Array.Empty<ReportSink>();
        }

        List<ReportSink> sinks = new(rawValues.Length);
        HashSet<string> destinations = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in rawValues)
        {
            int eqIndex = raw.IndexOf('=');
            if (eqIndex <= 0 || eqIndex >= raw.Length - 1)
            {
                throw new InvalidOperationException(
                    $"Invalid --report value: '{raw}'. Use format=destination (e.g. json=results.json).");
            }

            string format = raw[..eqIndex];
            string destination = raw[(eqIndex + 1)..];

            if (format is not ("human" or "json" or "sarif"))
            {
                throw new InvalidOperationException(
                    $"Invalid format in --report: '{format}'. Use human, json, or sarif.");
            }

            string dedupKey = destination switch
            {
                "stdout" => "stdout",
                "stderr" => "stderr",
                _ => Path.GetFullPath(destination),
            };

            if (!destinations.Add(dedupKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate --report destination: '{destination}'.");
            }

            ReportSink sink = destination switch
            {
                "stdout" => new ReportSink(format, ReportDestinationType.Stdout),
                "stderr" => new ReportSink(format, ReportDestinationType.Stderr),
                _ => new ReportSink(format, ReportDestinationType.File, destination),
            };

            sinks.Add(sink);
        }

        return sinks;
    }

    private static bool IsOption(string token, params string[] names)
    {
        return names.Any(name => string.Equals(token, name, StringComparison.Ordinal));
    }

    private static Option<string> CreateOption(string name, string defaultValue)
    {
        Option<string> option = new(name);
        option.DefaultValueFactory = _ => defaultValue;
        return option;
    }
}
