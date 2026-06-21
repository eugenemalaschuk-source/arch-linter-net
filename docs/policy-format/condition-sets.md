# Condition Sets

Condition sets define named preprocessor symbol groups for source-level analysis.

They matter when method-body scanning needs to understand conditional code such as `#if UNITY_EDITOR`, `#if DEBUG`, or product-specific compilation symbols.

## YAML shape

```yaml
analysis:
  condition_sets:
    runtime: []
    editor: [UNITY_EDITOR]
    debug: [DEBUG, UNITY_EDITOR]
  default_condition_set: runtime
```

## CLI selection

Use the default condition set from the policy:

```bash
arch-linter-net --mode strict
```

Override it for a run:

```bash
arch-linter-net --mode strict --condition-set editor
```

Unknown condition set names produce exit code `2`.

## What condition sets affect

Condition sets affect Roslyn source and method-body analysis. They determine which conditional branches are active while source is analyzed.

They do not reinterpret already compiled assemblies under different symbols. Reflection and IL scanners analyze the assemblies provided to the run.

## Recommended use

Use separate condition sets when the same repository should be checked under multiple source configurations:

```bash
arch-linter-net --mode strict --condition-set runtime
arch-linter-net --mode strict --condition-set editor
```

For CI, keep the selected condition set explicit when it affects architecture rules.

## Common mistake

Do not expect a condition set to validate runtime dependency injection or dynamic behavior. It only controls static source analysis visibility for conditional compilation branches.
