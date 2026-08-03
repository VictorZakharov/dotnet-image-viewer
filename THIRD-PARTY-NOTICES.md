# Third-party notices

ImageViewer's original source code and project assets are licensed under the
[MIT License](LICENSE). The dependencies below are separate works and retain
their own licenses. Nothing in ImageViewer's MIT license changes those terms.

This inventory reflects the Windows and Linux runtime dependency graphs audited
on 2026-08-03. Direct package versions are pinned in `ImageViewer.csproj` so
the published notices and reviewed dependency versions stay aligned.

## Runtime dependencies

| Component | Version | License | Upstream |
|---|---:|---|---|
| Avalonia UI packages | 12.1.1 | MIT | [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia) |
| Avalonia Controls ItemsRepeater | 12.0.0 | MIT | [AvaloniaUI/Avalonia.Controls.ItemsRepeater](https://github.com/AvaloniaUI/Avalonia.Controls.ItemsRepeater) |
| Avalonia BuildServices | 11.3.2 | MIT | [AvaloniaUI/Avalonia.BuildServices](https://github.com/AvaloniaUI/Avalonia.BuildServices) |
| Avalonia ANGLE Windows natives (Windows only) | 2.1.27548.20260419 | BSD-3-Clause-style | [AvaloniaUI/angle](https://github.com/AvaloniaUI/angle) |
| Inter font, embedded by Avalonia.Fonts.Inter | bundled with 12.1.1 | SIL OFL-1.1 | [rsms/inter](https://github.com/rsms/inter) |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | [CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet) |
| LibVLCSharp and LibVLCSharp.Avalonia | 3.10.0 | LGPL-2.1-or-later | [VideoLAN/LibVLCSharp](https://github.com/videolan/libvlcsharp/tree/3.10.0) |
| VideoLAN.LibVLC.Windows / LibVLC (Windows only) | 3.0.23.1 / 3.0.23 | LGPL-2.1-or-later | [VideoLAN source](https://download.videolan.org/pub/videolan/vlc/3.0.23/) |
| Magick.NET | 14.16.0 | Apache-2.0 | [dlemstra/Magick.NET](https://github.com/dlemstra/Magick.NET) |
| MetadataExtractor | 2.9.3 | Apache-2.0 | [drewnoakes/metadata-extractor-dotnet](https://github.com/drewnoakes/metadata-extractor-dotnet) |
| XmpCore | 6.1.10.1 | BSD | [drewnoakes/xmp-core-dotnet](https://github.com/drewnoakes/xmp-core-dotnet) |
| SkiaSharp and Windows/Linux native assets | 3.119.4 | MIT, plus bundled notices | [mono/SkiaSharp](https://github.com/mono/SkiaSharp) |
| HarfBuzzSharp and Windows/Linux native assets | 8.3.1.3 | MIT, plus bundled notices | [HarfBuzzSharp source](https://github.com/mono/SkiaSharp/tree/main/binding/HarfBuzzSharp) |
| MicroCom.Runtime | 0.11.6 | MIT | [kekekeks/MicroCom](https://github.com/kekekeks/MicroCom) |
| Tmds.DBus.Protocol | 0.94.1 | MIT | [tmds/Tmds.DBus](https://github.com/tmds/Tmds.DBus) |
| .NET Native AOT compiler/runtime components | 10.0.7 | MIT, plus bundled notices | [dotnet/runtime](https://github.com/dotnet/runtime) |

Each publish includes only the native assets selected for its runtime. Windows
packages include ANGLE and the matching bundled LibVLC runtime. Linux packages
include the selected Linux SkiaSharp, HarfBuzzSharp, and Magick.NET assets, but
do not bundle LibVLC, VLC plugins, or FFmpeg; those are replaceable operating-
system packages declared by the Debian package and documented for portable use.

## Test-only dependencies

The test project uses Microsoft VSTest packages 18.8.1 (MIT), xUnit 2.9.3 and
xUnit Visual Studio runner 3.1.5 (Apache-2.0). They are not included in the
application publish output.

## Included license material

Canonical license texts are stored in [`LICENSES`](LICENSES):

- Apache License 2.0
- BSD 3-Clause License
- GNU Lesser General Public License 2.1 or later
- SIL Open Font License 1.1
- Avalonia's upstream third-party notice

Builds also copy the exact license and third-party notice files shipped in the
restored CommunityToolkit.Mvvm, Magick.NET, SkiaSharp, HarfBuzzSharp, and .NET
Native AOT packages, plus ANGLE in Windows builds. These package notices can be
large, so they are copied from NuGet during the build instead of duplicated in
source control.

## LGPL components and corresponding source

ImageViewer loads LibVLCSharp and LibVLC as replaceable shared libraries; they
are not relicensed under MIT. Windows packages bundle LibVLC 3.0.23, whose
matching source is linked in the table above. Linux builds load the user's
distribution-provided LibVLC and plugins instead. Users may replace the LibVLC
shared libraries with a compatible modified build, subject to the applicable
LGPL terms.

Anyone redistributing ImageViewer binaries must keep `LICENSE.txt`, this file,
the `LICENSES` directory, and the package-generated notices together with the
application. Dependency upgrades require a fresh license and notice review.
