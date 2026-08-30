## 1. Exposure fact model and scanner

- [x] 1.1 Add Core-internal assembly-qualified target, structured path, exposure, and incomplete-evidence models; verify their canonical identity/order is covered by focused NUnit tests.
- [x] 1.2 Implement a defensive visible-contract exposure scanner that mirrors exported type/member visibility and records recursive signature, relationship, delegate, and nested-type paths; verify nested generic, tuple/array/nullable, generic-constraint, base/interface, and cycle scenarios.
- [x] 1.3 Extend the scanner to collect visible custom-attribute type and typed argument facts without interpreting primitive/string values; verify type/member/parameter/return/generic-parameter sites, `typeof`, enum, and primitive/string scenarios.

## 2. Session reuse and completeness

- [x] 2.1 Add a session-owned immutable exposure index that accepts caller-selected effective surface roots and caches only reusable exposure facts without recreating API membership; verify repeated and independent session behavior.
- [x] 2.2 Preserve deterministic incomplete evidence when required visible signature or metadata reflection fails; verify an unloadable first-party signature fixture cannot appear as a complete shortened exposure graph.
- [x] 2.3 Verify same-named targets from distinct assemblies and distinct member/metadata paths remain separate canonical evidence records.

## 3. Specification synchronization and validation

- [x] 3.1 Compare the implementation and focused tests with the change spec/design, updating the artifacts to match the delivered internal index behavior.
- [x] 3.2 Run focused Core tests, `make fmt`, relevant architecture lint, and `openspec validate --all`; record the exact passing commands before archiving.
