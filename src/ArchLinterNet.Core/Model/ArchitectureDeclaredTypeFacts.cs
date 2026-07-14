namespace ArchLinterNet.Core.Model;

public enum ArchitectureTypeKind
{
    Class,
    Interface,
    Struct,
    Enum,
    Record,
    Delegate,
    Unknown
}

// Static declared-type fact for one CLR type — assembly name, namespace, CLR full type name
// (dots for namespace separator, + for nesting, `N for generic arity), simple declared name,
// type kind (Record requires source; reflection falls back to Class/Struct), and optional source
// path facts available when exactly one source file declares the type.
// FolderSegments and NamespaceSegments are always in stable, normalized order.
public sealed record ArchitectureDeclaredTypeFact(
    string AssemblyName,
    string Namespace,
    string FullTypeName,
    string SimpleTypeName,
    ArchitectureTypeKind TypeKind,
    string? SourceFilePath,
    string? FileNameWithoutExtension,
    IReadOnlyList<string> FolderSegments,
    IReadOnlyList<string> NamespaceSegments);

// Recorded when a type is declared in more than one source file (partial class across files),
// making a deterministic primary source path unavailable. The corresponding ArchitectureDeclaredTypeFact
// will have null SourceFilePath; path-based rules cannot evaluate ambiguous types.
public sealed record ArchitectureDeclaredTypeSourceAmbiguity(
    string FullTypeName,
    IReadOnlyList<string> SourceFilePaths);
