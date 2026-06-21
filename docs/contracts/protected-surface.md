# Protected Surface Contracts

Protected surface contracts invert the usual dependency question.

Instead of asking "what must this source not reference?", they ask "who is allowed to reference this protected target?"

Groups:

- `strict_protected`
- `audit_protected`

## Example

```yaml
contracts:
  strict_protected:
    - id: core-internals-protected
      name: core-internals-are-protected
      protected: [core_internal]
      allowed_importers: [core]
      reason: External layers must use the public Core surface only.
```

## Semantics

Any reference to a protected layer from outside `allowed_importers` is a violation.

Self-references inside the protected layer are allowed.

## Allowed types

Use `allowed_types` for narrow exact exceptions:

```yaml
contracts:
  strict_protected:
    - id: internals-protected
      name: internals-protected
      protected: [internal_api]
      allowed_importers: [public_api]
      allowed_types:
        - MyApp.Internal.LegacyCompatibilityShim
      reason: Only the public API layer may import internal APIs.
```

Use exceptions sparingly and track migration debt with issue-linked reasons.
