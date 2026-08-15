namespace ArchLinterNet.Core.Model;

// One C# source declaration correlated with an analyzed CLR type. This deliberately remains
// separate from ArchitectureDeclaredTypeFact: a fact has at most one usable source path, whereas
// a partial type can have several declarations that policy must inspect individually.
internal sealed record ArchitectureTypeSourceDeclaration(
    string AssemblyName,
    string FullTypeName,
    ArchitectureTypeKind TypeKind,
    bool IsPartial,
    bool IsAbstract,
    string SourceFilePath,
    int SourceLine);
