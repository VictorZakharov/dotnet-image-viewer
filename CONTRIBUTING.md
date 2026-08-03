# Contributing

Contributions are welcome through GitHub issues and pull requests.
Participation is governed by the project [Code of Conduct](CODE_OF_CONDUCT.md).

## Development setup

1. Install the .NET 10 SDK on Windows 10/11 or a supported x64 Linux desktop.
2. Clone the repository.
3. Run `dotnet restore ImageViewer.sln`.
4. On Linux, install LibVLC, its video-output plugins, and FFmpeg as described in the README.
5. Run `dotnet test ImageViewer.Tests/ImageViewer.Tests.csproj`.
6. Start the app with `dotnet run --project ImageViewer`.

Keep changes focused and keep source files small. Add regression tests for
behavior changes, preserve unrelated worktree changes, and do not commit local
media, shortcuts, caches, or build output.

For changes that affect published runtime behavior, also validate the Native
AOT build described in the README on each affected target operating system.
Native AOT cross-compilation between Windows and Linux is not supported. Pull
requests should explain the user-facing change, important implementation
choices, and validation performed.

By submitting a contribution, you agree that it may be distributed under the
project's [MIT License](LICENSE).
