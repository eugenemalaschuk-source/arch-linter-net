namespace ArchLinterNet.Core.Scanning;

// Reflection-based enumeration of a type's base-type chain and implemented-interface set for the
// inheritance and interface_implementation contract families, mirroring the defensive-reflection
// posture of ArchitectureTypeRoleMatcher. Constructed generic base types/interfaces are matched by
// their generic type definition's CLR full name (e.g. "App.Repository`1"), so policies can name a
// generic type once without spelling out type arguments; prefix matching operates on the same
// normalized name.
internal static class ArchitectureTypeRelationshipScanner
{
    public static IEnumerable<string> GetForbiddenBaseTypeMatches(
        Type type, IReadOnlyList<string> forbiddenBaseTypes, IReadOnlyList<string> forbiddenBaseTypePrefixes)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (Type? current = SafeBaseType(type); current != null; current = SafeBaseType(current))
        {
            string baseTypeName = ComparableName(current);
            if (baseTypeName.Length == 0)
            {
                continue;
            }

            if (IsMatch(baseTypeName, forbiddenBaseTypes, forbiddenBaseTypePrefixes) && seen.Add(baseTypeName))
            {
                yield return baseTypeName;
            }
        }
    }

    public static IEnumerable<string> GetImplementedInterfaceMatches(
        Type type, IReadOnlyList<string> interfaces, IReadOnlyList<string> interfacePrefixes)
    {
        // An interface extending a selected interface is a contract extension, not an
        // implementation escaping the boundary, so interface types are never candidates.
        if (type.IsInterface)
        {
            yield break;
        }

        Type[] implementedInterfaces;
        try
        {
            implementedInterfaces = type.GetInterfaces();
        }
        catch (TypeLoadException)
        {
            yield break;
        }
        catch (FileNotFoundException)
        {
            yield break;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (Type implementedInterface in implementedInterfaces)
        {
            string interfaceName = ComparableName(implementedInterface);
            if (interfaceName.Length == 0)
            {
                continue;
            }

            if (IsMatch(interfaceName, interfaces, interfacePrefixes) && seen.Add(interfaceName))
            {
                yield return interfaceName;
            }
        }
    }

    private static string ComparableName(Type type)
    {
        Type target = type;
        try
        {
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                target = type.GetGenericTypeDefinition();
            }
        }
        catch (TypeLoadException)
        {
        }
        catch (FileNotFoundException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return ArchitectureTypeNames.SafeFullName(target);
    }

    private static bool IsMatch(string typeName, IReadOnlyList<string> exactNames, IReadOnlyList<string> prefixes)
    {
        foreach (string candidate in exactNames)
        {
            if (string.Equals(typeName, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (string prefix in prefixes)
        {
            if (typeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Type? SafeBaseType(Type type)
    {
        try
        {
            return type.BaseType;
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
