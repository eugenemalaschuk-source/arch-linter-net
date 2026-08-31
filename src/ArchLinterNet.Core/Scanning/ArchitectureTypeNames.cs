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

    public static string SafeFullName(Type type) =>
        TryGetFullName(type, out string fullName) ? fullName : string.Empty;

    // Keeps legacy callers best-effort while letting a measurement authority distinguish an
    // unavailable reflection name from a legitimate empty namespace or other display fallback.
    public static bool TryGetFullName(Type type, out string fullName)
    {
        try
        {
            fullName = type.FullName ?? type.Name;
            return true;
        }
        catch (FileNotFoundException)
        {
            fullName = string.Empty;
            return false;
        }
        catch (TypeLoadException)
        {
            fullName = string.Empty;
            return false;
        }
    }

    public static string? SafeAssemblyName(Type type)
    {
        try
        {
            return type.Assembly.GetName().Name;
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
}
