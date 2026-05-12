using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public static class DashboardPanelLayout
    {
        private const float FallbackWorldScale = 0.00060f;
        private const float HorizontalInset = 0.10f;
        private const float VerticalInset = 0.06f;
        private static readonly Vector3 SurfaceOffset = new(0.003f, 0f, 0f);
        private static readonly Quaternion LocalRotation = Quaternion.Euler(0f, -90f, 0f);
        private static readonly Vector2 ReferenceSize = new(1920f, 1080f);

        public static void Mount(Transform panel, Transform hitArea)
        {
            if (panel == null || hitArea == null) return;
            if (panel.parent != null)
            {
                panel.SetParent(null, true);
            }

            panel.position = hitArea.TransformPoint(SurfaceOffset);
            panel.rotation = hitArea.rotation * LocalRotation;
            var panelScale = ResolveScale(hitArea);
            // Keep the room-facing UI Toolkit panel readable without negative scale:
            // UIDocument's generated BoxCollider does not support negative scale.
            panel.localScale = new Vector3(panelScale, panelScale, panelScale);
        }

        private static float ResolveScale(Transform hitArea)
        {
            var collider = hitArea != null ? hitArea.GetComponent<BoxCollider>() : null;
            if (collider == null) return FallbackWorldScale;

            var usableWidth = Mathf.Max(0.1f, collider.size.z - HorizontalInset);
            var usableHeight = Mathf.Max(0.1f, collider.size.y - VerticalInset);
            var scaleByWidth = usableWidth / ReferenceSize.x;
            var scaleByHeight = usableHeight / ReferenceSize.y;
            return Mathf.Min(scaleByWidth, scaleByHeight);
        }
    }
}
