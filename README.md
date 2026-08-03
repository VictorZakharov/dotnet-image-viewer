# Image Viewer

A lightweight, filesystem-first image and video viewer for Windows and Linux. Built on .NET 10 and Avalonia 12.

> **Status**: public preview (`0.2.0`). The viewer, browser, and video playback work on Windows and Linux; Windows Explorer integration remains Windows-specific.

## Download

Prebuilt packages are published on the
[Releases](https://github.com/VictorZakharov/dotnet-image-viewer/releases) page.
Release assets use these names:

- Windows x64: `ImageViewer-v<version>-win-x64.zip`
- Linux x64: `ImageViewer-v<version>-linux-x64.deb` (Ubuntu 24.04) and a portable `.tar.gz` for compatible x64 desktops

On Windows, extract the complete archive to a writable folder and run
`ImageViewer.exe`. On Ubuntu 24.04, install the package with
`sudo apt install ./ImageViewer-v<version>-linux-x64.deb`, then start
`imageviewer` from a terminal or the desktop application menu. The package
registers ImageViewer as an available handler without changing user defaults.

Maintainer release tags build and publish the Windows archive, Linux portable
archive, Debian package, and their SHA-256 checksums from the tagged source.

The initial preview is not code-signed. Windows may therefore show an
unrecognized-app warning for a newly downloaded build. Verify that the archive
came from this repository and optionally compare its SHA-256 hash with the
published `.sha256` file before running it.

## Features

### Viewer mode
- Mouse-wheel zoom centered on the cursor
- Click and drag to pan when zoomed in
- Double-click toggles **Fit-to-window** ↔ **Actual size (100%)**
- `←` / `→` (or `↑` / `↓`) navigate to the previous / next media file in the folder
- Built-in video playback with seek, play/pause, mute, and volume controls
- `Space` toggles video playback; video automatically pauses when returning to the browser
- `R` rotates images (per session; doesn't modify the file)
- `F` or `F11` fullscreen
- `I` toggles an EXIF metadata overlay (camera, lens, exposure, dimensions, file size)
- `Space` or `F5` starts an image slideshow with a configurable interval
- Respects EXIF orientation on load

### Browser mode
- Explorer-style folder tree with Windows drives or the Linux filesystem root, compact rows, accurate leaf-folder chevrons, lazy-loaded subfolders, and a resizable splitter
- Thumbnail grid with disk-cached previews (tiered up to 512 px; cache lives in the platform local application-data directory); subfolders stay above media files and show a 2x2 image/video preview mosaic
- A single selection uses a bright blue frame; multi-selection adds visible check badges. Use `Ctrl`-click to toggle items, `Shift`-click for ranges, and `Ctrl+A` to select the current filtered grid
- Selection survives sort and filter changes, with a toolbar summary showing the selected count and aggregate media size
- Sort by name, date, or size — click the sort buttons or press `Ctrl+1` / `Ctrl+2` / `Ctrl+3`; click again to toggle direction
- Type any text to filter by filename — `Backspace` edits, `Esc` clears
- Click the current path in the toolbar to edit it Explorer-style; press Enter to navigate or Esc to revert (invalid paths surface an inline error)
- Copy, cut, paste, move, and Trash/Recycle Bin delete work on every selected file and folder, with collision choices, progress/cancel, aggregate failure details, and undo for the last move or rename
- Drag selected files and folders onto a writable folder in the tree to move them; folder tiles participate in filesystem operations while remaining excluded from viewer/EXIF-only media actions
- Click a file title or press `F2` to rename it inline; only the stem is edited so the extension can't be lost. Enter commits, Esc cancels, click-away commits, and starting another rename commits the pending one
- `Ctrl+wheel` resizes thumbnails (96–512 px). The cache regenerates at the new tier so larger thumbnails stay sharp; the size persists between launches
- Mouse-wheel scrolling is velocity-sensitive in both the folder tree and media grid: small movements stay precise while rapid input travels farther and decelerates smoothly. Fractional precision input keeps its native platform behavior
- The **Smooth** toolbar toggle disables animated scrolling for both panes and persists between launches; Windows' reduced-motion preference overrides it on Windows
- Folder/tree scans stay off the UI thread; the wrapping grid virtualizes item controls, realizing only the viewport plus a small overscan region
- Thumbnail and folder-mosaic work is visible-first, limited to four concurrent loads, and cancelled when it becomes stale; a top progress strip covers grid work and folder tiles show their own spinners
- Each thumbnail shows its file extension as a coloured pill in the top-right corner (JPG amber, PNG green, GIF magenta, BMP gold, WEBP cyan, TIFF violet, RAW red)
- Right-click any media thumbnail (or media in the viewer) → **Properties** opens a side-pane with EXIF date-taken data where available plus created, modified, and accessed file dates
- **Duplicates...** scans one or more folders for byte-identical or visually similar images, with reviewable keeper suggestions and Trash/Recycle Bin deletion
- Select 2–4 images and choose **Compare...** for a dedicated side-by-side view; similar-image groups also open compare directly and apply the chosen keeper back to their review selection
- The folder tree expands and centers on the active folder when one is opened from the viewer, drag-drop, or CLI; long names truncate with a tooltip
- `Enter` or double-click opens the selected image/video or navigates into the selected folder
- Drag-and-drop a file or folder anywhere on the window

### Other
- **Windows integration**: the **Explorer...** button registers this portable copy for Open with and Default Apps without changing defaults; supported files also get a lightweight “Browse containing folder in ImageViewer” action
- **Single-instance**: opening media files in rapid succession from a file manager reuses and focuses the running instance, including while the first window is still starting
- Window position, size, sort order, smooth-scrolling preference, EXIF overlay state, slideshow delay, and last folder persist between launches
- Follows the platform light / dark theme setting
- **Common formats** (JPG, PNG, GIF, BMP, WebP, TIFF, ICO) via Skia
- **RAW formats** (NEF, CR2, CR3, ARW, DNG, RAF, RW2, ORF, PEF, SRW) via Magick.NET
- **Video formats** (MP4, M4V, MOV, AVI, MKV, WebM, WMV, MPEG, MTS/M2TS, TS, 3GP, OGV, VOB) via LibVLC

## Requirements

- Windows 10 / 11 x64, or Ubuntu 24.04 x64 (the validated Linux target)
- Linux video playback uses the system LibVLC development/runtime package, and video previews use FFmpeg; the `.deb` declares both dependencies
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0 or newer) only when building from source

## Build & run

```text
dotnet build ImageViewer.sln -c Debug
dotnet run --project ImageViewer
```

Pass an image, video, or folder path after `--` to open it directly. With no
argument the app opens its browser at the last folder, falling back to the
platform Pictures folder on first launch. Linux development and tests require
the media/runtime packages used by the release:

```bash
sudo apt install libvlc-dev vlc-plugin-base vlc-plugin-video-output ffmpeg
```

For the fastest startup, publish the Avalonia 12 Native AOT build on the same
operating system it will run on.

Windows:

```powershell
dotnet publish ImageViewer\ImageViewer.csproj -c Release -r win-x64
ImageViewer\bin\Release\net10.0\win-x64\publish\ImageViewer.exe
```

This needs the Windows C++ build tools. The release archive and checksum are
created with `./scripts/Publish-Release.ps1`.

Ubuntu/Debian:

```bash
sudo apt install clang zlib1g-dev libx11-6 libice6 libsm6 libfontconfig1 \
  libvlc-dev vlc-plugin-base vlc-plugin-video-output ffmpeg desktop-file-utils
dotnet publish ImageViewer/ImageViewer.csproj -c Release -r linux-x64
./ImageViewer/bin/Release/net10.0/linux-x64/publish/ImageViewer
```

Maintainers with PowerShell installed can run `pwsh ./scripts/Publish-Linux.ps1`
to test, publish, and produce both the portable archive and Debian package. The
version comes from `ImageViewer.csproj`; generated files are written under the
ignored `artifacts` directory.

## Windows Explorer integration

When associations are missing, ImageViewer prompts after its first window appears. Select **Images**, **Videos**, or both, or customize any of the listed extensions before registering. Each group checkbox toggles all formats in that group. A partial selection keeps the reminder active for the remaining formats; select **Never ask again** to suppress future startup prompts. The same controls are always available from **Explorer...** in the browser toolbar, where that preference can be changed later. Registration is per-user, does not require elevation, and only makes ImageViewer available as a choice. Windows requires you to confirm any defaults yourself; use **Choose defaults...** to open the correct Settings page.

The equivalent portable-mode commands are:

```powershell
& ".\ImageViewer.exe" --register
& ".\ImageViewer.exe" --register images
& ".\ImageViewer.exe" --register videos
& ".\ImageViewer.exe" --register images videos
& ".\ImageViewer.exe" --default-apps
& ".\ImageViewer.exe" --unregister
```

Plain `--register` registers both groups. Re-running it with a category selection replaces the previous selection. The dialog can narrow that further to exact extensions. Images-only, videos-only, and custom selections are remembered; unless **Never ask again** is selected, startup continues to offer any formats that remain unregistered.

Registration stores the exact path of the executable that ran `--register`. If you move a portable build, run `--register` again from the new location to repair its commands. Run `--unregister` before deleting the final copy. Unregister removes ImageViewer-owned capabilities, ProgIDs, Open-with entries, and context actions; it does not touch media files, settings, caches, or registry values owned by other applications.

The Explorer action launches a separate ImageViewer process and hands the containing folder to the existing instance. It is a static registry verb, not an in-process shell extension; on Windows 11 it can appear under **Show more options**.

## Linux desktop integration

The Debian package installs a freedesktop desktop entry, application icon, and
the supported image/video MIME associations. This makes ImageViewer appear in
application menus and **Open With** lists; it deliberately does not replace any
default application. The portable archive does not install desktop metadata.
Linux deletion follows the freedesktop Trash specification so files remain
recoverable through the desktop Trash.

## Compare mode

Select 2–4 images in the browser and choose **Compare...**, or use **Compare 2–4...** on an exact/similar duplicate group. Videos and folders are rejected with a clear status message. The original browser selection is restored on return, excluding any files deliberately moved to the Trash or Recycle Bin.

Each cell loads a cached preview first and decodes full resolution in the background, with at most two full-resolution decodes at once. The active cell has a bright frame. **Synchronized** zoom and pan keep the same normalized image region centered even when dimensions differ; turn it off for independent inspection. **Fit**, **100%**, and two-image **Blink / alternate** are available from the toolbar and keyboard. Dimensions, size, date taken, camera, lens, and exposure stay aligned under each image, with differences highlighted.

Pick, Reject, and **Keep this; reject others** are comparison-session review marks. Returning to the browser or duplicate finder updates visible badges immediately, and rejected duplicate candidates become a reviewable deletion selection. Review marks are not written to media metadata. Rejected-file deletion always uses the existing confirmation, progress, and platform Trash/Recycle Bin workflow.

## Duplicate finder

Open **Duplicates...** from the browser toolbar and choose one or more folders. Exact mode first groups by size and SHA-256, then compares the bytes before reporting a match. Similar mode uses a 64-bit perceptual dHash; its distance threshold is adjustable from 0 (closest) to 20 (broadest), and every result is clearly labelled as exact or visually similar.

Results include thumbnails, full paths, dimensions, EXIF date taken, created/modified/accessed dates, camera metadata, and file sizes. Sort by reclaimable space, group size, or newest date. **Select suggested duplicates** uses the displayed keeper rule, but the initial selection is always empty and can be changed freely. A group must retain at least one file, and every removal goes through the normal review prompt and platform Trash/Recycle Bin workflow.

Hard-linked paths are identified as the same physical file and excluded from reclaimable totals. Individual read failures appear under **Scan details** without stopping other files. Pause, cancel, and restart are safe: completed hashes are cached in the platform local application-data directory (typically `%LOCALAPPDATA%\ImageViewer` on Windows or `~/.local/share/ImageViewer` on Linux) and reused only while file identity, size, and modified time still match.

## Keyboard shortcuts

| Scope   | Key                       | Action                                            |
|---------|---------------------------|---------------------------------------------------|
| Global  | `Enter`                   | Toggle viewer ↔ browser                           |
| Global  | `Esc`                     | Exit fullscreen → return to browser → clear filter (never closes) |
| Global  | `Ctrl+O`                  | Open folder picker                                |
| Viewer  | `←` `→` `↑` `↓`           | Previous / next media file                        |
| Viewer  | `R`                       | Rotate image display                              |
| Viewer  | `F` / `F11`               | Fullscreen                                        |
| Viewer  | `I`                       | EXIF overlay                                      |
| Viewer  | `Space`                   | Play/pause video, or toggle image slideshow       |
| Viewer  | `F5`                      | Toggle image slideshow                            |
| Viewer  | mouse wheel               | Zoom (cursor-centered)                            |
| Viewer  | click + drag              | Pan when zoomed                                   |
| Viewer  | double-click              | Fit ↔ 100%                                        |
| Browser | `Del`                     | Move to Trash / Recycle Bin                       |
| Browser | `F2` / click file title   | Rename selected file (Enter commits, Esc cancels) |
| Browser | `Ctrl`-click              | Toggle an item in the selection                   |
| Browser | `Shift`-click             | Select a range from the stable anchor             |
| Browser | `Ctrl+A`                  | Select every item in the filtered grid            |
| Browser | `Ctrl+C` / `Ctrl+X`       | Copy / cut selected files and folders             |
| Browser | `Ctrl+V`                  | Paste files and folders into the current folder   |
| Browser | `Ctrl+Z`                  | Undo the last move or rename                      |
| Browser | `Ctrl+Space`              | Toggle the keyboard-focused item                  |
| Browser | `Shift` + navigation      | Extend the keyboard selection                     |
| Browser | `Ctrl` + wheel            | Resize thumbnails                                 |
| Browser | `Backspace`               | Edit filter text                                  |
| Browser | `Ctrl+1` `Ctrl+2` `Ctrl+3`| Sort by name / date / size                        |
| Browser | (typing)                  | Filter by filename                                |
| Browser | arrows                    | 2D grid navigation (Up/Down by row)               |
| Browser | `PageUp` / `PageDown`     | Page through thumbnails                           |
| Browser | `Home` / `End`            | First / last thumbnail                            |
| Browser | right-click               | Properties → EXIF pane                            |
| Browser | `Enter` / double-click    | Open selected media or folder                     |
| Compare | `←` / `→`                 | Move the unambiguous active comparison cell       |
| Compare | `F` / `A`                 | Fit / show 100%                                   |
| Compare | `S`                       | Toggle synchronized normalized zoom and pan       |
| Compare | `B` / `Space`             | Enter or alternate the two-image blink view       |
| Compare | `P` / `X` / `K`           | Pick / reject / keep active and reject others     |
| Compare | `Delete`                  | Review rejected images for Trash deletion         |

## Tech stack

- [Avalonia 12.1](https://avaloniaui.net/blog/release-12-1) for the Windows and Linux UI
- Avalonia `ItemsRepeater` with `UniformGridLayout` for the virtualized media grid
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) MVVM source generators
- [Magick.NET](https://github.com/dlemstra/Magick.NET) for RAW decoding
- [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) for EXIF
- [LibVLCSharp](https://docs.videolan.me/libvlcsharp/) and LibVLC for video playback
- Skia (via Avalonia) for common-format decode and GPU-accelerated rendering

## License

ImageViewer's original source code and project assets are available under the
[MIT License](LICENSE). Third-party packages and native components retain their
own licenses; see [Third-party notices](THIRD-PARTY-NOTICES.md). Published
builds include these documents and the complete upstream notice files required
for redistribution.

## Project layout

```
ImageViewer.sln
ImageViewer\
├── Program.cs              Entry point — single-instance handoff via mutex + named pipe
├── App.axaml(.cs)          Avalonia application, Fluent theme, lifecycle wiring
├── Controls\               Custom ZoomPanImage control
├── Models\                 EXIF DTO and SortMode enum
├── Services\               Image loading, folder scanning, thumbnail cache,
│                           EXIF reader, platform Trash/file ops, settings store,
│                           single-instance pipe server
├── ViewModels\             MainWindow, Viewer, Browser, ThumbnailItem, FolderTreeItem
└── Views\                  MainWindow + ViewerView + BrowserView
ImageViewer.Tests\          Selection and safe file-operation tests
packaging/linux/            Desktop entry and Debian package metadata
scripts/Publish-Release.ps1 Test, Native AOT publish, archive, and checksum
scripts/Publish-Linux.ps1   Linux archive and Debian package build
```

## Roadmap

Known polish gaps, in rough priority order:
- **Lossless rotate-and-save** (`R` is display-only at the moment)

Planned work and product-scope decisions are tracked in
[GitHub issues](https://github.com/VictorZakharov/dotnet-image-viewer/issues),
with the current overview in [roadmap issue #3](https://github.com/VictorZakharov/dotnet-image-viewer/issues/3).

## Grid stress diagnostics

Set `IMAGEVIEWER_GRID_DIAGNOSTICS=1` before starting the app to show realized, queued, and active thumbnail counts plus viewport layout metrics in the top-right corner of the browser.

The following creates a disposable 10,000-entry folder for repeatable container and navigation checks. The files are intentionally empty: this isolates grid realization, queue bounds, keyboard navigation, and memory from image decode cost.

```powershell
$stressFolder = Join-Path $env:TEMP "ImageViewer-grid-stress-10000"
New-Item -ItemType Directory -Path $stressFolder -Force | Out-Null
0..9999 | ForEach-Object {
    [IO.File]::Create((Join-Path $stressFolder ("image-{0:D5}.jpg" -f $_))).Dispose()
}
$env:IMAGEVIEWER_GRID_DIAGNOSTICS = "1"
ImageViewer\bin\Release\net10.0\ImageViewer.exe $stressFolder
```

Press `End`, `Home`, `PageDown`, and `PageUp` while watching the overlay. Realized controls and pending work should remain proportional to the viewport, not the 10,000-item collection. Remove the fixture afterward with `Remove-Item -LiteralPath $stressFolder -Recurse`.
