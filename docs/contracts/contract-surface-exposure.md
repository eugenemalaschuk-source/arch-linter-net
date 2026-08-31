# Contract-surface exposure contracts

Contract-surface exposure contracts prevent selected exported types from exposing selected architectural types through their visible signatures. They answer a different question from dependency contracts: *can this type cross the boundary of a public, protected, or protected-internal contract?*

Groups:

- `strict_contract_surface_exposure`
- `audit_contract_surface_exposure`

## Example

```yaml
contracts:
  strict_contract_surface_exposure:
    - id: public-api-must-not-expose-persistence
      name: public-api-must-not-expose-persistence
      source:
        assemblies: [Acme.Ledger.Api]
        types_matching:
          has_attribute: Acme.Ledger.Api.PublicApiContractAttribute
      forbidden:
        - layer: Persistence
      reason: Persistence types are implementation details and must not appear in published contracts.
```

The source selects exported roots only. All populated source constraints combine with **AND** semantics:

- `assemblies` selects target assemblies by name;
- `projects` selects the assemblies resolved from declared project names;
- `types_matching` narrows roots with the bounded public-API surface selector vocabulary;
- `public_api_surface` uses the effective selected roots from an existing `public-api-surface` contract, by that contract's `id`.

At least one source constraint is required. `types_matching` uses `name_suffix`, `name_prefix`, `namespace`, `layer`, `base_type`, `implements_interface`, `has_attribute`, and `role`. Every populated selector field combines with **AND** semantics. The selector observes an existing semantic role; it never adds, changes, or replaces a role.

`forbidden` contains one or more of the same bounded selectors. Each selector is an AND-combined type match; the list is OR-combined, so a referenced type matching any entry is forbidden.

### Reusing a reviewed API surface

Use `public_api_surface` when the boundary is already deliberately defined by a reviewed public-API contract:

```yaml
contracts:
  strict_public_api_surface:
    - id: ledger-api
      name: ledger-api
      assemblies: [Acme.Ledger.Api]
      surface_selector:
        has_attribute: Acme.Ledger.Api.PublicApiContractAttribute
      api_snapshot: architecture/api/ledger-api.public-api.txt
      reason: The intentionally published API is reviewed in this snapshot.

  strict_contract_surface_exposure:
    - id: ledger-api-no-persistence-types
      name: ledger-api-no-persistence-types
      source:
        public_api_surface: ledger-api
      forbidden:
        - role: PersistenceModel
        - namespace: Acme.Ledger.Persistence
      reason: The reviewed API must not disclose persistence implementation types.
```

This consumes the existing public-API materialization; it does not read, alter, or create API snapshot membership. A type selected into the reviewed API remains its existing role—for example, a selected `ValueObject`, `Entity`, or `Controller` does not become an API-specific role.

## What is inspected

For every selected exported root, the checker uses the recursive visible-signature index. It follows each visible signature position through nested generic arguments, tuple elements, array and wrapper element types, and other metadata-supported signature shapes. A violation is emitted for each matched forbidden occurrence.

The diagnostic identifies the declaring source type, forbidden target type, source and target assembly, selected source surface, and a deterministic exposure path. Human output, JSON, and SARIF carry the same path-rich projection, so a generic or tuple leak can be located without reconstructing the signature manually. Baseline attribution keeps that normalized finding identity, allowing a reviewed occurrence to be tracked without masking a distinct path.

## Applicability and failures

The contract is fail-closed for evaluation evidence. It produces normal applicability evidence and is unassessable when any required input cannot be assessed, including an unresolved API-surface source, incomplete recursive exposure evidence, an incomplete type universe, zero selected source roots, or a forbidden selector that matches no target type. `strict` and `audit` use the same normalized findings and applicability records; the selected group controls the consuming validation mode.

`ignored_violations` uses the normal contract suppression lifecycle. Keep suppressions narrow and tied to the particular source/target occurrence and reason.

## Scope

This family is static contract-surface exposure analysis only. It does not:

- modify reviewed public-API snapshots or decide API membership;
- introduce multi-role classification or mutate a type's existing semantic role;
- evaluate runtime behavior, endpoint routing, serialization configuration, dependency injection, or data flow;
- replace dependency, type-placement, or binary/package compatibility checks.
