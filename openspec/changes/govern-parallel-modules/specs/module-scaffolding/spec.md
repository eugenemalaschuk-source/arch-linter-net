## ADDED Requirements

### Requirement: Scaffold a minimal governed CLI command module

The CLI SHALL provide a deterministic scaffold profile for a new top-level command module. Given a valid command name, it SHALL create a compiling module entry point, an application handler, and a focused test fixture under the CLI command container's standard feature-first shape. The generated fixture SHALL instantiate the generated entry point and assert its actual command token.

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

The scaffold SHALL reject an invalid or colliding module name before writing files, support a dry-run that lists canonical `/`-separated repository paths and namespaces, and never overwrite an existing source file without an explicit force option. An ordinary scaffold invocation SHALL hold one repository-scoped, atomically acquired scaffold lock from collision preflight until creation succeeds or rollback completes; dry-run SHALL not acquire the lock. A successful invocation SHALL return an error if it cannot release its lock; an already failing invocation SHALL preserve its original error and additionally report an unsuccessful lock release. In ordinary mode, finalising each planned target SHALL use an atomic no-clobber operation so a collision introduced after preflight cannot overwrite another contributor's file. If that late collision aborts the plan, the scaffold SHALL roll back every target and empty directory created by that invocation whose contents still exactly match the generated contents; it SHALL retain any target changed by another process for manual review. Its completion output SHALL identify the architecture policy check required before committing.

#### Scenario: Existing module is not overwritten
- **WHEN** a contributor scaffolds `inspect` and the target `Inspect` module already exists
- **THEN** the command fails before changing files and reports the conflicting path

#### Scenario: Dry run is deterministic
- **WHEN** a contributor invokes the scaffold with dry-run enabled
- **THEN** no files are created
- **AND** output lists the same paths, namespaces, and module name that a non-dry run would use

#### Scenario: Late collision leaves no partial scaffold output
- **WHEN** another process creates a later target after scaffold preflight and after the scaffold has created an earlier target
- **THEN** the command fails without overwriting the later target
- **AND** it removes the earlier target created by that invocation when its contents remain unchanged
- **AND** it removes empty directories created only for the aborted scaffold plan

#### Scenario: Repository paths are portable
- **WHEN** a contributor runs scaffold on any supported operating system
- **THEN** its plan and output use `/`-separated repository paths

#### Scenario: Rollback preserves externally changed generated target
- **WHEN** another process changes a target after the scaffold creates it and a later target collision aborts the plan
- **THEN** the command retains the changed target for manual review

#### Scenario: Concurrent scaffold invocation is rejected before preflight
- **WHEN** one ordinary scaffold invocation holds the repository-scoped scaffold lock
- **AND** another ordinary scaffold invocation starts for any module
- **THEN** the later invocation fails before checking collisions or writing files
- **AND** the lock is released after the first invocation completes or rolls back

#### Scenario: Lock release failure remains actionable
- **WHEN** a successful ordinary scaffold invocation cannot remove its scaffold lock
- **THEN** the command returns an error identifying the lock path and manual recovery action
- **AND WHEN** another scaffold failure is already being reported
- **THEN** the original error and unsuccessful lock release are both reported
