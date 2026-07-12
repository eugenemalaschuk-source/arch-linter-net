## Context

The existing contextual dependency and allow-only families match one resolved
role/metadata tuple at each end of a direct compiled-type reference. They do
not identify an approved seam, validate an adapter's claimed port, or describe
why a direct cross-context edge is prohibited. The semantic classification
model intentionally assigns a type only one winning role, so a port boundary
must not rely on accumulating `ApplicationLayer`, `Port`, and `Adapter` tags.

## Goals / Non-Goals

**Goals:**

- Add schema-backed strict and audit port-boundary contracts with deterministic
  direct-reference and interface-implementation evidence.
- Allow a source context to reach a target context through an explicit port
  selector while rejecting direct domain, infrastructure, and adapter targets.
- Validate an adapter's declared port metadata against its implemented
  interface and approved adapter location.
- Provide structured, explainable diagnostics and normal baseline handling.

**Non-Goals:**

- Runtime DI/container resolution, HTTP/RPC execution, database access
  observation, or data-flow analysis.
- Naming-based permission inference, unrestricted expressions, or broad
  implicit port exemptions.
- Changes to existing contextual dependency or allow-only semantics.

## Decisions

### Dedicated port-boundary contract family

Introduce `strict_port_boundaries` and `audit_port_boundaries`, rather than
overloading contextual dependency contracts. Each rule has a source selector,
a target-context selector, an allowed seam selector, an explicit forbidden
target selector list, and a reason. This keeps direct-edge semantics clear and
prevents an allowed port from becoming a general exemption.

The target context is matched by metadata on the target type. A target is
allowed only when it matches both the target context and a listed allowed seam;
forbidden candidates match both the target context and a forbidden selector.
The rule fails policy configuration when its selectors are ambiguous or when
the same target matches both allowed and forbidden selectors.

Alternative considered: combine `allowed` and `forbidden` into contextual
allow-only. Rejected because it cannot state the expected seam or distinguish a
direct prohibited edge from a missing/mismatched port claim.

### Adapter binding uses compiled interface facts

An adapter-boundary rule selects adapters by contextual selector, declares the
expected port selector, and requires a matching implemented interface selected
by resolved role/metadata. Type definitions and their full interface sets are
the only evidence. The adapter's location is constrained with existing
role/metadata selectors rather than a new location DSL.

Alternative considered: infer bindings from type/interface names. Rejected as
non-deterministic and contrary to the YAML-first requirement.

### ACL rules share the same direct-edge engine

Anti-corruption requirements are modeled as port-boundary rules whose allowed
seam selects `AntiCorruptionLayer` (or an explicitly mapped custom role). A
direct database/infrastructure target remains an explicit forbidden selector.
This avoids a second, overlapping traversal and preserves ordinary strict,
audit, baseline, and diagnostic behavior.

### Reporting retains structured seam evidence

Port-boundary violations include source and target role/metadata, evidence
kind (`direct_reference` or `interface_implementation`), expected seam,
matched forbidden selector, and a safe remediation hint. JSON transports the
same data, making SARIF mapping possible without parsing text.

## Risks / Trade-offs

- [A single-role model cannot express multiple architectural tags] → Rules use
  distinct explicitly classified port/adapter types and contextual metadata.
- [Compiled metadata lacks call-site intent] → Only direct type references and
  interface implementations are evaluated; unavailable evidence is reported
  as configuration/coverage diagnostics instead of guessed.
- [A broad selector weakens a boundary] → Require explicit reason and reject
  overlapping allowed/forbidden selector matches.
- [Existing registry order is pinned] → Append the new family deliberately and
  update its registry/order tests.
