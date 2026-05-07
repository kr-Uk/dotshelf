# DotShelfLauncher

Windows launcher for installing, updating, and starting the Dotshelf Unity app.

## Projects

- `src/DotShelf.Launcher`: WPF desktop UI
- `src/DotShelf.Launcher.Core`: manifest, install, update, and launch logic
- `manifests/dev/windows/latest.json`: sample update manifest

## Local Build

```powershell
dotnet build .\src\DotShelf.Launcher\DotShelf.Launcher.csproj
```

> `.slnx` solution file은 .NET SDK 8.x에서 직접 `dotnet build` 대상이 되지 않습니다. 메인 WPF 프로젝트를 직접 빌드하세요.

## Update Model

The launcher reads a static manifest from:

```text
https://dotshelf.digital/launcher/windows/latest.json
```

It downloads the versioned Unity zip, verifies SHA-256, extracts it to `%LOCALAPPDATA%\Dotshelf`, and runs `Dotshelf.exe`.
