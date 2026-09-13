## Why

The current turnkey badge setup can produce a green preview while the generated
producer cannot build a valid promotion manifest, the Cloudflare capability
probe requires a field absent from the documented Account Details response, and
the renewal schedule can run twice within the minimum 30-minute safety bound
around midnight. Doctor also probes a different GitHub raw branch than the
publisher writes. These are integration-level correctness failures on the
trusted setup path and must be closed before PR #858 is mergeable.

## What Changes

- **BREAKING** Treat `--provider-plan` as cost metadata and determine Relay
  readiness from documented Cloudflare account identity and required API
  capabilities, without requiring `result.plan.slug` or a plan label for
  capability satisfaction.
- Generate the producer manifest from documented pull-request event expressions
  rather than nonexistent `GITHUB_EVENT_NUMBER` and `GITHUB_BASE_SHA`
  environment variables.
- Upload producer artifacts from `runner.temp` without an unsupported
  `hashFiles()` guard, while retaining explicit missing-file failures.
- Generate renewal cron slots whose cyclic UTC gaps never fall below the
  configured cadence and keep the cost preview equal to the actual schedule.
- Make github-raw doctor probe the exact branch used by the generated publisher.
- Add packed/integration regression coverage for the Cloudflare response,
  producer manifest/upload wiring, midnight cadence wrap-around, and doctor URL.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `badge-turnkey-setup`: make live provider inspection, producer artifact
  transport, renewal scheduling, and github-raw doctor endpoint selection
  agree with their documented runtime contracts.

## Impact

The changes affect the CLI badge setup capability inspector, setup engine cost
calculation, generated producer and renewal workflow templates, doctor endpoint
resolution, the badge setup OpenSpec contract, and focused NUnit/packed-artifact
tests. No public CLI option is removed, but provider-plan semantics become
explicitly optional metadata-only and generated cadence counts may decrease for values
that do not divide a UTC day.
