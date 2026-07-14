## 1. Model Types

- [x] 1.1 Add `ArchitectureTypeKind` enum (Class, Interface, Struct, Enum, Record, Delegate, Unknown) in `src/ArchLinterNet.Core/Model/`
- [x] 1.2 Add `ArchitectureDeclaredTypeFact` record (AssemblyName, Namespace, FullTypeName, SimpleTypeName, TypeKind, SourceFilePath?, FileNameWithoutExtension?, FolderSegments, NamespaceSegments) in `src/ArchLinterNet.Core/Model/`
- [x] 1.3 Add `ArchitectureDeclaredTypeSourceAmbiguity` record (FullTypeName, SourceFilePaths) in `src/ArchLinterNet.Core/Model/`

## 2. Roslyn Syntax Parser

- [x] 2.1 Add internal static `ArchitectureDeclaredTypeParser` class in `src/ArchLinterNet.Core/Scanning/` with `ParseSourceText(string sourceText)` that returns `IReadOnlyList<ParsedTypeInfo>`
- [x] 2.2 Implement namespace extraction for both block-scoped (`namespace Foo { }`) and file-scoped (`namespace Foo;`) namespace declarations
- [x] 2.3 Implement type extraction for `ClassDeclarationSyntax`, `RecordDeclarationSyntax`, `StructDeclarationSyntax`, `InterfaceDeclarationSyntax`, `EnumDeclarationSyntax`, `DelegateDeclarationSyntax`
- [x] 2.4 Implement nested type handling — accumulate outer CLR name with `+` separator before recursing into member declarations
- [x] 2.5 Implement generic type arity suffix — count type parameters and append `` `N `` to produce CLR-format name (e.g., `Repository`1`)

## 3. Source File Fact Index

- [x] 3.1 Add `ArchitectureSourceFileFactIndex` class in `src/ArchLinterNet.Core/Execution/` with constructor `(IReadOnlyCollection<Assembly> targetAssemblies, string repositoryRoot, IReadOnlyList<string> sourceRoots, IArchitectureFileSystem? fileSystem = null)`
- [x] 3.2 Implement reflection pass in `BuildData()` — enumerate all loadable types via `ArchitectureTypeScanner.GetLoadableTypes`, build base facts keyed by CLR FullName
- [x] 3.3 Implement source scan in `BuildData()` — for each source root, enumerate `*.cs` files via `IArchitectureFileSystem`, parse each with `ArchitectureDeclaredTypeParser.ParseSourceText()`, build `FullName → List<(filePath, typeKind)>` map
- [x] 3.4 Implement merge logic — single file → enriched fact; multiple files → `ArchitectureDeclaredTypeSourceAmbiguity` + null path in fact; no file → reflection-only fact
- [x] 3.5 Implement path normalization helper — `Path.GetRelativePath(repositoryRoot, absoluteFilePath).Replace('\\', '/')`
- [x] 3.6 Implement `TryGetFact(string fullTypeName, out ArchitectureDeclaredTypeFact fact)` lookup
- [x] 3.7 Implement `GetFactsForFile(string relativeFilePath)` lookup — return all facts whose SourceFilePath matches
- [x] 3.8 Implement `GetFactsForNamespace(string namespaceName)` lookup — return all facts whose Namespace matches exactly
- [x] 3.9 Expose `AllFacts` and `Ambiguities` as read-only list properties backed by the lazy data

## 4. Session Integration

- [x] 4.1 Add `public ArchitectureSourceFileFactIndex SourceFileFactIndex { get; }` property to `ArchitectureAnalysisSession`
- [x] 4.2 Wire `SourceFileFactIndex` in the `ArchitectureAnalysisSession` constructor using `context.TargetAssemblies`, `context.RepositoryRoot`, and `document.Analysis.SourceRoots`

## 5. Tests

- [x] 5.1 Add `ArchitectureDeclaredTypeParserTests` with: single class, single interface, single struct, single enum, single delegate, record class, record struct, file-scoped namespace, nested type (CLR `+` format), generic type (backtick-N), multiple types in one file
- [x] 5.2 Add `ArchitectureSourceFileFactIndexTests` with: single type per file (full path data), multiple types per file (each gets path), reflection-only when source roots empty (null SourceFilePath), path normalization (backslash → forward slash), folder segments from repo root, namespace segments
- [x] 5.3 Add index tests for partial-class ambiguity: same CLR name in two files → one ambiguity record + null SourceFilePath in fact
- [x] 5.4 Add index tests for generic type matching: reflection `FullName` with backtick-N matches parser output
- [x] 5.5 Add index tests for `GetFactsForFile` and `GetFactsForNamespace` lookups
- [x] 5.6 Add index test for lazy computation: index constructed but not accessed → no filesystem calls (use FakeArchitectureFileSystem with no files added, verify no exception on construction)
- [x] 5.7 Verify existing `ArchitectureTypeIndexTests`, `ArchitectureRoleIndexTests`, and a representative namespace/dependency contract test still pass after session change

## 6. Validation

- [x] 6.1 Run `make fmt` and fix any formatting issues
- [x] 6.2 Run `make acceptance` and confirm all tests pass
