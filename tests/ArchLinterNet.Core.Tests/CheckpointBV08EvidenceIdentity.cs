namespace ArchLinterNet.Core.Tests;

// Synthetic, deliberately-fictitious evidence-context identity for the v0.8 full-cycle
// external-SARIF binding proof; never a real repository.
internal static class CheckpointBV08EvidenceIdentity
{
    internal const string Repository = "https://example.test/synthetic/v08-full-cycle";
    internal const string Scope = "checkpoint-b-v08";

    // SarifEvidenceReader.FileSystem.cs deliberately treats a rooted path (or one containing ':')
    // as unsafe and refuses to read it -- evidence paths are always resolved relative to the
    // analyzed repository root. The --external-evidence "path=" binding must therefore use this
    // repository-relative form, never the absolute validSarifPath used for File I/O.
    internal const string RelativePath = "evidence/v08-static-analysis.sarif";
}
