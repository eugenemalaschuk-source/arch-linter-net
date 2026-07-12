## Why

Contextual dependency contracts can identify a direct cross-context reference,
but cannot express the approved semantic seam through which that reference is
permitted.  Issue #173 requires deterministic, YAML-first contracts that make
ports, adapters, and anti-corruption layers reviewable static boundaries.

## What Changes

- Add a semantic port-boundary contract family for allowing a contextual target
  only through an explicitly selected port or API seam, while reporting direct
  domain, infrastructure, and concrete-adapter references.
- Add deterministic adapter-to-port consistency checks based on compiled
  interface implementation facts and reviewed port metadata.
- Add anti-corruption boundary rules that require a selected seam and report
  direct prohibited references without inferring permissions from names.
- Extend the semantic role catalog with the approved port/adapter vocabulary
  and document its static-analysis limits, strict/audit behavior, baseline
  support, and structured diagnostics.

## Capabilities

### New Capabilities

- `semantic-port-boundary-contracts`: YAML-first static contracts for approved
  port, adapter, and anti-corruption dependency seams.

### Modified Capabilities

- `semantic-role-catalog`: Define the reviewed port, adapter, and
  anti-corruption vocabulary and its allowed metadata.
- `contract-family-registry`: Register the strict and audit port-boundary
  families with normal execution and baseline behavior.
- `violation-reporting`: Preserve port-boundary seam evidence in human and
  machine-readable diagnostics.

## Impact

The Core policy schema, policy loader, contract-family registry, validation
handlers, violation/reporting model, JSON output, fixtures, and documentation
will be updated. No runtime DI, HTTP/RPC, service-call, or database behavior is
inspected, and no new runtime dependency is introduced.
