param([ValidateSet('Full','Presentation','LeftPresentation','ScarPresentation')][string]$Mode = 'Full')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repo 'output/validation/sword-retreat-20261101-122534/project'
$output = Join-Path $repo 'output/validation/sword-final-polish-20261102'
if (!(Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'Disposable Unity validation project is required' }
& robocopy (Join-Path $repo 'Assets/K4RMA') (Join-Path $project 'Assets/K4RMA') /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Asset sync failed' }
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo 'tests/Unity') -Recurse -Filter '*.cs') { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $project ('Assets/Editor/'+$file.Name)) }
$start = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$log = Join-Path $output ($start+'.log')
$method = switch ($Mode) { 'Presentation' {'KarmaSwordFinalPolishChecks.RunPresentationOnly'} 'LeftPresentation' {'KarmaSwordFinalPolishChecks.RunLeftPresentation'} 'ScarPresentation' {'KarmaSwordFinalPolishChecks.RunScarPresentation'} default {'KarmaSwordFinalPolishChecks.RunBatch'} }
$process = Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
$retainedHandle = $process.Handle
Write-Output "Unity PID: $($process.Id); log: $log"
$deadline = [DateTime]::UtcNow.AddMinutes(18)
while (!$process.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw 'Validation timeout' } }
$source = Get-ChildItem -LiteralPath (Join-Path $project 'output/final-sword-polish') -Directory -ErrorAction SilentlyContinue | Where-Object {$_.Name -ge $start} | Sort-Object Name -Descending | Select-Object -First 1
if (!$source) { throw "No fresh verdict; read $log" }
$artifacts = Join-Path $output $start
Copy-Item -LiteralPath $source.FullName -Destination $artifacts -Recurse
Write-Output "Artifacts: $artifacts; exit: $($process.ExitCode)"
Get-Content -LiteralPath (Join-Path $artifacts 'result.txt')
if ($process.ExitCode -ne 0) { throw 'Play Mode checks failed' }
