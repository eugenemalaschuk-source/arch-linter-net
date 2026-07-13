## 1. Public-Facing Format Draft

- [x] 1.1 Add a public-facing draft that documents `imports`, root and fragment roles, composition order, field ownership, conflicts, paths, limits, schemas, compatibility, and non-goals.
- [x] 1.2 Add recommended-name and arbitrary-name positive YAML examples, plus negative examples for shape, conflict, path, duplicate, cycle, and graph-limit failures.

## 2. Internal Implementation Handoff

- [x] 2.1 Add an internal architecture note defining resolver, parser, composer, provenance, schema, and validator boundaries compatible with #210 and #216.
- [x] 2.2 Add a complete #281/#282 test matrix covering success, compatibility, deterministic ordering, every merge category, path portability, graph safety, schemas, provenance, CLI, and Testing adapter behavior.
- [x] 2.3 Link the new design material from the internal documentation index and verify scope and non-goals remain explicit.

## 3. Validation

- [x] 3.1 Format repository documentation with `make fmt` and review the resulting diff.
- [x] 3.2 Run `make acceptance`, fix all failures, and rerun until it passes.
- [x] 3.3 Validate the OpenSpec change and confirm all design deliverables and acceptance criteria are represented before archive.
