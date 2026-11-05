param([ValidateSet('Baseline','Updated')][string]$Phase = 'Updated')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repo 'output/validation/sword-retreat-20261101-122534/project'
$output = Join-Path $repo 'output/validation/sword-overhaul-20261102'
foreach ($folder in @('Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave','Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo $folder) -Recurse -File) {
        $target = Join-Path $project $file.FullName.Substring($repo.Length + 1)
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
if ($Phase -eq 'Baseline') {
    $baselineRoot = Join-Path $output 'baseline-source'
    foreach ($file in Get-ChildItem -LiteralPath $baselineRoot -Recurse -File) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $project ('Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/' + $file.FullName.Substring($baselineRoot.Length + 1)))
    }
    $basic = Join-Path $project 'Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Patterns/SWBasic.cs'
    [IO.File]::WriteAllText($basic, [IO.File]::ReadAllText($basic).Replace('owner.Recovery(0.5f)', 'owner.Recovery(0.65f)'), [Text.UTF8Encoding]::new($false))
}
foreach ($name in @('KarmaSwordPlayChecks','KarmaSwordRetreatDashPlayChecks','KarmaSwordOverhaulPlayChecks')) {
    Copy-Item -LiteralPath (Join-Path $repo ('tests/Unity/SwordWave/' + $name + '.cs')) -Destination (Join-Path $project ('Assets/Editor/' + $name + '.cs'))
}
if ($Phase -eq 'Updated') {
    $previous = Get-ChildItem -LiteralPath $output -Directory -Filter 'baseline-*' | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'campaign.json') } | Sort-Object Name -Descending | Select-Object -First 1
    Copy-Item -LiteralPath (Join-Path $previous.FullName 'campaign.json') -Destination (Join-Path $project 'output/overhaul-before-campaign.json')
}
$runId = $Phase.ToLowerInvariant() + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$log = Join-Path $output ($runId + '.log')
$process = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"' + $project + '"'),'-executeMethod',('KarmaSwordOverhaulPlayChecks.Run' + $Phase),'-logFile',('"' + $log + '"')) -WindowStyle Hidden -PassThru
$retainedHandle = $process.Handle
Write-Output "Unity PID: $($process.Id); log: $log"
$deadline = [DateTime]::UtcNow.AddMinutes(12)
while (!$process.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw 'Validation timeout' } }
$artifacts = Join-Path $output $runId
$source = Get-ChildItem -LiteralPath (Join-Path $project ('output/overhaul-' + $Phase.ToLowerInvariant())) -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (!$source) { throw "No fresh verdict; read $log" }
Copy-Item -LiteralPath $source.FullName -Destination $artifacts -Recurse
Write-Output "Artifacts: $artifacts; exit: $($process.ExitCode)"
Get-Content -LiteralPath (Join-Path $artifacts 'result.txt')
if ($process.ExitCode -ne 0) { throw 'Play Mode checks failed' }
