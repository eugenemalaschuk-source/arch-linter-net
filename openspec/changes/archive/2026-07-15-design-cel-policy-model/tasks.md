## 1. Design artifacts

- [x] 1.1 Add a durable internal CEL policy model blueprint under
      `docs/internal/` and link it from `docs/internal/README.md`.
- [x] 1.2 Capture the approved YAML locations, typed contexts, failure
      semantics, reporting expectations, and worked examples in the blueprint.

## 2. OpenSpec source of truth

- [x] 2.1 Add the new `cel-policy-model` delta spec with normative requirements
      for explicit `when` fields, allowed/forbidden locations, typed contexts,
      fail-closed behavior, and compatibility.
- [x] 2.2 Keep the proposal and design aligned with the final spec language and
      #163 implementation boundary.

## 3. Public boundary clarification

- [x] 3.1 Update public capability documentation to state that CEL policy
      expressions are not implemented yet and will never be inferred from
      ordinary strings.

## 4. Validation

- [x] 4.1 Run `rtk openspec validate --all`.
- [x] 4.2 Run `rtk make fmt-docs`.
- [x] 4.3 Run `rtk make acceptance`.
