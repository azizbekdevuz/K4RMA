param([ValidateSet('Baseline','Updated')][string]$Phase = 'Updated')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'output/validation/sword-retreat-20261101-122534/project'
$output = Join-Path $repo 'output/validation/sword-random-parry2-20261102'
foreach ($folder in @('Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave','Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo $folder) -Recurse -File) {
        $target = Join-Path $project $file.FullName.Substring($repo.Length + 1)
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
$shared = 'Assets/K4RMA/Runtime/Enemies/KarmaEnemy.Presentation.cs'
Copy-Item -LiteralPath (Join-Path $repo $shared) -Destination (Join-Path $project $shared)
if ($Phase -eq 'Baseline') { Copy-Item -LiteralPath (Join-Path $output 'movement-before.cs') -Destination (Join-Path $project 'Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossMovement.cs') }
foreach ($name in @('KarmaSwordPlayChecks','KarmaSwordRetreatDashPlayChecks','KarmaSwordOverhaulPlayChecks','KarmaSwordPolishPlayChecks','KarmaSwordWallCounterChecks','KarmaSwordRandomParryChecks')) {
    Copy-Item -LiteralPath (Join-Path $repo ('tests/Unity/'+$name+'.cs')) -Destination (Join-Path $project ('Assets/Editor/'+$name+'.cs'))
}
$run = $Phase.ToLowerInvariant()+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$start = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$log = Join-Path $output ($run+'.log')
$process = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',('KarmaSwordRandomParryChecks.Run'+$Phase),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
$retainedHandle = $process.Handle
Write-Output "Unity PID: $($process.Id); log: $log"
$deadline = [DateTime]::UtcNow.AddMinutes(14)
while (!$process.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw 'Validation timeout' } }
$source = Get-ChildItem -LiteralPath (Join-Path $project 'output/random-parry2') -Directory | Where-Object {$_.Name -ge $start} | Sort-Object Name -Descending | Select-Object -First 1
if (!$source) { throw "No fresh verdict; read $log" }
$artifacts = Join-Path $output $run
Copy-Item -LiteralPath $source.FullName -Destination $artifacts -Recurse
Write-Output "Artifacts: $artifacts; exit: $($process.ExitCode)"
Get-Content -LiteralPath (Join-Path $artifacts 'result.txt')
if ($process.ExitCode -ne 0) { throw 'Play Mode checks failed' }
