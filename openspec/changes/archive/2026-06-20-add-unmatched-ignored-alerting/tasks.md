## 1. Core Infrastructure

- [x] 1.1 Add `ArchitectureUnmatchedIgnoredViolation` record to `ArchLinterNet.Core/Model/`
- [x] 1.2 Add `ArchitectureIgnoreUsageTracker` class to `ArchLinterNet.Core/Resolution/`
- [x] 1.3 Update `ArchitectureIgnoreMatcher.IsIgnored` to accept optional tracker + disable short-circuit when tracking

## 2. Signature Cleanup

- [x] 2.1 Change `IReadOnlyCollection<ArchitectureIgnoredViolation>` to `IReadOnlyList<ArchitectureIgnoredViolation>` in `ArchitectureNamespaceViolationFinder`
- [x] 2.2 Change same signature in `ArchitectureExternalDependencyViolationFinder.FindViolations`
- [x] 2.3 Update `ArchitectureSourceScanner.FindMethodBodyViolations` and `ArchitectureIlMethodBodyScanner` signatures if they differ

## 3. Tracker Propagation

- [x] 3.1 Thread tracker through `FindNamespaceViolations` → `IsIgnored` (direct path)
- [x] 3.2 Thread tracker through `FindTransitiveNamespaceViolations` → `IsIgnored` (transitive path)
- [x] 3.3 Thread tracker through `CheckAllowOnlyContract` → inlined `IsIgnored` call
- [x] 3.4 Thread tracker through `CheckCycleContract` → inlined `IsIgnored` call
- [x] 3.5 Thread tracker through `CheckProtectedContract` → inlined `IsIgnored` call
- [x] 3.6 Thread tracker through `CheckExternalContract` → `FindViolations` → `IsIgnored`
- [x] 3.7 Thread tracker through `CheckMethodBodyContract` → Roslyn scanner → `IsIgnored`
- [x] 3.8 Thread tracker through `CheckMethodBodyContract` → IL scanner → `IsIgnored` (shared tracker with Roslyn scanner)

## 4. Per-Contract Unmatched Detection

- [x] 4.1 Add unmatched ignore diff logic in `CheckContract` (dependency contracts)
- [x] 4.2 Add unmatched ignore diff logic in `CheckLayerContract` (layer contracts)
- [x] 4.3 Add unmatched ignore diff logic in `CheckAllowOnlyContract`
- [x] 4.4 Add unmatched ignore diff logic in `CheckCycleContract`
- [x] 4.5 Add unmatched ignore diff logic in `CheckMethodBodyContract`
- [x] 4.6 Add unmatched ignore diff logic in `CheckIndependenceContract`
- [x] 4.7 Add unmatched ignore diff logic in `CheckProtectedContract`
- [x] 4.8 Add unmatched ignore diff logic in `CheckExternalContract`

## 5. Configuration

- [x] 5.1 Add `UnmatchedIgnoredViolations` property to `ArchitectureAnalysisConfiguration` model (`error | warn | off`, default `error`)
- [x] 5.2 Update JSON schema with `analysis.unmatched_ignored_violations` enum

## 6. Output Formatting

- [x] 6.1 Add `FormatUnmatchedForHumans` method to `ArchitectureDiagnosticFormatter`
- [x] 6.2 Update `FormatResultForCiArtifacts` to include `unmatched_ignored_violations` JSON field
- [x] 6.3 Add `FormatUnmatchedForCiArtifacts` for per-contract CI artifact output

## 7. CLI Integration

- [x] 7.1 Collect unmatched ignores from all contract checks in `Program.cs` (both strict and audit modes)
- [x] 7.2 Compute `passed` with `unmatchedSeverity == error && unmatched.Count > 0`
- [x] 7.3 Render unmatched section in human output after violations/cycles
- [x] 7.4 Pass unmatched ignores to JSON formatter

## 8. Tests

- [x] 8.1 Unit tests for `ArchitectureIgnoreUsageTracker` (MarkMatched, IsMatched, empty state)
- [x] 8.2 Unit tests for `IsIgnored` with tracker: exact, wildcard, overlapping entries, untouched entries
- [x] 8.3 Integration test: dependency contract — matched ignore + unmatched ignore + mixed
- [x] 8.4 Integration test: layer contract — matched/unmatched ignores
- [x] 8.5 Integration test: cycle contract — matched/unmatched ignores
- [x] 8.6 Integration test: method-body contract — matched/unmatched ignores (Roslyn + IL)
- [x] 8.7 Integration test: `warn` severity does not fail, `off` skips detection
- [x] 8.8 Integration test: unmatched ignores in both strict and audit modes
- [x] 8.9 Run acceptance gate: `rtk make acceptance`

## 9. Documentation

- [x] 9.1 Update `docs/reference/yaml-schema.md` with `analysis.unmatched_ignored_violations`
- [x] 9.2 Update `docs/reference/yaml-schema.md` with unmatched diagnostic output format
- [x] 9.3 Update AI-facing guidance in `docs/ai/` if applicable
