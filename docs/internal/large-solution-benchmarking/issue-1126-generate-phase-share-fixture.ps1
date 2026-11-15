$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$fixtureDirectory = Join-Path $repositoryRoot '.benchmark\issue-1126-phase-share'
$assemblyDirectory = Join-Path $repositoryRoot 'src\ArchLinterNet.Core\bin\Release\net10.0'
New-Item -ItemType Directory -Path $fixtureDirectory -Force | Out-Null

$policy = [System.Text.StringBuilder]::new()
[void]$policy.AppendLine('version: 1')
[void]$policy.AppendLine('name: baseline-merge-phase-share')
[void]$policy.AppendLine('layers:')
[void]$policy.AppendLine('  core:')
[void]$policy.AppendLine('    namespace: Synthetic.Core')
[void]$policy.AppendLine('analysis:')
[void]$policy.AppendLine('  target_assemblies:')
[void]$policy.AppendLine('    - ArchLinterNet.Core')
[void]$policy.AppendLine('  assembly_search_paths:')
[void]$policy.AppendLine("    - '$($assemblyDirectory.Replace('\', '/'))'")
[void]$policy.AppendLine('contracts:')
[void]$policy.AppendLine('  strict:')
[void]$policy.AppendLine('    - id: merge-profile')
[void]$policy.AppendLine('      name: merge-profile')
[void]$policy.AppendLine('      source: core')
[void]$policy.AppendLine('      forbidden: []')
[void]$policy.AppendLine('      ignored_violations:')
for ($index = 0; $index -lt 1000; $index++) {
    [void]$policy.AppendLine("        - source_type: Synthetic.SourceType$index")
    [void]$policy.AppendLine("          forbidden_reference: Synthetic.ForbiddenType$index")
    [void]$policy.AppendLine('          reason: inline')
    [void]$policy.AppendLine('          identity_version: 2')
    [void]$policy.AppendLine('          contract_family: dependency')
    [void]$policy.AppendLine('          kind: dependency')
    [void]$policy.AppendLine('          source_assembly: Synthetic.SourceAssembly')
    [void]$policy.AppendLine("          source_member: SourceMember$index")
    [void]$policy.AppendLine('          target_assembly: Synthetic.TargetAssembly')
    [void]$policy.AppendLine("          target_type: Synthetic.TargetType$index")
    [void]$policy.AppendLine("          target_member: TargetMember$index")
    [void]$policy.AppendLine("          occurrence: $index")
    [void]$policy.AppendLine('          configuration: release')
}

$baseline = [System.Text.StringBuilder]::new()
[void]$baseline.AppendLine('version: 3')
[void]$baseline.AppendLine('baseline:')
[void]$baseline.AppendLine('  strict:')
[void]$baseline.AppendLine('    - id: merge-profile')
[void]$baseline.AppendLine('      ignored_violations:')
for ($index = 1000; $index -lt 2000; $index++) {
    [void]$baseline.AppendLine("        - source_type: Synthetic.SourceType$index")
    [void]$baseline.AppendLine("          forbidden_reference: Synthetic.ForbiddenType$index")
    [void]$baseline.AppendLine('          reason: baseline')
    [void]$baseline.AppendLine('          identity_version: 2')
    [void]$baseline.AppendLine('          contract_family: dependency')
    [void]$baseline.AppendLine('          kind: dependency')
    [void]$baseline.AppendLine('          source_assembly: Synthetic.SourceAssembly')
    [void]$baseline.AppendLine("          source_member: SourceMember$index")
    [void]$baseline.AppendLine('          target_assembly: Synthetic.TargetAssembly')
    [void]$baseline.AppendLine("          target_type: Synthetic.TargetType$index")
    [void]$baseline.AppendLine("          target_member: TargetMember$index")
    [void]$baseline.AppendLine("          occurrence: $index")
    [void]$baseline.AppendLine('          configuration: release')
}

$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $fixtureDirectory 'policy.yml'), $policy.ToString(), $utf8WithoutBom)
[System.IO.File]::WriteAllText((Join-Path $fixtureDirectory 'baseline.yml'), $baseline.ToString(), $utf8WithoutBom)
Write-Output "Generated phase-share fixtures in $fixtureDirectory"
