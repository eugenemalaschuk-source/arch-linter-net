param(
    [Parameter(Mandatory = $true)]
    [string] $BaseCli,

    [Parameter(Mandatory = $true)]
    [string] $CurrentCli,

    [ValidateRange(1, 20)]
    [int] $Pairs = 5
)

$ErrorActionPreference = 'Continue'
$repoRoot = (Get-Location).Path
$fixtureDirectory = Join-Path $repoRoot '.benchmark/issue-1126-phase-share'
$policyPath = Join-Path $fixtureDirectory 'policy.yml'
$baselinePath = Join-Path $fixtureDirectory 'baseline.yml'
$cliPaths = @{
    base = (Resolve-Path -LiteralPath $BaseCli).Path
    current = (Resolve-Path -LiteralPath $CurrentCli).Path
}
$rows = [System.Collections.Generic.List[object]]::new()

for ($pair = 1; $pair -le $Pairs; $pair++) {
    $order = if ($pair % 2 -eq 1) { @('base', 'current') } else { @('current', 'base') }
    foreach ($variant in $order) {
        $output = & dotnet $cliPaths[$variant] --policy $policyPath --mode audit --baseline $baselinePath --timings 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "$variant CLI pair $pair failed with exit code $LASTEXITCODE`n$($output -join "`n")"
        }

        $outputText = $output -join "`n"
        $totalMatch = [regex]::Match($outputText, '(?m)^\s*total\s+(\d+)\s+ms\s*$')
        $phaseMatch = [regex]::Match($outputText, '(?m)^\s*baseline_loading\s+(\d+)\s+ms\s*$')
        if (-not $totalMatch.Success -or -not $phaseMatch.Success) {
            throw "Could not parse timing output for $variant pair $pair`n$outputText"
        }

        $row = [pscustomobject]@{
            Variant = $variant
            Pair = $pair
            TotalMilliseconds = [int]$totalMatch.Groups[1].Value
            BaselineLoadingMilliseconds = [int]$phaseMatch.Groups[1].Value
        }
        $rows.Add($row)
        Write-Output "$variant pair=$pair total=$($row.TotalMilliseconds)ms baseline_loading=$($row.BaselineLoadingMilliseconds)ms"
    }
}

foreach ($variant in @('base', 'current')) {
    $variantRows = @($rows | Where-Object Variant -eq $variant)
    $totalValues = @($variantRows | ForEach-Object TotalMilliseconds | Sort-Object)
    $phaseValues = @($variantRows | ForEach-Object BaselineLoadingMilliseconds | Sort-Object)
    Write-Output "$variant median total=$($totalValues[[int][Math]::Floor($Pairs / 2)])ms baseline_loading=$($phaseValues[[int][Math]::Floor($Pairs / 2)])ms"
}

$pairedTotalDeltas = for ($pair = 1; $pair -le $Pairs; $pair++) {
    $baseRow = $rows | Where-Object { $_.Variant -eq 'base' -and $_.Pair -eq $pair }
    $currentRow = $rows | Where-Object { $_.Variant -eq 'current' -and $_.Pair -eq $pair }
    $currentRow.TotalMilliseconds - $baseRow.TotalMilliseconds
}
$pairedPhaseDeltas = for ($pair = 1; $pair -le $Pairs; $pair++) {
    $baseRow = $rows | Where-Object { $_.Variant -eq 'base' -and $_.Pair -eq $pair }
    $currentRow = $rows | Where-Object { $_.Variant -eq 'current' -and $_.Pair -eq $pair }
    $currentRow.BaselineLoadingMilliseconds - $baseRow.BaselineLoadingMilliseconds
}
$totalDeltaMedian = @($pairedTotalDeltas | Sort-Object)[[int][Math]::Floor($Pairs / 2)]
$phaseDeltaMedian = @($pairedPhaseDeltas | Sort-Object)[[int][Math]::Floor($Pairs / 2)]
Write-Output "paired median current-minus-base total=${totalDeltaMedian}ms baseline_loading=${phaseDeltaMedian}ms"
