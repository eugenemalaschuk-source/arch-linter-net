using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureReferenceScanner
{
    public static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        foreach (Type interfaceType in SafeGetInterfaces(type))
        {
            foreach (Type expanded in ExpandType(interfaceType))
            {
                yield return expanded;
            }
        }

        foreach (Type baseType in EnumerateBaseTypes(type))
        {
            foreach (Type expanded in ExpandType(baseType))
            {
                yield return expanded;
            }
        }

        foreach (FieldInfo field in SafeGetFields(type))
        {
            Type? fieldType = SafeGetFieldType(field);
            if (fieldType != null)
            {
                foreach (Type expanded in ExpandType(fieldType))
                {
                    yield return expanded;
                }
            }
        }

        foreach (PropertyInfo property in SafeGetProperties(type))
        {
            Type? propertyType = SafeGetPropertyType(property);
            if (propertyType != null)
            {
                foreach (Type expanded in ExpandType(propertyType))
                {
                    yield return expanded;
                }
            }
        }

        foreach (MethodInfo method in SafeGetMethods(type))
        {
            Type? returnType = SafeGetReturnType(method);
            if (returnType != null)
            {
                foreach (Type expanded in ExpandType(returnType))
                {
                    yield return expanded;
                }
            }

            foreach (ParameterInfo parameter in SafeGetParameters(method))
            {
                Type? parameterType = SafeGetParameterType(parameter);
                if (parameterType != null)
                {
                    foreach (Type expanded in ExpandType(parameterType))
                    {
                        yield return expanded;
                    }
                }
            }
        }

        foreach (ConstructorInfo constructor in SafeGetConstructors(type))
        {
            foreach (ParameterInfo parameter in SafeGetParameters(constructor))
            {
                Type? parameterType = SafeGetParameterType(parameter);
                if (parameterType != null)
                {
                    foreach (Type expanded in ExpandType(parameterType))
                    {
                        yield return expanded;
                    }
                }
            }
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

    private static IEnumerable<Type> SafeGetInterfaces(Type type)
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

    private static IEnumerable<FieldInfo> SafeGetFields(Type type)
    {
        try
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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

    private static IEnumerable<PropertyInfo> SafeGetProperties(Type type)
    {
        try
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                      BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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

    private static IEnumerable<MethodInfo> SafeGetMethods(Type type)
    {
        try
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                   BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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

    private static IEnumerable<ConstructorInfo> SafeGetConstructors(Type type)
    {
        try
        {
            return type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.DeclaredOnly);
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

    private static IEnumerable<ParameterInfo> SafeGetParameters(MethodBase method)
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
        }
        catch (TypeLoadException)
        {
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
