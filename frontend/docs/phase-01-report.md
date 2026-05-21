# Phase 01 Report - Scene Skeleton

## Completed
- Added `Assets/Scenes/MainScene.unity` and `Assets/Scenes/BootstrapScene.unity` as initial scene assets.
- Updated Build Settings so `MainScene` is the first enabled scene.
- Updated `ProjectSettings.asset` template default scene to `Assets/Scenes/MainScene.unity`.
- Added initial scene setup script at `Assets/App/Editor/PhaseSceneBuilder.cs`.
- Added `RoomStateController`, `CameraStateController`, and `DebugHud` skeletons.
- Added `BookshelfSceneInstaller` and gated the legacy auto bootstrap so a `MainScene` installer can own startup.

## Current MainScene Behavior
- `MainScene` is now the first build scene.
- `MainScene` now contains `BookshelfSceneInstaller`.
- `BookshelfSceneInstaller` installs the prototype room/bookshelf both in edit mode and in play mode.
- `MainScene.unity` now contains saved generated content, including `BookshelfSystems`, `RoomRoot`, `BookshelfRoot`, `BooksRoot`, `GuestbookBoard`, and `NotebookStation`.
- The legacy `BookshelfAutoBootstrap` only handles scenes that do not contain a `BookshelfSceneInstaller`.

## Pending
- If the room/bookshelf disappears after scene edits, run `Tools > Project Setup > Build MainScene Content` inside Unity to regenerate and save the scene content.
- Replace placeholder camera anchors with Cinemachine virtual cameras once the package resolves.
- Add screenshots after scene generation.
