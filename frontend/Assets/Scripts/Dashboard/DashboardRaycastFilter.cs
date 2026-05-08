using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
            _ = pointerScreen;
            var dashboard = ResolveDashboardFromRaycast(result);
            return dashboard != null && !dashboard.InteractionEnabled;
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
}
