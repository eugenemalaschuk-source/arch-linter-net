using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureReferenceScanner
{
    public static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        foreach (Type expanded in InterfaceReferencedTypes(type))
        {
            yield return expanded;
        }

        foreach (Type expanded in BaseTypeReferencedTypes(type))
        {
            yield return expanded;
        }

        foreach (Type expanded in FieldReferencedTypes(type))
        {
            yield return expanded;
        }

        foreach (Type expanded in PropertyReferencedTypes(type))
        {
            yield return expanded;
        }

        foreach (Type expanded in MethodReferencedTypes(type))
        {
            yield return expanded;
        }

        foreach (Type expanded in ConstructorReferencedTypes(type))
        {
            yield return expanded;
        }
    }

    private static IEnumerable<Type> InterfaceReferencedTypes(Type type)
    {
        foreach (Type interfaceType in SafeGetInterfaces(type))
        {
            foreach (Type expanded in ExpandType(interfaceType))
            {
                yield return expanded;
            }
        }
    }

    private static IEnumerable<Type> BaseTypeReferencedTypes(Type type)
    {
        foreach (Type baseType in EnumerateBaseTypes(type))
        {
            foreach (Type expanded in ExpandType(baseType))
            {
                yield return expanded;
            }
        }
    }

    private static IEnumerable<Type> FieldReferencedTypes(Type type)
    {
        foreach (FieldInfo field in SafeGetFields(type))
        {
            foreach (Type expanded in ExpandIfNotNull(SafeGetFieldType(field)))
            {
                yield return expanded;
            }
        }
    }

    private static IEnumerable<Type> PropertyReferencedTypes(Type type)
    {
        foreach (PropertyInfo property in SafeGetProperties(type))
        {
            foreach (Type expanded in ExpandIfNotNull(SafeGetPropertyType(property)))
            {
                yield return expanded;
            }
        }
    }

    private static IEnumerable<Type> MethodReferencedTypes(Type type)
    {
        foreach (MethodInfo method in SafeGetMethods(type))
        {
            foreach (Type expanded in ExpandIfNotNull(SafeGetReturnType(method)))
            {
                yield return expanded;
            }

            foreach (Type expanded in ParameterReferencedTypes(method))
            {
                yield return expanded;
            }
        }
    }

    private static IEnumerable<Type> ConstructorReferencedTypes(Type type)
    {
        foreach (ConstructorInfo constructor in SafeGetConstructors(type))
        {
            foreach (Type expanded in ParameterReferencedTypes(constructor))
            {
                yield return expanded;
            }
        }
    }

    private static IEnumerable<Type> ParameterReferencedTypes(MethodBase method)
    {
        foreach (ParameterInfo parameter in SafeGetParameters(method))
        {
            foreach (Type expanded in ExpandIfNotNull(SafeGetParameterType(parameter)))
            {
                yield return expanded;
            }
        }
    }

    private static IEnumerable<Type> ExpandIfNotNull(Type? type)
    {
        if (type == null)
        {
            yield break;
        }

        foreach (Type expanded in ExpandType(type))
        {
            yield return expanded;
        }
    }

    public static IEnumerable<(Type referenced, List<Type> path)> GetTransitiveReferencedTypes(
        Type type,
        Func<Type, bool>? traversePredicate = null)
    {
        HashSet<Type> visited = new();
        Queue<(Type current, List<Type> path)> queue = new();

        List<Type> initialPath = new() { type };
        queue.Enqueue((type, initialPath));
        visited.Add(type);

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            foreach (Type directRef in GetReferencedTypes(current))
            {
                if (visited.Contains(directRef))
                {
                    continue;
                }

                visited.Add(directRef);
                List<Type> refPath = new(path) { directRef };
                yield return (directRef, refPath);

                if (traversePredicate == null || traversePredicate(directRef))
                {
                    queue.Enqueue((directRef, refPath));
                }
            }
        }
    }

    private static IEnumerable<Type> EnumerateBaseTypes(Type type)
    {
        foreach (Type interfaceType in SafeGetInterfaces(type))
        {
            yield return interfaceType;
        }

        Type? baseType = SafeBaseType(type);
        while (baseType != null)
        {
            yield return baseType;
            baseType = SafeBaseType(baseType);
        }
    }

    private static Type? SafeBaseType(Type type)
    {
        try
        {
            return type.BaseType;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    private static Type[] SafeGetInterfaces(Type type)
    {
        try
        {
            return type.GetInterfaces();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<Type>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<Type>();
        }
    }

    private static FieldInfo[] SafeGetFields(Type type)
    {
        try
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly); // NOSONAR: intentional — deep type graph traversal needs full member visibility
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<FieldInfo>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<FieldInfo>();
        }
    }

    private static PropertyInfo[] SafeGetProperties(Type type)
    {
        try
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly); // NOSONAR: intentional — deep type graph traversal needs full member visibility
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<PropertyInfo>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<PropertyInfo>();
        }
    }

    private static MethodInfo[] SafeGetMethods(Type type)
    {
        try
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly); // NOSONAR: intentional — deep type graph traversal needs full member visibility
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<MethodInfo>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<MethodInfo>();
        }
    }

    private static ConstructorInfo[] SafeGetConstructors(Type type)
    {
        try
        {
            return type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.DeclaredOnly); // NOSONAR: intentional — deep type graph traversal needs full member visibility
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<ConstructorInfo>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<ConstructorInfo>();
        }
    }

    private static Type? SafeGetFieldType(FieldInfo field)
    {
        try
        {
            return field.FieldType;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    private static Type? SafeGetPropertyType(PropertyInfo property)
    {
        try
        {
            return property.PropertyType;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    private static Type? SafeGetReturnType(MethodInfo method)
    {
        try
        {
            return method.ReturnType;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    private static ParameterInfo[] SafeGetParameters(MethodBase method)
    {
        try
        {
            return method.GetParameters();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<ParameterInfo>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<ParameterInfo>();
        }
    }

    private static Type? SafeGetParameterType(ParameterInfo parameter)
    {
        try
        {
            return parameter.ParameterType;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    private static IEnumerable<Type> ExpandType(Type type)
    {
        Type normalized = NormalizeType(type);
        yield return normalized;

        foreach (Type arg in GetGenericArgumentsRecursive(normalized))
        {
            yield return arg;
        }
    }

    private static Type NormalizeType(Type type)
    {
        try
        {
            if (type.HasElementType && type.GetElementType() != null)
            {
                return type.GetElementType()!;
            }
        }
        catch (FileNotFoundException)
        {
            // Swallow — defensive reflection may encounter missing assemblies
        }
        catch (TypeLoadException)
        {
            // Swallow — defensive reflection may encounter unloadable types
        }

        return type;
    }

    private static IEnumerable<Type> GetGenericArgumentsRecursive(Type type)
    {
        Type[] args;
        try
        {
            args = type.GetGenericArguments();
        }
        catch (FileNotFoundException)
        {
            yield break;
        }
        catch (TypeLoadException)
        {
            yield break;
        }

        foreach (Type arg in args)
        {
            yield return arg;

            foreach (Type nested in GetGenericArgumentsRecursive(arg))
            {
                yield return nested;
            }
        }
    }
}
