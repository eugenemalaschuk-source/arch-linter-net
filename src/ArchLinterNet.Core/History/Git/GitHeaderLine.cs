using System.Text;

namespace ArchLinterNet.Core.History.Git;

// One direct header line of a commit or tag object. The value stays as raw bytes because canonical
// author, committer, and encoding evidence is defined over bytes, not over a decoded string.
internal sealed class GitHeaderLine(string name, byte[] value)
{
    public string Name { get; } = name;

    public byte[] Value { get; } = value;

    // Only safe for headers whose grammar is ASCII by construction, such as `tree`, `parent`, and
    // `object`. Author/committer/encoding evidence never goes through this.
    public string ValueText => Encoding.ASCII.GetString(Value);
}
