## ADDED Requirements

### Requirement: Canonical context metadata precedence and conflicts are explicit

The semantic role catalog SHALL define the finite canonical context metadata vocabulary and composition order. Winning role evidence metadata SHALL be authoritative; type-scope context values SHALL fill absent keys; assembly-scope context values SHALL fill only keys still absent. Equal values SHALL coalesce. Distinct values from the same context scope SHALL omit the affected context key and be reported deterministically. Context metadata SHALL remain separate from role identity and governance authority.

#### Scenario: Catalog documents role, type, and assembly precedence
- **WHEN** a user reviews the canonical annotation catalog
- **THEN** it states that role evidence wins, type context fills missing keys, and assembly context supplies only remaining defaults

#### Scenario: Catalog documents deterministic same-scope conflicts
- **WHEN** a user reviews the canonical annotation catalog
- **THEN** it states that equivalent values coalesce and conflicting same-scope values omit only the affected context key with a diagnostic
