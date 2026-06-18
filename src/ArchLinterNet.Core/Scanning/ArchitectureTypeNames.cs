namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureTypeNames
{
    public static string SafeNamespace(Type type)
    {
        try
        {
            return type.Namespace ?? string.Empty;
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
        catch (TypeLoadException)
        {
            return string.Empty;
        }
    }

    public static string SafeFullName(Type type)
    {
        try
        {
            return type.FullName ?? type.Name;
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
        catch (TypeLoadException)
        {
            return string.Empty;
        }
    }
}
