## 1. Record the approved v0.10 contract

- [x] 1.1 Add the versioned canonical annotation manifest with all approved role FQNs, targets, metadata keys, catalog generation, package identity, and skew boundary; verify the JSON parses and the entries match `design.md`.
- [x] 1.2 Add the versioned internal v0.10 contract authoritative for #567–#574 and link it from `docs/internal/README.md`; verify the child-issue ownership and compatibility rules are explicit.

## 2. Synchronize catalog and adoption guidance

- [x] 2.1 Add exactly one v0.10 annotation disposition to every catalog role and identify the first-class role set from the manifest; verify no catalog row is missing or has multiple dispositions.
- [x] 2.2 Update semantic-classification guidance to distinguish the package contract from behavior available in already-released tools; verify no documentation claims the package is already shipped.

## 3. Validate and archive the OpenSpec change

- [x] 3.1 Format the changed Markdown files and run focused OpenSpec and documentation validation; inspect the resulting diff for unrelated changes.
- [x] 3.2 Archive `define-v0-10-semantic-annotation-contract`, inspect the synchronized specs, and run `openspec validate --all`.
