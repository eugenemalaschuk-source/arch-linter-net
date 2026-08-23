## 1. Core-owned projection

- [x] 1.1 Add an internal Core snapshot projector that preserves every canonical entry, finding, and frozen baseline-debt identity.
- [x] 1.2 Replace the CLI handler's semantic projection helpers with delegation to the Core projector while retaining runtime, collision, I/O, and error handling.

## 2. Regression coverage

- [x] 2.1 Move/add focused Core tests for canonical project, graph edge, role/context, coverage blind spot, normalized finding, and baseline identity projection.
- [x] 2.2 Retain or add CLI regression coverage for snapshot command orchestration and output/collision behavior.

## 3. Verification and specification lifecycle

- [x] 3.1 Run focused Core and CLI tests, formatting, architecture lint, public API check, and strict OpenSpec validation.
- [x] 3.2 Synchronize artifacts with the implementation and archive the completed OpenSpec change before opening the pull request.
