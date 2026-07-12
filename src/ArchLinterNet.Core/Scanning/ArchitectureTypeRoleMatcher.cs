using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureTypeRoleMatcher
{
    // AND-combines every populated selector field. An unset/empty field is not applied (it never
    // narrows the selection), matching how every other contract's optional matcher fields behave.
    public static bool Matches(Type type, ArchitectureTypeMatcher matcher, ArchitectureContractDocument document, string contractName)
    {
        if (!string.IsNullOrEmpty(matcher.NameSuffix) && !type.Name.EndsWith(matcher.NameSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.NamePrefix) && !type.Name.StartsWith(matcher.NamePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.Namespace)
            && !ArchitectureLayerResolver.MatchesPrefix(ArchitectureTypeNames.SafeNamespace(type), matcher.Namespace))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.Layer))
        {
            ArchitectureLayer layer = ArchitectureLayerResolver.ResolveLayer(document, contractName, matcher.Layer);
            if (!ArchitectureLayerResolver.MatchesNamespace(layer, ArchitectureTypeNames.SafeNamespace(type)))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(matcher.BaseType) && !MatchesBaseType(type, matcher.BaseType))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.ImplementsInterface) && !ImplementsInterface(type, matcher.ImplementsInterface))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(matcher.HasAttribute) && !HasAttribute(type, matcher.HasAttribute))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesBaseType(Type type, string baseTypeFullName)
    {
        for (Type? current = SafeBaseType(type); current != null; current = SafeBaseType(current))
        {
            if (string.Equals(ArchitectureTypeNames.SafeFullName(current), baseTypeFullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsInterface(Type type, string interfaceFullName)
    {
        try
        {
            return type.GetInterfaces()
                .Any(i => string.Equals(ArchitectureTypeNames.SafeFullName(i), interfaceFullName, StringComparison.Ordinal));
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static bool HasAttribute(Type type, string attributeFullName)
    {
        try
        {
            return type.GetCustomAttributesData()
                .Any(a => string.Equals(ArchitectureTypeNames.SafeFullName(a.AttributeType), attributeFullName, StringComparison.Ordinal));
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (CustomAttributeFormatException)
        {
            return false;
        }
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
