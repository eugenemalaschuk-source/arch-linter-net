using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Abstractions;

public interface IArchitectureBaselineLoadingService
{
    void LoadAndMerge(ArchitectureContractDocument document, string baselinePath);

    ArchitectureBaselineDocument Load(string baselinePath);

    /// <summary>
    /// Reads the baseline file's raw text, so a caller can inspect what a rewrite would lose
    /// (comments) rather than only what the model captures.
    /// </summary>
    string ReadRawText(string baselinePath);
}
