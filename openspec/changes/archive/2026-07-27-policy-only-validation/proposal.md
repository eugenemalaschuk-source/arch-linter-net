## Why

Policy authors need fast, deterministic feedback on policy syntax and composition before a solution has been built. Full validation currently couples this feedback to project evaluation and target assemblies, which is unsuitable for clean checkouts, editors, and pre-commit checks.

## What Changes

- Add `arch-linter-net policy check --policy <path>` for assembly-free policy and static-configuration validation.
- Classify completed configuration checks, deferred fact-dependent checks, and failures separately in human, JSON, SARIF, and Testing API projections.
- Reuse import provenance so root diagnostics use root terminology and fragment diagnostics retain their complete import chain and authored location.
- Guarantee that policy checking neither invokes MSBuild nor loads target assemblies, and expose the command through help, capability metadata, and documentation.

## Capabilities

### New Capabilities

- `policy-check-command`: assembly-free policy validation, deferred-check reporting, and deterministic CLI/API behavior.

### Modified Capabilities

- `cli-validation`: add policy-only command dispatch and stable completion/exit behavior.
- `policy-document-validation-pipeline`: run each assembly-independent document and static configuration check once without analysis inputs.
- `diagnostics-model`: represent typed policy configuration and deferred-check diagnostics with provenance.
- `sarif-diagnostics-output`: project policy-check diagnostics and deferred state to SARIF.
- `test-adapter`: expose equivalent policy-only validation results to Testing API consumers.

## Impact

Affected areas include CLI command modules and output writers, Core policy loading and validation services, normalized diagnostics/report writers, Testing API adapters, capability metadata, documentation, and focused integration fixtures.
