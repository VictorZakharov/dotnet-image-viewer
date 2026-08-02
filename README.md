# Image Viewer

A lightweight, ACDSee-style image and video viewer for Windows. Built on .NET 10 and Avalonia 12.

> **Status**: early MVP. The viewer and browser work; file association is deferred — see [Roadmap](#roadmap).

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
- Explorer-style folder tree with drives at the root, compact rows, accurate leaf-folder chevrons, lazy-loaded subfolders, and a resizable splitter
- Thumbnail grid with disk-cached previews (tiered up to 512 px; cache lives under `%LOCALAPPDATA%`); subfolders stay above media files and show a 2x2 image/video preview mosaic
- Sort by name, date, or size — click the sort buttons or press `Ctrl+1` / `Ctrl+2` / `Ctrl+3`; click again to toggle direction
- Type any text to filter by filename — `Backspace` edits, `Esc` clears
- Click the current path in the toolbar to edit it Explorer-style; press Enter to navigate or Esc to revert (invalid paths surface an inline error)
- `Del` moves the selected media file to the Recycle Bin
- Click a file title or press `F2` to rename it inline; only the stem is edited so the extension can't be lost. Enter commits, Esc cancels, click-away commits, and starting another rename commits the pending one
- `Ctrl+wheel` resizes thumbnails (96–512 px). The cache regenerates at the new tier so larger thumbnails stay sharp; the size persists between launches
- Folder/tree scans stay off the UI thread; the wrapping grid virtualizes item controls, realizing only the viewport plus a small overscan region
- Thumbnail and folder-mosaic work is visible-first, limited to four concurrent loads, and cancelled when it becomes stale; a top progress strip covers grid work and folder tiles show their own spinners
- Each thumbnail shows its file extension as a coloured pill in the top-right corner (JPG amber, PNG green, GIF magenta, BMP gold, WEBP cyan, TIFF violet, RAW red)
- Right-click any media thumbnail (or media in the viewer) → **Properties** opens a side-pane with EXIF date-taken data where available plus created, modified, and accessed file dates
- The folder tree expands and centers on the active folder when one is opened from the viewer, drag-drop, or CLI; long names truncate with a tooltip
- `Enter` or double-click opens the selected image/video or navigates into the selected folder
- Drag-and-drop a file or folder anywhere on the window

### Other
- **Single-instance**: opening another media file from Explorer focuses the existing window and switches to it
- Window position, size, sort order, EXIF overlay state, slideshow delay, and last folder persist between launches
- Follows the Windows light / dark theme setting
- **Common formats** (JPG, PNG, GIF, BMP, WebP, TIFF, ICO) via Skia
- **RAW formats** (NEF, CR2, CR3, ARW, DNG, RAF, RW2, ORF, PEF, SRW) via Magick.NET
- **Video formats** (MP4, M4V, MOV, AVI, MKV, WebM, WMV, MPEG, MTS/M2TS, TS, 3GP, OGV, VOB) via LibVLC

## Requirements

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0 or newer) to build

## Build & run

```powershell
dotnet build ImageViewer.sln -c Debug
dotnet run --project ImageViewer
# or open a specific image or video:
dotnet run --project ImageViewer -- "C:\Pictures\photo.jpg"
```

Or launch `ImageViewer\bin\Debug\net10.0\ImageViewer.exe` directly. With no argument the app opens its browser at your last folder, falling back to `%USERPROFILE%\Pictures` on first launch.

For the fastest startup, publish the Avalonia 12 Native AOT build:

```powershell
dotnet publish ImageViewer\ImageViewer.csproj -c Release -r win-x64
ImageViewer\bin\Release\net10.0\win-x64\publish\ImageViewer.exe
```

Native AOT is self-contained and needs the Windows C++ build tools when publishing.

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
| Browser | `Del`                     | Move to Recycle Bin                               |
| Browser | `F2` / click file title   | Rename selected file (Enter commits, Esc cancels) |
| Browser | `Ctrl` + wheel            | Resize thumbnails                                 |
| Browser | `Backspace`               | Edit filter text                                  |
| Browser | `Ctrl+1` `Ctrl+2` `Ctrl+3`| Sort by name / date / size                        |
| Browser | (typing)                  | Filter by filename                                |
| Browser | arrows                    | 2D grid navigation (Up/Down by row)               |
| Browser | `PageUp` / `PageDown`     | Page through thumbnails                           |
| Browser | `Home` / `End`            | First / last thumbnail                            |
| Browser | right-click               | Properties → EXIF pane                            |
| Browser | `Enter` / double-click    | Open selected media or folder                     |

## Tech stack

- [Avalonia 12.1](https://avaloniaui.net/blog/release-12-1) for the UI (cross-platform-capable; deployed as Windows-only for now)
- Avalonia `ItemsRepeater` with `UniformGridLayout` for the virtualized media grid
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) MVVM source generators
- [Magick.NET](https://github.com/dlemstra/Magick.NET) for RAW decoding
- [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) for EXIF
- [LibVLCSharp](https://docs.videolan.me/libvlcsharp/) and LibVLC for video playback
- Skia (via Avalonia) for common-format decode and GPU-accelerated rendering

## Project layout

```
ImageViewer.sln
ImageViewer\
├── Program.cs              Entry point — single-instance handoff via mutex + named pipe
├── App.axaml(.cs)          Avalonia application, Fluent theme, lifecycle wiring
├── Controls\               Custom ZoomPanImage control
├── Models\                 EXIF DTO and SortMode enum
├── Services\               Image loading, folder scanning, thumbnail cache,
│                           EXIF reader, file ops (Recycle Bin), settings store,
│                           single-instance pipe server
├── ViewModels\             MainWindow, Viewer, Browser, ThumbnailItem, FolderTreeItem
└── Views\                  MainWindow + ViewerView + BrowserView
```

## Roadmap

Known polish gaps, in rough priority order:
- **Default-viewer file association** (`--register` / `--unregister` CLI flags are no-op stubs)
- **Lossless rotate-and-save** (`R` is display-only at the moment)

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

## Development notes

If you're working on this with Claude Code, `CLAUDE.md` at the repo root has a concise architecture map and conventions reference that's auto-loaded into every session.
