## 1. Baseline and source correction

- [x] 1.1 Record the frozen #804 audit identities, source paths, target paths, and governing policy reasons in `design.md`; verify the current comparable audit reports the same three History findings.
- [x] 1.2 Relocate the two History exception declarations and the enrichment provider interface to their recursive `Exceptions`/`Abstractions` directories, splitting the adjacent materialization class as needed; verify namespaces and declaration behavior are unchanged.

## 2. Regression proof and validation

- [x] 2.1 Add real-source self-policy audit regressions for a misplaced exception and interface; verify each produces its expected layout-convention finding.
- [x] 2.2 Run focused History and self-policy tests, a comparable audit, formatter, code-size and architecture lint, public-API review, and OpenSpec validation; verify only the three owned audit identities are removed and no new layout finding is introduced.

## 3. Synchronization

- [x] 3.1 Mark the minimal #820 completion hunk in the shared `decompose-god-classes` task list and prepare this focused no-spec change for archive; verify `openspec validate --all` succeeds.
