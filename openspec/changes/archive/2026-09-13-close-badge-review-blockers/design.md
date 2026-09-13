## Context

The existing writer always adds the Relay renewal file whenever the adapter is
Relay, even when the approved configuration disables renewal. Its cron helper
also falls back to `0 * * * *` for cadences that do not divide an hour, so the
generated trigger count can exceed the setup cost preview. See `proposal.md`
and the badge-turnkey-setup delta for the externally visible contract.

## Goals / Non-Goals

**Goals:**

- Make generated files, registry event claims, and managed paths agree with the
  renewal-enabled setting.
- Represent every supported cadence exactly as bounded daily UTC trigger slots.
- Preserve the existing trusted publisher, Relay OIDC, cost model, and workflow
  pins.

**Non-Goals:**

- Changing renewal authorization, lease semantics, or provider quotas.
- Guaranteeing that GitHub executes every scheduled event on time; the design
  only guarantees the configured schedule's cost bound.
- Adding a scheduler service or changing the reusable publisher workflow.

## Decisions

1. **Condition the complete renewal artifact set on `renewal.enabled`.** The
   writer will gate both the renewal workflow and its managed path on the same
   configuration flag. The existing registry `permitted_events` projection
   remains the corresponding trust-boundary signal (`push` only when disabled,
   `push` plus `schedule` when enabled).

2. **Generate exact daily slots rather than rounding to an hourly cron.** For a
   valid cadence `C`, enumerate `k * C` minutes for `k * C < 1440`, then group
   those instants by minute-of-hour and emit one POSIX cron entry per minute
   group with an explicit hour list. This keeps the generated YAML valid for
   non-hour-aligned values such as 90 minutes while preserving exactly
   `ceil(1440 / C)` trigger slots. Hour-aligned values naturally collapse to a
   compact expression such as `0 */2 * * *`.

3. **Test the rendered artifact, not only the in-memory cost.** Regression tests
   will write the generated bundle, inspect the actual workflow/registry/managed
   files, and count the rendered cron slots. This protects the relationship
   between preview output and the files an adopter reviews and commits.

## Risks / Trade-offs

- **Non-hour-aligned cadences can require several cron entries.** → Entries
  remain deterministic, explicit, and bounded by the number of intended daily
  slots; GitHub Actions supports multiple `schedule` events in one workflow.
- **Scheduled workflows are best effort and may be delayed or dropped under
  provider load.** → Keep origin expiry and fail-closed renewal semantics
  unchanged; the guarantee applies to configured maximum cost, not delivery
  timing.

## Migration Plan

Regenerate an existing managed bundle with renewal disabled if its configuration
has `renewal.enabled: false`; setup's existing conflict/rollback behavior
protects manual files. Enabled bundles are regenerated with the exact schedule
for their configured cadence. No remote migration or trust-registry change is
needed beyond reviewing the generated diff.
