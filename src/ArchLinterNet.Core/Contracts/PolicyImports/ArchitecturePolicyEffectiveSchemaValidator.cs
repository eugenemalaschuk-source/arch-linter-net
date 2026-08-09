using System.Reflection;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal static class ArchitecturePolicyEffectiveSchemaValidator
{
    private const string SchemaResourceName = "ArchLinterNet.Core.Schema.dependencies.arch.schema.json";

    private static readonly Lazy<JsonNode> _schemaDocument = new(LoadSchemaDocument);
    private static readonly Lazy<JsonSchema> _schema = new(() => JsonSchema.FromText(_schemaDocument.Value.ToJsonString()));

    public static void Validate(string yaml, ArchitecturePolicyProvenanceIndex provenance)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        JsonNode? instance = ConvertNode(stream.Documents[0].RootNode);
        RemoveValidatedContractIds(instance, provenance);
        EvaluationResults results = _schema.Value.Evaluate(
            instance,
            new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid)
        {
            return;
        }

        IReadOnlyList<SchemaFailure> failures = SelectActionableFailures(results, instance);
        string details = DescribeInvalidDiscoveryCoverageRoots(instance) ?? string.Join(
            "; ",
            failures.Take(12).Select(failure => $"{failure.InstanceLocation}: {failure.Message}"));
        ArchitecturePolicySourceLocation? location = failures
            .OrderByDescending(failure => InstanceDepth(failure.InstanceLocation))
            .ThenBy(failure => failure.Order)
            .Select(failure => FindLocation(provenance, failure.InstanceLocation))
            .FirstOrDefault(candidate => candidate is not null);
        throw ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.SourceShape,
            $"Composed policy does not satisfy the effective policy schema: {details}",
            location);
    }

    private static IReadOnlyList<SchemaFailure> SelectActionableFailures(
        EvaluationResults results,
        JsonNode? instance)
    {
        var failures = new List<SchemaFailure>();
        CollectInvalidLeafFailures(results, failures);

        IReadOnlyList<SchemaFailure> applicableFailures = SuppressInapplicableAlternatives(failures, instance);
        return applicableFailures
            .Where(failure => !applicableFailures.Any(other =>
                other.Order != failure.Order
                && IsDescendantOf(other.InstanceLocation, failure.InstanceLocation)))
            .ToArray();
    }

    private static void CollectInvalidLeafFailures(EvaluationResults result, List<SchemaFailure> failures)
    {
        if (result.IsValid)
        {
            return;
        }

        EvaluationResults[] invalidDetails = result.Details.Where(detail => !detail.IsValid).ToArray();
        if (invalidDetails.Length > 0)
        {
            foreach (EvaluationResults detail in invalidDetails)
            {
                CollectInvalidLeafFailures(detail, failures);
            }

            return;
        }

        foreach ((string _, string message) in (result.Errors?.OrderBy(error => error.Key, StringComparer.Ordinal)
                     ?? Enumerable.Empty<KeyValuePair<string, string>>()))
        {
            failures.Add(new SchemaFailure(
                result.InstanceLocation.ToString(),
                result.EvaluationPath.ToString(),
                message,
                failures.Count));
        }
    }

    private static IReadOnlyList<SchemaFailure> SuppressInapplicableAlternatives(
        IReadOnlyList<SchemaFailure> failures,
        JsonNode? instance)
    {
        var alternatives = new List<SchemaAlternative>();
        foreach (SchemaFailure failure in failures)
        {
            alternatives.AddRange(FindAlternatives(failure, instance));
        }

        HashSet<(string CompositePath, string BranchPath)> applicable = alternatives
            .Where(alternative => alternative.IsApplicable)
            .Select(alternative => (alternative.CompositePath, alternative.BranchPath))
            .ToHashSet();
        HashSet<string> compositesWithApplicableBranch = applicable
            .Select(alternative => alternative.CompositePath)
            .ToHashSet(StringComparer.Ordinal);

        return failures.Where(failure => !alternatives.Any(alternative =>
            alternative.FailureOrder == failure.Order
            && !alternative.IsApplicable
            && compositesWithApplicableBranch.Contains(alternative.CompositePath))).ToArray();
    }

    private static IEnumerable<SchemaAlternative> FindAlternatives(SchemaFailure failure, JsonNode? instance)
    {
        string[] segments = SplitPointer(failure.EvaluationPath);
        for (int index = 0; index < segments.Length - 1; index++)
        {
            if ((segments[index] != "anyOf" && segments[index] != "oneOf")
                || !int.TryParse(segments[index + 1], out _))
            {
                continue;
            }

            string compositePath = ToPointer(segments.Take(index + 1));
            string branchPath = ToPointer(segments.Take(index + 2));
            JsonNode? branch = FindNode(_schemaDocument.Value, branchPath);
            JsonNode? branchInstance = FindNearestObject(instance, failure.InstanceLocation);
            yield return new SchemaAlternative(
                failure.Order,
                compositePath,
                branchPath,
                IsApplicableBranch(branch, branchInstance));
        }
    }

    private static bool IsApplicableBranch(JsonNode? schema, JsonNode? instance)
    {
        if (schema is not JsonObject schemaObject)
        {
            return true;
        }

        if (schemaObject["required"] is JsonArray required)
        {
            if (instance is not JsonObject instanceObject
                || required.Any(property => property?.GetValue<string>() is string name && !instanceObject.ContainsKey(name)))
            {
                return false;
            }
        }

        if (schemaObject["properties"] is JsonObject properties && instance is JsonObject objectInstance)
        {
            foreach ((string propertyName, JsonNode? propertySchema) in properties)
            {
                if (propertySchema is JsonObject propertySchemaObject
                    && propertySchemaObject["const"] is JsonNode constant
                    && objectInstance[propertyName] is JsonNode value
                    && !JsonNode.DeepEquals(value, constant))
                {
                    return false;
                }
            }
        }

        return schemaObject["allOf"] is not JsonArray allOf
            || allOf.All(child => IsApplicableBranch(child, instance));
    }

    private static JsonNode? FindNearestObject(JsonNode? instance, string instanceLocation)
    {
        string[] segments = SplitPointer(instanceLocation);
        for (int length = segments.Length; length >= 0; length--)
        {
            JsonNode? candidate = FindNode(instance, ToPointer(segments.Take(length)));
            if (candidate is JsonObject)
            {
                return candidate;
            }
        }

        return instance;
    }

    private static JsonNode? FindNode(JsonNode? root, string pointer)
    {
        JsonNode? current = root;
        foreach (string segment in SplitPointer(pointer))
        {
            current = current switch
            {
                JsonObject mapping when mapping.TryGetPropertyValue(segment, out JsonNode? child) => child,
                JsonArray sequence when int.TryParse(segment, out int index)
                                    && index >= 0
                                    && index < sequence.Count => sequence[index],
                _ => null
            };
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static bool IsDescendantOf(string candidate, string ancestor) =>
        candidate.Length > ancestor.Length
        && candidate.StartsWith(ancestor, StringComparison.Ordinal)
        && candidate[ancestor.Length] == '/';

    private static string[] SplitPointer(string pointer) =>
        pointer.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal))
            .ToArray();

    private static string ToPointer(IEnumerable<string> segments) =>
        "/" + string.Join('/', segments.Select(segment => segment.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal)));

    private static string? DescribeInvalidDiscoveryCoverageRoots(JsonNode? instance)
    {
        if (instance is not JsonObject root
            || root["contracts"] is not JsonObject contracts)
        {
            return null;
        }

        foreach (string groupName in new[] { "strict_coverage", "audit_coverage" })
        {
            if (contracts[groupName] is not JsonArray contractsInGroup)
            {
                continue;
            }

            for (int index = 0; index < contractsInGroup.Count; index++)
            {
                if (contractsInGroup[index] is not JsonObject contract
                    || !contract.ContainsKey("roots")
                    || contract["scope"] is not JsonValue scopeValue
                    || !scopeValue.TryGetValue(out string? scope)
                    || (scope != "project" && scope != "assembly"))
                {
                    continue;
                }

                return $"/contracts/{groupName}/{index}/roots: 'roots' is not valid for {scope} coverage; " +
                    "that scope classifies all discovered units.";
            }
        }

        return null;
    }

    private static JsonNode LoadSchemaDocument()
    {
        Assembly assembly = typeof(ArchitecturePolicyEffectiveSchemaValidator).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException($"Embedded policy schema '{SchemaResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd())
            ?? throw new InvalidOperationException($"Embedded policy schema '{SchemaResourceName}' was empty.");
    }

    private static void RemoveValidatedContractIds(
        JsonNode? instance,
        ArchitecturePolicyProvenanceIndex provenance)
    {
        if (instance is not JsonObject root
            || root["contracts"] is not JsonObject contracts)
        {
            return;
        }

        foreach ((string groupName, JsonNode? group) in contracts)
        {
            if (group is not JsonArray entries)
            {
                continue;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] is not JsonObject contract)
                {
                    continue;
                }

                if (!contract.TryGetPropertyValue("id", out JsonNode? idNode))
                {
                    continue;
                }

                if (idNode is not JsonValue idValue
                    || !idValue.TryGetValue(out string? id)
                    || string.IsNullOrEmpty(id))
                {
                    provenance.TryGetLocation(
                        ArchitecturePolicyProvenancePath.AppendProperty(
                            ArchitecturePolicyProvenancePath.AppendIndex(
                                ArchitecturePolicyProvenancePath.AppendProperty(
                                    ArchitecturePolicyProvenancePath.Property("contracts"), groupName),
                                index),
                            "id"),
                        out ArchitecturePolicySourceLocation? location);
                    throw ArchitecturePolicyDiagnosticFactory.Exception(
                        ArchitecturePolicyImportErrorCategory.SourceShape,
                        "A composed contract id must be a non-empty string when declared.",
                        location);
                }

                // Validate the shared id field here (non-empty string, with a precise composed-
                // policy provenance location) rather than relying solely on the family schema's
                // own error reporting, then evaluate the remaining contract against the family
                // schema. The effective YAML itself is unchanged.
                contract.Remove("id");
            }
        }
    }

    private static ArchitecturePolicySourceLocation? FindLocation(
        ArchitecturePolicyProvenanceIndex provenance,
        string instanceLocation)
    {
        string path = ArchitecturePolicyProvenancePath.Normalize(instanceLocation);
        while (true)
        {
            if (provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location))
            {
                return location;
            }

            if (path == ArchitecturePolicyProvenancePath.Root)
            {
                return null;
            }

            path = ArchitecturePolicyProvenancePath.Parent(path);
        }
    }

    private static int InstanceDepth(string instanceLocation)
    {
        return instanceLocation.Count(character => character == '/');
    }

    private sealed record SchemaFailure(string InstanceLocation, string EvaluationPath, string Message, int Order);

    private sealed record SchemaAlternative(int FailureOrder, string CompositePath, string BranchPath, bool IsApplicable);

    private static JsonNode? ConvertNode(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => ConvertScalar(scalar),
            YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ConvertNode).ToArray()),
            YamlMappingNode mapping => new JsonObject(mapping.Children.Select(pair =>
                new KeyValuePair<string, JsonNode?>(
                    ((YamlScalarNode)pair.Key).Value ?? string.Empty,
                    ConvertNode(pair.Value)))),
            _ => throw new NotSupportedException($"Unsupported YAML node type: {node.GetType()}")
        };
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        string? value = scalar.Value;
        if (value is null)
        {
            return value;
        }

        bool explicitlyTyped = !scalar.Tag.IsEmpty
            && !scalar.Tag.IsNonSpecific
            && scalar.Tag.Value.StartsWith("tag:yaml.org,2002:", StringComparison.Ordinal);
        if (scalar.Style != ScalarStyle.Plain && !explicitlyTyped)
        {
            return value;
        }

        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || value == "~")
        {
            return null;
        }

        if (bool.TryParse(value, out bool boolean))
        {
            return boolean;
        }

        string normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
        if (TryParseInteger(normalized, out long integer))
        {
            return integer is >= int.MinValue and <= int.MaxValue ? (int)integer : integer;
        }

        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double number))
        {
            return number;
        }

        return value;
    }

    private static bool TryParseInteger(string value, out long result)
    {
        const System.Globalization.NumberStyles Decimal = System.Globalization.NumberStyles.Integer;
        if (long.TryParse(value, Decimal, System.Globalization.CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        bool negative = value.StartsWith("-", StringComparison.Ordinal);
        string unsignedValue = negative ? value[1..] : value;
        int radix = unsignedValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 16
            : unsignedValue.StartsWith("0o", StringComparison.OrdinalIgnoreCase) ? 8
            : unsignedValue.StartsWith("0b", StringComparison.OrdinalIgnoreCase) ? 2
            : 0;
        if (radix == 0)
        {
            result = default;
            return false;
        }

        string digits = unsignedValue[2..];
        if (digits.Length == 0)
        {
            result = default;
            return false;
        }

        try
        {
            result = Convert.ToInt64(digits, radix);
            if (negative)
            {
                result = -result;
            }

            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }
}
