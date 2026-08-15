## ADDED Requirements

### Requirement: Reflection command discovery is confined to governed modules

The reflection-based command-module catalog SHALL instantiate a concrete command-module type only when its namespace belongs to a direct module discovered under the governed `ArchLinterNet.Cli.Commands` container. Types in the container root, an undeclared module segment, or a generic shared bucket SHALL not become commands through interface implementation alone.

#### Scenario: A root helper cannot be accidentally registered as a top-level command
- **WHEN** a concrete type in `ArchLinterNet.Cli.Commands` implements `ITopLevelCliSubcommandModule`
- **THEN** command composition rejects it as outside a governed direct module

#### Scenario: A scaffolded module is discovered
- **WHEN** a scaffolded command module implements the top-level command-module abstraction from its `EntryPoint` namespace
- **THEN** command composition discovers and composes it without a central registration edit

#### Scenario: Multiple root modules remain a deterministic configuration error
- **WHEN** more than one concrete governed type implements `IRootCliCommandModule`
- **THEN** command composition fails before execution with a deterministic diagnostic identifying the candidate module types
