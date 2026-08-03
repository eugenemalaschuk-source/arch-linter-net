using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Caching;

internal static class AnalysisCacheJson
{
    // Concrete DTO types plus one explicit closed-set converter for
    // IArchitectureDiagnosticPayload's 18 concrete record types (AnalysisCacheDiagnosticPayloadConverter)
    // — never a polymorphic $type/TypeNameHandling-style converter, so deserializing a cache entry
    // can never construct or execute an arbitrary CLR type from untrusted on-disk bytes.
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(), new AnalysisCacheDiagnosticPayloadConverter() },
    };
}
