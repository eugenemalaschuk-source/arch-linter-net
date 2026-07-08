## 1. SARIF formatter

- [x] 1.1 Create `ArchitectureSarifFormatter` in `src/ArchLinterNet.Core/Reporting/` with a method that takes `mode`, violations, and cycles and returns a SARIF 2.1.0 JSON string.
- [x] 1.2 Implement rule collection: deduplicate by `ContractId` (falling back to `ArchitecturePolicyDocumentLoader.NormalizeToContractId(ContractName)`), sort alphabetically by rule ID, populate `id` and `shortDescription.text`.
- [x] 1.3 Implement severity mapping: `mode == "strict"` → `"error"`, `mode == "audit"` → `"warning"`.
- [x] 1.4 Implement physical location parsing for method-body diagnostics: detect `ForbiddenNamespace == "method-body"`, regex-parse `"line {N}: ..."` from each `ForbiddenReferences` entry, emit `physicalLocation` with `artifactLocation.uri = SourceType` and `region.startLine`; omit `region` when parsing fails but keep the artifact location.
- [x] 1.5 Implement logical location mapping for all other diagnostic kinds: `fullyQualifiedName = SourceType` (or cycle path for cycles), with a `kind` hint chosen by a local switch on the diagnostic's concrete subtype.
- [x] 1.6 Implement deterministic result ordering: sort by `(ruleId, SourceType, ForbiddenNamespace)`.
- [x] 1.7 Build per-result `message.text` reusing the existing contract name/id/SourceType/ForbiddenNamespace/references conventions from the human formatter.
- [x] 1.8 Exclude coverage findings, unmatched-ignored-violations, and policy-consistency findings from SARIF results.

## 2. CLI wiring

- [x] 2.1 Extend the `--format`/`-f` validation in `RunValidateCommand` (`src/ArchLinterNet.Cli/Program.cs`) to accept `sarif` alongside `human`/`json`.
- [x] 2.2 Add a SARIF dispatch branch that calls `ArchitectureSarifFormatter` and writes the result to stdout.
- [x] 2.3 Update the invalid-format error message to list `human`, `json`, and `sarif`.
- [x] 2.4 Update `PrintHelp()` to document `--format sarif`.

## 3. Tests

- [x] 3.1 Add `ArchitectureSarifFormatter` unit tests in `tests/ArchLinterNet.Core.Tests/` covering: envelope shape (version, `$schema`, `tool.driver.name`), rule dedup and fallback ID, strict vs audit level mapping, physical location parsing (including the unparseable-reference fallback), logical locations for at least three different diagnostic kinds, deterministic ordering across mixed violation kinds, and cycle results.
- [x] 3.2 Add a CLI integration test in `tests/ArchLinterNet.Cli.Tests/CliIntegrationTests.cs` invoking `--format sarif` end-to-end and asserting the output parses as valid SARIF with the expected top-level shape.
- [x] 3.3 Run existing `ArchitectureDiagnosticFormatterTests` and `UnifiedJsonOutputTests` to confirm human/JSON output is unchanged.

## 4. Spec synchronization

- [x] 4.1 Confirm delta specs (`sarif-diagnostics-output`, `cli-validation`, `violation-reporting`) match the implemented behavior; adjust wording if implementation diverged from design.
- [ ] 4.2 Run `openspec validate --all` after archiving to confirm all specs remain valid.

## 5. Validation

- [x] 5.1 Run `make fmt`.
- [x] 5.2 Run `make acceptance` and fix any failures.
