[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = "win-x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Remove-DirectoryWithin {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$AllowedRoot
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullRoot = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside $AllowedRoot."
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

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

$releaseOutputRoot = Join-Path $repoRoot "ImageViewer\bin\Release\net10.0"
$publishDir = Join-Path $releaseOutputRoot "$RuntimeIdentifier\publish"
$artifactsDir = Join-Path $repoRoot "artifacts"
$stagingDir = Join-Path $artifactsDir ".release-$RuntimeIdentifier"
$archiveName = "ImageViewer-v$version-$RuntimeIdentifier.zip"
$archivePath = Join-Path $artifactsDir $archiveName
$checksumPath = "$archivePath.sha256"

dotnet test $solutionPath -c Release
if ($LASTEXITCODE -ne 0) { throw "Release tests failed." }

dotnet restore $projectPath -r $RuntimeIdentifier -p:Configuration=Release -p:PublishAot=true --force-evaluate
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

dotnet clean $projectPath -c Release -r $RuntimeIdentifier --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed." }

Remove-DirectoryWithin -Path $publishDir -AllowedRoot $releaseOutputRoot

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

$libVlcRoot = Join-Path $publishDir "libvlc"
$expectedLibVlcDir = Join-Path $libVlcRoot $RuntimeIdentifier
if (-not (Test-Path -LiteralPath $expectedLibVlcDir -PathType Container)) {
    throw "Publish output is missing the $RuntimeIdentifier LibVLC runtime."
}

$unexpectedLibVlcDirs = @(Get-ChildItem -LiteralPath $libVlcRoot -Directory |
    Where-Object Name -ne $RuntimeIdentifier)
if ($unexpectedLibVlcDirs.Count -ne 0) {
    $unexpectedNames = $unexpectedLibVlcDirs.Name -join ", "
    throw "Publish output contains unrelated LibVLC runtimes: $unexpectedNames."
}

New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
Remove-DirectoryWithin -Path $stagingDir -AllowedRoot $artifactsDir

New-Item -ItemType Directory -Path $stagingDir | Out-Null
try {
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $stagingDir -Recurse -Exclude "*.pdb"

    $packagedSymbols = @(Get-ChildItem -LiteralPath $stagingDir -Recurse -File -Filter "*.pdb")
    if ($packagedSymbols.Count -ne 0) {
        throw "Release staging contains debug symbols."
    }

    Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $archivePath -CompressionLevel Optimal -Force
}
finally {
    Remove-DirectoryWithin -Path $stagingDir -AllowedRoot $artifactsDir
}

$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = "$hash  $archiveName`n"
[IO.File]::WriteAllText($checksumPath, $checksum, [Text.UTF8Encoding]::new($false))

Write-Output "Created $archivePath"
Write-Output "Created $checksumPath"
