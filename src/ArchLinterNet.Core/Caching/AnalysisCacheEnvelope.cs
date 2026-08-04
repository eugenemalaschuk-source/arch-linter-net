namespace ArchLinterNet.Core.Caching;

// The versioned envelope identity for issue #365's persistent cache — see
// openspec/specs/analysis-cache/spec.md. Mirrors the AnalysisProfileId.V1 pattern in
// ArchLinterNet.Core.Profiling: one constant, never redefined in place.
public static class AnalysisCacheEnvelope
{
    public const string SchemaId = "analysis-cache/v1";

    // Format 2 adds immutable artifact byte manifests. Version-1 entries are intentionally not
    // reusable because they authorize only project inputs, not the PE/PDB/receipt bytes consumed
    // by the original run.
    public const int FormatVersion = 2;

    // The product/schema version segment used by the default `--cache auto` location
    // (`ArchLinterNet/0.5.1/analysis-cache/v1`), matching the packaged schema version already
    // used by ArchLinterNet.Core.Schema.PackagedSchemaRegistry.
    public const string ProductSchemaVersion = "0.5.1";

    public static string ToolVersion { get; } =
        typeof(AnalysisCacheEnvelope).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
