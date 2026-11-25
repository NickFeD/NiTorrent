param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [ValidateRange(1, 120)][int]$ObservationSeconds = 15
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
$processName = [IO.Path]::GetFileNameWithoutExtension($executablePath)
if (@(Get-Process -Name $processName -ErrorAction SilentlyContinue).Count) {
    throw 'NiTorrent is already running. Exit through the app before collecting a new startup sample.'
}

$sampleId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$outputDirectory = Join-Path $repositoryRoot "artifacts/startup-baseline/installed-$sampleId"
$null = New-Item -ItemType Directory -Path $outputDirectory
$executableInfo = Get-Item -LiteralPath $executablePath
$runtimeConfigPath = Join-Path $executableInfo.DirectoryName "$processName.runtimeconfig.json"
$runtimeConfig = if (Test-Path -LiteralPath $runtimeConfigPath) {
    Get-Content -LiteralPath $runtimeConfigPath -Raw -Encoding utf8 | ConvertFrom-Json
} else { $null }

$clock = [Diagnostics.Stopwatch]::StartNew()
# A visible window is required to observe this interactive startup. The script
# leaves the app running and never kills it or changes its settings.
$appProcess = Start-Process -FilePath $executablePath -WorkingDirectory $executableInfo.DirectoryName -WindowStyle Normal -PassThru
$launchReturnMs = $clock.Elapsed.TotalMilliseconds
$firstWindowMs = $null
$firstIdleMs = $null
$observedWindowTitle = $null
$peakWorkingSetBytes = 0L
$cpuMs = 0.0
while ($clock.Elapsed.TotalSeconds -lt $ObservationSeconds) {
    $appProcess.Refresh()
    if ($appProcess.HasExited) { break }
    if ($null -eq $firstWindowMs -and $appProcess.MainWindowHandle -ne [IntPtr]::Zero) {
        $firstWindowMs = $clock.Elapsed.TotalMilliseconds
        $observedWindowTitle = $appProcess.MainWindowTitle
    }
    if ($null -eq $firstIdleMs) {
        try {
            if ($appProcess.WaitForInputIdle(1)) { $firstIdleMs = $clock.Elapsed.TotalMilliseconds }
        } catch [InvalidOperationException] { }
    }
    $peakWorkingSetBytes = [Math]::Max($peakWorkingSetBytes, $appProcess.WorkingSet64)
    $cpuMs = $appProcess.TotalProcessorTime.TotalMilliseconds
    Start-Sleep -Milliseconds 20
}
$appProcess.Refresh()
$didExit = $appProcess.HasExited
$exitCode = if ($didExit) { $appProcess.ExitCode } else { $null }
$result = [ordered]@{
    SchemaVersion = 1
    SampleId = $sampleId
    Executable = $executablePath
    ExecutableSha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
    ExecutableLastWriteTimeUtc = $executableInfo.LastWriteTimeUtc.ToString('o')
    ProductVersion = $executableInfo.VersionInfo.ProductVersion
    RuntimeConfig = $runtimeConfig
    ProcessId = $appProcess.Id
    LaunchReturnMs = $launchReturnMs
    WindowHandleObservedMs = $firstWindowMs
    InputIdleObservedMs = $firstIdleMs
    ObservedWindowTitle = $observedWindowTitle
    ObservationMs = $clock.Elapsed.TotalMilliseconds
    PeakObservedWorkingSetBytes = $peakWorkingSetBytes
    LastObservedCpuMs = $cpuMs
    ExitedDuringObservation = $didExit
    ExitCode = $exitCode
    Limitations = @(
        'One sample; cache state is uncontrolled. No median or cold-start claim.',
        'A window handle and input-idle are proxies, not first rendered frame or runtime readiness.',
        'Installed binary provenance may differ from the repository.',
        'The application uses its normal profile and may perform its normal persistence and network work.',
        'The script leaves a surviving process open for normal user exit.'
    )
}
$json = $result | ConvertTo-Json -Depth 12
$resultPath = Join-Path $outputDirectory 'sample.json'
[IO.File]::WriteAllText($resultPath, $json, [Text.UTF8Encoding]::new($false))
$json
"Result: $resultPath"
