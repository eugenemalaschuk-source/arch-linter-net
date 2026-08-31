## 1. Measurement setup isolation

- [x] 1.1 Carry the requested metric IDs into measurement snapshot construction and select definitions before runner setup; verify an unknown ID retains the existing invalid-argument behavior.
- [x] 1.2 Add a Core regression with an ordinary selected metric and an unavailable unselected project metric; verify the selected measurement succeeds without project-artifact setup.

## 2. External dependency projection

- [x] 2.1 Index topology classifications by canonical identity in external dependency grouping; verify existing external-group contributor and applicability tests pass.

## 3. Validation and lifecycle

- [x] 3.1 Run focused Core metric tests, OpenSpec validation, formatting verification, and architecture lint; archive the completed change after checks pass.
