using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public static class BookFootprintUtility
    {
        private const float SpineFootprintMultiplier = 0.96f;
        private const float FrontFootprintMultiplier = 0.94f;
        private const float AngledFootprintMultiplier = 0.90f;
        private const float EdgePaddingMultiplier = 0.45f;

        public static float GetFootprint(float thickness, float depth, BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Spine:
                    return thickness;
                case BookOrientation.Front:
                    return depth;
                case BookOrientation.Angled45:
                    return Mathf.Abs(thickness * Mathf.Cos(Mathf.Deg2Rad * 45f)) +
                           Mathf.Abs(depth * Mathf.Sin(Mathf.Deg2Rad * 45f));
                default:
                    return thickness;
            }
        }

        public static float GetLayoutFootprint(float thickness, float depth, BookOrientation orientation)
        {
            var physicalFootprint = GetFootprint(thickness, depth, orientation);
            switch (orientation)
            {
                case BookOrientation.Spine:
                    return Mathf.Max(0f, physicalFootprint * SpineFootprintMultiplier);
                case BookOrientation.Front:
                    return Mathf.Max(0f, physicalFootprint * FrontFootprintMultiplier);
                case BookOrientation.Angled45:
                    return Mathf.Max(0f, physicalFootprint * AngledFootprintMultiplier);
                default:
                    return Mathf.Max(0f, physicalFootprint);
            }
        }

        public static float GetLayoutSpacing(float baseSpacing, BookOrientation orientation)
        {
            var safeBase = Mathf.Max(0f, baseSpacing);
            switch (orientation)
            {
                case BookOrientation.Spine:
                    return Mathf.Clamp(safeBase * 0.60f, 0.004f, 0.008f);
                case BookOrientation.Front:
                    return Mathf.Clamp(safeBase * 0.90f, 0.008f, 0.012f);
                case BookOrientation.Angled45:
                    return Mathf.Clamp(safeBase * 1.20f, 0.012f, 0.018f);
                default:
                    return safeBase;
            }
        }

        public static float GetPairSpacing(float baseSpacing, BookEntity left, BookEntity right)
        {
            if (left == null && right == null)
            {
                return Mathf.Max(0f, baseSpacing);
            }

            if (left == null)
            {
                return GetLayoutSpacing(baseSpacing, right.orientation);
            }

            if (right == null)
            {
                return GetLayoutSpacing(baseSpacing, left.orientation);
            }

            return Mathf.Max(
                GetLayoutSpacing(baseSpacing, left.orientation),
                GetLayoutSpacing(baseSpacing, right.orientation));
        }

        public static float GetEdgePadding(float baseSpacing)
        {
            return Mathf.Max(0f, baseSpacing) * EdgePaddingMultiplier;
        }

        public static float GetDepthExtent(float thickness, float depth, BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Spine:
                    return depth;
                case BookOrientation.Front:
                    return thickness;
                case BookOrientation.Angled45:
                    return Mathf.Abs(thickness * Mathf.Sin(Mathf.Deg2Rad * 45f)) +
                           Mathf.Abs(depth * Mathf.Cos(Mathf.Deg2Rad * 45f));
                default:
                    return depth;
            }
        }

        public static Quaternion GetVisualRotation(BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Spine:
                    return Quaternion.Euler(0f, 0f, 0f);
                case BookOrientation.Front:
                    return Quaternion.Euler(0f, 90f, 0f);
                case BookOrientation.Angled45:
                    return Quaternion.Euler(0f, 45f, 0f);
                default:
                    return Quaternion.identity;
            }
        }
    }
}
