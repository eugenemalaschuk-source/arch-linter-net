## 1. Diagnostic selection

- [x] 1.1 Bind the specialized coverage-`roots` message to its own instance pointer.
- [x] 1.2 Suppress failures beneath an `if` discriminator.
- [x] 1.3 Treat a satisfied alternative as applicable and a type-incompatible alternative as
  inapplicable.

## 2. Verification

- [x] 2.1 Add composed-policy regressions for location binding, discriminator suppression, and
  satisfied `anyOf` alternatives.
- [x] 2.2 Re-run the packed-artifact consumer-cleanup gate and remove the tracked-defect entry.
