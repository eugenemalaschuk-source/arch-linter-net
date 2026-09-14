## 1. Reproducible SonarCloud debt inventory

- [x] 1.1 Add `tools/release/sonar_debt_inventory.py` with a read-only client for
      `api/project_analyses/search`, `api/qualitygates/project_status`, `api/measures/component`,
      `api/issues/search` and `api/hotspots/search`.
- [x] 1.2 Explicit pagination for issues and hotspots (page size 500) with a collected-vs-reported
      total cross-check that fails non-zero on any shortfall.
- [x] 1.3 Revision binding: fail non-zero when the requested revision has no completed analysis on the
      default branch; record analysis date, revision, branch and organization.
- [x] 1.4 Deterministic JSON inventory and Markdown triage report classifying every finding by rule,
      component, line, type, severity, quality/impact and debt, with reviewed dispositions kept
      separate from untriaged findings. Takes no path arguments; writes to stdout.
- [x] 1.5 Add `tools/release/tests/test_sonar_debt_inventory.py` covering revision mismatch, full
      pagination, total mismatch and individually scoped dispositions.
- [x] 1.6 Capture and commit the post-v0.8 baseline at revision
      `103b2680511b702288a95ee4a16189bcec099027` as
      `docs/internal/sonar-debt-baseline-2026-09-13.{json,md}` (460 findings, gate ERROR, 0 hotspots).

## 2. Quality-gate-blocking findings

- [x] 2.1 Refactor `ArchitectureHealthPublicationEvidenceProjector.Project` to pass the reasons list
      explicitly instead of a closure-mutated captured list, removing the `csharpsquid:S2583` false
      positive. Projector tests pass (8/8).
- [x] 2.2 Sanitize the glob input in `tools/scripts/test_coverage_badge.py` (the one real taint sink)
      via `collect_cobertura_reports`, and add tests proving absolute/traversal globs are rejected.
- [x] 2.3 Replace the public-writable literal in the `python:S5443` negative test with a relative
      traversal path, preserving the rejection assertion.
- [x] 2.4 Record individually reviewed false-positive dispositions in the baseline for
      `pythonsecurity:S2083` (`main_quality_coverage.py`), `pythonsecurity:S8707`
      (`verify_restored_main_packages.py`) and `csharpsquid:S2583`; each names the guard/reasoning and
      covers only that finding. No rule/file/directory-wide suppression was introduced.

## 3. Python tooling debt (behavior-preserving)

- [x] 3.1 `tools/release/main_quality_coverage.py`: `S1192` constants and both `S3776` functions.
- [x] 3.2 `tools/release/release_distribution.py`: `S1192` literals, `S5843`/`S6353` regex, `S3776`.
- [x] 3.3 `tools/release/verify_relay_dependencies.py` and `main_build.py`: `S1192`/`S3776`/`S1854`.
- [x] 3.4 `tools/badge_promotion/{cli,config,decision,adapters,artifact}.py`: `S3776`/`S1192`/`S5713`/
      `S7504`/`S7500`/`S1172`/`S5886`, preserving CLI behavior and contracts.
- [x] 3.5 Record `python:S5655` and `python:S5778` as untriaged in the baseline. They are test-double
      type-inference and pytest-invocation style findings that require individual review; none was
      silently suppressed, and they are handed to the follow-up remediation slice.

## 4. Test and benchmark debt

- [x] 4.1 Resolve `external_roslyn:CA1861` and `CA1822` findings in `tests/**` and `benchmarks/**`,
      preserving every assertion; SYSLIB1054/CA1068/CA2101/CA2208/CA2016 fixes included.
- [x] 4.2 Add the missing assertion for `csharpsquid:S2699` in `CheckpointBProcessRunnerTests.cs` by
      strengthening the test with `Assert.DoesNotThrowAsync`.

## 5. Relay mechanical debt (behavior-preserving)

- [x] 5.1 Apply the mechanical TypeScript rules under `relay/src/**` (`S6653`, `S7780`, `S6353`,
      `S1128`, `S2737`, `S6571`, `S7763`, `S6535`, `S6582`, `S7750`, `S7778`, `S7737`, `S1854`,
      `S5906`, `S5843`, `S7059`, `S3358`).
- [x] 5.2 Re-hash the shipped relay bundle: update `relay/bundle-manifest.json` digests and the
      trusted `ShippedRelayBundleManifestSha256` pin consistently.
- [x] 5.3 Run the relay suite (`vitest`, 40 tests) and typecheck; both pass.

## 6. Validation, archive, delivery

- [x] 6.1 Run `make fmt`; revert incidental formatting of unrelated pre-existing docs.
- [x] 6.2 Run `make lint` (code-size, dotnet-format, architecture) — clean.
- [x] 6.3 Run `make public-api-check` — no reviewed public API drift.
- [x] 6.4 Run the three test projects: Core 3862 passed, CEL 583 passed, Cli 885 passed / 1
      pre-existing macOS-only `FileSystemTests` failure also present on clean `main`.
- [x] 6.5 Record the before inventory (460 findings / gate ERROR) and the expected post-analysis
      comparison; the authoritative after-inventory is produced by the PR's SonarCloud analysis at the
      merged revision.
- [x] 6.6 Run `openspec archive reduce-post-v0-8-sonar-debt` and `openspec validate --all`.
- [x] 6.7 Commit on `fix/post-v0-8-sonar-debt` and open one pull request containing the complete
      change.

## Deferred (explicit non-goals, handed to focused owners)

- C#/CLI `S3776`/`S107` cognitive-complexity and parameter-count debt (architectural root causes).
- Public-API-risky Roslyn rules (`CA1859`, `S3871`, `S2365`, `S3218`).
- Relay `typescript:S3776` complexity refactors.
- `python:S5655`/`S5778` individual review.
