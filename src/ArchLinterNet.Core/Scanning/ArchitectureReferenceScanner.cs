using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureReferenceScanner
{
    public static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        foreach (Type interfaceType in SafeGetInterfaces(type))
        {
            yield return NormalizeType(interfaceType);
        }

        foreach (Type baseType in EnumerateBaseTypes(type))
        {
            yield return NormalizeType(baseType);
        }

        foreach (FieldInfo field in SafeGetFields(type))
        {
            Type? fieldType = SafeGetFieldType(field);
            if (fieldType != null)
            {
                yield return NormalizeType(fieldType);
            }
        }

        foreach (PropertyInfo property in SafeGetProperties(type))
        {
            Type? propertyType = SafeGetPropertyType(property);
            if (propertyType != null)
            {
                yield return NormalizeType(propertyType);
            }
        }

        foreach (MethodInfo method in SafeGetMethods(type))
        {
            Type? returnType = SafeGetReturnType(method);
            if (returnType != null)
            {
                yield return NormalizeType(returnType);
            }

            foreach (ParameterInfo parameter in SafeGetParameters(method))
            {
                Type? parameterType = SafeGetParameterType(parameter);
                if (parameterType != null)
                {
                    yield return NormalizeType(parameterType);
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
                    yield return NormalizeType(parameterType);
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
}
