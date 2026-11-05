param(
    [ValidateSet('Baseline','Updated')] [string]$Phase = 'Updated',
    [string]$Project = 'output/validation/sword-retreat-20261101-122534/project',
    [string]$Unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectPath = (Resolve-Path -LiteralPath (Join-Path $repo $Project)).Path
$validationRoot = Join-Path $repo 'output/validation/'
if (!$projectPath.StartsWith($validationRoot.Replace('/','\'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Only a disposable project beneath output/validation may be used'
}
if (!(Test-Path -LiteralPath (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'))) { throw 'Missing validation project' }
$output = Join-Path $repo 'output/validation/sword-retreat-dash-20261101'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sourceRoot = Join-Path $repo 'Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave'
foreach ($file in (Get-ChildItem -LiteralPath $sourceRoot -Recurse -File)) {
    $relative = $file.FullName.Substring($repo.Length + 1)
    $target = Join-Path $projectPath $relative
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
if ($Phase -eq 'Baseline') {
    # Reproduce the recorded pre-change version inside the disposable project.
    # Working source files are never replaced when rerunning the comparison.
    foreach ($mapping in @(
        @('movement-before.cs','Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/KarmaSwordBossMovement.cs'),
        @('settings-before.cs','Assets/K4RMA/Runtime/Enemies/Bosses/SwordWave/Settings/KarmaSwordBossSettings.cs')
    )) {
        $saved = Join-Path $output $mapping[0]
        if (!(Test-Path -LiteralPath $saved)) { throw "Missing recorded baseline source: $saved" }
        Copy-Item -LiteralPath $saved -Destination (Join-Path $projectPath $mapping[1])
    }
}
foreach ($name in @('KarmaSwordPlayChecks.cs','KarmaSwordRetreatDashPlayChecks.cs')) {
    Copy-Item -LiteralPath (Join-Path $repo ('tests/Unity/SwordWave/' + $name)) -Destination (Join-Path $projectPath ('Assets/Editor/' + $name))
}
$runId = $Phase.ToLowerInvariant() + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$log = Join-Path $output ($runId + '.log')
$arguments = @('-batchmode','-force-d3d11','-projectPath',('"' + $projectPath + '"'),'-executeMethod',
    ('KarmaSwordRetreatDashPlayChecks.Run' + $Phase),'-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$retainedHandle = $process.Handle
Write-Output "Unity PID: $($process.Id); log: $log"
$deadline = [DateTime]::UtcNow.AddMinutes(10)
while (!$process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw 'Validation timeout' }
}
$source = Join-Path $projectPath ('output/dash-' + $Phase.ToLowerInvariant())
$artifacts = Join-Path $output $runId
if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $artifacts -Recurse }
Write-Output "Artifacts: $artifacts; exit: $($process.ExitCode)"
$verdict = Join-Path $artifacts 'result.txt'
if (!(Test-Path -LiteralPath $verdict)) { throw "No verdict; read $log" }
Get-Content -LiteralPath $verdict
if ($process.ExitCode -ne 0) { throw 'Play Mode checks failed' }
