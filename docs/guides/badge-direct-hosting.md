# Publish a badge to your own hosting

Use this path when you will maintain the publisher and hosting yourself.
ArchLinterNet produces the architecture result and badge JSON; your integration
verifies their provenance and publishes the approved projection. This is a
consumer-owned integration recipe, **not** an installed ArchLinterNet adapter
or a complete reusable workflow.

For private checks without public disclosure, stop at
[CI integration](ci-integration.md). For the experimental Worker/Durable
Object/OIDC product integration, use [Relay setup](badge-setup.md) instead.
An ordinary Cloudflare Worker is not automatically an ArchLinterNet Relay.

## Before enabling publication

Have these three pieces reviewed and working:

1. A producing workflow that emits canonical Health, badge JSON, and a bounded
   private provenance manifest. Select [PR/post-merge or nightly](badge-adoption.md#choose-the-producing-workflow).
2. An isolated trusted publisher that verifies the manifest against GitHub's
   run/PR/commit metadata, not against other untrusted artifact fields alone.
3. Your destination and serving implementation: fixed public routes, an
   unavailable initial state, an absolute validity limit, and an operator who
   can revoke or repair publication.

The CLI does not generate this direct publisher, its manifest format, or your
Worker deployment script. Do not invoke `setup --mode relay` to provision an
unrelated direct endpoint. Keep publication disabled until the three pieces
above pass the acceptance checks below.

## Producer and verifier contract

Keep the full evidence private. Allow only the bounded canonical badge JSON
and, if used, a fixed-template SVG to leave the repository. A renderer may draw
the CLI's label/message/color; it must escape text, reject active SVG content,
and never calculate a new Health category or substitute missing counts.

Before accepting a publication, verify:

| Input | Required comparison |
| --- | --- |
| Repository and producer | Expected repository identity, event/ref, reviewed workflow and job/check, and qualifying result. Do not select an arbitrary successful workflow. |
| Source | Actual analyzed commit/tree; PR number, base and head for PR evidence. For post-merge publication, prove the merged PR relationship and equality with the accepted target tree. For nightly, bind the exact selected main source. |
| Run | Exact producer run ID **and attempt**, checked through trusted provider metadata. An artifact with the same name from another attempt is not interchangeable. |
| Files | One permitted artifact set, schema/kind, byte-size limits and SHA-256 digests. Reject extra paths, traversal, symlinks, malformed JSON and ambiguous duplicates before use. |
| Meaning | Canonical Health and badge projection from the intended CLI version and analysis context. A workflow success or self-authored hash alone does not prove the result. |
| Freshness | Qualifying evidence and a finite absolute expiry that does not outlive its validity. Retrying the upload cannot create a new evaluation or slide the old expiry. |

Resolve the producer explicitly. Do not select the latest green run anywhere
in the repository, accept caller-provided download URLs, or silently fall back
to an older attempt. A canonical `FAIL` is still an architecture result; a
missing trusted producer is a publication failure. Neither permits inventing
`PASS`.

## Isolate publication authority

The publisher may read Actions/PR/check/commit metadata. Direct hosting does
not need GitHub `contents: write`; only the provider upload needs the provider
credential. Keep it out of PR/fork jobs, artifact files, URLs, and logs.

Use a separate clean job/runner from candidate execution. Passing a secret only
to the last step is **not** sufficient if earlier untrusted code ran in the
same privileged workspace. Trusted publisher code must be reviewed and pinned;
never execute downloaded artifacts or restore executable candidate caches in
that job. See [GitHub's workflow security guidance](https://docs.github.com/en/actions/reference/security/secure-use).

Serialize all writers to the same destination, including manual retries. Do
not rely on cancellation alone to stop an in-flight upload. Recheck source
eligibility immediately before writing: an obsolete run must not overwrite a
newer publication, even with an unavailable payload. Keep the same expiry on
retries. A check against GitHub and a later provider write are not an atomic
transaction; describe the badge as a verified bounded snapshot, not an
instantaneous current-main oracle.

## Cloudflare Worker deployment

A direct Worker can serve only the approved JSON and SVG routes from a reviewed
fixed template. The account and Worker name are configuration reviewed with the
publisher, not values read from PR artifacts. Provision the route and initial
`UNAVAILABLE` response before turning on CI publication.

Configure a scoped provider token using the permissions required by the
[Worker upload API](https://developers.cloudflare.com/api/resources/workers/subresources/scripts/methods/update/).
Restrict it to the intended account/resources as the provider permits; a fixed
Worker name in YAML does not itself restrict token authority. Review billing
and quota limits. No Durable Object registry or Relay bootstrap is needed for
this direct serving design. For a `workers.dev` URL, also configure
[public routing](https://developers.cloudflare.com/workers/configuration/routing/workers-dev/).

The following is **only an upload step** for an existing single-file
Service Worker-format template. Before it runs, your trusted verifier must
have accepted the evidence, rendered `publication/worker.js`, and completed the
source-eligibility check. That file is generated by your integration, not
shipped by ArchLinterNet. ES-module Workers use a different multipart upload
shape; follow the provider's API for that deployment format.

Set `CLOUDFLARE_ACCOUNT_ID` and `CLOUDFLARE_WORKER_NAME` as fixed job
configuration, and store the scoped token as the `CF_API_TOKEN` Actions secret:

```yaml
- name: Upload the verified badge Worker
  env:
    CF_API_TOKEN: ${{ secrets.CF_API_TOKEN }}
  run: |
    set -euo pipefail
    test -n "$CF_API_TOKEN"
    test -f publication/worker.js
    curl --fail --silent --show-error --max-time 60 \
      --request PUT \
      "https://api.cloudflare.com/client/v4/accounts/${CLOUDFLARE_ACCOUNT_ID}/workers/scripts/${CLOUDFLARE_WORKER_NAME}" \
      --header "Authorization: Bearer ${CF_API_TOKEN}" \
      --header 'Content-Type: application/javascript' \
      --data-binary @publication/worker.js \
      --output "$RUNNER_TEMP/badge-deploy.json"
    python3 - "$RUNNER_TEMP/badge-deploy.json" <<'PY'
    import json
    import sys
    with open(sys.argv[1], encoding="utf-8") as source:
        response = json.load(source)
    if response.get("success") is not True:
        raise SystemExit("Provider rejected the badge deployment")
    PY
```

Keep the provider response private. Do not print the token or paste deployment
responses into public issues. The upload step does not verify PR provenance,
render the Worker, or establish that the public URL now serves the right bytes.

## Verify the public responses

In a following step **without** the provider token, fetch each fixed public URL
and compare its bytes with the accepted local projection. This example assumes
your verifier retained the accepted JSON/SVG under `publication/` and your
trusted configuration defines `BADGE_JSON_URL` and `BADGE_SVG_URL`:

```bash
set -euo pipefail
curl --fail --silent --show-error --max-time 30 \
  "$BADGE_JSON_URL" --output "$RUNNER_TEMP/published-badge.json"
cmp publication/architecture-health-badge.json "$RUNNER_TEMP/published-badge.json"
curl --fail --silent --show-error --max-time 30 \
  "$BADGE_SVG_URL" --output "$RUNNER_TEMP/published-badge.svg"
cmp publication/architecture-health-badge.svg "$RUNNER_TEMP/published-badge.svg"
```

A bounded retry may accommodate provider propagation, but retries must compare
again and ultimately fail on a mismatch. A successful PUT alone is not
acceptance. For JSON-only hosting, verify the JSON; do not invent an SVG route.
Link the README image to the public JSON or another approved public explanation,
never to a private artifact URL containing credentials.

## Expiry and failure behavior

Enforce expiry at the origin on reads, including `HEAD` and conditional
requests, before returning a ready payload or `304`. Limit cache lifetime to the
remaining validity interval. Reject unsupported paths/methods. After expiry or
invalid state, serve an explicit unavailable representation, not the previous
healthy result. The four-field JSON must not acquire private provenance or
extra timestamps; any additional public representation needs explicit approval.

Choose the validity bound for your evidence and cadence. Relay's 60-minute
lease and renewal contract do not apply to this independent transport, and a
consumer-specific seven-day limit is not a product default. A stopped publisher
must not keep extending expiry. A skipped nightly is not a refresh.

For a failed verification on the current eligible source, publish an explicit
unavailable representation and fail the publication job. If the provider write
itself fails, you cannot promise the old origin was replaced; report the failure
and rely on its existing finite expiry or an operator action. Preserve newer
state when the failed job is obsolete. Keep architecture failure, unavailable
evidence, and transport failure distinct in private diagnostics.

## Acceptance and operations

Before adding the README image, exercise a real qualifying publication and
record the source, producer/run attempt, accepted digests, deployment identity,
and anonymous JSON/SVG readback privately. Also test wrong-tree/attempt/hash,
missing or malformed evidence, a delayed older writer, provider/readback
failure, expiry with all publishers stopped, and recovery through fresh valid
evidence. Use a separate test destination for destructive lifecycle scenarios.

Document who rotates the provider token, disables writers, makes the origin
unavailable, and removes hosting. Restore readiness only through the verified
publication path. Stopping CI or removing an image is not origin revocation;
origin revocation cannot recall saved images. Diagnose caches separately as
shown in [badge adoption](badge-adoption.md#verify-the-result).
