param(
    [ValidateSet('melee','rising','isolation','default','candidate','streaks')]
    [string]$Suite = 'melee'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repo 'output/rising-validation'
$version = ((Get-Content (Join-Path $repo 'ProjectSettings/ProjectVersion.txt'))[0] -split ': ')[1]
$editor = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
if (!(Test-Path $editor)) { throw "Install Unity $version or use the repository's validation runner with --unity." }
if (Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -like '*rising-validation*' })
{ throw 'Close the Rising validation Editor before synchronizing its disposable Assets.' }
New-Item -ItemType Directory -Force -Path $project | Out-Null
foreach ($name in @('Assets','Packages','ProjectSettings')) {
    # Do not remove the clone or the source. Preserve the imported Library between runs.
    Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $project -Recurse -Force
}
$testFolder = Join-Path $project 'Assets/Editor'
New-Item -ItemType Directory -Force -Path $testFolder | Out-Null
Get-ChildItem (Join-Path $repo 'tests/Unity') -Recurse -Filter '*.cs' | Copy-Item -Destination $testFolder -Force
$methods = @{
    melee = 'KarmaRisingMeleeChecks.RunBatch'
    rising = 'KarmaRisingPlayChecks.RunBatch'
    isolation = 'KarmaBossIsolationChecks.RunBatch'
    default = 'KarmaRisingCombatReview.RunBatch'
    candidate = 'KarmaRisingCombatReview.RunCandidateBatch'
    streaks = 'KarmaRisingCombatReview.RunStreakBatch'
}
$log = Join-Path $repo "output/rising-$Suite.log"
# Relative projectPath avoids Windows absolute-path normalization in this Editor build.
$process = Start-Process -FilePath $editor -WorkingDirectory $project -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode','-projectPath','.', '-executeMethod',$methods[$Suite], '-logFile',('"' + $log + '"'))
if (!$process.WaitForExit(600000)) { throw "Unity validation is still running. Inspect $log and process $($process.Id)." }
if ($process.ExitCode -ne 0) { throw "Unity returned $($process.ExitCode). Inspect $log." }
if ($Suite -in @('default','candidate','streaks')) {
    $evidence = Join-Path $repo "output/evidence/$Suite"
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    Copy-Item -Path (Join-Path $project 'output/rising-review/*') -Destination $evidence -Force
}
Write-Output "Unity completed $Suite. Results: $project/output. Log: $log"
