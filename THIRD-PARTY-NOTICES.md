# Third-party notices

ImageViewer's original source code and project assets are licensed under the
[MIT License](LICENSE). The dependencies below are separate works and retain
their own licenses. Nothing in ImageViewer's MIT license changes those terms.

This inventory reflects the Windows runtime dependency graph audited on
2026-08-03. Direct package versions are pinned in `ImageViewer.csproj` so the
published notices and the reviewed dependency versions stay aligned.

## Runtime dependencies

| Component | Version | License | Upstream |
|---|---:|---|---|
| Avalonia UI packages | 12.1.1 | MIT | [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia) |
| Avalonia Controls ItemsRepeater | 12.0.0 | MIT | [AvaloniaUI/Avalonia.Controls.ItemsRepeater](https://github.com/AvaloniaUI/Avalonia.Controls.ItemsRepeater) |
| Avalonia BuildServices | 11.3.2 | MIT | [AvaloniaUI/Avalonia.BuildServices](https://github.com/AvaloniaUI/Avalonia.BuildServices) |
| Avalonia ANGLE Windows natives | 2.1.27548.20260419 | BSD-3-Clause-style | [AvaloniaUI/angle](https://github.com/AvaloniaUI/angle) |
| Inter font, embedded by Avalonia.Fonts.Inter | bundled with 12.1.1 | SIL OFL-1.1 | [rsms/inter](https://github.com/rsms/inter) |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | [CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet) |
| LibVLCSharp and LibVLCSharp.Avalonia | 3.10.0 | LGPL-2.1-or-later | [VideoLAN/LibVLCSharp](https://github.com/videolan/libvlcsharp/tree/3.10.0) |
| VideoLAN.LibVLC.Windows / LibVLC | 3.0.23.1 / 3.0.23 | LGPL-2.1-or-later | [VideoLAN source](https://download.videolan.org/pub/videolan/vlc/3.0.23/) |
| Magick.NET | 14.16.0 | Apache-2.0 | [dlemstra/Magick.NET](https://github.com/dlemstra/Magick.NET) |
| MetadataExtractor | 2.9.3 | Apache-2.0 | [drewnoakes/metadata-extractor-dotnet](https://github.com/drewnoakes/metadata-extractor-dotnet) |
| XmpCore | 6.1.10.1 | BSD | [drewnoakes/xmp-core-dotnet](https://github.com/drewnoakes/xmp-core-dotnet) |
| SkiaSharp and Windows native assets | 3.119.4 | MIT, plus bundled notices | [mono/SkiaSharp](https://github.com/mono/SkiaSharp) |
| HarfBuzzSharp and Windows native assets | 8.3.1.3 | MIT, plus bundled notices | [HarfBuzzSharp source](https://github.com/mono/SkiaSharp/tree/main/binding/HarfBuzzSharp) |
| MicroCom.Runtime | 0.11.6 | MIT | [kekekeks/MicroCom](https://github.com/kekekeks/MicroCom) |
| Tmds.DBus.Protocol | 0.94.1 | MIT | [tmds/Tmds.DBus](https://github.com/tmds/Tmds.DBus) |
| .NET Native AOT compiler/runtime components | 10.0.7 | MIT, plus bundled notices | [dotnet/runtime](https://github.com/dotnet/runtime) |

Linux, macOS, and WebAssembly native-asset packages can appear in NuGet's
transitive restore graph, but the current distributed application targets
`win-x64`; those platform binaries are not part of that publish output.

## Test-only dependencies

The test project uses Microsoft VSTest packages 17.14.1 (MIT), xUnit 2.9.3 and
xUnit Visual Studio runner 3.1.5 (Apache-2.0), and Newtonsoft.Json 13.0.3
(MIT, transitively through the test platform). They are not included in the
application publish output.

## Included license material

Canonical license texts are stored in [`LICENSES`](LICENSES):

- Apache License 2.0
- BSD 3-Clause License
- GNU Lesser General Public License 2.1 or later
- SIL Open Font License 1.1
- Avalonia's upstream third-party notice

Builds also copy the exact license and third-party notice files shipped in the
restored ANGLE, CommunityToolkit.Mvvm, Magick.NET, SkiaSharp, HarfBuzzSharp,
and .NET Native AOT packages. These package notices can be large, so they are
copied from NuGet during the build instead of duplicated in source control.

## LGPL components and corresponding source

ImageViewer loads LibVLCSharp and LibVLC as replaceable shared libraries; they
are not relicensed under MIT. The matching source is available from the
LibVLCSharp 3.10.0 and LibVLC 3.0.23 upstream links in the table above. Users
may replace the LibVLC shared libraries in a binary distribution with a
compatible modified build, subject to the LGPL terms.

Anyone redistributing ImageViewer binaries must keep `LICENSE.txt`, this file,
the `LICENSES` directory, and the package-generated notices together with the
application. Dependency upgrades require a fresh license and notice review.
