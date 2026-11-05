param(
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe',
    [switch]$NoGraphics
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!(Test-Path -LiteralPath $Unity)) { throw "Unity editor not found: $Unity" }
$output = Join-Path $repo 'output/validation'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$project = Join-Path $output ('sword-project-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $project | Out-Null
# Snapshot tracked project files plus new SwordWave code/art. Never open or
# mutate the working project's Library, scenes, settings, or untracked assets.
$tracked = & git -C $repo ls-files Assets Packages ProjectSettings
if ($LASTEXITCODE) { throw 'Cannot list project files' }
$files = @($tracked) + @(Get-ChildItem (Join-Path $repo 'Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave') -File -Recurse | ForEach-Object { $_.FullName.Substring($repo.Length + 1) })
$files += @(Get-ChildItem (Join-Path $repo 'Assets/K4RMA/Arts/Bosses/SwordWave/Effects/Variants') -File | ForEach-Object { $_.FullName.Substring($repo.Length + 1) })
foreach ($relative in ($files | Sort-Object -Unique)) {
    $target = Join-Path $project $relative
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $target
}
$editorFolder = Join-Path $project 'Assets/Editor'
New-Item -ItemType Directory -Path $editorFolder -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'tests/Unity/SwordWave/KarmaSwordPlayChecks.cs') -Destination $editorFolder
# Reuse already installed packages without touching the working cache.
$cache = Join-Path $repo 'Library/PackageCache'
if (Test-Path -LiteralPath $cache) {
    New-Item -ItemType Directory -Path (Join-Path $project 'Library') -Force | Out-Null
    Copy-Item -LiteralPath $cache -Destination (Join-Path $project 'Library/PackageCache') -Recurse
}
$log = Join-Path $output 'unity-sword-play.log'
$arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'KarmaSwordPlayChecks.RunBatch', '-logFile', ('"' + $log + '"'))
if ($NoGraphics) { $arguments += '-nographics' }
Write-Output "Validation project: $project"
Write-Output "Unity log: $log"
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(600000)) {
    Stop-Process -Id $process.Id
    throw "Unity validation timed out after 10 minutes. Read $log"
}
$report = Join-Path $project 'output/validation/sword-play-validation.txt'
if (!(Test-Path -LiteralPath $report)) { throw "Unity produced no test verdict. Read $log" }
Copy-Item -LiteralPath $report -Destination (Join-Path $output 'sword-play-validation.txt')
foreach ($name in @('sword-player-compilation.txt', 'sword-spacing-metrics.json')) {
    $artifact = Join-Path $project ('output/validation/' + $name)
    if (Test-Path -LiteralPath $artifact) { Copy-Item -LiteralPath $artifact -Destination (Join-Path $output $name) }
}
Get-ChildItem (Join-Path $project 'output/validation') -Filter '*.png' | Copy-Item -Destination $output
$verdict = Get-Content -LiteralPath $report
$verdict | Write-Output
if ($process.ExitCode -ne 0 -or !$verdict[0].StartsWith('PASS:')) { throw "Sword checks failed. Read $log" }
