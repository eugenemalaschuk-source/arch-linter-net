## 1. Contract and live provider inspection

- [x] 1.1 Update the badge turnkey delta spec for documented Cloudflare account
  responses and metadata-only provider plans.
- [x] 1.2 Remove the undocumented `result.plan.slug` dependency and keep account,
  Worker, Durable Object, and quota observations fail-closed and separate.
- [x] 1.3 Add live-inspector regression coverage with a documented Account
  Details response that has no `plan.slug`, plus an unavailable capability
  control.
- [x] 1.4 Keep nullable `provider_plan` as metadata only so proven live
  capabilities can produce a valid Relay plan without a plan label; add an
  engine regression test.

## 2. Producer workflow integration

- [x] 2.1 Replace nonexistent GitHub environment variables with explicit
  pull-request event context bindings for PR number, base ref, and base SHA.
- [x] 2.2 Remove `hashFiles()` guards around runner-temporary artifacts while
  preserving explicit upload failure semantics.
- [x] 2.3 Add template assertions covering context bindings, no stale variables,
  numeric provenance types, and both upload paths.

## 3. Renewal and doctor correctness

- [x] 3.1 Align cost preview and cron generation on a floor-based cyclic slot
  count that preserves the minimum configured interval across midnight.
- [x] 3.2 Add 31-minute/59-minute schedule regression tests that expand the
  rendered cron slots and verify cyclic gaps and preview counts.
- [x] 3.3 Make github-raw doctor use the publisher's fixed publication branch
  and assert the exact probed URI for a non-default base ref.

## 4. Validation and delivery

- [x] 4.1 Run focused BadgeSetup tests, packed disclosure coverage, formatting,
  architecture/public-API/docs checks, and OpenSpec validation.
- [x] 4.2 Run the owning CLI tests and inspect the final diff for unrelated
  changes before updating PR #858.
- [x] 4.3 Archive this OpenSpec change and publish the implementation/validation
  summary to issue #832 and PR #858.
