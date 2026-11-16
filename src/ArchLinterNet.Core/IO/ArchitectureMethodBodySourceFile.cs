using Microsoft.CodeAnalysis.Text;

namespace ArchLinterNet.Core.IO;

// A run-scoped source snapshot. The exact path and text retained here are the inputs consumed by
// Roslyn, and both participate in the reuse identity.
internal sealed record ArchitectureMethodBodySourceFile(string Path, SourceText Text);
