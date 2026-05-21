using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIDocument = UnityEngine.UIElements.UIDocument;
using RuntimePanelUtils = UnityEngine.UIElements.RuntimePanelUtils;

namespace Dotshelf.Scripts.Dashboard
{
    public static class DashboardRaycastFilter
    {
        private static readonly List<DashboardScreenController> Controllers = new();

        public static void Register(DashboardScreenController controller)
        {
            if (controller == null || Controllers.Contains(controller))
            {
                return;
            }

            Controllers.Add(controller);
        }

        public static void Unregister(DashboardScreenController controller)
        {
            if (controller == null)
            {
                return;
            }

            Controllers.Remove(controller);
        }

        public static bool IsNonInteractiveDashboardRaycast(RaycastResult result, Vector2 pointerScreen)
        {
            var dashboard = ResolveDashboardFromRaycast(result);
            if (dashboard != null)
            {
                return !dashboard.InteractionEnabled;
            }

            if (!IsUiToolkitRaycast(result))
            {
                return false;
            }

            PruneDestroyedControllers();
            for (var i = 0; i < Controllers.Count; i++)
            {
                dashboard = Controllers[i];
                if (dashboard == null || !dashboard.isActiveAndEnabled || dashboard.InteractionEnabled)
                {
                    continue;
                }

                if (IsPointerOverDashboardDocument(dashboard, pointerScreen))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsBlockingRaycastResult(RaycastResult result, Vector2 pointerScreen)
        {
            if (result.gameObject == null)
            {
                return false;
            }

            if (IsNonInteractiveDashboardRaycast(result, pointerScreen))
            {
                return false;
            }

            // [Fix MR135] UI Toolkit WorldSpace UIDocument hit (WorldDocumentRaycaster) 는
            // 본질적으로 월드 오브젝트의 일부 (대시보드 패널 등) 이라 클릭 = 월드 클릭으로 봐야
            // 책장/노트북/대시보드 자체 포커싱이 동작. dev fix (697c92a) 가 RoomEditModeController
            // 에는 같은 skip 을 넣었지만 BookshelfExperienceManager.IsPointerOverUi 는 이 필터 기반이라
            // 빠져 있었음 → 모든 월드 클릭이 막히는 회귀. dashboard 자체 interaction 은 UI Toolkit
            // 의 자체 입력 dispatch 가 처리해서 EventSystem 차단과는 별개.
            if (IsUiToolkitWorldSpaceRaycast(result))
            {
                return false;
            }

            return !IsNonBlockingWorldSpaceGraphicRaycast(result);
        }

        // WorldDocumentRaycaster = UI Toolkit world-space panel raycaster. PanelRaycaster
        // (screen-space) 와 구분 — screen-space 는 진짜 HUD 차단 대상이라 skip 안 함.
        private static bool IsUiToolkitWorldSpaceRaycast(RaycastResult result)
        {
            var moduleTypeName = result.module != null ? result.module.GetType().FullName : null;
            return moduleTypeName == "UnityEngine.UIElements.WorldDocumentRaycaster";
        }

        public static bool IsPointerOverInteractiveDashboard(Vector2 pointerScreen)
        {
            PruneDestroyedControllers();
            for (var i = 0; i < Controllers.Count; i++)
            {
                var dashboard = Controllers[i];
                if (dashboard == null || !dashboard.isActiveAndEnabled || !dashboard.InteractionEnabled)
                {
                    continue;
                }

                if (IsPointerOverDashboardDocument(dashboard, pointerScreen))
                {
                    return true;
                }
            }

            return false;
        }

        private static DashboardScreenController ResolveDashboardFromRaycast(RaycastResult result)
        {
            var dashboard = ResolveDashboardObject(result.gameObject);
            if (dashboard != null)
            {
                return dashboard;
            }

            var moduleObject = result.module != null ? result.module.gameObject : null;
            dashboard = ResolveDashboardObject(moduleObject);
            if (dashboard != null)
            {
                return dashboard;
            }

            return null;
        }

        private static DashboardScreenController ResolveDashboardObject(GameObject resultObject)
        {
            if (resultObject == null)
            {
                return null;
            }

            return resultObject.GetComponentInParent<DashboardScreenController>();
        }

        private static bool IsUiToolkitRaycast(RaycastResult result)
        {
            // UI Toolkit world-panel raycasters are internal Unity types, so keep
            // this string check isolated and re-verify it when upgrading Unity.
            var moduleTypeName = result.module != null ? result.module.GetType().FullName : null;
            return moduleTypeName == "UnityEngine.UIElements.PanelRaycaster" ||
                   moduleTypeName == "UnityEngine.UIElements.WorldDocumentRaycaster";
        }

        private static bool IsNonBlockingWorldSpaceGraphicRaycast(RaycastResult result)
        {
            var resultObject = result.gameObject;
            if (resultObject == null)
            {
                return false;
            }

            Canvas canvas = null;
            if (result.module is GraphicRaycaster graphicRaycaster)
            {
                canvas = graphicRaycaster.GetComponent<Canvas>();
            }

            if (canvas == null)
            {
                canvas = resultObject.GetComponentInParent<Canvas>();
            }

            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            {
                return false;
            }

            if (resultObject.GetComponentInParent<Selectable>() != null ||
                resultObject.GetComponentInParent<ScrollRect>() != null)
            {
                return false;
            }

            return resultObject.GetComponentInParent<WorldSpaceRaycastPassthrough>() != null;
        }

        private static bool IsPointerOverDashboardDocument(DashboardScreenController dashboard, Vector2 pointerScreen)
        {
            if (dashboard == null)
            {
                return false;
            }

            var document = dashboard.GetComponent<UIDocument>();
            if (document == null || !document.isActiveAndEnabled)
            {
                return false;
            }

            var root = document.rootVisualElement;
            if (root == null || root.panel == null)
            {
                return false;
            }

            var panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, pointerScreen);
            return root.panel.Pick(panelPosition) != null;
        }

        private static void PruneDestroyedControllers()
        {
            for (var i = Controllers.Count - 1; i >= 0; i--)
            {
                if (Controllers[i] == null)
                {
                    Controllers.RemoveAt(i);
                }
            }
        }
    }

    public sealed class WorldSpaceRaycastPassthrough : MonoBehaviour
    {
    }
}
