# source-file-fact-index Specification

## Purpose
TBD - created by archiving change source-file-fact-index. Update Purpose after archive.
## Requirements
### Requirement: Declared type facts are available per run
The system SHALL provide a per-run `ArchitectureSourceFileFactIndex` that returns a fact record for every loadable declared type across the session's target assemblies, including assembly name, namespace, full CLR type name, simple type name, and type kind.

#### Scenario: Fact includes assembly and namespace for every type
- **WHEN** the index is queried for a type present in a target assembly
- **THEN** the returned fact contains the correct assembly name, namespace, full type name, and simple type name matching the CLR reflection metadata for that type

#### Scenario: Type kind is correct from reflection when source is unavailable
- **WHEN** the index is built with empty source roots
- **THEN** each fact's TypeKind reflects the CLR kind: Interface for interfaces, Struct for value types, Enum for enumerations, Delegate for delegate types, Class for reference types that are none of the above, and Unknown otherwise

### Requirement: Source file path facts are available when deterministic and ownership-aware
The system SHALL enrich each declared type fact with source file path data only when the declaration can be tied to the same target assembly as the reflected type and exactly one owned source file declares that type — the normalized relative path, the file name without extension, folder segments relative to the repository root, and the Roslyn-accurate type kind (including Record).

#### Scenario: Single-declaration type gets source path data
- **WHEN** a type is declared in exactly one owned source file within the configured source roots
- **THEN** the fact for that type contains a non-null SourceFilePath (forward-slash normalized, relative to repository root), a non-null FileNameWithoutExtension, a non-empty FolderSegments list, and a TypeKind that accurately reflects the Roslyn syntax (including Record for record types)

#### Scenario: Unowned source roots do not enrich by CLR-name coincidence
- **WHEN** a configured source root contains a declaration whose CLR full name matches a type in a target assembly but the root's owning assembly cannot be determined
- **THEN** the fact remains reflection-only with null SourceFilePath and the declaration does not create an ambiguity record

#### Scenario: Standalone single-target configuration treats configured source roots as owned
- **WHEN** the index is built directly with one target assembly and explicit `source_roots`, without project discovery metadata
- **THEN** those configured source roots are treated as belonging to that sole target assembly and can enrich matching facts

#### Scenario: Common source root resolves ownership by discovered project subtree
- **WHEN** one configured source root such as `src` contains multiple discovered target projects like `src/App` and `src/Domain`
- **THEN** each scanned file is correlated to the owning assembly of the most specific discovered project directory that contains that file, rather than dropping the whole root as ambiguous

#### Scenario: Configured source root may be narrower than the owning project directory
- **WHEN** a discovered project lives at `src/App/App.csproj` and the configured source root is a nested subtree such as `src/App/Domain`
- **THEN** files under that configured subtree are still correlated to the owning project assembly

#### Scenario: Root-level project owns files under configured source roots
- **WHEN** a discovered project file is at the repository root and a configured source root contains files below it such as `src/`
- **THEN** those files are still correlated to that root-level project's assembly

#### Scenario: Equal-specificity project ownership conflict leaves source enrichment unavailable
- **WHEN** two discovered target projects map to the same-most-specific project directory for a scanned file
- **THEN** the file is treated as unowned for source correlation and no assembly is chosen by discovery order

#### Scenario: Record type kind is detected from source
- **WHEN** a source file declares `public record MyRecord { }` or `public record class MyRecord { }` or `public record struct MyRecord { }`
- **THEN** the fact for that type has TypeKind equal to Record

#### Scenario: Multiple types in one file each get source path data
- **WHEN** a source file declares two or more types in the same namespace
- **THEN** each type gets its own fact with the same SourceFilePath, FileNameWithoutExtension, and FolderSegments

#### Scenario: Nested type gets CLR-format full name and source path
- **WHEN** a type is declared as a nested type inside another type
- **THEN** the fact's FullTypeName uses the `+` separator between outer and inner names (CLR format), and the fact carries the same SourceFilePath as its declaring file

