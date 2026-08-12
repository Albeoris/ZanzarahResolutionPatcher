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

<img width="731" height="276" alt="image" src="https://github.com/user-attachments/assets/b3cf8d97-f488-459b-aa3d-b4e53bff54bb" />

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
| `--fov-fix`                              | Require the automatic FOV fix and apply it without confirmation. |
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

The executable body is scanned once for all known game resolutions. A resolution is patchable only when the scanner can reduce its matches to exactly six offsets whose relative layout is shared by at least one other game resolution. Unrelated matches are discarded; missing, ambiguous, or differently arranged matches are not offered for patching. Only those six validated occurrences are replaced.

A patch plan is rejected if its final metadata would contain the same resolution more than once. Swapping two resolutions in one multi-replacement operation remains valid because the resulting set is still unique.

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
- 16:9: `fov 1333,750`

For the supported x86 executable, an interactive widescreen patch offers an automatic FOV fix after the resolution patch has succeeded in memory. The fix is offered only when the original `SetFOV` function is present at physical file offset `0x0755F`. It adds a read/execute code section and makes the game calculate the horizontal FOV from the active width and height whenever it receives the default 4:3 FOV `1000,750`. Use `--fov-fix` to apply it without confirmation in either interactive or non-interactive mode. When explicitly requested, an unavailable or unsafe fix is an error; an existing fix is accepted and skipped.

If that marker already exists, the fix is not offered or applied again. Patch metadata is appended only after this PE transformation.

If the automatic fix is declined, unavailable, or skipped by `--non-interactive`, the application prints the required console commands in red. Apply the matching command after every game launch; otherwise the rendered world is horizontally distorted. The FOV warning is omitted when the automatic fix was already present or was applied successfully. This does not suppress unrelated warnings: Steam installations still show the task-switching warning, and interactive runs wait for ENTER after any warning so it cannot disappear when the console window closes.

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
