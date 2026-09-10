using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Policy.Application;

internal sealed class PolicyWeakeningCommandHandler(ICliRuntime runtime, ICliConsole console, IFileSystem fileSystem)
{
    private const string HelpText =
        """
        arch-linter-net policy weakening — compare separately exported effective policy contexts

        Usage:
          arch-linter-net policy weakening --base-context <path> --current-context <path> [options]

        Produce each JSON context in its own repository/policy state with:
          arch-linter-net policy context --policy <path> --format json

        Without a public API approval, this command reads only the supplied artifacts. An approval
        additionally captures the current contract surface from --policy, without writing a file.

        Options:
          --base-context <path>     JSON policy context from the base state
          --current-context <path>  JSON policy context from the current state
          --public-api-approval <path> JSON approvals for exact reviewed API additions
          --policy <path>           Current policy used to capture approved live API evidence
          -f, --format <fmt>        Output format: human, json, or sarif (default: human)
          -h, --help                Show this help message

        Exit codes:
          0   Comparison completed with no error-severity weakening
          1   Comparison completed with error-severity weakening
          2   Invalid arguments, unreadable artifact, or invalid comparison input
        """;

    public int Execute(PolicyWeakeningCommandOptions options)
    {
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (options.Format is not ("human" or "json" or "sarif"))
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "invalid-format",
                $"Invalid format: {options.Format}. Use 'human', 'json', or 'sarif'.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (string.IsNullOrWhiteSpace(options.BaseContextPath) || string.IsNullOrWhiteSpace(options.CurrentContextPath))
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "missing-policy-context",
                "Both --base-context and --current-context are required.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            if (options.PublicApiApprovalPath is not null && !fileSystem.FileExists(options.PublicApiApprovalPath))
            {
                throw new ArgumentException($"Public API approval artifact does not exist: {options.PublicApiApprovalPath}");
            }

            ArchitecturePolicyContextExport baseContext = ArchitecturePolicyWeakeningFormatter.DeserializeContext(
                fileSystem.ReadAllText(options.BaseContextPath));
            ArchitecturePolicyContextExport currentContext = ArchitecturePolicyWeakeningFormatter.DeserializeContext(
                fileSystem.ReadAllText(options.CurrentContextPath));
            IReadOnlyList<ArchitecturePublicApiWeakeningApproval> approvals = options.PublicApiApprovalPath is null
                ? []
                : ArchitecturePolicyWeakeningFormatter.DeserializePublicApiApprovals(fileSystem.ReadAllText(options.PublicApiApprovalPath));
            ArchitecturePolicyWeakeningResult result = runtime.ComparePolicyWeakening(new ArchitecturePolicyWeakeningRequest(
                baseContext,
                currentContext)
            {
                PublicApiApprovals = approvals,
                PublicApiLiveEvidence = CaptureLiveEvidence(runtime.CapturePublicApi, options.PolicyPath!, currentContext, approvals),
            });
            console.Out.WriteLine(options.Format switch
            {
                "json" => runtime.FormatPolicyWeakeningAsJson(result),
                "sarif" => runtime.FormatPolicyWeakeningAsSarif(result),
                _ => runtime.FormatPolicyWeakeningAsHuman(result),
            });
            return result.HasErrors ? CliExitCodes.ValidationFailure : CliExitCodes.Success;
        }
        catch (Exception exception)
        {
            CliErrorOutputWriter.Write(
                console,
                options.Format,
                "policy-weakening-comparison-error",
                $"Policy weakening comparison error: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    internal static List<ArchitecturePublicApiLiveEvidence> CaptureLiveEvidence(
        Func<PublicApiCaptureRequest, PublicApiCaptureOutcome> capturePublicApi,
        string policyPath,
        ArchitecturePolicyContextExport currentContext,
        IReadOnlyList<ArchitecturePublicApiWeakeningApproval> approvals)
    {
        if (approvals.Count == 0)
        {
            return [];
        }

        string contextDigest = ArchitecturePolicyWeakeningFormatter.ComputeContextDigest(currentContext);
        List<ArchitecturePublicApiLiveEvidence> evidence = new();
        foreach (ArchitecturePublicApiWeakeningApproval approval in approvals)
        {
            PublicApiCaptureOutcome capture = capturePublicApi(new PublicApiCaptureRequest
            {
                PolicyPath = policyPath,
                ContractId = approval.ContractId,
                OutputPath = "architecture/public-api-approval-evidence.txt",
            });
            if (!capture.Succeeded || capture.Snapshot is null)
            {
                throw new InvalidOperationException(capture.Error ??
                    $"Unable to capture live public API evidence for contract '{approval.ContractId}'.");
            }

            PublicApiSnapshotDocument document = PublicApiSnapshotFormat.Parse(capture.Snapshot, "captured live public API");
            evidence.Add(new ArchitecturePublicApiLiveEvidence(
                ArchitecturePublicApiLiveEvidence.CurrentSchemaVersion,
                ArchitecturePublicApiLiveEvidence.EvidenceKind,
                contextDigest,
                approval.ContractId,
                document.Entries));
        }

        return evidence;
    }
}
