namespace ArchLinterNet.Core.Scanning;

internal readonly record struct ArchitectureTypeRelationshipMatch(string TypeName, string AssemblyName);

// Reflection-based enumeration of a type's base-type chain and implemented-interface set for the
// inheritance and interface_implementation contract families, mirroring the defensive-reflection
// posture of ArchitectureTypeRoleMatcher. Constructed generic base types/interfaces are matched by
// their generic type definition's CLR full name (e.g. "App.Repository`1"), so policies can name a
// generic type once without spelling out type arguments; prefix matching operates on the same
// normalized name.
internal static class ArchitectureTypeRelationshipScanner
{
    public static IEnumerable<ArchitectureTypeRelationshipMatch> GetForbiddenBaseTypeMatches(
        Type type, IReadOnlyList<string> forbiddenBaseTypes, IReadOnlyList<string> forbiddenBaseTypePrefixes)
    {
        HashSet<(string TypeName, string AssemblyName)> seen = new();

        for (Type? current = SafeBaseType(type); current != null; current = SafeBaseType(current))
        {
            Type comparableType = ComparableType(current);
            string baseTypeName = ArchitectureTypeNames.SafeFullName(comparableType);
            if (baseTypeName.Length == 0)
            {
                continue;
            }

            string baseTypeAssembly = ArchitectureTypeNames.SafeAssemblyName(comparableType) ?? string.Empty;
            if (IsMatch(baseTypeName, forbiddenBaseTypes, forbiddenBaseTypePrefixes)
                && seen.Add((baseTypeName, baseTypeAssembly)))
            {
                yield return new ArchitectureTypeRelationshipMatch(baseTypeName, baseTypeAssembly);
            }
        }
    }

    public static IEnumerable<ArchitectureTypeRelationshipMatch> GetImplementedInterfaceMatches(
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

        HashSet<(string TypeName, string AssemblyName)> seen = new();

        foreach (Type implementedInterface in implementedInterfaces)
        {
            Type comparableType = ComparableType(implementedInterface);
            string interfaceName = ArchitectureTypeNames.SafeFullName(comparableType);
            if (interfaceName.Length == 0)
            {
                continue;
            }

            string interfaceAssembly = ArchitectureTypeNames.SafeAssemblyName(comparableType) ?? string.Empty;
            if (IsMatch(interfaceName, interfaces, interfacePrefixes)
                && seen.Add((interfaceName, interfaceAssembly)))
            {
                yield return new ArchitectureTypeRelationshipMatch(interfaceName, interfaceAssembly);
            }
        }
    }

    private static Type ComparableType(Type type)
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
            // Swallow — defensive reflection may encounter unloadable types
        }
        catch (FileNotFoundException)
        {
            // Swallow — defensive reflection may encounter missing assemblies
        }
        catch (NotSupportedException)
        {
            // Swallow — defensive reflection may encounter unsupported type metadata
        }

        return target;
    }

    private static bool IsMatch(string typeName, IReadOnlyList<string> exactNames, IReadOnlyList<string> prefixes)
    {
        return exactNames.Any(candidate => string.Equals(typeName, candidate, StringComparison.Ordinal))
            || prefixes.Any(prefix => typeName.StartsWith(prefix, StringComparison.Ordinal));
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
