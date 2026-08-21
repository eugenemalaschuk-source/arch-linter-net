## Why

The initial OCP-pressure projection does not preserve the forensics capability's
canonical file-finding total order and overstates the per-finding provenance it
actually exposes. Correcting these contracts prevents rank/JSON byte drift and
keeps the OCP specification consistent with the established ignore boundary.

## What Changes

- Make OCP findings use the common same-category ranking order after score ties.
- Clarify that OCP findings exist only for retained, non-ignored logical files.
- Narrow per-finding provenance to the pair-exclusive TaskKey-pair evidence that
  the OCP projection actually carries.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `release-architecture-forensics`: Correct OCP ranking, retained-file scope,
  and per-finding evidence wording.

## Impact

Core OCP ordering and its regression tests change, along with the existing
Release Architecture Forensics specification. No public API, configuration, or
new dependency is introduced.
