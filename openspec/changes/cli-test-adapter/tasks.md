## 1. CLI Tool Manifest & Distribution

- [ ] 1.1 Add `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>arch-linter-net</ToolCommandName>` to `src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj`
- [ ] 1.2 Create `.config/dotnet-tools.json` manifest registering the locally-built CLI tool at the solution root
- [ ] 1.3 Add `.config/` to `.gitignore` if not already present (tool restore writes local state)

## 2. CLI Integration Tests

- [ ] 2.1 Rewrite `tests/ArchLinterNet.Cli.Tests/UnitTest1.cs` with real integration tests: `--help`, `--version`, default policy path
- [ ] 2.2 Add tests for `--policy` / `-p` with valid, missing, and custom paths
- [ ] 2.3 Add tests for `--mode` / `-m` / `--strict` / `--audit` flag parsing and mode validation
- [ ] 2.4 Add tests for `--format` / `-f` / `--json` flag parsing and format validation
- [ ] 2.5 Add tests for exit code 0 (pass), exit code 1 (violations found), exit code 2 (runtime errors)
- [ ] 2.6 Add tests for unknown flag and invalid argument combinations
- [ ] 2.7 Add a test that runs strict validation against the sample policy and verifies exit code 0
- [ ] 2.8 Add a test that verifies JSON output is valid JSON with the expected schema

## 3. Sample Policy Enhancement

- [ ] 3.1 Verify `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml` loads and passes all its own strict contracts
- [ ] 3.2 Add an audit-mode contract to the sample policy (e.g., an `audit_layers` or `audit_cycles` entry) so audit mode has something to check
- [ ] 3.3 Create a sample test file in `samples/BasicCleanArchitecture/` that demonstrates `ArchitectureAssertions.FromPolicy(...)` usage with NUnit (documented, not necessarily in the test runner)

## 4. README Documentation

- [ ] 4.1 Add CLI usage section: flags, examples, exit codes, default values
- [ ] 4.2 Add test adapter API section: `ArchitectureAssertions.FromPolicy`, `ValidateStrict`, `ValidateAudit`, `ShouldPass`
- [ ] 4.3 Add CI wiring section: GitHub Actions step example + `Makefile` target example
- [ ] 4.4 Add sample usage section: running against `samples/BasicCleanArchitecture/`

## 5. Make Targets & Quality

- [ ] 5.1 Wire `make audit-architecture` in `make/lint.mk` to use `dotnet run --project src/ArchLinterNet.Cli -- --mode audit --policy architecture/dependencies.arch.yml` instead of the current stub
- [ ] 5.2 Run `rtk make verify` to confirm all lint and test targets pass after changes

## 6. Final Verification

- [ ] 6.1 Run `rtk make restore` then `rtk make verify` end-to-end
- [ ] 6.2 Verify `make audit-architecture` produces human-readable audit output (no errors, correct mode label)
- [ ] 6.3 Verify `dotnet tool restore` + `dotnet arch-linter-net --help` works from repository root
