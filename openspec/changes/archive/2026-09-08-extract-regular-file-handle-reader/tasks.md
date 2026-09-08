## 1. Repository-local responsibility extraction

- [x] 1.1 Replace the `RegularFileHandleReader.RepositoryLocal` partial fragment with one purpose-named internal non-partial repository-local reader while preserving the `ArchitectureFileSystem` public seam.
- [x] 1.2 Retain native regular-file verification and exception behavior in the one handwritten `RegularFileHandleReader` declaration.

## 2. Regression evidence

- [x] 2.1 Add focused direct tests for contained reads, repository containment rejection, link/reparse escape prevention, and regular-file rejection; retain bounded artifact-read proof.

## 3. Governance synchronization and validation

- [x] 3.1 Remove only the exact `RegularFileHandleReader` declaration-count waiver and update the #813 evidence hunk in the active umbrella change.
- [x] 3.2 Run focused Core tests, formatting, architecture/code-size/API checks, and OpenSpec validation.
- [x] 3.3 Archive this scoped no-delta change without archiving `decompose-god-classes`.
