## 1. Contract and regression coverage

- [x] 1.1 Update the repository metrics specification for physical source inventory independent of assembly ownership.
- [x] 1.2 Add a Core regression test for readable sources beneath multiple root-level projects; verify metrics count each file while source type correlation remains unowned.

## 2. Implementation and documentation

- [x] 2.1 Record source file counts and physical line counts before ownership-dependent type parsing, preserving generated-file exclusion, input tracking, and path deduplication.
- [x] 2.2 Document that source-size metrics use configured roots while type-source enrichment still requires unique ownership.
- [x] 2.3 Run formatting, targeted Core tests, and repository acceptance checks; validate the OpenSpec change.
- [ ] 2.4 Archive the completed OpenSpec change and revalidate all specs.

## 3. Pull request

- [ ] 3.1 Review the final diff and open a pull request against `main`.
