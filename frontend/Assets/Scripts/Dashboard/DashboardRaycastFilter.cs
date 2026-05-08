using UnityEngine;
using UnityEngine.EventSystems;
using UIDocument = UnityEngine.UIElements.UIDocument;
using RuntimePanelUtils = UnityEngine.UIElements.RuntimePanelUtils;

namespace Dotshelf.Scripts.Dashboard
{
    public static class DashboardRaycastFilter
    {
        private const string PanelRaycasterTypeName = "UnityEngine.UIElements.PanelRaycaster";
        private const string WorldDocumentRaycasterTypeName = "UnityEngine.UIElements.WorldDocumentRaycaster";

        public static bool IsNonInteractiveDashboardRaycast(RaycastResult result, Vector2 pointerScreen)
        {
            if (IsNonInteractiveDashboardObject(result.gameObject))
            {
                return true;
            }

            var moduleObject = result.module != null ? result.module.gameObject : null;
            if (IsNonInteractiveDashboardObject(moduleObject))
            {
                return true;
            }

            return IsUiToolkitPanelRaycast(result) &&
                   IsPointerOverNonInteractiveDashboardDocument(pointerScreen);
        }

        private static bool IsNonInteractiveDashboardObject(GameObject resultObject)
        {
            if (resultObject == null)
            {
                return false;
            }

            var dashboard = resultObject.GetComponentInParent<DashboardScreenController>();
            return dashboard != null && !dashboard.InteractionEnabled;
        }

        private static bool IsUiToolkitPanelRaycast(RaycastResult result)
        {
            if (result.module == null)
            {
                return false;
            }

            var moduleTypeName = result.module.GetType().FullName;
            return moduleTypeName == PanelRaycasterTypeName ||
                   moduleTypeName == WorldDocumentRaycasterTypeName;
        }

        private static bool IsPointerOverNonInteractiveDashboardDocument(Vector2 pointerScreen)
        {
            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                if (document == null || !document.isActiveAndEnabled)
                {
                    continue;
                }

                var dashboard = document.GetComponentInParent<DashboardScreenController>();
                if (dashboard == null || dashboard.InteractionEnabled)
                {
                    continue;
                }

                var root = document.rootVisualElement;
                if (root == null || root.panel == null)
                {
                    continue;
                }

                var panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, pointerScreen);
                if (root.panel.Pick(panelPosition) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
