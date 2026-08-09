## MODIFIED Requirements

### Requirement: FrameworkReference declarations are discovered through real per-target-framework MSBuild evaluation
The system SHALL discover a project's `FrameworkReference` declarations by running an actual MSBuild design-time build (via Buildalyzer) separately for each of the project's configured target frameworks, with the MSBuild global property `Configuration` set to `analysis.configuration` (defaulting to `Debug`), rather than by parsing the project file's raw XML. That evaluation SHALL use an isolated MSBuild intermediate output path and SHALL NOT delete or rewrite an existing selected primary build output. `Condition` on both the `FrameworkReference` item itself and its containing `ItemGroup` SHALL be resolved by this real MSBuild evaluation — including conditions that depend on `$(Configuration)` — and declarations contributed by imported `.props`/`.targets` files (including `Directory.Build.props` and SDK-injected targets) SHALL be included exactly as MSBuild itself would resolve them.

#### Scenario: ItemGroup-level condition is honored per target framework
- **WHEN** a multi-targeted project declares `<ItemGroup Condition="'$(TargetFramework)'=='net10.0'"><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>`
- **THEN** the discovered `FrameworkReference` for `Microsoft.AspNetCore.App` SHALL apply only to the `net10.0` build, not to other configured target frameworks

#### Scenario: Item-level condition is honored per target framework
- **WHEN** a multi-targeted project declares a `FrameworkReference` item with its own `Condition` attribute scoping it to one target framework
- **THEN** the discovered `FrameworkReference` SHALL apply only to the target framework(s) for which that condition evaluates true
