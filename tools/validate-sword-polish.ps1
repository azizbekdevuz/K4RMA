param(
    [ValidateSet('compile', 'core', 'play', 'cadence', 'all')]
    [string]$Suite = 'all',
    [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Data',
    [ValidateRange(60, 3600)]
    [int]$TimeoutSeconds = 900,
    [string]$LicensingIpc,
    [string]$ReuseProjectDirectory
)

# Every invocation writes a new run directory. Authored assets, previous reports,
# imported Libraries are preserved. Reuse is restricted to closed, marked projects.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath.TrimEnd('\')
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Write-Utf8([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, $utf8)
}

function Read-SharedText([string]$Path) {
    # Unity's import/ILPP workers can inherit log handles briefly after Editor
    # exit. Permit their existing write handles while collecting the fresh log.
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

function Assert-ChildPath([string]$Base, [string]$Candidate) {
    $basePath = [IO.Path]::GetFullPath($Base).TrimEnd('\')
    $fullPath = [IO.Path]::GetFullPath($Candidate).TrimEnd('\')
    if (!$fullPath.StartsWith($basePath + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path must remain below $basePath : $fullPath"
    }
    # Reject redirected output ancestors even when the final child does not exist.
    $ancestor = $fullPath
    while ($ancestor.Length -gt $basePath.Length) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing a redirected validation path: $ancestor"
            }
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
    return $fullPath
}

function New-SafeDirectory([string]$Base, [string]$Candidate) {
    $path = Assert-ChildPath $Base $Candidate
    if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite an existing run directory: $path" }
    New-Item -ItemType Directory -Path $path | Out-Null
    return (Resolve-Path -LiteralPath $path).ProviderPath
}

function Quote-Argument([string]$Value) { return '"' + $Value.Replace('"', '\"') + '"' }

function Invoke-CheckedProcess(
    [string]$Executable, [string[]]$Arguments, [string]$Folder,
    [string]$Name, [int]$DeadlineSeconds = $TimeoutSeconds
) {
    $stdout = Join-Path $Folder ($Name + '.stdout.txt')
    $stderr = Join-Path $Folder ($Name + '.stderr.txt')
    $log = Join-Path $Folder ($Name + '.log')
    $quoted = @($Arguments | ForEach-Object { Quote-Argument $_ })
    $process = Start-Process -FilePath $Executable -ArgumentList $quoted -WorkingDirectory $repo `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    # Retain the process handle before a short compiler process can exit.
    # Windows PowerShell can otherwise lose ExitCode after redirected execution.
    $retainedHandle = $process.Handle
    Write-Utf8 (Join-Path $Folder ($Name + '.pid')) ([string]$process.Id)
    $deadline = [DateTime]::UtcNow.AddSeconds($DeadlineSeconds)
    $nextUpdate = [DateTime]::UtcNow.AddSeconds(30)
    while (!$process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -ge $deadline) {
            $process.Kill()
            $process.WaitForExit(10000) | Out-Null
            throw "$Name timed out after $DeadlineSeconds seconds. See $Folder"
        }
        if ([DateTime]::UtcNow -ge $nextUpdate) {
            Write-Host "$Name is running (PID $($process.Id)); logs: $Folder"
            $nextUpdate = [DateTime]::UtcNow.AddSeconds(30)
        }
    }
    $process.WaitForExit()
    $outText = if (Test-Path -LiteralPath $stdout) { Read-SharedText $stdout } else { '' }
    $errText = if (Test-Path -LiteralPath $stderr) { Read-SharedText $stderr } else { '' }
    Write-Utf8 $log ($outText + [Environment]::NewLine + $errText)
    if ($null -eq $process.ExitCode -or $process.ExitCode -ne 0) {
        throw "$Name exited with code $($process.ExitCode). Read $log"
    }
    return [pscustomobject]@{ Text = $outText + "`n" + $errText; Log = $log }
}

function Get-SourceHashes {
    $files = @(Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/K4RMA') -Recurse -File -Filter '*.cs')
    $files += @(Get-ChildItem -LiteralPath (Join-Path $repo 'tests/Unity') -File -Filter '*.cs')
    $files += Get-Item -LiteralPath (Join-Path $repo 'tests/CoreChecks/Program.cs')
    return @($files | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName.Substring($repo.Length + 1)
            SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
}

function Assert-SourcesUnchanged($Before) {
    $after = @(Get-SourceHashes)
    Write-Utf8 (Join-Path $run 'source-hashes-after.json') ($after | ConvertTo-Json -Depth 4)
    if (($Before | ConvertTo-Json -Depth 4 -Compress) -ne ($after | ConvertTo-Json -Depth 4 -Compress)) {
        throw 'Source files changed during validation. The reports do not certify the final source.'
    }
    Write-Host "PASS: $($after.Count) source hashes unchanged throughout this invocation"
}

function Write-Response([string]$Path, [string[]]$Arguments) {
    Write-Utf8 $Path (($Arguments | ForEach-Object { Quote-Argument $_ }) -join "`n")
}

function Invoke-Compile {
    $folder = New-SafeDirectory $run (Join-Path $run 'compile')
    $refs = @(Get-ChildItem -Path (Join-Path $UnityData 'DotNetSdk/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/*.dll'))
    $refs += @(Get-ChildItem -Path (Join-Path $UnityData 'Managed/UnityEngine/UnityEngine*.dll'))
    $refs += Get-Item -LiteralPath (Join-Path $repo 'Library/ScriptAssemblies/Unity.InputSystem.dll')
    if ($refs.Count -lt 2) { throw 'Unity compile reference assemblies are missing' }
    foreach ($target in @('player', 'editor')) {
        $targetRefs = @($refs)
        if ($target -eq 'editor') {
            $targetRefs += @(Get-ChildItem -Path (Join-Path $UnityData 'Managed/UnityEngine/UnityEditor*.dll'))
            $targetRefs += Get-Item -LiteralPath (Join-Path $repo 'Library/ScriptAssemblies/Unity.2D.Sprite.Editor.dll')
            $targetRefs += Get-Item -LiteralPath (Join-Path $repo 'Library/ScriptAssemblies/Unity.RenderPipelines.Core.Runtime.dll')
            $targetRefs += Get-Item -LiteralPath (Join-Path $repo 'Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Runtime.dll')
        }
        $sources = @(Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/K4RMA') -Filter '*.cs' -Recurse |
            Where-Object { $target -eq 'editor' -or $_.FullName -notmatch '\\Editor\\' })
        if ($target -eq 'editor') {
            $sources += @(Get-ChildItem -LiteralPath (Join-Path $repo 'tests/Unity') -Filter '*.cs' -File)
        }
        $flags = @('-nologo', '-target:library', '-langversion:9', '-warnaserror+',
            ('-out:' + (Join-Path $folder ('Karma.' + $target + '.dll'))))
        $flags += if ($target -eq 'editor') { '-define:UNITY_EDITOR,ENABLE_INPUT_SYSTEM' } else { '-define:ENABLE_INPUT_SYSTEM' }
        $flags += @($targetRefs | Sort-Object FullName -Unique | ForEach-Object { '-r:' + $_.FullName })
        $flags += @($sources | Sort-Object FullName | ForEach-Object { $_.FullName })
        $response = Join-Path $folder ($target + '.rsp')
        Write-Response $response $flags
        $result = Invoke-CheckedProcess $dotnet @($compiler, ('@' + $response)) $folder ($target + '-compile')
        $message = "PASS: Unity $target compile (warnings as errors), $($sources.Count) source files"
        Write-Utf8 (Join-Path $folder ($target + '-compile.txt')) ($result.Text + "`n" + $message + "`n")
        Write-Host $message
    }
}

function Invoke-Core {
    $folder = New-SafeDirectory $run (Join-Path $run 'core')
    $referencePack = Get-ChildItem -LiteralPath (Join-Path $UnityData 'DotNetSdk/packs/Microsoft.NETCore.App.Ref') -Directory |
        Where-Object { $_.Name -like '8.*' } | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if ($null -eq $referencePack) { throw 'Unity .NET 8 reference pack is missing' }
    $references = @(Get-ChildItem -LiteralPath (Join-Path $referencePack.FullName 'ref/net8.0') -Filter '*.dll')
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/K4RMA/Core') -File -Filter '*.cs')
    $sources += Get-Item -LiteralPath (Join-Path $repo 'tests/CoreChecks/Program.cs')
    $assembly = Join-Path $folder 'Karma.CoreChecks.dll'
    $flags = @('-nologo', '-target:exe', '-langversion:9', '-warnaserror+', ('-out:' + $assembly))
    $flags += @($references | Sort-Object FullName | ForEach-Object { '-r:' + $_.FullName })
    $flags += @($sources | Sort-Object FullName | ForEach-Object { $_.FullName })
    $response = Join-Path $folder 'core-checks.rsp'
    Write-Response $response $flags
    Invoke-CheckedProcess $dotnet @($compiler, ('@' + $response)) $folder 'core-compile' | Out-Null
    $runtime = @{ runtimeOptions = @{ tfm = 'net8.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $referencePack.Name } } }
    Write-Utf8 (Join-Path $folder 'Karma.CoreChecks.runtimeconfig.json') ($runtime | ConvertTo-Json -Depth 5)
    $result = Invoke-CheckedProcess $dotnet @($assembly) $folder 'core-checks'
    if ($result.Text -notmatch '(?m)^PASS: \d+ core assertions\s*$') { throw "Core produced no PASS verdict. Read $($result.Log)" }
    Write-Host $result.Text.Trim()
}

function Assert-ClosedReusableProject {
    if (![IO.Path]::IsPathRooted($ReuseProjectDirectory)) { throw '-ReuseProjectDirectory requires an absolute path' }
    $project = Assert-ChildPath $validationRoot $ReuseProjectDirectory
    if (!(Test-Path -LiteralPath $project -PathType Container)) { throw 'Reusable project directory does not exist' }
    $project = (Resolve-Path -LiteralPath $project).ProviderPath
    Assert-ChildPath $validationRoot $project | Out-Null
    $marker = Assert-ChildPath $project (Join-Path $project '.karma-isolated-project')
    if (!(Test-Path -LiteralPath $marker -PathType Leaf) -or [IO.File]::ReadAllText($marker) -ne $repo) {
        throw 'Reusable project marker must identify this exact repository'
    }
    # A matching marker proves ownership; the process list and exclusive lock-file
    # probe ensure no live Editor can read a source tree while it is synchronized.
    foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")) {
        if (!$process.CommandLine) { throw "Cannot verify the project used by Unity PID $($process.ProcessId)" }
        if ($process.CommandLine -match '(?i)(?:^|\s)-projectPath\s+(?:"([^"]+)"|(\S+))') {
            $activePath = if ($Matches[1]) { $Matches[1] } else { $Matches[2] }
            if ([IO.Path]::GetFullPath($activePath).TrimEnd('\') -eq $project.TrimEnd('\')) {
                throw "Reusable project is still open in Unity PID $($process.ProcessId)"
            }
        }
    }
    $lockPath = Assert-ChildPath $project (Join-Path $project 'Temp/UnityLockfile')
    if (Test-Path -LiteralPath $lockPath) {
        $lease = $null
        try { $lease = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch { throw "Reusable project is locked by another process: $lockPath" }
        finally { if ($null -ne $lease) { $lease.Dispose() } }
    }
    return $project
}

function Archive-ReusedVerdicts([string]$Project) {
    $parent = Split-Path -Parent $Project
    Assert-ChildPath $validationRoot $parent | Out-Null
    $archive = $null
    foreach ($name in @('boss-play-validation.txt', 'wave-cadence-validation.json', 'SW05measurements.json')) {
        $previous = Assert-ChildPath $Project (Join-Path $Project $name)
        if (!(Test-Path -LiteralPath $previous)) { continue }
        if (!(Test-Path -LiteralPath $previous -PathType Leaf)) { throw "Verdict is not an ordinary file: $previous" }
        if ($null -eq $archive) {
            $archiveName = 'reused-verdict-archive-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-utc'
            $archive = New-SafeDirectory $parent (Join-Path $parent $archiveName)
        }
        $saved = Join-Path $archive $name
        Copy-Item -LiteralPath $previous -Destination $saved
        if ((Get-FileHash -LiteralPath $previous -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $saved -Algorithm SHA256).Hash) { throw 'Previous verdict archive differs' }
        # Scalar removal only, after its absolute path and archival hash were checked.
        Remove-Item -LiteralPath $previous -Force
        Write-Host "Archived prior generated verdict: $saved"
    }
}

function New-IsolatedProject([string]$Folder) {
    $reusing = ![string]::IsNullOrWhiteSpace($ReuseProjectDirectory)
    if ($reusing) {
        $project = Assert-ClosedReusableProject
        Archive-ReusedVerdicts $project
    } else {
        $project = New-SafeDirectory $Folder (Join-Path $Folder 'isolated-project')
        $marker = Join-Path $project '.karma-isolated-project'
        Write-Utf8 $marker $repo
        if ([IO.File]::ReadAllText($marker) -ne $repo) { throw 'Isolated project marker verification failed' }
    }
    foreach ($name in @('Assets', 'Packages', 'ProjectSettings', 'image')) {
        $destination = Assert-ChildPath $project (Join-Path $project $name)
        if (Test-Path -LiteralPath $destination) {
            if (!$reusing) { throw "Isolated source destination already exists: $destination" }
            $redirected = @(Get-ChildItem -LiteralPath $destination -Recurse -Force |
                Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })
            if ($redirected.Count) { throw "Refusing a redirected reusable source entry: $($redirected[0].FullName)" }
        } else { New-Item -ItemType Directory -Path $destination | Out-Null }
        foreach ($source in @(Get-ChildItem -LiteralPath (Join-Path $repo $name) -Force)) {
            Copy-Item -LiteralPath $source.FullName -Destination $destination -Recurse -Force
        }
    }
    $editorFolder = Join-Path $project 'Assets/Editor'
    Assert-ChildPath $project $editorFolder | Out-Null
    if (!(Test-Path -LiteralPath $editorFolder)) { New-Item -ItemType Directory -Path $editorFolder | Out-Null }
    $testFiles = @(Get-ChildItem -LiteralPath (Join-Path $repo 'tests/Unity') -Filter '*.cs' -File)
    if (!$testFiles.Count) { throw 'No Unity checks found' }
    if ($reusing) {
        foreach ($oldFile in @(Get-ChildItem -LiteralPath $editorFolder -Filter '*.cs' -File)) {
            if ($oldFile.Name -notin $testFiles.Name -and
                !(Test-Path -LiteralPath (Join-Path $repo ('Assets/Editor/' + $oldFile.Name)))) {
                throw "Reusable project contains a removed test; use a fresh project: $($oldFile.Name)"
            }
        }
    }
    foreach ($file in $testFiles) {
        $destination = Join-Path $editorFolder $file.Name
        if ((Test-Path -LiteralPath $destination) -and !$reusing) { throw "A Unity test would replace an authored asset: $destination" }
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { throw "Test copy differs: $($file.Name)" }
    }
    foreach ($source in @(Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/K4RMA') -Recurse -File -Filter '*.cs')) {
        $relative = $source.FullName.Substring($repo.Length + 1)
        $copied = Assert-ChildPath $project (Join-Path $project $relative)
        if ((Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash) { throw "Runtime copy differs: $relative" }
    }
    Write-Host "Prepared isolated project (reuse=$reusing) with all $($testFiles.Count) Unity checks: $project"
    return $project
}

function Invoke-UnityChecks([string]$Name) {
    $folder = New-SafeDirectory $run (Join-Path $run $Name)
    $project = New-IsolatedProject $folder
    $cadence = $Name -eq 'cadence'
    $entry = if ($cadence) { 'KarmaWaveCadenceChecks.Run' } else { 'KarmaBossPlayChecks.Run' }
    $reportName = if ($cadence) { 'wave-cadence-validation.json' } else { 'boss-play-validation.txt' }
    $generated = Join-Path $project $reportName
    $report = Join-Path $folder $reportName
    $log = Join-Path $folder ('unity-' + $Name + '.log')
    foreach ($artifact in @($generated, $report, $log)) {
        if (Test-Path -LiteralPath $artifact) { throw "A stale artifact could satisfy this run: $artifact" }
    }
    $arguments = @('-batchmode', '-nographics', '-projectPath', $project, '-executeMethod', $entry, '-logFile', $log)
    if ($LicensingIpc) { $arguments += @('-licensingIpc', $LicensingIpc) }
    Invoke-CheckedProcess $unity $arguments $folder ('unity-' + $Name + '-process') | Out-Null
    if (!(Test-Path -LiteralPath $generated)) { throw "Unity produced no fresh verdict. Read $log" }
    if (!(Test-Path -LiteralPath $log)) { throw 'Unity produced no fresh Editor log' }
    Copy-Item -LiteralPath $generated -Destination $report
    $text = [IO.File]::ReadAllText($report)
    $logText = Read-SharedText $log
    if ($logText -match '(?m)^(?:[\w.+`]*Exception:|Assertion failed\b)|\berror CS\d{4}\b') {
        throw "Unity logged an unhandled error. Read $log"
    }
    if ($cadence) {
        $data = $text | ConvertFrom-Json
        if ($data.result -ne 'PASS') { throw "Cadence failed. Read $report" }
        Write-Host "PASS: cadence, $($data.stages.Count) rows, $($data.assertions) assertions"
    } else {
        if (!$text.StartsWith('PASS:')) { throw "Play checks failed. Read $report" }
        $measurements = Join-Path $project 'SW05measurements.json'
        if (!(Test-Path -LiteralPath $measurements)) { throw 'Play checks produced no SW05 timing/boundary measurements' }
        $timingData = [IO.File]::ReadAllText($measurements) | ConvertFrom-Json
        if ($timingData.result -ne 'PASS' -or $timingData.cases.Count -ne 2) { throw 'SW05 normal/enraged measurements are incomplete' }
        Copy-Item -LiteralPath $measurements -Destination (Join-Path $folder 'SW05measurements.json')
        $slashMeasurements = Join-Path $project 'SW02measurements.json'
        if (!(Test-Path -LiteralPath $slashMeasurements)) { throw 'Play checks produced no SW02 movement/escape measurements' }
        $slashData = [IO.File]::ReadAllText($slashMeasurements) | ConvertFrom-Json
        if ($slashData.result -ne 'PASS' -or $slashData.cases.Count -ne 13) { throw 'SW02 live measurements are incomplete' }
        Copy-Item -LiteralPath $slashMeasurements -Destination (Join-Path $folder 'SW02measurements.json')
        Write-Host $text.Trim()
    }
    Write-Host "Report: $report"
    Write-Host "Log: $log"
}

$UnityData = (Resolve-Path -LiteralPath $UnityData).ProviderPath
$dotnet = Join-Path $UnityData 'DotNetSdk/dotnet.exe'
$unity = [IO.Path]::GetFullPath((Join-Path $UnityData '../Unity.exe'))
$compiler = Get-ChildItem -Path (Join-Path $UnityData 'DotNetSdk/sdk/*/Roslyn/bincore/csc.dll') |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (!(Test-Path -LiteralPath $dotnet) -or !(Test-Path -LiteralPath $unity) -or !$compiler) {
    throw 'Unity executable, bundled .NET SDK or Roslyn compiler is missing'
}
$validationRoot = Assert-ChildPath $repo (Join-Path $repo 'output/validation/sword-polish-20261029')
if (!(Test-Path -LiteralPath $validationRoot)) { New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null }
if ($ReuseProjectDirectory -and $Suite -notin @('play', 'cadence', 'all')) {
    throw '-ReuseProjectDirectory is supported only by play, cadence or all'
}
$runName = $Suite + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-utc'
$run = New-SafeDirectory $validationRoot (Join-Path $validationRoot $runName)
$before = @(Get-SourceHashes)
Write-Utf8 (Join-Path $run 'source-hashes-before.json') ($before | ConvertTo-Json -Depth 4)
Write-Utf8 (Join-Path $run 'invocation.json') (@{
    suite = $Suite; repository = $repo; unityData = $UnityData
    startedUtc = [DateTime]::UtcNow.ToString('o'); timeoutSeconds = $TimeoutSeconds
    reuseProjectDirectory = $ReuseProjectDirectory
} | ConvertTo-Json)
Write-Host "Validation output: $run"
try {
    if ($Suite -in @('compile', 'all')) { Invoke-Compile }
    if ($Suite -in @('core', 'all')) { Invoke-Core }
    if ($Suite -in @('play', 'all')) { Invoke-UnityChecks 'play' }
    if ($Suite -in @('cadence', 'all')) { Invoke-UnityChecks 'cadence' }
    Assert-SourcesUnchanged $before
    Write-Utf8 (Join-Path $run 'result.txt') ("PASS: $Suite`n")
} catch {
    Write-Utf8 (Join-Path $run 'result.txt') ("FAIL: " + $_.Exception.Message + "`n")
    throw
}
