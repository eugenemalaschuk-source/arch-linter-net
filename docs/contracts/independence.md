# Independence Contracts

Independence contracts enforce mutual separation across a set of layers.

Groups:

- `strict_independence`
- `audit_independence`

## Example

```yaml
contracts:
  strict_independence:
    - id: bounded-contexts-independent
      name: bounded-contexts-must-be-independent
      layers:
        - billing
        - shipping
        - notifications
      reason: Bounded contexts must not reference each other directly.
```

## Semantics

For every configured layer pair, references in either direction are violations.

Use this when modules should communicate through explicit public contracts, messaging, APIs, or another boundary outside the forbidden direct references.

## Independence vs cycles

Use independence when any cross-reference is wrong.

Use [cycle contracts](cycles.md) when cross-references are allowed but directed cycles are not.
