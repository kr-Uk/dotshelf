# Dotshelf Launcher Update Protocol

The launcher updates the Unity app with a static JSON manifest and a versioned zip package.

## Flow

1. Download `latest.json`.
2. Compare `latestVersion` with local `install-state.json`.
3. Download `packageUrl` when the remote version is newer or no local install exists.
4. Verify the package SHA-256.
5. Extract the package into `%LOCALAPPDATA%\Dotshelf\versions\{version}`.
6. Write `%LOCALAPPDATA%\Dotshelf\install-state.json`.
7. Launch `entrypoint` from the installed version directory.

## Manifest

```json
{
  "latestVersion": "0.1.0",
  "mandatory": false,
  "packageUrl": "https://dotshelf.digital/launcher/windows/Dotshelf-0.1.0.zip",
  "sha256": "replace-with-package-sha256",
  "entrypoint": "Dotshelf.exe",
  "minLauncherVersion": "0.1.0",
  "releaseNotes": [
    "Initial launcher-compatible Unity build."
  ]
}
```

## Package Shape

The zip must contain the Unity Windows build output, including the executable named by `entrypoint`.

```text
Dotshelf.exe
Dotshelf_Data/
UnityPlayer.dll
```

If the zip contains one top-level folder, the launcher searches recursively for `entrypoint` and installs from that folder.
