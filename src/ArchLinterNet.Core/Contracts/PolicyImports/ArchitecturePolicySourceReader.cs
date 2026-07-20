using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal static class ArchitecturePolicySourceReader
{
    public static string ReadAllText(
        IArchitectureFileSystem fileSystem,
        string path,
        ArchitecturePolicySourceLocation location,
        IEnumerable<string> importChain)
    {
        try
        {
            return fileSystem.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            throw ArchitecturePolicyDiagnosticFactory.Exception(
                ArchitecturePolicyImportErrorCategory.MissingFile,
                $"Policy source file not found: {location.SourcePath}",
                location,
                importChain: importChain);
        }
        catch (UnauthorizedAccessException)
        {
            throw Unreadable(location, importChain);
        }
        catch (IOException)
        {
            throw Unreadable(location, importChain);
        }
    }

    private static ArchitecturePolicyImportException Unreadable(
        ArchitecturePolicySourceLocation location,
        IEnumerable<string> importChain)
    {
        return ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.UnreadableFile,
            $"Policy source '{location.SourcePath}' is not readable.",
            location,
            importChain: importChain);
    }
}
