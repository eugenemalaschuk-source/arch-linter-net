using ArchLinterNet.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArchLinterNet.Core.Scanning;

// Syntax-only (no compilation) extractor that walks a C# source text and returns one ParsedTypeInfo
// per declared type. Full type names are produced in CLR format: dots for namespace separators,
// + for nesting (Outer+Inner), and `N for generic arity (Repository`1) — matching Type.FullName
// so the index can correlate Roslyn-extracted names with reflection-side keys without conversion.
internal static class ArchitectureDeclaredTypeParser
{
    internal sealed record ParsedTypeInfo(
        string FullTypeName,
        string SimpleTypeName,
        string Namespace,
        ArchitectureTypeKind TypeKind);

    public static IReadOnlyList<ParsedTypeInfo> ParseSourceText(
        string sourceText,
        IReadOnlyList<string>? preprocessorSymbols = null)
    {
        CSharpParseOptions options = preprocessorSymbols?.Count > 0
            ? CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols)
            : CSharpParseOptions.Default;

        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceText, options);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        List<ParsedTypeInfo> results = new();
        CollectMembers(root.Members, ns: string.Empty, outerClrPrefix: null, results);
        return results;
    }

    private static void CollectMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        string ns,
        string? outerClrPrefix,
        List<ParsedTypeInfo> results)
    {
        foreach (MemberDeclarationSyntax member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax nsDecl:
                    string childNs = BuildNamespace(ns, DecodeNameSyntax(nsDecl.Name));
                    CollectMembers(nsDecl.Members, childNs, outerClrPrefix: null, results);
                    break;

                case FileScopedNamespaceDeclarationSyntax fsNs:
                    string fileScopedNs = BuildNamespace(ns, DecodeNameSyntax(fsNs.Name));
                    CollectMembers(fsNs.Members, fileScopedNs, outerClrPrefix: null, results);
                    break;

                case TypeDeclarationSyntax typeDecl:
                    // ValueText decodes escaped identifiers (@class → class) to match CLR names.
                    string typeName = typeDecl.Identifier.ValueText;
                    int arity = typeDecl.TypeParameterList?.Parameters.Count ?? 0;
                    string clrSimple = arity > 0 ? $"{typeName}`{arity}" : typeName;
                    string clrFull = BuildClrFull(ns, outerClrPrefix, clrSimple);

                    results.Add(new ParsedTypeInfo(clrFull, typeName, ns, GetKind(typeDecl)));

                    // Recurse for nested type declarations (members of any TypeDeclaration may contain
                    // nested classes, structs, interfaces, records, enums, or delegates).
                    CollectMembers(typeDecl.Members, ns, outerClrPrefix: clrFull, results);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    string enumName = enumDecl.Identifier.ValueText;
                    string enumFull = BuildClrFull(ns, outerClrPrefix, enumName);
                    results.Add(new ParsedTypeInfo(enumFull, enumName, ns, ArchitectureTypeKind.Enum));
                    break;

                case DelegateDeclarationSyntax delegateDecl:
                    string delName = delegateDecl.Identifier.ValueText;
                    int delArity = delegateDecl.TypeParameterList?.Parameters.Count ?? 0;
                    string delClrSimple = delArity > 0 ? $"{delName}`{delArity}" : delName;
                    string delFull = BuildClrFull(ns, outerClrPrefix, delClrSimple);
                    results.Add(new ParsedTypeInfo(delFull, delName, ns, ArchitectureTypeKind.Delegate));
                    break;
            }
        }
    }

    // Decodes a NameSyntax to its CLR-compatible string, using ValueText on each component
    // identifier so that escaped keywords (@class, @namespace) produce their unescaped forms.
    private static string DecodeNameSyntax(NameSyntax name) =>
        name switch
        {
            QualifiedNameSyntax qualified =>
                $"{DecodeNameSyntax(qualified.Left)}.{qualified.Right.Identifier.ValueText}",
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => name.ToString()
        };

    private static string BuildNamespace(string existing, string declared) =>
        string.IsNullOrEmpty(existing) ? declared : $"{existing}.{declared}";

    // Nested types use + separator; top-level types use namespace.SimpleName or just SimpleName
    // when the type is in the global namespace. outerClrPrefix is the already-built CLR name of
    // the enclosing type (which may itself be nested), not the namespace.
    private static string BuildClrFull(string ns, string? outerClrPrefix, string clrSimple)
    {
        if (outerClrPrefix != null) return $"{outerClrPrefix}+{clrSimple}";
        return string.IsNullOrEmpty(ns) ? clrSimple : $"{ns}.{clrSimple}";
    }

    // RecordDeclarationSyntax is a distinct node type in Roslyn 4.x and must precede the base
    // TypeDeclarationSyntax cases; without this ordering ClassDeclarationSyntax would never match
    // because RecordDeclarationSyntax is not a subtype of ClassDeclarationSyntax.
    private static ArchitectureTypeKind GetKind(TypeDeclarationSyntax typeDecl) =>
        typeDecl switch
        {
            RecordDeclarationSyntax => ArchitectureTypeKind.Record,
            InterfaceDeclarationSyntax => ArchitectureTypeKind.Interface,
            StructDeclarationSyntax => ArchitectureTypeKind.Struct,
            ClassDeclarationSyntax => ArchitectureTypeKind.Class,
            _ => ArchitectureTypeKind.Unknown
        };
}
