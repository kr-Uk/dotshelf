using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // Screen GameObject (NotebookStation > Desk > Notebook > Screen) 에 부착되는 MonoBehaviour 로,
    // ScreenSaverCanvas 와 AdImage 의 RectTransform 이 native (NotebookScreenController.FadeCanvas /
    // RemoveExistingCanvas → CreateCanvas) 으로 reset 됐을 때 DeepSeaScreenOverride 의 ReapplyIfActive
    // 를 호출. 직접 transform 을 write 하지는 않는다 — single source 인 orchestrator 에 위임.
    //
    // 감지 패턴:
    //   1. canvas / adImage instance 가 null (또는 cached reference 와 다름) — canvas 재생성으로 인식,
    //      reason="screen_rebuild" 으로 ReapplyIfActive 호출.
    //   2. canvas / adImage 의 transform 값이 target 과 다름 — FadeCanvas state 전환으로 인식,
    //      reason="guard_divergence" 으로 ReapplyIfActive 호출.
    //
    // 매 프레임 발생할 수 있는 비용은 reference / Vector3 비교 6~7회. transform 을 직접 write 하는
    // 코드 경로는 없다 — divergence 가 감지된 경우에만 orchestrator 가 한 번 set.
    //
    // 테마 전환 (DeepSea → 다른 테마) 시 DeepSeaScreenOverride.Deactivate 가 이 컴포넌트를 destroy.
    [DisallowMultipleComponent]
    public sealed class ScreenSaverCanvasOverrideGuard : MonoBehaviour
    {
        private const string CanvasName = "ScreenSaverCanvas";
        private const string AdImageName = "AdImage";

        // entry reference — target 값 source. orchestrator 가 destroy 한 후엔 null 안전.
        [SerializeField] private long entryAssetId;

        private CustomizationEntry trackedEntry;
        private Transform cachedCanvas;
        private RectTransform cachedCanvasRect;
        private Transform cachedAdImage;
        private RectTransform cachedAdImageRect;
        private int reapplyCountSinceLog;
        private const int LogIntervalFrames = 600; // ~10초 (60fps) — spam 방지

        public void Initialize(CustomizationEntry entry)
        {
            // entry 가 같으면 cache reset 안 함 — 그렇지 않으면 매 Reapply 호출이 cache 를 비우고,
            // 다음 LateUpdate 가 ResolveChildren 으로 같은 canvas 를 다시 찾으면 instance 변경으로
            // 인식해서 ReapplyIfActive("screen_rebuild") 호출 → 또 Initialize → cache reset → 무한
            // 루프. ReferenceEquals 로 entry instance 비교해 신규/변경 시에만 cache 무효화.
            bool entryChanged = !object.ReferenceEquals(trackedEntry, entry);
            trackedEntry = entry;
            entryAssetId = entry != null ? entry.AssetId : 0L;
            if (entryChanged)
            {
                cachedCanvas = null;
                cachedCanvasRect = null;
                cachedAdImage = null;
                cachedAdImageRect = null;
                reapplyCountSinceLog = 0;
            }
        }

        private void LateUpdate()
        {
            if (trackedEntry == null) return;

            // (1) canvas instance 재생성 감지 — cached null 또는 destroy 됐으면 재탐색 + reapply.
            bool canvasRebuilt = false;
            if (cachedCanvas == null)
            {
                ResolveChildren();
                if (cachedCanvas != null) canvasRebuilt = true;
            }
            if (cachedCanvas == null) return; // 아직 canvas 가 안 만들어진 상태

            // (2) AdImage instance 재생성 감지 — canvas 가 같아도 AdImage 만 다시 만들어진 경우 대비.
            if (cachedAdImage == null)
            {
                ResolveAdImage();
                if (cachedAdImage != null) canvasRebuilt = true;
            }

            if (canvasRebuilt)
            {
                DeepSeaScreenOverride.ReapplyIfActive("screen_rebuild");
                // ReapplyIfActive 가 transform 을 set 했으니 즉시 divergence 검사 skip.
                return;
            }

            // (3) transform divergence 감지 — FadeCanvas / ApplyCanvasPresentation 등이 canvas
            //     localPosition/Rotation/Scale 을 native 로 reset 했거나 AdImage 가 native 값으로 돌아간 경우.
            //     OverrideScreenSaverCanvasAnchors=true 인 entry (예: Vintage) 는 anchor/pivot 도 검사
            //     — Unity RectTransform default (0.5, 0.5) 로 reset 됐는지 catch.
            bool divergent = false;
            if (trackedEntry.OverrideScreenSaverCanvasTransform)
            {
                var targetScale = trackedEntry.ScreenSaverCanvasLocalScale;
                if (targetScale.sqrMagnitude < 1e-10f) targetScale = Vector3.one;
                if (cachedCanvas.localPosition != trackedEntry.ScreenSaverCanvasLocalPosition ||
                    cachedCanvas.localRotation != Quaternion.Euler(trackedEntry.ScreenSaverCanvasLocalRotation) ||
                    cachedCanvas.localScale != targetScale)
                {
                    divergent = true;
                }
                if (!divergent && trackedEntry.OverrideScreenSaverCanvasAnchors && cachedCanvasRect != null)
                {
                    if (cachedCanvasRect.anchorMin != trackedEntry.ScreenSaverCanvasAnchorMin ||
                        cachedCanvasRect.anchorMax != trackedEntry.ScreenSaverCanvasAnchorMax ||
                        cachedCanvasRect.pivot != trackedEntry.ScreenSaverCanvasPivot)
                    {
                        divergent = true;
                    }
                }
            }
            if (!divergent && trackedEntry.StretchCanvasAdImageOnDesk && cachedAdImageRect != null)
            {
                var targetAdScale = trackedEntry.AdImageLocalScale;
                if (targetAdScale.sqrMagnitude < 1e-10f) targetAdScale = Vector3.one;
                if (cachedAdImageRect.offsetMin != trackedEntry.AdImageOffsetMin ||
                    cachedAdImageRect.offsetMax != trackedEntry.AdImageOffsetMax ||
                    cachedAdImageRect.localScale != targetAdScale)
                {
                    divergent = true;
                }
            }

            if (divergent)
            {
                DeepSeaScreenOverride.ReapplyIfActive("guard_divergence");
                if (reapplyCountSinceLog >= LogIntervalFrames)
                {
                    Debug.Log($"[DeepSeaScreenUI] Guard divergence reapply count entryAssetId={entryAssetId} " +
                              $"(이 로그는 ~{LogIntervalFrames} frame 마다 한 번 — divergence trigger 추적용)");
                    reapplyCountSinceLog = 0;
                }
                else
                {
                    reapplyCountSinceLog++;
                }
            }
        }

        private void ResolveChildren()
        {
            cachedCanvas = null;
            cachedCanvasRect = null;
            cachedAdImage = null;
            cachedAdImageRect = null;

            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c == null) continue;
                if (c.gameObject.name != CanvasName) continue;
                cachedCanvas = c;
                cachedCanvasRect = c.GetComponent<RectTransform>();
                break;
            }
            ResolveAdImage();
        }

        private void ResolveAdImage()
        {
            cachedAdImage = null;
            cachedAdImageRect = null;
            if (cachedCanvas == null) return;
            for (int i = 0; i < cachedCanvas.childCount; i++)
            {
                var c = cachedCanvas.GetChild(i);
                if (c == null) continue;
                if (c.gameObject.name != AdImageName) continue;
                cachedAdImage = c;
                cachedAdImageRect = c.GetComponent<RectTransform>();
                break;
            }
        }
    }
}
