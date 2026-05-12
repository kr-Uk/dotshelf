using BookshelfPorting.Runtime;
using UnityEngine;

namespace App.Bootstrap
{
    // 노트북 포커스 진입 시 CameraController 가 사용할 mode 를 desk 카테고리 (theme) 별로 라우팅.
    //
    // 책임 분리 — Camera 시스템 (BookshelfPorting.Runtime.CameraController) 은 mode enum 만 받아
    // pose 계산. desk 별 mode 결정은 catalog/카테고리 swap 경로 (App.Bootstrap.DeskVisualSkinApplier)
    // 에서 entry.NotebookFocusCameraMode 를 읽어 여기로 push. Manager (BookshelfPorting.Runtime
    // .BookshelfExperienceManager) 는 FocusNotebook 시점에 Current 를 pull 해 CameraController 에
    // 전달 — Manager 가 catalog 를 직접 안 만져도 됨 (Manager 의 asmdef 가 App.Infrastructure.Catalogs
    // 를 안 봄).
    //
    // 정책:
    //   - 기본값 = ScreenAnchorBaked (vintage / 기본 procedural laptop). desk skin 미적용 / catalog
    //     entry 의 NotebookFocusCameraMode == 0 인 경우 모두 이 값.
    //   - DeskVisualSkinApplier.TryAttach 성공 시 entry 의 mode 로 Set.
    //   - DeskVisualSkinApplier.ClearSkin 시 default (ScreenAnchorBaked) 로 Reset — 테마 해제 후
    //     native procedural laptop 으로 돌아가면 CameraFocusPoint baked offset 이 정답이기 때문.
    //
    // CoreCLR 고려 — 정적 상태지만 ScriptableObject 처럼 OnDisable 에서 리셋하는 라이프사이클이
    // 아니라, 다음 desk swap 호출이 항상 덮어쓰므로 domain reload 후에도 안전. 초기값은 enum default
    // (= 0 = ScreenAnchorBaked) 라 첫 진입에도 backward-compatible.
    public static class NotebookFocusCameraModeRegistry
    {
        public static NotebookFocusCameraMode Current { get; private set; } = NotebookFocusCameraMode.ScreenAnchorBaked;

        // CustomizationEntry.NotebookFocusCameraMode (int) 를 enum 으로 cast 후 set. 알 수 없는
        // 값은 ScreenAnchorBaked 로 fallback.
        public static void SetFromEntryInt(int modeInt, string source)
        {
            NotebookFocusCameraMode mode;
            switch (modeInt)
            {
                case (int)NotebookFocusCameraMode.ScreenForwardGeometry:
                    mode = NotebookFocusCameraMode.ScreenForwardGeometry;
                    break;
                case (int)NotebookFocusCameraMode.ScreenAnchorBaked:
                    mode = NotebookFocusCameraMode.ScreenAnchorBaked;
                    break;
                default:
                    Debug.LogWarning($"[NotebookFocusCameraMode] unknown modeInt={modeInt} from source={source} — fallback ScreenAnchorBaked.");
                    mode = NotebookFocusCameraMode.ScreenAnchorBaked;
                    break;
            }
            if (Current != mode)
            {
                Debug.Log($"[NotebookFocusCameraMode] {Current} → {mode} (source={source})");
            }
            Current = mode;
        }

        public static void ResetToDefault(string source)
        {
            if (Current != NotebookFocusCameraMode.ScreenAnchorBaked)
            {
                Debug.Log($"[NotebookFocusCameraMode] {Current} → ScreenAnchorBaked (reset, source={source})");
            }
            Current = NotebookFocusCameraMode.ScreenAnchorBaked;
        }
    }
}
