param(
    [ValidateSet('net10.0', 'net11.0')][string]$Framework = 'net11.0',
    [string]$Catalog,
    [ValidateRange(3, 100)][int]$Samples = 7,
    [ValidateRange(1, 20)][int]$Warmups = 2
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projectPath = Join-Path $PSScriptRoot 'StartupBaseline.csproj'
$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$runRoot = Join-Path $repositoryRoot "artifacts/startup-baseline/storage-$Framework-$runId"
$null = New-Item -ItemType Directory -Path $runRoot

$sdkVersion = (& dotnet --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to identify the SDK.' }
$buildOutput = & dotnet build $projectPath --configuration Release "-p:BaselineFramework=$Framework" --ignore-failed-sources 2>&1
$buildExit = $LASTEXITCODE
$buildOutput | Out-File -LiteralPath (Join-Path $runRoot 'build.txt') -Encoding utf8
$buildOutput | ForEach-Object { Write-Host $_ }
if ($buildExit -ne 0) { throw "Build failed; see $runRoot/build.txt" }

[xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw -Encoding utf8
$sourceManifest = @($projectXml.Project.ItemGroup.Compile | ForEach-Object {
    $sourcePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $_.Include))
    [ordered]@{ Path = $_.Include; Sha256 = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash }
})
$metadata = [ordered]@{
    Sdk = $sdkVersion
    TargetFramework = $Framework
    RepositoryCommit = (& git -C $repositoryRoot rev-parse HEAD | Out-String).Trim()
    Sources = $sourceManifest
    HarnessSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Program.cs') -Algorithm SHA256).Hash
    ProjectSha256 = (Get-FileHash -LiteralPath $projectPath -Algorithm SHA256).Hash
}
$metadata | ConvertTo-Json -Depth 8 | Out-File -LiteralPath (Join-Path $runRoot 'manifest.json') -Encoding utf8

$applicationPath = Join-Path $PSScriptRoot "bin/Release/$Framework/StartupBaseline.dll"
$probeArguments = @($applicationPath, '--output', (Join-Path $runRoot 'measurements'), '--samples', "$Samples", '--warmups', "$Warmups")
$beforeHash = $null
if ($Catalog) {
    $resolvedCatalog = (Resolve-Path -LiteralPath $Catalog).Path
    $beforeHash = (Get-FileHash -LiteralPath $resolvedCatalog -Algorithm SHA256).Hash
    $probeArguments += @('--catalog', $resolvedCatalog)
}
& dotnet @probeArguments
if ($LASTEXITCODE -ne 0) { throw 'The measurement probe failed.' }
if ($Catalog) {
    $afterHash = (Get-FileHash -LiteralPath $resolvedCatalog -Algorithm SHA256).Hash
    [ordered]@{ BeforeSha256=$beforeHash; AfterSha256=$afterHash; Unchanged=($beforeHash -eq $afterHash) } |
        ConvertTo-Json | Out-File -LiteralPath (Join-Path $runRoot 'input-integrity.json') -Encoding utf8
    if ($beforeHash -ne $afterHash) {
        Write-Warning 'The live input changed during observation (for example, by a running app). The probe used its initial snapshot.'
    }
}
Write-Host "Run directory: $runRoot"
