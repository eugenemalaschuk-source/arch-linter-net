## ADDED Requirements

### Requirement: Scaffold a minimal governed CLI command module

The CLI SHALL provide a deterministic scaffold profile for a new top-level command module. Given a valid command name, it SHALL create a compiling module entry point, an application handler, and a focused test fixture under the CLI command container's standard feature-first shape.

#### Scenario: Scaffold creates an independent command module
- **WHEN** a contributor invokes the CLI-command scaffold for `inspect`
- **THEN** it creates the `Inspect` module beneath `src/ArchLinterNet.Cli/Commands`
- **AND** the generated entry point is placed in `Inspect.EntryPoint`
- **AND** the generated handler is placed in `Inspect.Application`
- **AND** a focused `Inspect` command test fixture is created

### Requirement: Scaffold creates optional convention folders only when needed

The scaffold SHALL not create empty `Abstractions`, `Models`, or `Exceptions` placeholders. It SHALL document and generate the correct namespace/path when an option requests one of those categories, so new types cannot be placed in an arbitrary helper folder.

#### Scenario: Minimal command has no empty convention folders
- **WHEN** a contributor scaffolds a command with no optional model, abstraction, or exception
- **THEN** the generated module contains no empty placeholder directory
- **AND** the module remains valid under the module-container profile

#### Scenario: Requested model uses the model convention
- **WHEN** a contributor requests a model while scaffolding a command
- **THEN** the generated model is placed in the module's `Models` namespace and directory
- **AND** the generated source contains no first-party dependency on an application or sibling module type

### Requirement: Scaffold does not create central registration or policy conflicts

For the reflection-composed CLI, scaffolding a command SHALL not edit `Program.cs`, a hard-coded command registry, or a hand-maintained list of sibling command layers. The generated module SHALL be discoverable through the governed command-module seam.

#### Scenario: Two branches scaffold distinct commands
- **WHEN** two contributors independently scaffold distinct command names from the same base revision
- **THEN** each change is confined to its own command and focused test paths
- **AND** neither change requires an edit to a central command-registration or peer-inventory file

### Requirement: Scaffold is safe and validation-oriented

The scaffold SHALL reject an invalid or colliding module name before writing files, support a dry-run that lists the intended paths and namespaces, and never overwrite an existing source file without an explicit force option. Its completion output SHALL identify the architecture policy check required before committing.

#### Scenario: Existing module is not overwritten
- **WHEN** a contributor scaffolds `inspect` and the target `Inspect` module already exists
- **THEN** the command fails before changing files and reports the conflicting path

#### Scenario: Dry run is deterministic
- **WHEN** a contributor invokes the scaffold with dry-run enabled
- **THEN** no files are created
- **AND** output lists the same paths, namespaces, and module name that a non-dry run would use
