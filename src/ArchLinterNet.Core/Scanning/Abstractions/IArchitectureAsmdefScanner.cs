using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Scanning.Abstractions;

public interface IArchitectureAsmdefScanner
{
    IEnumerable<ArchitectureViolation> FindAsmdefViolations(
        string contractName,
        string? contractId,
        string repositoryRoot,
        ArchitectureAsmdefContract contract,
        string? asmdefRoot = null,
        IArchitectureFileSystem? fileSystem = null);
}
