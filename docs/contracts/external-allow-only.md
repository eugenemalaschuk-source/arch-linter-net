# External Allow-Only Contracts

External allow-only contracts use whitelist semantics: a source layer may reference only the declared `external_dependencies` groups explicitly listed in `allowed`. This is the external-dependency counterpart to [allow-only contracts](allow-only.md), which apply the same whitelist semantics to first-party layers.

Groups:

- `strict_external_allow_only`
- `audit_external_allow_only`

## Example

```yaml
external_dependencies:
  approved_sdk:
    namespace_prefixes:
      - MyApp.Vendor.ApprovedSdk
  unsupported_sdk:
    namespace_prefixes:
      - MyApp.Vendor.LegacySdk

contracts:
  strict_external_allow_only:
    - id: adapter-approved-sdk-only
      name: adapter-may-only-reference-approved-sdk
      source: infrastructure_adapter
      allowed: [approved_sdk]
      reason: This adapter may only use the approved SDK, not any other declared vendor dependency group.
```

## When to use

Use external allow-only contracts when a layer's external surface should be a small, explicitly enumerated set rather than an ever-growing blocklist:

- domain code that may use only BCL primitives (declare no `allowed` groups and no BCL group);
- an infrastructure adapter that may only use one specific SDK, not any other vendor SDK the codebase has declared;
- Unity runtime code that may use runtime-safe Unity namespaces but not editor-only or other vendor namespaces.

## Semantics

For a source layer, every group declared in the policy's top-level `external_dependencies` section that is **not** present in the contract's `allowed` list is evaluated as disallowed. A reference matching a disallowed group is a violation; a reference matching an allowed group is not.

Only *declared* external dependency groups are ever evaluated. A reference that does not match any group declared in `external_dependencies` is never reported as a violation by this contract family — regardless of whether it is a BCL/system reference, a first-party reference, or anything else undeclared. In practice this means:

- **BCL/system references are not implicitly flagged.** If a policy never declares an external dependency group matching BCL namespaces (e.g. `System`), references to `System.*` types are never in scope for this contract and are never violations.
- **BCL/system references are only in scope if a policy author explicitly declares a matching group.** If a policy declares a group such as `bcl_http` with `namespace_prefixes: [System.Net.Http]`, and that group is not in a contract's `allowed` list, references to `System.Net.Http` types are evaluated exactly like any other disallowed external group.

An `allowed` entry naming a group that does not exist in `external_dependencies` (for example, a typo) has no effect: it can't exclude anything from the disallowed-group set because it never matches a real declared group. The contract fails closed — a misspelled `allowed` entry can only make the contract more restrictive than intended, never less.

Every group that becomes disallowed for at least one `external_allow_only` contract (any declared group not in that contract's `allowed`) must declare at least one non-empty `namespace_prefixes` or `type_prefixes` matcher, the same requirement [external dependency contracts](external-dependencies.md) already enforce for `forbidden` groups. A disallowed group without a usable matcher fails validation with an `invalid external dependency group` configuration diagnostic instead of silently never matching anything. A group that is *only* ever referenced through some contract's `allowed` list (never disallowed) is not required to have a matcher, since it is never evaluated.

Use `allowed_types` for narrow exact type exceptions within an otherwise-disallowed group:

```yaml
contracts:
  strict_external_allow_only:
    - id: adapter-approved-sdk-only
      name: adapter-may-only-reference-approved-sdk
      source: infrastructure_adapter
      allowed: [approved_sdk]
      allowed_types:
        - MyApp.Vendor.LegacySdk.SharedTypes.CorrelationId
      reason: One legacy type is temporarily permitted while the adapter migrates off the legacy SDK.
```

`allowed_types` entries are exact full type names, not namespace patterns — the same convention as the namespace-level [allow-only contract](allow-only.md)'s `allowed_types`.

Violations identify the source type, the disallowed external dependency group, the matched reference(s), and the contract's full `allowed` group list (so the diagnostic makes clear what *is* permitted, not just what isn't), using the same violation shape as [external dependency contracts](external-dependencies.md).

`ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families.

## Scope: what's not covered here

Detection is **type-level only** (base types, interfaces, fields, properties, method signatures, generic arguments) — the same detection [external dependency contracts](external-dependencies.md) used before method-body IL scanning was added as a separate follow-up. Method-body-only references (a forbidden external type used only inside a method body, not visible through type-level metadata) are not detected by this contract family yet; this is a deliberate MVP scope decision, not an oversight, and may be added as a symmetric follow-up.
