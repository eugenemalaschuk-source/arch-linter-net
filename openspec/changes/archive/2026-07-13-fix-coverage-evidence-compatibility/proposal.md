# Fix coverage evidence compatibility

## Why

Adding semantic exclusion evidence changed legacy output and constructor compatibility. Contextual selector evidence also displays collection CLR names instead of canonical values.

## What Changes

- Keep legacy exclusion output unchanged when evidence is absent and retain the two-argument constructor.
- Format contextual selector metadata recursively and invariantly.
