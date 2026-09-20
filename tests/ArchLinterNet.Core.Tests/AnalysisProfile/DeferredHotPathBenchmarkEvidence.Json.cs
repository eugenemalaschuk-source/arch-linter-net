using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Tests;

internal static class DeferredHotPathBenchmarkEvidenceJson
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    public static string Serialize(DeferredHotPathEvidenceDocument document) =>
        JsonSerializer.Serialize(document, _options);

    public static DeferredHotPathEvidenceDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<DeferredHotPathEvidenceDocument>(json, _options)
        ?? throw new InvalidOperationException("Deferred hot-path evidence deserialized to null.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}
