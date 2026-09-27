## 1. Align package-version parsing and range selection

- [x] 1.1 Expose the accepted NuGet package-version parser from `calculate_version.py` and verify it preserves `rc`, `alpha`, and build-metadata parts in focused parser tests.
- [x] 1.2 Extend history-range selection and transport verification to the shared parser and SemVer precedence; verify alpha/rc/build-metadata predecessor scenarios and equal-precedence ambiguity tests with the release-forensics test family.

## 2. Separate runner and analyzer timing

- [x] 2.1 Start `bundle_render_ms` after candidate analysis and remove the duplicated analyzer process-wall entry from orchestration; verify a deterministic timing regression test.

## 3. Synchronize contract and validate

- [x] 3.1 Update maintainer release-process documentation for general prerelease-line selection and build metadata ordering; run the formatter and affected documentation guards.
- [x] 3.2 Run the focused Python test families, formatting/diff checks, and strict OpenSpec change validation; record exact results.
