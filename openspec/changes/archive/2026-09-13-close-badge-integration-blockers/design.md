## Context

See `proposal.md` for the motivation. The current setup has four independent
representations of the same runtime contract: live provider inspection,
generated GitHub workflow context, generated artifact transport, and doctor
endpoint resolution. Renewal cost calculation and cron rendering also derive
their daily counts independently, so a fix must keep those two calculations in
agreement.

## Goals / Non-Goals

**Goals:**

- Make Relay readiness depend on documented Cloudflare account and capability
  responses while leaving `provider_plan` as operator cost metadata.
- Make the generated producer executable on the real `pull_request` event and
  preserve bound manifest identity.
- Ensure both generated artifacts are attempted and fail loudly when missing.
- Keep every cyclic renewal interval at least the configured cadence and make
  the preview count equal the rendered slot count.
- Make doctor and publisher use the same fixed raw publication branch.

**Non-Goals:**

- Introducing a Cloudflare plan catalogue or billing API dependency.
- Changing the trusted reusable publisher, OIDC contract, or Relay runtime.
- Guaranteeing that GitHub executes a scheduled job exactly on time.
- Replacing POSIX cron with a new scheduler service.

## Decisions

1. **Use capability endpoints, not an account plan slug.** The Account Details
   response is used only to verify the requested account ID. Successful,
   bounded Worker-script and Durable Object namespace responses establish the
   provider capability, while the nullable closed `provider_plan` value remains
   an optional operator cost label. Requiring `result.plan.slug` would make a valid
   documented response unusable and would turn metadata into an unsupported
   authority.

2. **Bind manifest context through workflow expressions.** The producer exports
   `github.event.pull_request.number`, `.base.ref`, and `.base.sha` into named
   environment variables before the Python manifest step. This preserves a
   simple shell/Python boundary while using GitHub's supported event model.
   The PR number, run ID, and run attempt are explicitly converted to JSON
   integers before manifest serialization, matching the promotion model rather
   than relying on shell environment strings. Existing valid default variables
   such as repository and head values remain available, but the two reported
   nonexistent variables are removed.

3. **Remove the workspace-only artifact guard.** `actions/upload-artifact` is
   given the exact runner-temporary paths and `if-no-files-found: error` remains
   the authoritative presence check. `hashFiles()` is not used because it is a
   workspace-scoped expression and cannot prove a file outside that scope.

4. **Use a cyclic floor schedule.** Cron slots are generated for elapsed times
   `0, cadence, 2*cadence, ...` only while the next cadence still fits inside
   the 1,440-minute UTC day. This yields `floor(1440 / cadence)` slots, and the
   overnight gap is the remainder-adjusted final gap, never less than the
   cadence. The same floor formula is used by the cost preview. Grouping slots
   by minute with explicit hour lists preserves exact non-hour-aligned times.

5. **Centralize the raw publication branch.** The publisher/README contract
   already uses `architecture-health-badge`; doctor will use the same fixed
   constant. The producer's `base_ref` continues to mean the branch receiving
   the required check and triggering publication, not the artifact storage
   branch.

## Risks / Trade-offs

- [Non-divisible cadences have one longer overnight gap] → The requested
  cadence is a minimum interval and the preview reports the lower, exact slot
  count; no extra job is added to create an unsafe short gap.
- [Provider APIs can deny one capability endpoint independently] → Keep account
  identity, provider capability, and quota as separate observations so setup
  fails closed with the existing actionable prerequisite diagnostics.
- [Upload steps run after a failed producer] → `if-no-files-found: error`
  preserves explicit evidence failure and avoids a false successful run.
- [The fixed publication branch is not the configured producer base ref] → The
  generated registry, README, and doctor URL are tested together against the
  same branch literal.
