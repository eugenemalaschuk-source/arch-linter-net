---
name: release-preparation
description: Prepare repository-side content and reviewed release-scope authority for a concrete ArchLinterNet release without publishing it. Use when the user asks to prepare, ready, close, or cut a release/version, including patch maintenance releases and minor/major releases.
---

Read and follow this workflow as mandatory instructions:

`../../../docs/ai/release-preparation-workflow.md`

Treat an explicit version in the current user message as `RELEASE_TARGET`, normalizing only an optional leading `v`. If the user requests the next patch/minor/major/preview release, derive the target only through the repository release-process rules and current tag/release facts.

Execute the repository-side preparation autonomously through release-story reconciliation, factual scope reconstruction, release-note label audit, release-scope declaration, regression coverage, validation, and pull request creation.

Do not publish packages, create tags/GitHub Releases, deploy release docs, or trigger the publication workflow as an implicit part of this skill. Publication remains the separate maintainer procedure in `docs/reference/release-process.md`.
