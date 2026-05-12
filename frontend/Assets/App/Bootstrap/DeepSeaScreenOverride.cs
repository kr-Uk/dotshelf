using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // DeepSea 테마 (assetId=300030) 의 노트북 화면 fit (ScreenSaverCanvas + AdImage)
    // single orchestrator.
    //
    // 책임:
    //   - 활성/비활성 lifecycle 관리. 테마 적용 시 Activate, 테마 전환 시 Deactivate.
    //   - 알려진 trigger 시점마다 ReapplyIfActive(reason) 호출 → 두 RectTransform 재set.
    //     (theme_apply / mode_change / edit_enter / edit_exit / screen_rebuild /
    //     notebook_station_placement / guard_divergence)
    //   - 값 single source of truth = CustomizationEntry (DeskCatalog asset). 중복 하드코딩 없음.
    //
    // 비활성/다른 테마 보호:
    //   - ReapplyIfActive 는 active 가 아니면 즉시 no-op.
    //   - Activate 는 entry.AssetId != DeepSeaAssetId 면 무시.
    //   - 다른 테마의 native ScreenSaverCanvas / AdImage 동작은 절대 건드리지 않는다.
    //
    // CameraFocusPoint / Notebook 클릭/포커스 로직 변경 없음:
    //   - ScreenSaverCanvas 와 AdImage 의 RectTransform 만 수정.
    //   - Screen.transform / CameraFocusPoint / NotebookHitArea / 카메라 이동/포커스 로직은 unaffected.
    public static class DeepSeaScreenOverride
    {
        public const long DeepSeaAssetId = 300030L;

        // 활성화된 DeepSea entry 의 reference. Activate 시 set, Deactivate 시 null.
        // ReapplyIfActive 는 이 reference 가 살아 있을 때만 실 작업.
        private static CustomizationEntry s_entry;
        // roomRoot reference — Screen Transform 을 resolve 하는 출발점.
        private static Transform s_roomRoot;

        public static bool IsActive => s_entry != null && s_roomRoot != null;
        public static long ActiveAssetId => s_entry != null ? s_entry.AssetId : 0L;
        public static CustomizationEntry ActiveEntry => s_entry;

        // 외부에서 호출 (DeskVisualSkinApplier.AlignNotebookScreenToMonitor 마지막).
        // 활성 등록 + 즉시 reapply + Screen 에 ScreenSaverCanvasOverrideGuard 부착.
        // entry.AssetId != DeepSeaAssetId 또는 필수 플래그가 꺼져 있으면 활성화 skip.
        public static void RegisterActive(Transform roomRoot, CustomizationEntry entry, string reason)
        {
            if (entry == null || roomRoot == null) return;
            if (entry.AssetId != DeepSeaAssetId)
            {
                // DeepSea 가 아닌 entry 에서 호출되면 무시. 다른 테마는 native 동작 보존.
                return;
            }
            if (!entry.OverrideScreenSaverCanvasTransform && !entry.StretchCanvasAdImageOnDesk)
            {
                // entry 가 둘 다 비활성이면 override 할 게 없음. skip.
                return;
            }

            s_entry = entry;
            s_roomRoot = roomRoot;
            Reapply(reason);
        }

        // 외부에서 호출 (DeskVisualSkinApplier.ClearSkinUnder).
        // 활성 상태 해제. 다음 ReapplyIfActive 부터 no-op. Screen 의 Guard 도 자동 제거.
        public static void Deactivate(string reason)
        {
            if (!IsActive)
            {
                s_entry = null;
                s_roomRoot = null;
                return;
            }
            long prevAssetId = s_entry.AssetId;
            // Guard 정리 — Screen 에 부착된 ScreenSaverCanvasOverrideGuard 제거.
            // (DeskVisualSkinApplier.ClearSkinUnder 가 이미 destroy 하지만 idempotent 보호.)
            var screen = ResolveScreen(s_roomRoot);
            if (screen != null)
            {
                var guard = screen.GetComponent<ScreenSaverCanvasOverrideGuard>();
                if (guard != null)
                {
                    if (UnityEngine.Application.isPlaying) Object.Destroy(guard);
                    else Object.DestroyImmediate(guard);
                }
            }
            s_entry = null;
            s_roomRoot = null;
            Debug.Log($"[DeepSeaScreenUI] deactivated entryAssetId={prevAssetId} reason={reason}");
        }

        // 알려진 trigger 시점마다 호출 — 비활성 상태면 no-op 이라 호출 site 에서 가드 불필요.
        // reason 은 log 에 그대로 출력 (theme_apply / mode_change / edit_enter / edit_exit /
        // screen_rebuild / notebook_station_placement / guard_divergence).
        public static void ReapplyIfActive(string reason)
        {
            if (!IsActive) return;
            Reapply(reason);
        }

        // 내부 reapply — Screen resolve 실패 시 deactivate.
        private static void Reapply(string reason)
        {
            var screen = ResolveScreen(s_roomRoot);
            if (screen == null)
            {
                Debug.LogWarning($"[DeepSeaScreenUI] reapply skip — Screen transform not found under roomRoot. " +
                                 $"entryAssetId={ActiveAssetId} reason={reason}. roomRoot may have been destroyed; deactivating.");
                s_entry = null;
                s_roomRoot = null;
                return;
            }

            bool canvasApplied = false;
            bool adImageApplied = false;

            if (s_entry.OverrideScreenSaverCanvasTransform)
            {
                canvasApplied = ApplyDeepSeaScreenCanvasOverride(screen, s_entry, reason);
            }
            if (s_entry.StretchCanvasAdImageOnDesk)
            {
                adImageApplied = ApplyDeepSeaAdImageOverride(screen, s_entry, reason);
            }

            // Guard 부착 — 매 reapply 마다 Initialize 갱신 (target 값 변화 따라가게 함).
            EnsureGuardAttached(screen, s_entry, reason);

            Debug.Log($"[DeepSeaScreenUI] reapplied reason={reason} entryAssetId={s_entry.AssetId} " +
                      $"canvasApplied={canvasApplied} adImageApplied={adImageApplied}");
        }

        // ScreenSaverCanvas (Screen 자식) 의 RectTransform 을 entry 값으로 강제 set.
        // 반환: 실제 적용 성공 여부.
        public static bool ApplyDeepSeaScreenCanvasOverride(Transform screen, CustomizationEntry entry, string reason)
        {
            if (screen == null || entry == null) return false;

            Transform screenSaverCanvas = FindChild(screen, "ScreenSaverCanvas");
            if (screenSaverCanvas == null)
            {
                Debug.LogWarning($"[DeepSeaScreenUI] canvas override skip reason={reason} cause=screenSaverCanvas_not_found " +
                                 $"entryAssetId={entry.AssetId}. NotebookScreenController.Configure 호출 전이면 다음 trigger 까지 대기.");
                return false;
            }

            screenSaverCanvas.localPosition = entry.ScreenSaverCanvasLocalPosition;
            screenSaverCanvas.localEulerAngles = entry.ScreenSaverCanvasLocalRotation;

            var scale = entry.ScreenSaverCanvasLocalScale;
            if (scale.sqrMagnitude < 1e-10f) scale = Vector3.one;
            screenSaverCanvas.localScale = scale;

            var rect = screenSaverCanvas.GetComponent<RectTransform>();
            var sizeDelta = entry.ScreenSaverCanvasSizeDelta;
            bool applySizeDelta = sizeDelta.sqrMagnitude > 1e-4f;
            if (rect != null && applySizeDelta)
            {
                rect.sizeDelta = sizeDelta;
            }
            return true;
        }

        // ScreenSaverCanvas > AdImage 의 RectTransform 을 entry 값으로 강제 set.
        // 반환: 실제 적용 성공 여부.
        public static bool ApplyDeepSeaAdImageOverride(Transform screen, CustomizationEntry entry, string reason)
        {
            if (screen == null || entry == null) return false;

            Transform screenSaverCanvas = FindChild(screen, "ScreenSaverCanvas");
            if (screenSaverCanvas == null)
            {
                Debug.LogWarning($"[DeepSeaScreenUI] adImage override skip reason={reason} cause=screenSaverCanvas_not_found " +
                                 $"entryAssetId={entry.AssetId}. NotebookScreenController.Configure 호출 전일 수 있음.");
                return false;
            }

            Transform adImageTransform = FindChild(screenSaverCanvas, "AdImage");
            if (adImageTransform == null)
            {
                Debug.LogWarning($"[DeepSeaScreenUI] adImage override skip reason={reason} cause=adImage_not_found " +
                                 $"entryAssetId={entry.AssetId}. BuildScreenSaver 가 AdImage 를 만든 직후인지 확인.");
                return false;
            }

            var rect = adImageTransform.GetComponent<RectTransform>();
            if (rect == null)
            {
                Debug.LogWarning($"[DeepSeaScreenUI] adImage override skip reason={reason} cause=rectTransform_null " +
                                 $"entryAssetId={entry.AssetId}.");
                return false;
            }

            rect.anchorMin = entry.AdImageAnchorMin;
            rect.anchorMax = entry.AdImageAnchorMax;
            rect.pivot = entry.AdImagePivot;
            rect.offsetMin = entry.AdImageOffsetMin;
            rect.offsetMax = entry.AdImageOffsetMax;
            // localPosition.x/y 는 anchor 가 stretch 라 의미 없음. z=0 으로 set 해 카메라 plane 정렬.
            var lp = rect.localPosition;
            lp.z = 0f;
            rect.localPosition = lp;
            rect.localRotation = Quaternion.identity;

            var scale = entry.AdImageLocalScale;
            if (scale.sqrMagnitude < 1e-10f) scale = Vector3.one;
            rect.localScale = scale;
            return true;
        }

        // Guard 부착 — 이미 있으면 Initialize 만 다시 호출해 target 값 갱신.
        // Guard 는 LateUpdate 에서 canvas/AdImage 의 transform 이 native (FadeCanvas 등으로 reset)
        // 으로 돌아갔거나 canvas instance 가 재생성됐을 때 ReapplyIfActive 를 trigger.
        private static void EnsureGuardAttached(Transform screen, CustomizationEntry entry, string reason)
        {
            if (screen == null) return;
            var guard = screen.GetComponent<ScreenSaverCanvasOverrideGuard>();
            if (guard == null)
            {
                guard = screen.gameObject.AddComponent<ScreenSaverCanvasOverrideGuard>();
            }
            guard.Initialize(entry);
        }

        // roomRoot → NotebookStation → Desk → Notebook → Screen.
        private static Transform ResolveScreen(Transform roomRoot)
        {
            if (roomRoot == null) return null;
            Transform notebookStation = null;
            var all = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name == Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.NotebookStation)
                {
                    notebookStation = all[i];
                    break;
                }
            }
            if (notebookStation == null) return null;

            Transform desk = FindChild(notebookStation, "Desk");
            if (desk == null) return null;
            Transform notebook = FindChild(desk, Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.Notebook);
            if (notebook == null) return null;
            return FindChild(notebook, "Screen");
        }

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c != null && c.gameObject.name == name) return c;
            }
            return null;
        }
    }
}
