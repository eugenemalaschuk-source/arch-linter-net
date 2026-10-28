# Design

## Context

See `proposal.md` for the observed failure. The badge resolver currently selects the job named by the successful check and uses that job's `run_attempt` and ID in `EvidenceContext` and producer identity. The badge manifest binds the attempt and may include the job ID. Temporal receipts bind the producer identity. The PR-report publisher already resolves copied jobs through consecutive attempt history and execution timestamps.

## Goals / Non-Goals

**Goals:** Resolve a selected copied job to the original record for that execution; retain a genuinely rerun producer's later job identity; keep API evidence, manifest validation, identity hashing, and temporal receipts aligned; fail closed when lineage cannot be established.

**Non-Goals:** Change the artifact schema or producer workflow, weaken manifest checks, change badge payloads or publication adapters, edit release-source acquisition, or select a patch version.

## Decisions

1. Keep the selected check's exact run ID and job ID as the entry point. Validate the workflow run and exact job against the configured repository, workflow, event, head, producer name, successful conclusion, and run attempt before resolving lineage.
1. Use GitHub's complete workflow-job history (`filter=all`), paginated and bounded, then group the configured producer job by attempt. Require exactly one configured producer job in each traversed attempt. Starting at the selected job's attempt, walk backward while start and completion timestamps identify the same execution. Each matching successful predecessor becomes the effective job; stop at the first valid different execution, retaining the later attempt and job ID. Missing, malformed, ambiguous, or contradictory history fails closed. Bound total attempts to 51 (the initial execution plus GitHub's documented maximum of 50 reruns) and history to 20,000 job records.
1. Move this provider-specific lineage logic into a small internal badge-promotion helper. `cli.py` is already near 1,000 lines; extracting the existing producer resolver together with its history logic avoids adding another stateful branch there and keeps it independently testable.
1. Populate `EvidenceContext` with the effective job ID and attempt. Existing artifact validation then compares the manifest's attempt (and job identity when present) to those provider-resolved facts. The same effective values feed the producer identity hash and any temporal receipt. The manifest never chooses the expected attempt or job.
1. Preserve the existing verification timestamp, freshness lease, unavailable behavior, and read-only preflight/publication boundaries. No producer workflow, artifact, or action schema update is needed for the observed manifest, which binds attempt 1.

Alternatives rejected: trusting the selected copied job's attempt reproduces the defect; decrementing once fails across multiple partial reruns; always choosing the oldest attempt misbinds a genuinely rerun producer; and deriving the expected identity from the artifact would move authority to untrusted input.

## Risks / Trade-offs

- **Attempt history is unavailable or inconsistent** → fail closed before artifact publication; do not fall back to a job-name-only match.
- **History adds provider reads** → walk only backward from the selected job and stop at the first different valid execution; cap attempts and paginate with explicit limits.
- **Timestamp precision could blur executions** → require timezone-aware, ordered timestamps and validate run/head/attempt/job identity at every traversed step; ambiguous history remains unavailable.
