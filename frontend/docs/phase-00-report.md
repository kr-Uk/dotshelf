# Phase 00 Report - Project Setup

## Completed
- Added project guidance files: `AGENTS.md`, `README.md`, `.gitattributes`.
- Added `Assets/App` baseline folders.
- Added baseline asmdefs:
  - `Assets/App/Core/App.Core.asmdef`
  - `Assets/App/Domain/App.Domain.asmdef`
  - `Assets/App/Application/App.Application.asmdef`
  - `Assets/App/Infrastructure/App.Infrastructure.asmdef`
- Added `AppApiConfig` ScriptableObject type at `Assets/App/Config/AppApiConfig.cs`.
- Added folder/scenes setup menu:
  - `Tools > Project Setup > Create Folder Structure`
  - `Tools > Project Setup > Run Phase 0-1 Setup`
- Added required package entries for Cinemachine, Addressables, TextMeshPro shim, Newtonsoft JSON, UniTask, and FBX format support.

## Pending
- DOTween still requires manual installation from Asset Store or an agreed package source.
- Unity Editor was open, so batchmode scene generation could not run.
- `Packages/packages-lock.json` will update after Unity resolves the new package manifest.

## Verification
- Open Unity and let Package Manager resolve `Packages/manifest.json`.
- Run `Tools > Project Setup > Run Phase 0-1 Setup` after closing/reopening or once Unity finishes compiling.
- Confirm Console has 0 errors.

