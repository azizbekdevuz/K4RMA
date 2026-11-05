param([string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $repo ('output/validation/sword-retreat-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$project = Join-Path $output 'project'
New-Item -ItemType Directory -Path $project -Force | Out-Null
$files = @(& git -C $repo ls-files Assets Packages ProjectSettings)
if ($LASTEXITCODE) { throw 'Cannot list project files' }
$files += @(Get-ChildItem (Join-Path $repo 'Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave') -File -Recurse | ForEach-Object { $_.FullName.Substring($repo.Length + 1) })
$files += @(Get-ChildItem (Join-Path $repo 'Assets/K4RMA/Art/Resources/K4RMAArtwork/Effects/Sword') -File | ForEach-Object { $_.FullName.Substring($repo.Length + 1) })
foreach ($relative in ($files | Sort-Object -Unique)) {
    $target = Join-Path $project $relative
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $target
}
$editor = Join-Path $project 'Assets/Editor'
New-Item -ItemType Directory -Path $editor -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'tests/Unity/KarmaSwordRetreatPlayChecks.cs') -Destination $editor
Copy-Item -LiteralPath (Join-Path $repo 'tests/Unity/KarmaSwordPlayChecks.cs') -Destination $editor
$cache = Join-Path $repo 'Library/PackageCache'
if (Test-Path -LiteralPath $cache) {
    New-Item -ItemType Directory -Path (Join-Path $project 'Library') -Force | Out-Null
    Copy-Item -LiteralPath $cache -Destination (Join-Path $project 'Library/PackageCache') -Recurse
}
$log = Join-Path $output 'unity.log'
Write-Output "Validation output: $output"
$arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod',
    'KarmaSwordRetreatPlayChecks.RunBatch', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$retainedHandle = $process.Handle
Write-Output "Unity PID: $($process.Id)"
$deadline = [DateTime]::UtcNow.AddMinutes(10)
while (!$process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) {
        Stop-Process -Id $process.Id
        throw "Unity validation timed out; read $log"
    }
}
$artifacts = Join-Path $project 'output/retreat'
if (Test-Path -LiteralPath $artifacts) { Copy-Item -LiteralPath $artifacts -Destination (Join-Path $output 'artifacts') -Recurse }
$report = Join-Path $output 'artifacts/result.txt'
if (!(Test-Path -LiteralPath $report)) { throw "No Play Mode verdict; read $log" }
Get-Content -LiteralPath $report
if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode); read $log" }
