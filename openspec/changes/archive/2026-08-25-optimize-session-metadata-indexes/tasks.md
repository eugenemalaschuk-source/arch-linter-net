## 1. Session index foundation

- [x] 1.1 Add lazy immutable session projections for retained assemblies and discovered-project metadata, preserving first-wins and project-path normalization semantics.
- [x] 1.2 Expose only narrow fact/checker-context lookup operations and record internal index materialization counters.

## 2. Covered contract-family migration

- [x] 2.1 Route package dependency/allow-only and project metadata checks through the project metadata projection; remove their per-contract lookup construction.
- [x] 2.2 Route framework-reference source ownership and assembly dependency/allow-only checks through the session projections without altering evaluation caching.

## 3. Regression evidence and completion

- [x] 3.1 Add focused parity tests and a synthetic many-project/many-contract fixture proving index materialization is bounded independently of contract fan-out.
- [x] 3.2 Run focused Core tests, formatter, architecture lint, and applicable OpenSpec validation; resolve issue-related failures.
- [x] 3.3 Synchronize the final spec with the implementation and archive the completed OpenSpec change.
