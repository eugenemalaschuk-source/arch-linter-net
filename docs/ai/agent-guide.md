# Agent Guide

Use this guide before editing `architecture/dependencies.arch.yml`.

ArchLinterNet policies are repository contracts, not aspirational diagrams. A
good AI-authored policy starts from code that exists and then chooses the
smallest enforceable rules.

## Investigation Flow

1. Find the existing policy file, usually `architecture/dependencies.arch.yml`.
1. List real projects and assemblies from the solution and project files.
1. Inspect namespaces in source files and compiled target assemblies.
1. Identify existing architectural seams: packages, modules, bounded contexts, UI, application, domain, infrastructure, testing, Unity runtime, and Unity editor code.
1. Check current references before deciding whether a rule belongs in `strict` or `audit`.
1. Read `schema/dependencies.arch.schema.json` before adding fields.
1. Read `archlinternet.capabilities.json` before proposing a contract family.
1. If the project uses conditional compilation (`#if UNITY_EDITOR`, `#if DEBUG`), check whether method-body contracts need a condition set to avoid false positives or missed violations. Define `analysis.condition_sets` when the same source should be validated under different symbol configurations.

## Mental Model

```text
real code facts
    |
    v
layers with namespace prefixes
    |
    v
strict rules that pass today       audit rules for migration discovery
    |                              |
    v                              v
CI no-new-debt gate                future-state visibility
```

## What To Inspect

Use actual repository facts:

- Assembly names used by `.csproj` files and compiled outputs.
- Namespace roots used by source files.
- Existing project references and package boundaries.
- Existing policy rules and ignored violations.
- Migration issues or comments explaining known architecture debt.
- Unity `.asmdef` files under `Assets` when working on Unity projects.

## What Not To Do

Do not create layers from ideal labels unless they map to real namespaces. Do
not use `strict` for future-state rules that fail today. Do not add broad
ignores to hide new debt. Do not invent fields such as `from`, `to`, `pattern`,
`regex`, `severity`, or custom contract groups unless the schema supports them.

## Output Expectations

When proposing a policy change, include the rationale:

- Which assemblies and namespaces were inspected.
- Which rules are strict because they are enforceable today.
- Which rules are audit because they represent future-state architecture.
- Why every ignored violation is narrow and temporary.
- Which validation command was run locally.
