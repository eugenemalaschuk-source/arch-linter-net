## Context

The Community .NET engine is EAP and its cloud token is optional. A separate direct Docker
invocation avoids Qodana Cloud, PR write permissions and another action/CLI dependency.

## Decisions

Use the version-pinned Community image declared in qodana.yaml. Capture its immutable resolved
image ID and reuse that ID for every scan in the same run. Analyze ArchLinterNet.slnx with the
recommended inspection profile and no baseline/exclusions. Let Community perform its own build;
do not duplicate acceptance, restore or release matrices.

Run on ephemeral GitHub-hosted Linux with contents: read and no persisted checkout credentials.
Do not pass host environment variables or mount a Docker socket into the scanner. Preserve
scanner output as inert artifacts, not workflow commands or privileged downstream input.

Use no cross-run cache during burn-in. An optional same-job warm scan measures cache benefit
without PR-to-main cache poisoning. A standalone generated probe never enters the solution.

## Risks and Open Questions

The exact image must prove .NET 10/.slnx compatibility in CI. A release-line tag is a version
pin, not an immutable manifest lock across runs; record resolved identity and never compare
runs with different identities as deterministic reruns. Pin a reviewed digest during burn-in.
Initial inventory triage, actual billing, fork execution and the reviewed promotion decision
remain acceptance work under #864; infrastructure success alone cannot close adoption.
