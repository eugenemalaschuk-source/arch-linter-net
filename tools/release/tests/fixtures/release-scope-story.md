## Release intent

Fixture for the release-scope declaration generator.

```toml release-scope/v1
schema = "release-scope-input/v1"
release_target = "0.10.0-preview.3"
declaration_id = "v0.10.0-preview.3-fixture-scope"
comments = ["Test scope generation from a release story."]
delivered_items = []

[[required_items]]
issue = 701
finding = "fixture-required"
summary = "A required release item."

[[excluded_items]]
issue = 702
reason = "An explicitly non-blocking item."
```
