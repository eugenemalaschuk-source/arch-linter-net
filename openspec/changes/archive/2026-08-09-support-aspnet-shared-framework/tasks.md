## 1. Policy schema and model

- [x] 1.1 Add `analysis.shared_frameworks` to `ArchitectureAnalysisConfiguration` and the packaged JSON schema.

## 2. Resolution

- [x] 2.1 Add shared-framework directory discovery (env var and runtime-directory-derived roots, highest-version selection) and wire it into the post-build isolated load scope's probing paths.
- [x] 2.2 Fail with an actionable `InvalidOperationException` when a named framework cannot be located.

## 3. Regression coverage

- [x] 3.1 Add fake-filesystem/environment unit tests for discovery (version selection, env var precedence, missing-framework diagnostic).
- [x] 3.2 Add a representative ASP.NET Core host fixture and an acceptance test exercising `--ensure-built` analysis through the built CLI entrypoint.

## 4. Validation and synchronization

- [x] 4.1 Run focused tests, format the repository, and run full acceptance; fix issue-related failures.
- [x] 4.2 Document `analysis.shared_frameworks`; synchronize OpenSpec artifacts; validate and archive the change.
