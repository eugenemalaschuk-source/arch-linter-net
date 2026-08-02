using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Profiling;

public static class AnalysisProfileJsonWriter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Write(AnalysisProfile profile)
    {
        return JsonSerializer.Serialize(profile, _options);
    }
}
