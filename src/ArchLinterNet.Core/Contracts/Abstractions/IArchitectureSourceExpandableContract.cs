using ArchLinterNet.Core.Model;
namespace ArchLinterNet.Core.Contracts;

// Implemented by the single-source contract families that ArchitectureSourceSetExpander fans out
// into one instance per resolved source. Everything downstream of the loader keeps seeing ordinary
// single-source contracts; only the expander and the reporters that name the authored contract
// touch this interface.
public interface IArchitectureSourceExpandableContract : IArchitectureContract
{
    // The identity domain the contract's source lives in, which the expander uses to reject a set
    // whose `kind` does not match the referencing contract.
    ArchitectureSourceSetKind SourceKind { get; }

    string Source { get; set; }

    List<string> Sources { get; set; }

    List<string> SourceSets { get; set; }

    List<string> ExcludedSources { get; set; }

    List<string> ExcludedSourceSets { get; set; }

    ArchitectureSourceExpansionOrigin? ExpansionOrigin { get; set; }

    // Returns a copy of this contract bound to exactly one resolved source. Implemented per family
    // rather than by reflection so every copied field is explicit and reviewable. Mutable list
    // fields are copied, not shared: baseline loading appends to a contract's IgnoredViolations
    // after expansion, and a shared list would let one resolved source's baseline entry suppress
    // findings for every other source.
    IArchitectureSourceExpandableContract CloneForSource(string source);
}
