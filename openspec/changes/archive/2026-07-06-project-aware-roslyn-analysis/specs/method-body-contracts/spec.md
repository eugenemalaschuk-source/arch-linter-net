## MODIFIED Requirements

### Requirement: Detect forbidden calls via Roslyn semantic analysis
The system SHALL parse C# source files, build a Roslyn compilation, walk executable bodies (methods, constructors, accessors, local functions), resolve symbols via `SemanticModel`, and match against forbidden call patterns. When project discovery is configured and project-aware compilation context resolves successfully for the contract's single owning project, the compilation SHALL use that project's resolved reference assembly paths (project references, package references, and framework references) instead of the AppDomain-loaded reference list; otherwise the system SHALL use the existing lightweight, AppDomain-based reference list unchanged.

#### Scenario: Forbidden call found in method body
- **WHEN** a method body contains a call matching a forbidden pattern
- **THEN** a violation is returned with the file path, line number, matched pattern, and resolved symbol

#### Scenario: No forbidden calls
- **WHEN** no method bodies contain calls matching forbidden patterns
- **THEN** the contract returns an empty violation list

#### Scenario: Source file not in target namespace
- **WHEN** a source file does not contain types in the source namespace prefix
- **THEN** that file is skipped entirely

#### Scenario: Cross-project call resolved via project-aware references
- **WHEN** a method body in one discovered project calls a member of a type defined in a referenced project or package, and project-aware resolution succeeds for the calling project
- **THEN** the call's symbol resolves via the project-aware reference assemblies and is matched against forbidden call patterns as accurately as a call to a type in the same project

#### Scenario: Behavior unchanged without project discovery
- **WHEN** `analysis.solution` and `analysis.projects` are both unset for a repository
- **THEN** method-body scanning behaves exactly as before this change, using the AppDomain-based reference list

### Requirement: Method body contract accepts optional id
A method body contract SHALL accept an optional `id` field. When provided, violations from this contract SHALL include the contract ID.

#### Scenario: Violation includes contract ID
- **WHEN** a method body contract with `id: no-reflection` produces a violation
- **THEN** the violation SHALL have `ContractId == "no-reflection"`

#### Scenario: Violation without explicit ID
- **WHEN** a method body contract without explicit `id` produces a violation
- **THEN** the violation SHALL have `ContractId` set to the fallback ID derived from `name`

## ADDED Requirements

### Requirement: Exclude generated and build-output files from source scanning
The system SHALL exclude source files from method-body scanning when their path contains a `bin`, `obj`, `Library`, `Temp`, or `PackageCache` directory segment, or when their filename ends in `.g.cs`, `.g.i.cs`, or `.designer.cs` (case-insensitive), regardless of whether the file list came from directory enumeration or from project-aware resolution.

#### Scenario: Build output directory excluded
- **WHEN** a `.cs` file's path contains a `bin` or `obj` directory segment
- **THEN** that file is not scanned for method-body violations

#### Scenario: Unity-generated directory excluded
- **WHEN** a `.cs` file's path contains a `Library`, `Temp`, or `PackageCache` directory segment
- **THEN** that file is not scanned for method-body violations

#### Scenario: Generated filename suffix excluded
- **WHEN** a `.cs` file's name ends in `.g.cs`, `.g.i.cs`, or `.designer.cs`
- **THEN** that file is not scanned for method-body violations

#### Scenario: Ordinary source file is not excluded
- **WHEN** a `.cs` file is under a source root with no excluded directory segment and no excluded filename suffix
- **THEN** that file remains eligible for method-body scanning
