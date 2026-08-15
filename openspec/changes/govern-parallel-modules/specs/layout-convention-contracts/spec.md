## ADDED Requirements

### Requirement: Layout conventions can require a role- and modifier-pure folder

Layout convention contracts SHALL support an all-declarations expectation that evaluates each declared type in a matched source file against permitted type kinds, resolved semantic roles, and the C# abstract modifier. The source-fact model SHALL preserve abstractness for this evaluation. A contract with no effective permitted shape SHALL be rejected as invalid.

#### Scenario: An abstraction folder accepts an interface
- **WHEN** a recursive abstraction-folder convention permits interfaces and abstract classes
- **AND** a matched file declares an interface
- **THEN** the file satisfies the convention

#### Scenario: An abstraction folder rejects a concrete class
- **WHEN** a matched `Abstractions` file declares a non-abstract class
- **THEN** the convention reports the file, type, actual kind, and actual abstractness

#### Scenario: An exception folder rejects a non-exception record or interface
- **WHEN** a matched `Exceptions` file declares a type whose resolved role is not `Exception`
- **THEN** the convention reports a folder-purity violation regardless of whether that type is a class, record, struct, enum, interface, or delegate

### Requirement: Existing layout contracts retain their behavior

Existing type-kind, name, counterpart, source-path, and declaration-count layout conventions SHALL retain their documented behavior when a policy does not opt into the new role- or modifier-purity expectation.

#### Scenario: Existing service layout policy is unchanged
- **WHEN** an existing policy contains only `require_type_kind: class` and a name suffix
- **THEN** it produces the same pass or violation result as before the new folder-purity support
