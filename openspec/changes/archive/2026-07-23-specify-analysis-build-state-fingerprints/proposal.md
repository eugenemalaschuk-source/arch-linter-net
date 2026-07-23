## Why

Issue #362 cannot implement build-state preflight, `--ensure-built`, or stale-assembly detection safely until the shared **Analysis and build state** slice from #355 defines what counts as the same build input and analysis input, the expected output for a selected project/configuration/TFM, and sufficient evidence that an artifact was built from those build inputs. Guessing that contract inside #362 would create incompatible identities for the immutable snapshot (#363), cache (#365), acceptance corpus (#366), profiling (#374), and cancellation (#375).

The current product can discover projects and load assemblies, but it has no versioned, portable fingerprint model and no normative separation between build identity, policy-dependent analysis identity, physical artifact evidence, diagnostic-only machine data, and process-local snapshot ownership. Timestamp-only freshness is insufficient, while absolute paths, timestamps, and process-local handles are unsuitable as portable identity fields.

## What Changes

- Add a new `analysis-build-state-fingerprints` capability defining a versioned canonical fingerprint envelope and seven related identity layers: project evaluation, effective build inputs, effective analysis inputs, expected build output, verified artifact set, completed analysis session, and Testing snapshot ownership.
- Keep artifact freshness independent from architecture policy changes: receipts/compiler evidence bind the effective build-input fingerprint; the completed analysis identity adds effective policy/configuration provenance and requested analysis views separately.
- Define portable normalization across Windows, Linux, and macOS: repository-relative `/` paths, ordinal canonical ordering, SHA-256 content digests, explicit configuration/TFM/RID fields, and no absolute checkout path in stable identity.
- Define authoritative artifact verification from an ArchLinterNet build receipt or equivalent supported compiler-produced evidence (portable PDB document checksums/compilation metadata plus PE metadata and resolved references). Timestamps and file size remain supporting evidence only.
- Define fail-closed build-state categories for missing, stale, wrong-configuration, wrong-TFM, wrong-project/output association, inconsistent dependency, restore-required, and unverifiable states.
- Define preparation semantics for ordinary validation, `--ensure-built`, `--no-restore`, caller-supplied structured build commands, offline execution, and cancellation. Policy YAML can never enable or control build execution.
- Define immutable snapshot ownership and reuse rules for CLI and `ArchLinterNet.Testing`, plus downstream constraints for cache, profiling, and cancellation.
- Add an internal analysis/build-state architecture blueprint. This design change does not implement #362 or advertise the planned behavior as an available product capability.

## Capabilities

### New Capabilities

- `analysis-build-state-fingerprints`: portable build and analysis input identity, exact artifact verification identity, preflight state classification, explicit preparation behavior, and immutable snapshot ownership for the 0.5.1 analysis/build-state slice.

### Modified Capabilities

- None. Existing validation, project-discovery, CLI, Testing API, cache, and timing capabilities remain unchanged until their implementation issues adopt this contract.

## Impact

- `openspec/changes/specify-analysis-build-state-fingerprints/`: normative proposal, design, task plan, and delta specification.
- `docs/internal/analysis-build-state-blueprint.md`: implementation blueprint and field-classification tables for #362, #363, #365, #374, and #375.
- `docs/internal/README.md`: internal-documentation index entry.
- No production code, public policy schema, public command, exit code, JSON/SARIF schema, Testing API, or capability-manifest behavior changes in this design-only PR.
