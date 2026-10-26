using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace ArchLinterNet.Core.Tests;

internal static class JsonSchemaJsonNodeEvaluationExtensions
{
    internal static EvaluationResults Evaluate(
        this JsonSchema schema,
        JsonNode? instance,
        EvaluationOptions? options = null)
    {
        JsonElement element = JsonSerializer.SerializeToElement(instance);
        return schema.Evaluate(element, options);
    }
}
