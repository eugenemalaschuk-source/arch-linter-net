## 1. CLI Tool Manifest & Distribution

- [x] 1.1 Add `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>arch-linter-net</ToolCommandName>` to `src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj`
- [x] 1.2 Create `.config/dotnet-tools.json` manifest registering the locally-built CLI tool at the solution root
- [x] 1.3 _(skipped — `.config/dotnet-tools.json` should be committed, not gitignored)_

## 2. CLI Integration Tests

- [x] 2.1 Rewrite `tests/ArchLinterNet.Cli.Tests/UnitTest1.cs` with real integration tests: `--help`, `--version`, custom policy path
- [x] 2.2 Add tests for `--policy` / `-p` with valid, missing, and custom paths
- [x] 2.3 Add tests for `--mode` / `-m` / `--strict` / `--audit` flag parsing and mode validation
- [x] 2.4 Add tests for `--format` / `-f` / `--json` flag parsing and format validation
- [x] 2.5 Add tests for exit code 0 (pass), exit code 1 (violations found), exit code 2 (runtime errors)
- [x] 2.6 Add tests for unknown flag and invalid argument combinations
- [x] 2.7 Add a test that runs validation against the sample policy and verifies diagnostics
- [x] 2.8 Add a test that verifies JSON output is valid JSON with the expected schema

## 3. Sample Policy Enhancement

- [x] 3.1 Verify `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml` loads (no build-time errors)
- [x] 3.2 Add an audit-mode contract to the sample policy (audit-infrastructure-must-not-depend-on-web)
- [x] 3.3 Create a sample test file in `samples/BasicCleanArchitecture/` that demonstrates `ArchitectureAssertions.FromPolicy(...)` usage

## 4. README Documentation

- [x] 4.1 Add CLI usage section: flags, examples, exit codes, default values
- [x] 4.2 Add test adapter API section: `ArchitectureAssertions.FromPolicy`, `ValidateStrict`, `ValidateAudit`, `ShouldPass`
- [x] 4.3 Add CI wiring section: GitHub Actions step example + `Makefile` target example
- [x] 4.4 Add sample usage section: running against `samples/BasicCleanArchitecture/`

## 5. Make Targets & Quality

- [x] 5.1 Wire `make audit-architecture` to use CLI instead of stub
- [x] 5.2 Run `rtk make acceptance` — all tests and lint pass

## 6. Final Verification

- [x] 6.1 `rtk make acceptance` passes (Core.Tests: 74, Cli.Tests: 19, Unity.Tests: 1)
- [x] 6.2 `make audit-architecture` produces clean audit output
- [x] 6.3 `dotnet arch-linter-net --help` works from repository root
