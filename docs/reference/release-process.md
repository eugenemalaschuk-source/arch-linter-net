# Release Process

## Versioning

ArchLinterNet follows [Semantic Versioning 2.0](https://semver.org/).

Pre-1.0 releases use `0.x.y`:

- **Patch** (`0.x.z`): Bug fixes, documentation, internal refactoring
- **Minor** (`0.y.0`): New features, contract format changes, behavioral changes

## Release steps

1. **Prepare the release branch**

   ```bash
   git checkout -b release/v0.x.y
   ```

1. **Update version**

   Update `Directory.Build.props` with the new version number.

1. **Update documentation**

   - Update `docs/reference/release-process.md` if procedures changed
   - Update `mkdocs.yml` if new pages were added
   - Verify docs build: `make docs-build`

1. **Run full verification**

   ```bash
   make verify
   ```

1. **Pack NuGet packages**

   ```bash
   make pack
   ```

1. **Create a GitHub release**

   - Tag the release: `git tag v0.x.y`
   - Push tag: `git push origin v0.x.y`
   - Create a release with release notes

1. **Publish NuGet packages**

   ```bash
   dotnet nuget push nupkg/ArchLinterNet.Core.0.x.y.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json
   dotnet nuget push nupkg/ArchLinterNet.Cli.0.x.y.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json
   dotnet nuget push nupkg/ArchLinterNet.Testing.0.x.y.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json
   ```

## Package publication

Package release is separate from documentation deployment. See
[issue #661](https://github.com/eugenemalaschuk-source/firstice/issues/661)
for the official publication workflow.
