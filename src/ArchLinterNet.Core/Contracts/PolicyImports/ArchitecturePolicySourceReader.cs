using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal static class ArchitecturePolicySourceReader
{
    public static string ReadAllText(
        IArchitectureFileSystem fileSystem,
        string path,
        string sourcePath,
        string? expectedFileIdentity,
        ArchitecturePolicySourceLocation location,
        IEnumerable<string> importChain)
    {
        try
        {
            return fileSystem is ArchitectureFileSystem && expectedFileIdentity is not null
                ? ArchitecturePolicyPathResolver.ReadVerifiedAllText(path, sourcePath, expectedFileIdentity)
                : fileSystem.ReadAllText(path);
        }
        catch (ArchitecturePolicyImportException exception)
        {
            throw ArchitecturePolicyDiagnosticFactory.Enrich(exception, location, importChain);
        }
        catch (FileNotFoundException)
        {
            throw ArchitecturePolicyDiagnosticFactory.Exception(
                ArchitecturePolicyImportErrorCategory.MissingFile,
                $"Policy source file not found: {sourcePath}",
                location,
                importChain: importChain);
        }
        catch (DirectoryNotFoundException)
        {
            throw ArchitecturePolicyDiagnosticFactory.Exception(
                ArchitecturePolicyImportErrorCategory.MissingFile,
                $"Policy source file not found: {sourcePath}",
                location,
                importChain: importChain);
        }
        catch (UnauthorizedAccessException)
        {
            throw Unreadable(sourcePath, location, importChain);
        }
        catch (IOException)
        {
            throw Unreadable(sourcePath, location, importChain);
        }
    }

    private static ArchitecturePolicyImportException Unreadable(
        string sourcePath,
        ArchitecturePolicySourceLocation location,
        IEnumerable<string> importChain)
    {
        return ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.UnreadableFile,
            $"Policy source '{sourcePath}' is not readable.",
            location,
            importChain: importChain);
    }
}
