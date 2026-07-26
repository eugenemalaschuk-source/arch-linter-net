## ADDED Requirements

### Requirement: `explain` reports authored source-set expansion
The `explain` command SHALL report, in both human and JSON output, each expanded contract's authored contract id, the set that produced each instance, the concrete resolved source, and the authored policy location, so an author can see why a contract applies to a given source.

#### Scenario: Explain shows set, source, and fragment
- **WHEN** `explain` runs against a policy whose contracts were expanded from a named source set
- **THEN** the output names the authored contract, the set, each concrete resolved source, and the authored policy fragment location
