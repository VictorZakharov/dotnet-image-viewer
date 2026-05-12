# Image Viewer

A lightweight, ACDSee-style image viewer for Windows. Built on .NET 10 and Avalonia 11.

> **Status**: early MVP. The viewer and browser work; some polish features (rename, file association, virtualized grid) are deferred — see [Roadmap](#roadmap).

## Features

### Viewer mode
- Mouse-wheel zoom centered on the cursor
- Click and drag to pan when zoomed in
- Double-click toggles **Fit-to-window** ↔ **Actual size (100%)**
- `←` / `→` (or `↑` / `↓`) navigate to the previous / next image in the folder
- `R` rotates the display (per session; doesn't modify the file)
- `F` or `F11` fullscreen
- `I` toggles an EXIF metadata overlay (camera, lens, exposure, dimensions, file size)
- `Space` or `F5` starts a slideshow with a configurable interval
- Respects EXIF orientation on load

### Browser mode
- Explorer-style folder tree with drives at the root, lazy-loaded subfolders, resizable splitter
- Thumbnail grid with disk-cached thumbnails (256 px max, JPEG; cache lives under `%LOCALAPPDATA%`)
- Sort by name, date, or size — click the sort buttons or press `Ctrl+1` / `Ctrl+2` / `Ctrl+3`; click again to toggle direction
- Type any text to filter by filename — `Backspace` edits, `Esc` clears
- Click the current path in the toolbar to edit it Explorer-style; press Enter to navigate or Esc to revert (invalid paths surface an inline error)
- `Del` moves the selected image to the Recycle Bin
- `F2` renames the selected file inline; only the stem is edited so the extension can't be lost. Enter commits, Esc cancels, click-away commits, and starting another rename commits the pending one
- `Ctrl+wheel` resizes thumbnails (96–512 px). The cache regenerates at the new tier so larger thumbnails stay sharp; the size persists between launches
- Each thumbnail shows its file extension as a coloured pill in the top-right corner (JPG amber, PNG green, GIF magenta, BMP gold, WEBP cyan, TIFF violet, RAW red)
- Right-click any thumbnail (or image in the viewer) → **Properties** opens an EXIF side-pane; in the viewer the "EXIF" pill toggles the same panel
- The folder tree expands and centers on the active folder when one is opened from the viewer, drag-drop, or CLI; long names truncate with a tooltip
- `Enter` or double-click opens the selected image in the viewer
- Drag-and-drop a file or folder anywhere on the window

### Other
- **Single-instance**: opening another image from Explorer focuses the existing window and switches to that image
- Window position, size, sort order, EXIF overlay state, slideshow delay, and last folder persist between launches
- Follows the Windows light / dark theme setting
- **Common formats** (JPG, PNG, GIF, BMP, WebP, TIFF, ICO) via Skia
- **RAW formats** (NEF, CR2, CR3, ARW, DNG, RAF, RW2, ORF, PEF, SRW) via Magick.NET

## Requirements

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0 or newer) to build

## Build & run

```powershell
dotnet build ImageViewer.sln -c Debug
dotnet run --project ImageViewer
# or open a specific image:
dotnet run --project ImageViewer -- "C:\Pictures\photo.jpg"
```

Or launch `ImageViewer\bin\Debug\net10.0\ImageViewer.exe` directly. With no argument the app opens its browser at your last folder, falling back to `%USERPROFILE%\Pictures` on first launch.

## Keyboard shortcuts

| Scope   | Key                       | Action                                            |
|---------|---------------------------|---------------------------------------------------|
| Global  | `Enter`                   | Toggle viewer ↔ browser                           |
| Global  | `Esc`                     | Exit fullscreen → return to browser → clear filter (never closes) |
| Global  | `Ctrl+O`                  | Open folder picker                                |
| Viewer  | `←` `→` `↑` `↓`           | Previous / next image                             |
| Viewer  | `R`                       | Rotate display                                    |
| Viewer  | `F` / `F11`               | Fullscreen                                        |
| Viewer  | `I`                       | EXIF overlay                                      |
| Viewer  | `Space` / `F5`            | Slideshow                                         |
| Viewer  | mouse wheel               | Zoom (cursor-centered)                            |
| Viewer  | click + drag              | Pan when zoomed                                   |
| Viewer  | double-click              | Fit ↔ 100%                                        |
| Browser | `Del`                     | Move to Recycle Bin                               |
| Browser | `F2`                      | Rename selected file (Enter commits, Esc cancels) |
| Browser | `Ctrl` + wheel            | Resize thumbnails                                 |
| Browser | `Backspace`               | Edit filter text                                  |
| Browser | `Ctrl+1` `Ctrl+2` `Ctrl+3`| Sort by name / date / size                        |
| Browser | (typing)                  | Filter by filename                                |
| Browser | arrows                    | 2D grid navigation (Up/Down by row)               |
| Browser | `PageUp` / `PageDown`     | Page through thumbnails                           |
| Browser | `Home` / `End`            | First / last thumbnail                            |
| Browser | right-click               | Properties → EXIF pane                            |
| Browser | `Enter` / double-click    | Open selected image                               |

## Tech stack

- [Avalonia 11](https://avaloniaui.net/) for the UI (cross-platform-capable; deployed as Windows-only for now)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) MVVM source generators
- [Magick.NET](https://github.com/dlemstra/Magick.NET) for RAW decoding
- [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) for EXIF
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
- **Thumbnail-grid virtualization** for folders with 1000+ images

## Development notes

If you're working on this with Claude Code, `CLAUDE.md` at the repo root has a concise architecture map and conventions reference that's auto-loaded into every session.
