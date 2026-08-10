# Zanzarah Resolution Patcher

Zanzarah Resolution Patcher adds modern resolutions to **Zanzarah: The Hidden Portal**. Both the classic retail release and the [Steam version](https://store.steampowered.com/app/384570/Zanzarah_The_Hidden_Portal/) are supported.

## Quick start

1. Download and run `ZanzarahResolutionPatcher.exe`.
2. Select `zanthp.exe` when asked.
3. Choose one or more replacements and select **Patch**.

No .NET installation or command line is required.

> [!IMPORTANT]
> The original game does not scale its interface. Unless this has been fixed by the time you read this, try `1920x1080` before 4K or 8K, where text may become too small to read.

> [!NOTE]
> The Steam version has a [known task-switching issue](https://steamcommunity.com/app/384570/discussions/1/521643320368729405/): Alt+Tab may crash or reload the game, especially in windowed mode. This is unrelated to the resolution patch. If this has been fixed by the time you read this, ignore the warning.

## Interactive mode

The output path defaults to the input path when `--input` is supplied. If the input is selected through the Windows open-file dialog, a standard save-file dialog is also shown unless `--output` was supplied. Closing either dialog without selecting a file prints `Operation cancelled by the user.` and exits successfully without an error pause.

When neither resolution is supplied in interactive mode, the resolution menu supports any number of replacements in one pass. Use the arrow keys and ENTER to select a game resolution, then select its replacement. The menu updates each configured mapping inline. Select `Patch` after configuring at least one mapping, or `Cancel` to exit without changing files. Selecting an already configured game resolution edits its replacement.

## Command-line usage (optional)

The interactive workflow above is recommended for most users. Command-line arguments are available for scripts and automation:

```powershell
ZanzarahResolutionPatcher.exe `
  --input "C:\Steam\steamapps\common\ZanZarah\System\zanthp.exe" `
  --old-resolution 800x600 `
  --new-resolution 1920x1080
```

To disable every dialog, prompt, confirmation, and error pause:

```powershell
ZanzarahResolutionPatcher.exe `
  -i "C:\Games\ZanZarah\System\zanthp.exe" `
  -or 800x600 `
  -nr 1920x1080 `
  -ni
```

## Options

Options are shown in their normal workflow order:

| Option                                   | Description |
|------------------------------------------|---|
| `-i`, `--input <PATH>`                   | Input game executable. A Windows open-file dialog is shown when omitted. |
| `-o`, `--output <PATH>`                  | Output file. Defaults to the input file for command-line input. |
| `-or`, `--old-resolution <WIDTHxHEIGHT>` | Resolution to replace. |
| `-nr`, `--new-resolution <WIDTHxHEIGHT>` | Replacement resolution. |
| `-ow`, `--old-width <WIDTH>`             | Old width; requires `-oh`/`--old-height`. |
| `-oh`, `--old-height <HEIGHT>`           | Old height; requires `-ow`/`--old-width`. |
| `-nw`, `--new-width <WIDTH>`             | New width; requires `-nh`/`--new-height`. |
| `-nh`, `--new-height <HEIGHT>`           | New height; requires `-nw`/`--new-width`. |
| `-u`, `--unchecked`                      | Skip validation against display modes reported by Windows. |
| `-nb`, `--no-backup`                     | Disable creation of the original in-place backup. |
| `-ni`, `--non-interactive`               | Disable dialogs, input prompts, confirmation, and the error pause. |
| `-h`, `--help`, `/?`                     | Show complete help. |
| `-v`, `--version`                        | Show the application version. |

The combined and separate forms cannot be mixed for the same resolution. For example, `-nr 1920x1080 -nw 1920 -nh 1080` is rejected. Width and height must both be present, must be positive integers, and must fit in an unsigned 16-bit value (`1`–`65535`).

## Behavior

Windows display modes are enumerated through `EnumDisplaySettings`. Unless `--unchecked` is used, the new resolution must be in that list. With `--unchecked`, a missing new resolution is entered manually as a width and height.

The original executable is expected to contain the following three resolutions:

- `640x480`
- `800x600`
- `1024x768`

Each pattern has this six-byte, little-endian layout:

```text
uint16 width
uint16 zero
uint16 height
```

The executable body is scanned once for every requested game resolution. Matches are non-overlapping. Every occurrence of the selected old resolution is replaced.

After a successful patch, a footer is appended:

```text
3 × (uint16 width, uint16 zero, uint16 height)
uint16 resolutionCount
uint32 reserved
uint32 metadataVersion
4 bytes ASCII "ZZRP"
```

The footer is currently 32 bytes (`resolutionCount = 3`, `reserved = 0`, `metadataVersion = 1`). On later runs it supplies the resolutions that must be searched, allowing a patched resolution to be replaced again. The existing footer is removed before scanning and replaced after patching, so metadata never accumulates.

Before an interactive patch, a colored summary shows the input, output, old and new resolutions, validation mode, and backup state. No file is modified until the user confirms.

## Widescreen field of view

The application displays a calculated game-console FOV beside every target resolution. It uses a vertical reference of `750` and rounds the aspect-correct horizontal value to the nearest `50`:

```text
horizontal = round-to-nearest-50((width / height) * 750)
command    = fov horizontal,750
```

This reproduces the established recommendations:

- 16:10: `fov 1200,750`
- 16:9: `fov 1350,750`

The general angular conversion was also verified: a 4:3 horizontal FOV of 45 degrees becomes approximately 57.822402 degrees at 16:9 and 52.859924 degrees at 16:10. Converting from 16:9 to 16:10 produces the same result as converting directly from 4:3. Zanzarah's `fov x,y` command uses the pair above rather than IEEE-754 degree bytes, so no FOV hex edit is required.

After patching any non-4:3 target, the application prints the required commands in red. Apply them after every game launch; otherwise the rendered world is horizontally distorted.

For the Steam version:

1. Open `...\Steam\steamapps\common\ZanZarah\ZanZarah.bat`.
2. Change `start zanzarah.exe` to `start zanzarah.exe -console`.
3. Start the game and select the patched resolution in the startup menu or Video Options.
4. In game, press `F11` and enter the displayed command, for example `fov 1350,750` for `1920x1080`.

For the classic release, launch `zanzarah.exe` with the `-console` argument, then use `F11` in the same way.

## Backups and writes

For an in-place first patch, the original file is copied to:

```text
zanthp.exe_resolution.bak
```

The backup is created only when all of these conditions are true:

- input and output paths are the same;
- the input has no existing `ZZRP` metadata;
- the backup does not already exist; and
- `--no-backup` was not supplied.

Output is first written and flushed to a temporary file in the destination directory, then moved over the destination. An existing backup is never overwritten.

## Exit codes

| Code | Meaning |
|---:|---|
| `0` | Patch succeeded, help/version was shown, or the user declined final confirmation. |
| `1` | The user cancelled a file dialog or resolution menu, or arguments, paths, executable patterns, metadata, or requested resolutions were invalid. |
| `2` | An operational failure occurred after the complete patch plan was resolved, such as a write failure. |

Unless `--non-interactive` is active, errors end with `Press ENTER to exit...`.

## Build and test

The project targets .NET 10, the current LTS release, and requires Windows because it uses native display-mode enumeration and Windows file dialogs.

The process enables Per-Monitor V2 DPI awareness before creating any UI. Open and Save dialogs therefore render at the native scale of the monitor they are displayed on and rescale when moved between monitors with different DPI settings.

```powershell
dotnet restore ZanzarahResolutionPatcher.slnx
dotnet test ZanzarahResolutionPatcher.slnx -c Release
dotnet publish src/ZanzarahResolutionPatcher/ZanzarahResolutionPatcher.csproj `
  -p:PublishProfile=win-x64 `
  -o artifacts/publish
```

The publish profile produces one compressed, self-contained `win-x64` executable. It deliberately does not use IL trimming because Windows Forms is not trim-safe; unused satellite languages, symbols, and documentation files are omitted instead.

## Releases

Push a semantic-version tag beginning with `v`, for example `v1.2.0`. The release workflow tests the solution, verifies that publishing produced exactly one `.exe`, creates its SHA-256 checksum, and publishes both files in a GitHub Release. GitHub-generated release notes provide the change list since the previous release.
