# Progress Audit

## Completed In This Pass
- Phase 00 baseline structure exists under `Assets/App`.
- Phase 01 setup script exists and Build Settings now start from `Assets/Scenes/MainScene.unity`.
- `Assets/Scenes/BootstrapScene.unity` and `Assets/Scenes/MainScene.unity` exist as initial scene assets.
- `MainScene` contains saved generated room/bookshelf content plus a scene-owned `BookshelfSceneInstaller`.
- The existing prototype bootstrap is gated so it does not duplicate startup.
- Phase 02 and Phase 03 architecture skeletons exist for shelves, sections, books, and focus state.
- Package manifest includes Addressables, Cinemachine, UniTask, Newtonsoft JSON, TextMeshPro, and FBX support.

## Blocked Or Pending
- Unity batchmode could not run because another Unity instance has this project open.
- If Unity scene edits remove generated content, run `Tools > Project Setup > Build MainScene Content` inside Unity.
- DOTween is still not installed because no usable package source was confirmed.
- Phases 04 through 10 are not implemented yet.

## Next Implementation Order
1. Run the setup menu inside Unity and confirm Console has 0 compile errors.
2. Complete Phase 02 prefab/scene authoring for real shelf sections.
3. Complete Phase 03 book prefab and input binding.
4. Implement Phase 04 same-shelf drag and commit-on-drop behavior.
5. Implement Phase 05 gap preview without mutating Domain placement data.
6. Implement Phase 06 cross-shelf drag movement.
7. Implement Phase 07 mock API and repository layer.
8. Implement Phase 08 search/add flow.
9. Implement Phase 09 guestbook/feed.
10. Stabilize with tests and cleanup in Phase 10.
