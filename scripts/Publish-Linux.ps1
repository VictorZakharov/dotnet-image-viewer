[CmdletBinding()]
param(
    [string]$RuntimeIdentifier = "linux-x64"
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
    if (-not $fullPath.StartsWith($fullRoot, [StringComparison]::Ordinal)) {
        throw "Refusing to remove a directory outside $AllowedRoot."
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

if (-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [Runtime.InteropServices.OSPlatform]::Linux)) {
    throw "Linux packages must be built on Linux."
}

$architecture = switch ($RuntimeIdentifier) {
    "linux-x64" { "amd64" }
    default { throw "Unsupported Linux runtime identifier: $RuntimeIdentifier" }
}

& (Join-Path $PSScriptRoot "Publish-Release.ps1") -RuntimeIdentifier $RuntimeIdentifier

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot "ImageViewer"
$projectPath = Join-Path $projectDir "ImageViewer.csproj"
[xml]$project = Get-Content -LiteralPath $projectPath
$version = $project.SelectSingleNode("/Project/PropertyGroup/Version").InnerText

$releaseOutputRoot = Join-Path (Join-Path (Join-Path $projectDir "bin") "Release") "net10.0"
$publishDir = Join-Path (Join-Path $releaseOutputRoot $RuntimeIdentifier) "publish"
$artifactsDir = Join-Path $repoRoot "artifacts"
$stagingDir = Join-Path $artifactsDir ".deb-$RuntimeIdentifier"
$packageRoot = Join-Path $repoRoot "packaging/linux"
$debName = "ImageViewer-v$version-$RuntimeIdentifier.deb"
$debPath = Join-Path $artifactsDir $debName

Remove-DirectoryWithin -Path $stagingDir -AllowedRoot $artifactsDir
New-Item -ItemType Directory -Path $stagingDir | Out-Null
try {
    $debianDir = New-Item -ItemType Directory -Path (Join-Path $stagingDir "DEBIAN")
    $appDir = New-Item -ItemType Directory -Path (Join-Path $stagingDir "usr/lib/imageviewer") -Force
    $binDir = New-Item -ItemType Directory -Path (Join-Path $stagingDir "usr/bin") -Force
    $desktopDir = New-Item -ItemType Directory -Path (
        Join-Path $stagingDir "usr/share/applications") -Force
    $iconDir = New-Item -ItemType Directory -Path (
        Join-Path $stagingDir "usr/share/icons/hicolor/512x512/apps") -Force

    Copy-Item -Path (Join-Path $publishDir "*") -Destination $appDir -Recurse
    Copy-Item -LiteralPath (Join-Path $packageRoot "imageviewer") -Destination $binDir
    Copy-Item -LiteralPath (Join-Path $packageRoot "imageviewer.desktop") -Destination $desktopDir
    Copy-Item -LiteralPath (Join-Path $projectDir "Assets/ImageViewer.png") `
        -Destination (Join-Path $iconDir "imageviewer.png")
    Copy-Item -LiteralPath (Join-Path $packageRoot "postinst") -Destination $debianDir
    Copy-Item -LiteralPath (Join-Path $packageRoot "postrm") -Destination $debianDir

    & chmod 755 (Join-Path $appDir "ImageViewer") (Join-Path $binDir "imageviewer") `
        (Join-Path $debianDir "postinst") (Join-Path $debianDir "postrm")
    if ($LASTEXITCODE -ne 0) { throw "Could not mark Linux executables." }

    $installedBytes = (Get-ChildItem -LiteralPath (Join-Path $stagingDir "usr") -Recurse -File |
        Measure-Object -Property Length -Sum).Sum
    $installedSize = [Math]::Ceiling($installedBytes / 1KB)
    $control = Get-Content -LiteralPath (Join-Path $packageRoot "control.template") -Raw
    $control = $control.Replace("@VERSION@", $version)
    $control = $control.Replace("@ARCHITECTURE@", $architecture)
    $control = $control.Replace(
        "@INSTALLED_SIZE@",
        $installedSize.ToString([Globalization.CultureInfo]::InvariantCulture))
    [IO.File]::WriteAllText(
        (Join-Path $debianDir "control"),
        $control,
        [Text.UTF8Encoding]::new($false))

    if (Test-Path -LiteralPath $debPath) { Remove-Item -LiteralPath $debPath -Force }
    & dpkg-deb --root-owner-group --build $stagingDir $debPath
    if ($LASTEXITCODE -ne 0) { throw "Debian package creation failed." }
}
finally {
    Remove-DirectoryWithin -Path $stagingDir -AllowedRoot $artifactsDir
}

$hash = (Get-FileHash -LiteralPath $debPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(
    "$debPath.sha256",
    "$hash  $debName`n",
    [Text.UTF8Encoding]::new($false))

Write-Output "Created $debPath"
Write-Output "Created $debPath.sha256"
