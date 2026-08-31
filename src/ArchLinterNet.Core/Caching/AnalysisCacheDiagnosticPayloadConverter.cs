using System.Text.Json;
using System.Text.Json.Serialization;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Caching;

// Explicit closed-set converter for IArchitectureDiagnosticPayload's 20 concrete record types
// (FrameworkReferenceAllowOnlyPayload, PackageDependencyPayload, FrameworkReferencePayload,
// ConfigurationPayload, ProjectMetadataPayload, CompositionPayload, TypePlacementPayload,
// ExternalDependencyPayload, DependencyPayload, PortBoundaryPayload, AttributeUsagePayload,
// InheritancePayload, InterfaceImplementationPayload, LayoutConventionPayload,
// ContextAllowOnlyPayload, ContextDependencyPayload, PackageAllowOnlyPayload,
// PublicApiSurfacePayload, ContractSurfaceExposurePayload, MetricBudgetPayload — see src/ArchLinterNet.Core/Model/*Payload.cs).
//
// This is the exact closed-set discrimination review finding #1 asked for: the "$kind"
// discriminator written on serialization is matched against a fixed switch statement enumerating
// every known concrete type on read. An unrecognized "$kind" value is rejected as a JsonException
// (surfaced by AnalysisCacheStore.TryGet as AnalysisCacheRejectReason.Corrupt) — this converter
// never resolves a type by name from untrusted bytes (no Type.GetType, no assembly-qualified-name
// lookup, no TypeNameHandling-style behavior), so a corrupted or foreign cache entry can never
// construct or execute an arbitrary CLR type.
internal sealed class AnalysisCacheDiagnosticPayloadConverter : JsonConverter<IArchitectureDiagnosticPayload>
{
    private const string KindProperty = "$kind";
    private const string ValueProperty = "value";

    public override IArchitectureDiagnosticPayload? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty(KindProperty, out JsonElement kindElement) || kindElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Cached diagnostic payload is missing a recognized '$kind' discriminator.");
        }

        if (!root.TryGetProperty(ValueProperty, out JsonElement valueElement))
        {
            throw new JsonException("Cached diagnostic payload is missing its 'value' object.");
        }

        string? kind = kindElement.GetString();
        string raw = valueElement.GetRawText();

        return kind switch
        {
            nameof(FrameworkReferenceAllowOnlyPayload) => Deserialize<FrameworkReferenceAllowOnlyPayload>(raw, options),
            nameof(PackageDependencyPayload) => Deserialize<PackageDependencyPayload>(raw, options),
            nameof(FrameworkReferencePayload) => Deserialize<FrameworkReferencePayload>(raw, options),
            nameof(ConfigurationPayload) => Deserialize<ConfigurationPayload>(raw, options),
            nameof(ProjectMetadataPayload) => Deserialize<ProjectMetadataPayload>(raw, options),
            nameof(CompositionPayload) => Deserialize<CompositionPayload>(raw, options),
            nameof(TypePlacementPayload) => Deserialize<TypePlacementPayload>(raw, options),
            nameof(ExternalDependencyPayload) => Deserialize<ExternalDependencyPayload>(raw, options),
            nameof(DependencyPayload) => Deserialize<DependencyPayload>(raw, options),
            nameof(PortBoundaryPayload) => Deserialize<PortBoundaryPayload>(raw, options),
            nameof(AttributeUsagePayload) => Deserialize<AttributeUsagePayload>(raw, options),
            nameof(InheritancePayload) => Deserialize<InheritancePayload>(raw, options),
            nameof(InterfaceImplementationPayload) => Deserialize<InterfaceImplementationPayload>(raw, options),
            nameof(LayoutConventionPayload) => Deserialize<LayoutConventionPayload>(raw, options),
            nameof(ContextAllowOnlyPayload) => Deserialize<ContextAllowOnlyPayload>(raw, options),
            nameof(ContextDependencyPayload) => Deserialize<ContextDependencyPayload>(raw, options),
            nameof(PackageAllowOnlyPayload) => Deserialize<PackageAllowOnlyPayload>(raw, options),
            nameof(PublicApiSurfacePayload) => Deserialize<PublicApiSurfacePayload>(raw, options),
            nameof(ContractSurfaceExposurePayload) => Deserialize<ContractSurfaceExposurePayload>(raw, options),
            nameof(MetricBudgetPayload) => Deserialize<MetricBudgetPayload>(raw, options),
            _ => throw new JsonException(
                $"Cached diagnostic payload has an unrecognized '$kind' value '{kind}'. Never deserialized " +
                "as an arbitrary type — this is a closed set of 20 known payload records."),
        };
    }

    private static T Deserialize<T>(string raw, JsonSerializerOptions options)
        where T : IArchitectureDiagnosticPayload =>
        JsonSerializer.Deserialize<T>(raw, options)
        ?? throw new JsonException($"Cached diagnostic payload of kind '{typeof(T).Name}' deserialized to null.");

    public override void Write(Utf8JsonWriter writer, IArchitectureDiagnosticPayload value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString(KindProperty, value.GetType().Name);
        writer.WritePropertyName(ValueProperty);
        // value.GetType() is always one of the 20 concrete sealed record types above — never the
        // IArchitectureDiagnosticPayload interface itself — so this call resolves the reflection-based
        // default converter for that concrete record, not this converter again (JsonConverter<T>.CanConvert
        // only matches the exact declared T == IArchitectureDiagnosticPayload).
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
        writer.WriteEndObject();
    }
}