#### Scenario: Effective preprocessor symbols control source enrichment
- **WHEN** a declaration is behind `#if` and the active preprocessor symbol set excludes it
- **THEN** the fact remains reflection-only with null SourceFilePath

### Requirement: Folder and namespace segments are stable and normalized
The system SHALL expose folder segments as a normalized, ordered list of path components relative to the repository root, and namespace segments as an ordered list of dot-separated components of the type's namespace.

#### Scenario: Folder segments match the file's directory hierarchy
- **WHEN** a type's source file is at `src/MyProject/Domain/Order.cs` relative to the repository root
- **THEN** FolderSegments equals `["src", "MyProject", "Domain"]`

#### Scenario: Namespace segments split on dot
- **WHEN** a type's namespace is `MyApp.Domain.Orders`
- **THEN** NamespaceSegments equals `["MyApp", "Domain", "Orders"]`

#### Scenario: Path normalization uses forward slashes on all platforms
- **WHEN** the underlying filesystem returns backslash-separated paths
- **THEN** SourceFilePath and all path values in the fact use forward slashes

### Requirement: Ambiguous source data is reported deterministically per assembly-owned type
The system SHALL record an `ArchitectureDeclaredTypeSourceAmbiguity` for any owned `(AssemblyName, FullTypeName)` pair declared in more than one source file (partial class across files), set that type's fact's SourceFilePath and FileNameWithoutExtension to null, and set its FolderSegments to an empty list.

#### Scenario: Partial class across two files produces an ambiguity record
- **WHEN** two source files each declare the same type (partial class across files)
- **THEN** the index contains one `ArchitectureDeclaredTypeSourceAmbiguity` for that assembly/type pair listing both file paths, and the corresponding fact has null SourceFilePath

#### Scenario: Ambiguous type fact still carries reflection-derived fields
- **WHEN** a type has an ambiguity record
- **THEN** the fact for that type still contains the correct AssemblyName, Namespace, FullTypeName, SimpleTypeName, and a TypeKind derived from reflection

### Requirement: Index computation is lazy and zero-overhead when unused
The system SHALL NOT execute the source file scan or reflection pass until a caller first accesses the index's data — ensuring that runs with no source-fact-dependent rules pay no overhead.

#### Scenario: Index builds lazily on first access
- **WHEN** `ArchitectureSourceFileFactIndex` is constructed but no property or method is called
- **THEN** no file system enumeration and no type reflection has occurred

#### Scenario: Repeated lookups reuse cached data
- **WHEN** any fact-lookup method is called more than once on the same index instance
- **THEN** the underlying build pass executes exactly once per index instance

### Requirement: Index supports lookup by full type name, file, and namespace
The system SHALL expose `TryGetFact` (by CLR full type name), `TryGetFact` (by assembly name plus CLR full type name), `GetFactsForFile` (by normalized relative file path), and `GetFactsForNamespace` (by exact namespace string).

#### Scenario: TryGetFact returns the correct fact for a known type
- **WHEN** `TryGetFact` is called with the CLR FullName of a type that is in the index and that CLR FullName is unique across target assemblies
- **THEN** it returns true and the out parameter contains the fact for that type

#### Scenario: TryGetFact returns false for an unknown type name
- **WHEN** `TryGetFact` is called with a name that matches no type in the target assemblies
- **THEN** it returns false

#### Scenario: TryGetFact returns false for an ambiguous full type name
- **WHEN** two target assemblies both declare the same CLR FullName
- **THEN** `TryGetFact(fullTypeName)` returns false instead of picking one assembly implicitly

#### Scenario: Assembly-aware lookup disambiguates colliding CLR names
- **WHEN** two target assemblies both declare the same CLR FullName
- **THEN** `TryGetFact(assemblyName, fullTypeName)` returns the fact for the exact owning assembly

#### Scenario: GetFactsForFile returns all types declared in that file
- **WHEN** two types are declared in the same source file
- **THEN** `GetFactsForFile` called with that file's normalized path returns both facts

#### Scenario: GetFactsForNamespace returns all types in that namespace
- **WHEN** multiple types share the same namespace
- **THEN** `GetFactsForNamespace` called with that namespace string returns all of them
