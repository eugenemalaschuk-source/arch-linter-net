using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Caching;

internal static class AnalysisCacheJson
{
    // Concrete DTO types only (see AnalysisCacheEntryV1) — never a polymorphic or
    // TypeNameHandling-style converter, so deserializing a cache entry can never construct or
    // execute an arbitrary CLR type from untrusted on-disk bytes.
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
