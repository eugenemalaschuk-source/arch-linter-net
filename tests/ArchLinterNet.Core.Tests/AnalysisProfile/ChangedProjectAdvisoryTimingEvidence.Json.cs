using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLinterNet.Core.Tests;

internal static class ChangedProjectAdvisoryTimingEvidenceJson
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    public static string Serialize(ChangedProjectAdvisoryTimingEvidenceDocument document)
    {
        document.Validate();
        return JsonSerializer.Serialize(document, _options);
    }

    public static ChangedProjectAdvisoryTimingEvidenceDocument Deserialize(string json)
    {
        ChangedProjectAdvisoryTimingEvidenceDocument document = JsonSerializer.Deserialize<ChangedProjectAdvisoryTimingEvidenceDocument>(json, _options)
            ?? throw new InvalidOperationException("Changed-project timing evidence deserialized to null.");
        document.Validate();
        return document;
    }

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
