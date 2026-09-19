namespace ArchLinterNet.Cli.Commands.Health.Application;

internal sealed record HealthRevalidatePublicationCommandOptions(
    string InputPath,
    string EvaluationDate,
    string SourceHealthSha256,
    string BadgePayloadSha256,
    string MergedTreeSha,
    string ProducerIdentitySha256,
    string? OutputPath,
    bool ShowHelp);

