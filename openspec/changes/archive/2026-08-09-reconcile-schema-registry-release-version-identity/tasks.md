## 1. Release identity guidance

- [x] 1.1 Update the packaged README and public schema/release references to distinguish the `0.6.0` product line from the embedded `0.5.1` schema registry.
- [x] 1.2 Verify every documented immutable `$schema` URL remains an identity shipped by the registry.

## 2. Packaged-artifact regression coverage

- [x] 2.1 Add focused CLI/Core tests for the explicit product-to-registry mapping and the packaged README.
- [x] 2.2 Extend package validation to compare an installed packed tool, its schema list, the packed README, and supported schema URLs.

## 3. Validation and synchronization

- [x] 3.1 Run focused tests, formatting, package inspection, and full acceptance.
- [x] 3.2 Synchronize and archive the OpenSpec change after validation passes.
