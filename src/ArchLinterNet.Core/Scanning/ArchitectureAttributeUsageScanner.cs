using System.Linq;
using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

internal readonly record struct ArchitectureAttributeUsageMatch(string SourceIdentifier, string MatchedAttribute);

// Reflection-based enumeration of attribute usage on a type and its declared members, mirroring the
// defensive-reflection posture of ArchitecturePublicApiSurfaceScanner/ArchitectureTypeRoleMatcher.
// Unlike the public-API-surface scanner, this does NOT filter by visibility: markers such as Unity's
// [SerializeField] commonly decorate private fields, and [Authorize]/[Route] can be internal, so every
// declared member is in scope regardless of its access modifier.
internal static class ArchitectureAttributeUsageScanner
{
    private const BindingFlags MemberFlags =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static IEnumerable<ArchitectureAttributeUsageMatch> GetMatches(
        Type type, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        string typeName = ArchitectureTypeNames.SafeFullName(type);

        foreach (ArchitectureAttributeUsageMatch match in TypeMatches(type, typeName, attributes, attributePrefixes))
        {
            yield return match;
        }

        foreach (ArchitectureAttributeUsageMatch match in ConstructorMatches(type, typeName, attributes, attributePrefixes))
        {
            yield return match;
        }

        foreach (ArchitectureAttributeUsageMatch match in MethodMatches(type, typeName, attributes, attributePrefixes))
        {
            yield return match;
        }

        foreach (ArchitectureAttributeUsageMatch match in PropertyMatches(type, typeName, attributes, attributePrefixes))
        {
            yield return match;
        }

        foreach (ArchitectureAttributeUsageMatch match in FieldMatches(type, typeName, attributes, attributePrefixes))
        {
            yield return match;
        }

        foreach (ArchitectureAttributeUsageMatch match in EventMatches(type, typeName, attributes, attributePrefixes))
        {
            yield return match;
        }
    }

    private static IEnumerable<ArchitectureAttributeUsageMatch> TypeMatches(
        Type type, string typeName, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        foreach (string matchedAttribute in MatchedAttributes(type, attributes, attributePrefixes))
        {
            yield return new ArchitectureAttributeUsageMatch(typeName, matchedAttribute);
        }
    }

    private static IEnumerable<ArchitectureAttributeUsageMatch> ConstructorMatches(
        Type type, string typeName, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        foreach (ConstructorInfo ctor in SafeGetMembers(type, t => t.GetConstructors(MemberFlags)))
        {
            foreach (string matchedAttribute in MatchedAttributes(ctor, attributes, attributePrefixes))
            {
                yield return new ArchitectureAttributeUsageMatch(
                    $"{typeName}.{ctor.Name}({ParameterSignature(ctor)})", matchedAttribute);
            }
        }
    }

    private static IEnumerable<ArchitectureAttributeUsageMatch> MethodMatches(
        Type type, string typeName, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        foreach (MethodInfo method in SafeGetMembers(type, t => t.GetMethods(MemberFlags)))
        {
            if (method.IsSpecialName && IsAccessorMethodName(method.Name))
            {
                continue;
            }

            foreach (string matchedAttribute in MatchedAttributes(method, attributes, attributePrefixes))
            {
                yield return new ArchitectureAttributeUsageMatch(
                    $"{typeName}.{method.Name}({ParameterSignature(method)})", matchedAttribute);
            }
        }
    }

    private static IEnumerable<ArchitectureAttributeUsageMatch> PropertyMatches(
        Type type, string typeName, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        foreach (PropertyInfo property in SafeGetMembers(type, t => t.GetProperties(MemberFlags)))
        {
            foreach (string matchedAttribute in MatchedAttributes(property, attributes, attributePrefixes))
            {
                yield return new ArchitectureAttributeUsageMatch($"{typeName}.{property.Name}", matchedAttribute);
            }
        }
    }

    private static IEnumerable<ArchitectureAttributeUsageMatch> FieldMatches(
        Type type, string typeName, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        foreach (FieldInfo field in SafeGetMembers(type, t => t.GetFields(MemberFlags)))
        {
            foreach (string matchedAttribute in MatchedAttributes(field, attributes, attributePrefixes))
            {
                yield return new ArchitectureAttributeUsageMatch($"{typeName}.{field.Name}", matchedAttribute);
            }
        }
    }

    private static IEnumerable<ArchitectureAttributeUsageMatch> EventMatches(
        Type type, string typeName, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        foreach (EventInfo evt in SafeGetMembers(type, t => t.GetEvents(MemberFlags)))
        {
            foreach (string matchedAttribute in MatchedAttributes(evt, attributes, attributePrefixes))
            {
                yield return new ArchitectureAttributeUsageMatch($"{typeName}.{evt.Name}", matchedAttribute);
            }
        }
    }

    // Included so overloaded constructors/methods produce distinct source identifiers: without a
    // parameter signature, two overloads decorated with different attributes would collapse onto the
    // same "Type.Method" identifier, making diagnostics and ignored_violations entries ambiguous.
    private static string ParameterSignature(MethodBase method)
    {
        try
        {
            return string.Join(", ", method.GetParameters().Select(p => ArchitectureTypeNames.SafeFullName(p.ParameterType)));
        }
        catch (TypeLoadException)
        {
            return string.Empty;
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> MatchedAttributes(
        MemberInfo member, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        IList<CustomAttributeData> attributeData;
        try
        {
            attributeData = member.GetCustomAttributesData();
        }
        catch (TypeLoadException)
        {
            yield break;
        }
        catch (FileNotFoundException)
        {
            yield break;
        }
        catch (CustomAttributeFormatException)
        {
            yield break;
        }

        foreach (CustomAttributeData data in attributeData)
        {
            string? attributeName = TrySafeAttributeTypeName(data);
            if (attributeName == null)
            {
                continue;
            }

            if (IsMatch(attributeName, attributes, attributePrefixes))
            {
                yield return attributeName;
            }
        }
    }

    private static string? TrySafeAttributeTypeName(CustomAttributeData data)
    {
        try
        {
            return ArchitectureTypeNames.SafeFullName(data.AttributeType);
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (CustomAttributeFormatException)
        {
            return null;
        }
    }

    private static bool IsMatch(string attributeName, IReadOnlyList<string> attributes, IReadOnlyList<string> attributePrefixes)
    {
        foreach (string candidate in attributes)
        {
            if (string.Equals(attributeName, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (string prefix in attributePrefixes)
        {
            if (attributeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<TMember> SafeGetMembers<TMember>(Type type, Func<Type, TMember[]> selector)
    {
        try
        {
            return selector(type);
        }
        catch (TypeLoadException)
        {
            return Array.Empty<TMember>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<TMember>();
        }
    }

    private static bool IsAccessorMethodName(string name)
    {
        return name.StartsWith("get_", StringComparison.Ordinal)
            || name.StartsWith("set_", StringComparison.Ordinal)
            || name.StartsWith("add_", StringComparison.Ordinal)
            || name.StartsWith("remove_", StringComparison.Ordinal);
    }
}
