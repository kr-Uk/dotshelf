# Phase 04 Report - Dashboard Foundation And Modals

## Completed
- Added clean-architecture skeleton under `Assets/App/{Domain,Application,Infrastructure}/Dashboard/` (7 entities, 5 repository interfaces, 18 use cases, error/page DTOs).
- Added `MockDashboardRepository`, `MockGuestbookRepository`, `MockSpaceSettingsRepository`, `MockFollowRepository`, `MockLibraryViewRepository` with seeded deterministic data for offline UI work.
- Added `ApiDashboardRepository`, `ApiGuestbookRepository`, `ApiSpaceSettingsRepository`, `ApiFollowRepository`, `ApiLibraryViewRepository` and `DashboardDtoMapper`; backend wiring waits on the spec already filed in Notion.
- Extended `NotebookApiClient` and `NotebookApiDtos` with the new endpoints + DTO gaps (whitelist GET/PUT, user-scoped follow lists, reply delete, all field additions). Existing callers untouched.
- Added EditMode tests under `Assets/App/Tests/EditMode/Dashboard/` (8 test files, 1 fake repository helper, `App.Tests.EditMode.asmdef`).
- Added `Assets/App/Infrastructure/Catalogs/PostItStyleCatalogAsset.cs` (ScriptableObject) + `PostItStyleCatalogAdapter` for client-bundled post-it lookup by `assetId`.
- Added `Assets/App/Features/Dashboard/Presentation/DashboardView.uxml` (left profile/stats/carousels + right guestbook grid + absolute action overlay) and matching USS.
- Added `Assets/Scripts/Dashboard/DashboardScreenController.cs` and `DashboardSceneInstaller.cs` ([DefaultExecutionOrder(-100)], Mock/Api toggle); controller falls back to Mock if no installer is present.
- Implemented main view bindings: profile header with introduction/follow counts, like toggle (debounced + optimistic), stats row, recent books and reading-records carousels (10 items, ScrollView with hidden scrollbars and pointer-drag scroll), guestbook 3x5 grid with cursor pagination.
- Implemented modals: followers/followings (mutual badge, follow/unfollow/block actions, owner-only block), guestbook write (30-char counter, post-it picker, anonymous toggle), guestbook detail (delete/report + reply form/display with permission branching), dashboard edit (introduction, visibility radio, post-it whitelist), owned-books grid with 3-way sort + load more, reading-records list with 5-way sort + load more.
- Added motion tokens (`--motion-fast/base/slow`, `--easing-*`) at `:root` and applied GPU-safe transitions: button `:active` scale on action buttons, like-button pulse on `liked`, card `:hover` translate on book/record/postit/guestbook cells.
- Backend extension request published to Notion ([API > 사용자 앱 대시보드 확장 요청 v1.1](https://app.notion.com/p/35169b2a131a811f8788df1810764f55)) — DTO gaps, 5 new endpoints, error codes.

## Current Behavior
- Attaching `DashboardScreenController` (and optionally `DashboardSceneInstaller`) to a UIDocument in any scene renders the full dashboard from mock data — no backend required. The README at `Assets/Scripts/Dashboard/README.md` is the 5-minute setup guide.
- Like button on the stats row toggles between `♥`/`♡` with a small `:active` press scale and a pulse when liked; double-clicks within ~1s are coalesced.
- Recent books/records carousels show 10 items each, scroll horizontally by mouse drag (≥5px threshold so short clicks still hit cells), and have hidden scrollbars.
- Guestbook grid renders 3x5 cells with `prev/next` pagination; clicking a cell opens the detail modal where the owner can reply (post-it style + 30-char body), and report/delete buttons appear based on `MockSelfUserId == ownerId || writerUserId`.
- "전체보기" buttons open the owned-books modal (90×120 cover grid with title) and the reading-records modal (cards with up to 200-char body and like/comment/view counts); both support sort change (replaces list) and "더 보기" (appends with cursor) and stop showing the load-more button when `hasMore == false`.
- Followers/Followings count buttons in the profile header open a shared modal; rows show avatar placeholder + nickname + "맞팔" tag, with optimistic follow/unfollow toggle. The block button only appears in the owner's followers list.
- Edit modal pulls current introduction/visibility/whitelist via use cases, then dispatches three updates in parallel on submit and refreshes the snapshot.
- Repository mode is decided once in `DashboardSceneInstaller.Awake` (default `Mock`); switching to `Api` instantiates `NotebookApiClient` + `Api*Repository` without touching controller code — base-URL-only divergence as required by `frontend/CLAUDE.md`.

## Pending
- Backend ships the spec'd endpoints/DTO fields, then flip `DashboardSceneInstaller.Mode` to `Api` and smoke-test against `docker compose up`-ed backend.
- `ApiLibraryViewRepository.LoadOwnedBooks` / `LoadReadingRecords` currently return empty pages — backend sort/cursor parameters missing. Replace TODO with real mapping when the API gains them.
- `PostItStyleCatalogAsset` instance has no entries yet — author needs to `Create > Dotshelf > PostIt Style Catalog` and fill assetId + sprite per style; controller currently uses hardcoded ids 1–5 from `MockPostItStyleIds`.
- Modal show/hide remains a `display:none` toggle; no fade-in transition. UI Toolkit `display` is discrete, so smoothing requires switching `.modal-root.hidden` to opacity + picking-mode and adding a transition pass.
- "차단" button hides a row in the followers modal but the unblock surface lives in the future Settings screen, out of dashboard scope (per Notion spec, decision a/b/c).
- Block-relation 403 handling for `/{userId}/followers` and `/{userId}/followings` — backend will return `BLOCKED_RELATION`, controller currently falls through `OnError`.
- Wire haptic feedback on commit-meaningful interactions (like toggle, write submit) once a `Features/Haptics/` adapter exists.
- PlayMode tests for the modal flows (open → submit → close → list refreshed). EditMode tests cover use-case validation only.
