## MODIFIED Requirements

### Requirement: User can generate a baseline file from current violations

The system SHALL provide a `baseline generate` CLI subcommand that runs validation against the current codebase and writes a baseline file containing `ignored_violations` entries for all current violations not already suppressed by manual ignores.

The generated baseline file SHALL be deterministic — identical output for identical input code, regardless of when or how many times generation is run.

The generated baseline SHALL only contain entries for violations that survive after manual `ignored_violations` in the policy file are applied. Manually ignored violations SHALL NOT appear in the generated baseline.

The `baseline generate` subcommand SHALL accept an optional `--contract <id>` flag, repeatable, that scopes generation to only the named contract id(s). Without `--contract`, generation SHALL cover all contracts in the selected mode, as today.

Newly generated baseline files SHALL use format version `2`, with each `ignored_violations` entry carrying a structured `ArchitectureViolationIdentity` (contract family, kind, source/target assembly, source/target type, source/target member, and an occurrence discriminator) in addition to human-readable `source_type`/`forbidden_reference` display fields and `reason`:

```yaml
version: 2
baseline:
  <contract-group>:
    - id: "<contract-id>"
      ignored_violations:
        - identity_version: 1
          contract_family: "<family>"
          kind: "<dependency|reference|call|package|framework|api_change|coverage>"
          source_assembly: "<assembly-name-or-null>"
          source_type: "<exact-source-type-fqn>"
          source_member: "<member-or-null>"
          target_assembly: "<assembly-name-or-null>"
          target_type: "<target-type-fqn-or-null>"
          target_member: "<exact-forbidden-symbol-or-null>"
          occurrence: 0
          forbidden_reference: "<exact-forbidden-reference-fqn>"
          reason: "generated baseline"
```

For contract families whose checks are qualified with assembly/member information (dependency-style and method-body/call contracts), `source_assembly`, `target_assembly`, and — for method-body/call contracts — `target_member` and `occurrence` SHALL be populated from the actual scanned symbols, not left null. For families not yet qualified with assembly/member data, those fields SHALL be `null` and matching SHALL fall back to `(contract family, contract id, source_type, target_type)` — this is strictly no less precise than the pre-existing `(source_type, forbidden_reference)` behavior for those families.

One baseline entry SHALL suppress exactly one `ArchitectureViolationIdentity`. Multiple distinct occurrences that previously collapsed into a single generated entry (because their legacy `(source_type, forbidden_reference)` strings were identical) SHALL now each produce their own entry, distinguished by the `occurrence` discriminator.

Display messages (including any embedded source line number) SHALL NOT be used as identity; identity SHALL be composed only of the structured fields above.

#### Scenario: Generate baseline for a clean project
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml` on a project with zero violations
- **THEN** the generated `baseline.yml` SHALL contain `version: 2` and `baseline:` with empty contract groups (no entries)

#### Scenario: Generate baseline captures exact violations
- **WHEN** user runs baseline generation on a project with known dependency violations
- **THEN** each violation SHALL appear as one or more baseline entries under the correct contract group and contract ID, each with a structured `ArchitectureViolationIdentity`

#### Scenario: Deterministic output across repeated runs
- **WHEN** user runs baseline generation twice on the same unchanged codebase
- **THEN** both output files SHALL be byte-identical, including `occurrence` discriminators

#### Scenario: Manual ignores are not duplicated in baseline
- **WHEN** user runs baseline generation on a project where some violations are already covered by manual `ignored_violations` in the policy
- **THEN** the baseline SHALL NOT contain entries for those already-ignored violations

#### Scenario: CLI help describes baseline subcommand
- **WHEN** user runs `arch-linter --help` or `arch-linter baseline --help`
- **THEN** output SHALL include usage information for `baseline generate`, `baseline update`, `baseline prune`, `baseline diff`, `baseline verify`, and `baseline migrate`

#### Scenario: Selected-contract generation scopes output
- **WHEN** user runs `arch-linter baseline generate --config policy.yml --output baseline.yml --contract app-boundaries` on a project with violations in multiple contracts
- **THEN** the generated baseline SHALL only contain entries for the `app-boundaries` contract id, even if other contracts also have current violations

#### Scenario: Same-named types in different assemblies do not collide
- **WHEN** two different assemblies each contain a violating type with the same simple name and namespace (e.g. two `Program` types), and baseline generation is run then one occurrence is baselined
- **THEN** the baseline entry SHALL suppress only the violation from its own `source_assembly`; the same-named violation in the other assembly SHALL still be reported as new debt by `validate --baseline`

#### Scenario: Multiple forbidden calls in one type each get a distinct entry
- **WHEN** a single source type contains multiple distinct forbidden-call occurrences to the same target member, and baseline generation is run then only the first occurrence's entry is baselined
- **THEN** the additional occurrences SHALL still be reported as new debt; baselining one occurrence SHALL NOT suppress the others
