using System.Globalization;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed class HealthRevalidatePublicationCommandHandler(
    ICliConsole console,
    IFileSystem fileSystem)
{
    private const string HelpText =
        """
        arch-linter-net health revalidate-publication — refresh temporal publication evidence

        Usage:
          arch-linter-net health revalidate-publication --input <architecture-health.json>
            --evaluation-date <yyyy-MM-dd> --source-health-sha256 <sha256>
            --badge-payload-sha256 <sha256> --merged-tree-sha <sha1>
            --producer-identity-sha256 <sha256> [--output <receipt.json>]

        This read-only command consumes only serialized Health report evidence. It does not load
        policy, build assemblies, evaluate findings, recompute Health, or modify the input file.

        Exit codes:
          0   Temporal receipt is ready
          2   Receipt is unassessable or command input/output is invalid
        """;

    public int Execute(HealthRevalidatePublicationCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            console.Out.WriteLine(HelpText);
            return CliExitCodes.Success;
        }

        if (!TryParseEvaluationDate(options.EvaluationDate, out DateOnly evaluationDate))
        {
            console.Error.WriteLine("--evaluation-date must be an explicit UTC date in yyyy-MM-dd format.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (string.IsNullOrWhiteSpace(options.InputPath)
            || string.IsNullOrWhiteSpace(options.SourceHealthSha256)
            || string.IsNullOrWhiteSpace(options.BadgePayloadSha256)
            || string.IsNullOrWhiteSpace(options.MergedTreeSha)
            || string.IsNullOrWhiteSpace(options.ProducerIdentitySha256))
        {
            console.Error.WriteLine("Revalidation requires an input, evaluation date, and all four publication identity bindings.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        if (options.OutputPath is not null
            && fileSystem.FileExists(options.InputPath)
            && fileSystem.FileExists(options.OutputPath)
            && fileSystem.AreSameExistingFile(options.InputPath, options.OutputPath))
        {
            console.Error.WriteLine("--output must not replace the Health input artifact.");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }

        try
        {
            byte[] healthBytes = fileSystem.ReadAllBytes(options.InputPath);
            var binding = new ArchitectureHealthTemporalPublicationBinding(
                options.SourceHealthSha256,
                options.BadgePayloadSha256,
                options.MergedTreeSha,
                options.ProducerIdentitySha256);
            ArchitectureHealthTemporalPublicationReceipt receipt =
                ArchitectureHealthTemporalPublicationRevalidator.Revalidate(healthBytes, evaluationDate, binding);
            string json = ArchitectureHealthTemporalPublicationReceiptWriter.Format(receipt);

            if (options.OutputPath is null)
            {
                console.Out.WriteLine(json);
            }
            else
            {
                string temporaryPath = fileSystem.WriteAllTextToTemp(options.OutputPath, json + Environment.NewLine);
                fileSystem.RenameTempToTarget(temporaryPath, options.OutputPath);
            }

            return receipt.IsReady
                ? CliExitCodes.Success
                : CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or JsonException)
        {
            console.Error.WriteLine($"Could not revalidate Architecture Health publication evidence: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }

    private static bool TryParseEvaluationDate(string value, out DateOnly evaluationDate)
    {
        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out evaluationDate)
            && evaluationDate != DateOnly.MinValue;
    }
}

