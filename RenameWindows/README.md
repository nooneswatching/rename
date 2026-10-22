# Rename for Windows

Native Windows port of the Rename app (the macOS original lives in `../Rename`). Same idea: load files,
drag them into the order you want on the canvas, stack rename rules, review, apply, undo.

Built with WPF on .NET 10 using the Windows 11 Fluent theme (light and dark follow the system setting).
Runs on Windows 10 and 11, x64 and Arm64.

## Layout

| Project | Purpose |
|---------|---------|
| `Rename.Core` | Platform-neutral logic: models, `RenameEngine`, `ConflictDetector`, `RenameExecutor`, `FileLoader`, `AppState`. No UI dependency, so it builds and tests on any OS. |
| `Rename.App` | The WPF desktop app (`Rename.exe`): views, Windows file dialogs, shell thumbnails. |
| `Rename.Core.Tests` | xUnit tests ported from the macOS `RenameTests` target, plus Windows-specific cases. |

The three layers mirror the Swift app: views are declarative and never touch the disk, `AppState` is the
single source of truth, services do the work. `AppState` takes an `IFileDialogService` and an
`IThumbnailService`, which is how the tests drive it without a window.

## Building

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Visual Studio 2022 17.12+
with the ".NET desktop development" workload is the easiest way to work on the UI, but it is not required.

```powershell
cd RenameWindows
dotnet build Rename.sln
dotnet test Rename.Core.Tests
dotnet run --project Rename.App
```

`Rename.Core` and the tests also build and run on macOS/Linux. `Rename.App` compiles there too
(`EnableWindowsTargeting` is on) but can only run on Windows.

## Packaging

A single self-contained executable, the Windows equivalent of the DMG:

```powershell
cd RenameWindows
dotnet publish Rename.App -c Release -r win-x64 -p:PublishProfile=win-x64
# → Rename.App/bin/publish/win-x64/Rename.exe (about 75 MB; the .NET runtime and WPF are inside)
```

Swap `win-x64` for `win-arm64` for Arm devices. The GitHub Actions workflow in
`.github/workflows/windows.yml` builds, tests, and uploads both executables on every push that touches
this folder, so you do not need a Windows machine to get a build.

To ship without SmartScreen warnings, sign `Rename.exe` with an Authenticode certificate
(`signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a Rename.exe`). An MSIX package
for the Microsoft Store can be added later with the Windows Application Packaging Project.

## Behaviour differences from the macOS app

- **Case-insensitive conflicts.** Two files that would become `Photo.jpg` and `photo.jpg` are flagged as a
  conflict, because NTFS treats them as the same name.
- **Invalid Windows names are blocked.** A computed name containing `< > : " / \ | ? *`, ending in a space
  or period, or using a reserved device name (`CON`, `LPT1`, …) is shown in red and blocks Apply, like a conflict.
- **Unchanged files are skipped on Apply.** Only files whose computed name differs from the current name are
  renamed, so a Find & Replace rule that matches a subset of files just works.
- **Thumbnails** come from the Windows shell for non-image files, so videos, PDFs and Office documents show
  real previews when a thumbnail handler is installed. HEIC/WebP previews need the Microsoft Store codec packs.
- **Date formats** use .NET custom format strings. The common ICU tokens (`yyyy`, `MM`, `dd`, `HH`, `mm`, `ss`)
  are identical.
- **Keyboard:** Ctrl+O open folder, Ctrl+Shift+O add files, Ctrl+Z undo, Ctrl+Enter review/apply,
  Delete remove selected files from the list, Ctrl+A select all visible files, Esc clear selection.
