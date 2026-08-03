[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = "win-x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($RuntimeIdentifier -notmatch '^[A-Za-z0-9.-]+$') {
    throw "Runtime identifier contains unsupported characters."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "ImageViewer.sln"
$projectPath = Join-Path $repoRoot "ImageViewer\ImageViewer.csproj"
[xml]$project = Get-Content -LiteralPath $projectPath
$versionNode = $project.SelectSingleNode("/Project/PropertyGroup/Version")
$version = if ($null -eq $versionNode) { "" } else { $versionNode.InnerText }

if ([string]::IsNullOrWhiteSpace($version) -or $version -notmatch '^[0-9A-Za-z.+-]+$') {
    throw "ImageViewer.csproj must contain a release-safe Version value."
}

$publishDir = Join-Path $repoRoot "ImageViewer\bin\Release\net10.0\$RuntimeIdentifier\publish"
$artifactsDir = Join-Path $repoRoot "artifacts"
$archiveName = "ImageViewer-v$version-$RuntimeIdentifier.zip"
$archivePath = Join-Path $artifactsDir $archiveName
$checksumPath = "$archivePath.sha256"

dotnet test $solutionPath -c Release
if ($LASTEXITCODE -ne 0) { throw "Release tests failed." }

dotnet restore $projectPath -r $RuntimeIdentifier -p:Configuration=Release -p:PublishAot=true --force-evaluate
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

dotnet clean $projectPath -c Release -r $RuntimeIdentifier --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed." }

dotnet publish $projectPath -c Release -r $RuntimeIdentifier --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

foreach ($requiredFile in "ImageViewer.exe", "LICENSE.txt", "THIRD-PARTY-NOTICES.md") {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $requiredFile))) {
        throw "Publish output is missing $requiredFile."
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $publishDir "LICENSES") -PathType Container)) {
    throw "Publish output is missing the LICENSES directory."
}

New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $archivePath -CompressionLevel Optimal -Force

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = "$hash  $archiveName`n"
[IO.File]::WriteAllText($checksumPath, $checksum, [Text.UTF8Encoding]::new($false))

Write-Output "Created $archivePath"
Write-Output "Created $checksumPath"
