using Json.Schema;

namespace ArchLinterNet.Core.Schema;

internal static class JsonSchemaTextFactory
{
    internal static JsonSchema FromText(string jsonText) => JsonSchema.FromText(
        jsonText,
        new BuildOptions { SchemaRegistry = new SchemaRegistry() });
}
