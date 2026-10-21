# ArchLinterNet.Annotations

`ArchLinterNet.Annotations` is an optional source-only NuGet package for authoring architecture roles and context metadata recognized by ArchLinterNet 0.10.x.

```xml
<PackageReference Include="ArchLinterNet.Annotations" Version="0.10.*" />
```

The package adds its internal C# definitions to the project that directly references it. Each project compiles its own definitions, so the package adds no runtime assembly or package dependency. Use a matching ArchLinterNet 0.10.x tool to analyze the compiled annotations. Earlier tools do not recognize the catalog identity marker.

The package targets C# 9-compatible source and uses NuGet `contentFiles/cs/any` with compile build action. Central Package Management is supported by placing the package version in `Directory.Packages.props` and keeping a versionless `PackageReference` in each authoring project.

For end-to-end examples, custom attribute mappings, migration guidance, and the manifest-backed catalog, see the [semantic annotation adoption guide](https://eugenemalaschuk-source.github.io/arch-linter-net/guides/semantic-annotations/) and [canonical annotation reference](https://eugenemalaschuk-source.github.io/arch-linter-net/reference/semantic-annotation-catalog/).

Add the direct reference to every project that uses the annotations. A reference in one project does not inject sources into other projects, and no package source is copied to build or publish output. Removing the reference and rebuilding removes the package-provided source definitions.

## Unity source import

Unity projects that do not consume NuGet content files can import the reviewed C# files from the restored package. Run `dotnet nuget locals global-packages --list` to locate the global packages folder, then use `archlinternet.annotations/<version>/contentFiles/cs/any/` within it. Import every `.cs` file in that directory into each Unity authoring assembly that uses the annotations. The package does not ship a Unity assembly or a second vocabulary of annotation types.
