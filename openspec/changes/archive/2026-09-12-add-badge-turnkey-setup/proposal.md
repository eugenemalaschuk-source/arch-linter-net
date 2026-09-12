## Why

The repository now ships the trusted promotion contract and Relay runtime, but a new consumer still has to assemble configuration, deployment files, workflows, and README wiring by hand. This change closes that product gap with a deterministic, reviewable setup/doctor surface that can be used from a packed release without source-specific scripts or hidden hosting assumptions.

## What Changes

- Add a versioned setup/doctor command surface for inspecting repository visibility and capabilities, previewing disclosure/cost choices, validating prerequisites, and writing an idempotent setup plan.
- Define a closed, versioned setup configuration schema with explicit `none`, public `github-raw`, and adopter-owned `relay` modes, disclosure profile, opaque alias, cadence, pins, and bounded cost/resource settings.
- Generate deterministic Relay deployment assets, Worker/Durable Object bindings and migrations, registry entries, producer/publisher/renewal workflows, and a reviewable README patch from approved templates.
- Make setup dry-runs and doctor read-only; make repeated setup, conflicts, partial failures, rollback, and retries deterministic and fail closed.
- Add machine-readable diagnostics with concise human remediation for unsupported versions, visibility/profile conflicts, missing permissions or plans, invalid pins/OIDC claims, unavailable destinations, missing evidence, expiry, quota, and cache observations.
- Add a versioned distributable bundle manifest and packed-consumer tests so the setup path does not depend on the source checkout or embedded handwritten scripts.

## Capabilities

### New Capabilities

- `badge-turnkey-setup`: Versioned setup, deployment-template generation, consumer workflow wiring, deterministic lifecycle, and doctor diagnostics for the supported badge transports.

### Modified Capabilities

<!-- Existing promotion and Relay requirements remain authoritative; this change supplies their consumer-facing setup and distribution boundary. -->

## Impact

- Adds a CLI/setup application layer and versioned setup/configuration schemas under `tools/` and `schema/`.
- Adds deterministic Relay bundle templates and release inventory metadata under `.github/`/`relay/`.
- Adds setup/doctor unit and packed-consumer tests plus executable adoption documentation.
- Uses the existing promotion registry, OIDC contract, Relay protocol, canonical badge projector, and release authority; it does not change Health semantics, public v0.8.0 assets, or introduce a project-operated hosting service.
