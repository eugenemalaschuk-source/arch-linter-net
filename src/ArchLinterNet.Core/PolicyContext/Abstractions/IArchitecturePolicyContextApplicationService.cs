namespace ArchLinterNet.Core.PolicyContext.Abstractions;

/// <summary>Exports effective policy facts for AI coding-agent context.</summary>
public interface IArchitecturePolicyContextApplicationService
{
    /// <summary>Loads and projects the selected effective policy without architecture analysis.</summary>
    ArchitecturePolicyContextExport Export(ArchitecturePolicyContextRequest request);
}
