## 1. Policy model and execution registration

- [x] 1.1 Add strict and audit port-boundary YAML model types, schema entries,
  loader validation, and baseline-compatible catalog accessors.
- [x] 1.2 Register the family checker and configuration contributor in the
  ordered contract-family registry and update registry tests.

## 2. Deterministic boundary evaluation

- [x] 2.1 Implement direct-reference evaluation for source, target context,
  allowed seam, forbidden target, exclusions, and ignored violations.
- [x] 2.2 Implement adapter-to-port binding and approved adapter-context
  validation from compiled full interface facts.
- [x] 2.3 Add explicit configuration or coverage diagnostics for unsupported
  dependency evidence and ambiguous policy matches.

## 3. Diagnostics and fixtures

- [x] 3.1 Extend human and JSON reporting with structured seam evidence and
  safe remediation hints.
- [x] 3.2 Add fixtures and NUnit coverage for allowed ports, forbidden direct
  domain/adapter references, valid and mismatched adapters, ACL/direct database
  behavior, strict/audit modes, and baseline behavior.

## 4. Documentation and specification synchronization

- [x] 4.1 Document the port-boundary policy YAML, static-analysis limits, and
  Sales/Catalog plus LegacyCRM/LegacyERP examples.
- [x] 4.2 Update the semantic role catalog vocabulary and supported capability
  inventory.
- [x] 4.3 Run formatting and acceptance; synchronize artifacts and archive the
  OpenSpec change after all validation passes.
