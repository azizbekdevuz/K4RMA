param([ValidateSet('Baseline','Updated','SW05','Presentation')][string]$Phase = 'Updated', [switch]$UiReview)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repo 'output/validation/sword-retreat-20261101-122534/project'
$output = Join-Path $repo 'output/validation/sword-slide16-24-20261102'
if (!(Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Existing disposable Unity validation project is required.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Overlay the current working tree, including the user's existing uncommitted files; never purge it.
& robocopy (Join-Path $repo 'Assets/K4RMA') (Join-Path $project 'Assets/K4RMA') /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Could not sync current project assets' }
if ($Phase -eq 'Baseline') {
    Copy-Item -LiteralPath (Join-Path $output 'movement-before.cs') -Destination (Join-Path $project 'Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossMovement.cs')
    Copy-Item -LiteralPath (Join-Path $output 'settings-before.cs') -Destination (Join-Path $project 'Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/KarmaSwordBossSettings.cs')
    if (Test-Path -LiteralPath (Join-Path $output 'config-before.asset')) { Copy-Item -LiteralPath (Join-Path $output 'config-before.asset') -Destination (Join-Path $project 'Assets/K4RMA/Settings/KarmaConfig.asset') }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo 'tests/Unity') -Recurse -Filter '*.cs') { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $project ('Assets/Editor/'+$file.Name)) }
$start = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$run = $Phase.ToLowerInvariant()+'-'+$start
$log = Join-Path $output ($run+'.log')
$unityArgs = @('-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',('KarmaSwordSlideDistanceChecks.Run'+$Phase),'-logFile',('"'+$log+'"'))
if ($UiReview) { $unityArgs += '-slideUiReview' } else { $unityArgs += '-batchmode' }
$process = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe' -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
$retainedHandle = $process.Handle
Write-Output "Unity PID: $($process.Id); log: $log"
$deadline = [DateTime]::UtcNow.AddMinutes(15)
while (!$process.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw 'Validation timeout' } }
$source = Get-ChildItem -LiteralPath (Join-Path $project 'output/slide-distance') -Directory | Where-Object {$_.Name -ge $start} | Sort-Object Name -Descending | Select-Object -First 1
if (!$source) { throw "No fresh verdict; read $log" }
$artifacts = Join-Path $output $run
Copy-Item -LiteralPath $source.FullName -Destination $artifacts -Recurse
Write-Output "Artifacts: $artifacts; exit: $($process.ExitCode)"
Get-Content -LiteralPath (Join-Path $artifacts 'result.txt')
if ($process.ExitCode -ne 0) { throw 'Play Mode checks failed' }
