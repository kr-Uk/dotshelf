using UnityEngine;

namespace App.Features.ShelfSwitch
{
    public static class ShelfLayoutCalculator
    {
        public static bool IsInsideShelfBounds(Vector3 worldPoint, ShelfView shelf)
        {
            if (shelf == null)
            {
                return false;
            }

            var local = shelf.transform.InverseTransformPoint(worldPoint);
            var bounds = CalculateShelfLocalBounds(shelf);
            return bounds.Contains(local);
        }

        public static ShelfSectionView FindNearestSection(Vector3 worldPoint, ShelfView shelf, float maxDistance = 0.3f)
        {
            if (shelf == null || shelf.Sections == null)
            {
                return null;
            }

            ShelfSectionView nearest = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < shelf.Sections.Length; i++)
            {
                var section = shelf.Sections[i];
                if (section == null)
                {
                    continue;
                }

                var center = section.transform.position;
                var distance = Vector3.Distance(worldPoint, center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = section;
                }
            }

            return bestDistance <= maxDistance ? nearest : null;
        }

        public static int ComputeInsertIndex(Vector3 worldPoint, ShelfSectionView section, float bookThickness)
        {
            if (section == null)
            {
                return 0;
            }

            var local = section.transform.InverseTransformPoint(worldPoint);
            var bounds = section.GetLocalBounds();
            var normalized = Mathf.InverseLerp(bounds.min.x, bounds.max.x, local.x);
            var maxSlots = Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / Mathf.Max(0.001f, bookThickness)));
            return Mathf.Clamp(Mathf.RoundToInt(normalized * maxSlots), 0, maxSlots);
        }

        public static Vector3 GetBookLocalPosition(ShelfSectionView section, int slotIndex, int totalBooks, float bookThickness)
        {
            if (section == null)
            {
                return Vector3.zero;
            }

            var bounds = section.GetLocalBounds();
            var spacing = bookThickness + 0.002f;
            var x = bounds.min.x + bookThickness * 0.5f + spacing * Mathf.Max(0, slotIndex);
            return new Vector3(x, bounds.min.y + bounds.size.y * 0.5f, bounds.center.z);
        }

        public static Bounds GetSectionLocalBounds(ShelfSectionView section)
        {
            return section != null ? section.GetLocalBounds() : new Bounds(Vector3.zero, Vector3.zero);
        }

        private static Bounds CalculateShelfLocalBounds(ShelfView shelf)
        {
            var hasBounds = false;
            var result = new Bounds(Vector3.zero, Vector3.zero);

            if (shelf.Sections == null)
            {
                return result;
            }

            for (var i = 0; i < shelf.Sections.Length; i++)
            {
                var section = shelf.Sections[i];
                if (section == null)
                {
                    continue;
                }

                var sectionBounds = section.GetLocalBounds();
                var worldMin = section.transform.TransformPoint(sectionBounds.min);
                var worldMax = section.transform.TransformPoint(sectionBounds.max);
                var localMin = shelf.transform.InverseTransformPoint(worldMin);
                var localMax = shelf.transform.InverseTransformPoint(worldMax);

                if (!hasBounds)
                {
                    result = new Bounds((localMin + localMax) * 0.5f, localMax - localMin);
                    hasBounds = true;
                    continue;
                }

                result.Encapsulate(localMin);
                result.Encapsulate(localMax);
            }

            return result;
        }
    }
}

