# Method-Body Contracts

Method-body contracts detect forbidden API usage inside executable bodies using source-level analysis and fallback scanning where supported.

Groups:

- `strict_method_body`
- `audit_method_body`

## Example

```yaml
contracts:
  strict_method_body:
    - id: domain-no-repository-calls
      name: domain-must-not-call-repositories
      source: domain
      forbidden_calls:
        - MyApp.Infrastructure.Repositories.
        - Microsoft.EntityFrameworkCore.DbContext
      reason: Domain code must not call infrastructure APIs.
```

## Matching surface

Forbidden call patterns can represent:

- member names;
- `Type.Member` names;
- fully qualified members;
- namespace or type prefixes where supported by the scanner.

Keep patterns narrow enough to avoid false positives.

## Condition sets

If source uses conditional compilation, configure [condition sets](../policy-format/condition-sets.md):

```yaml
analysis:
  condition_sets:
    runtime: []
    editor: [UNITY_EDITOR]
  default_condition_set: runtime
```

Then run:

```bash
arch-linter-net --condition-set editor
```

## Non-goals

Method-body contracts are static reference checks. They do not validate runtime dispatch, dependency injection resolution, authorization behavior, or semantic data flow.
