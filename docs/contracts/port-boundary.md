# Semantic Port Boundary Contracts

Semantic port-boundary contracts require an explicitly classified port or
anti-corruption seam for cross-context references. They inspect compiled,
direct type references and interface implementation facts only; they do not
inspect DI registrations, HTTP/RPC calls, runtime database access, or proxies.

Groups: `strict_port_boundaries` and `audit_port_boundaries`.

```yaml
contracts:
  strict_port_boundaries:
    - id: sales-to-catalog-through-port
      name: sales-may-reach-catalog-only-through-port
      source: { role: ApplicationLayer, metadata: { domain: Sales } }
      target_context: { metadata: { domain: Catalog } }
      allowed_seams:
        - { role: Port, metadata: { domain: Catalog, name: Catalog } }
      forbidden:
        - { role: DomainLayer, metadata: { domain: Catalog } }
        - { role: Adapter, metadata: { domain: Catalog } }
      adapter_bindings:
        - adapter: { role: Adapter, metadata: { domain: Payment } }
          expected_port: { role: Port, metadata: { domain: Payment } }
          allowed_contexts:
            - { role: Adapter, metadata: { layer: Infrastructure } }
      reason: Sales must use the reviewed Catalog port, never Catalog internals.
```

`target_context` is metadata-only because classification is single-role: a
Catalog `Port` and a Catalog `DomainLayer` cannot both have the same role but
can share reviewed `domain: Catalog` metadata. A target is allowed only when it
matches the target context and an `allowed_seams` selector. A matching
`forbidden` selector produces a direct-edge diagnostic; a port elsewhere never
implicitly exempts a concrete adapter or domain type.

`adapter_bindings` evaluates the adapter's compiled interface set. A selected
adapter must implement its `expected_port`; `allowed_contexts` limits where
that adapter may live. Use `AntiCorruptionLayer` in `allowed_seams` for legacy
CRM/ERP boundaries and explicitly list direct persistence or database adapters
under `forbidden`.

Strict findings fail validation. Audit findings are reported under the normal
audit behavior. Both groups support ordinary `ignored_violations` baselines.

This shape — including the Catalog port seam, the forbidden direct Catalog
reference, and an `AntiCorruptionLayer` seam for a legacy context — is
exercised end-to-end (pass, fail, strict-vs-audit, and JSON diagnostic output)
in `tests/ArchLinterNet.Cli.Tests/PortLayoutCliTests.cs`, and demonstrated in
narrative form in the modular-monolith sample under
`samples/policies/imports/modular-monolith/architecture/policy/bounded-contexts/`.
