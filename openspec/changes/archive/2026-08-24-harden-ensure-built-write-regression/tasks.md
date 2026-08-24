## 1. Reproducible write-conflict regressions

- [x] 1.1 Make the source-CLI fixture explicitly probe its Debug output, force a compile-input change after its initial build, and assert the replaced DLL and receipt digests.
- [x] 1.2 Make the packed installed-tool fixture force the same rebuilt `ArchLinterNet.Testing.dll` path on Windows and assert the replacement receipt.

## 2. Resolver coverage

- [x] 2.1 Add fast tests for prepared Debug-path reuse and all rejected prepared-path fallbacks.
- [x] 2.2 Add fast tests showing explicit configuration, target framework, and RID requests bypass prepared-path reuse.

## 3. Verification and lifecycle

- [x] 3.1 Run focused regressions, resolver tests, formatting, architecture lint, code-size lint, and OpenSpec validation.
- [x] 3.2 Archive the completed OpenSpec change and update the existing pull request.
