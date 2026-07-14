## 1. SARIF policy-exception evidence

- [x] 1.1 Reuse the normal policy-location SARIF mapping for typed policy
      exceptions, retaining the primary result location.
- [x] 1.2 Add an exact CLI regression for a root-versus-fragment conflict with
      ordered related declarations, portable URIs, and YAML-path messages.

## 2. Policy-consistency provenance identity

- [x] 2.1 Select consistency provenance by the diagnostic participant IDs when
      IDs are available, with name matching only as the no-ID fallback.
- [x] 2.2 Add a regression with a same-named contract in another family and
      assert only the participating contracts are located.

## 3. Validation and synchronization

- [x] 3.1 Run focused regression tests, `make fmt`, and `make acceptance`.
- [x] 3.2 Synchronize and archive the OpenSpec change, then validate all specs.

> Validation note: `rtk make fmt` ran but its workflow formatter cannot start
> because this Windows environment has no WSL `/bin/bash`. The scoped
> `rtk dotnet format ArchLinterNet.slnx --no-restore --verify-no-changes` and
> `rtk make acceptance` checks passed.
