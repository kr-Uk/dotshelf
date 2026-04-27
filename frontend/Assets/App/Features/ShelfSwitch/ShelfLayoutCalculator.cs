using UnityEngine;

namespace App.Features.ShelfSwitch
{
    public static class ShelfLayoutCalculator
    {
        private const float BookGap = 0.002f;
        private const float MinBookThickness = 0.001f;
        private const float DistanceEpsilon = 0.0001f;

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
            var bestCenterDistance = float.MaxValue;

            for (var i = 0; i < shelf.Sections.Length; i++)
            {
                var section = shelf.Sections[i];
                if (section == null)
                {
                    continue;
                }

                var bounds = section.GetLocalBounds();
                var closestPoint = GetClosestWorldPoint(section.transform, bounds, worldPoint);
                var distance = Vector3.Distance(worldPoint, closestPoint);
                var centerDistance = Vector3.SqrMagnitude(worldPoint - section.transform.TransformPoint(bounds.center));

                if (distance + DistanceEpsilon < bestDistance ||
                    Mathf.Abs(distance - bestDistance) <= DistanceEpsilon && centerDistance < bestCenterDistance)
                {
                    bestDistance = distance;
                    bestCenterDistance = centerDistance;
                    nearest = section;
                }
            }

            return bestDistance <= maxDistance ? nearest : null;
        }

        public static int ComputeInsertIndex(Vector3 worldPoint, ShelfSectionView section, float bookThickness)
        {
            var capacity = GetSectionCapacity(section, bookThickness);
            return ComputeInsertIndex(worldPoint, section, capacity, bookThickness);
        }

        public static int ComputeInsertIndex(Vector3 worldPoint, ShelfSectionView section, int itemCount, float bookThickness)
        {
            if (section == null)
            {
                return 0;
            }

            var local = section.transform.InverseTransformPoint(worldPoint);
            var bounds = section.GetLocalBounds();
            var thickness = Mathf.Max(MinBookThickness, bookThickness);
            var spacing = thickness + BookGap;
            var firstCenterX = bounds.min.x + thickness * 0.5f;
            var relativeToFirstCenter = local.x - firstCenterX;
            var index = relativeToFirstCenter <= 0f
                ? 0
                : Mathf.CeilToInt(relativeToFirstCenter / spacing);
            var maxInsertIndex = Mathf.Max(0, Mathf.Min(itemCount, GetSectionCapacity(section, thickness)));

            return Mathf.Clamp(index, 0, maxInsertIndex);
        }

        public static Vector3 GetBookLocalPosition(ShelfSectionView section, int slotIndex, int totalBooks, float bookThickness)
        {
            if (section == null)
            {
                return Vector3.zero;
            }

            var bounds = section.GetLocalBounds();
            var thickness = Mathf.Max(MinBookThickness, bookThickness);
            var spacing = thickness + BookGap;
            var maxSlotIndex = Mathf.Max(0, Mathf.Min(totalBooks - 1, GetSectionCapacity(section, thickness) - 1));
            var clampedSlotIndex = Mathf.Clamp(slotIndex, 0, maxSlotIndex);
            var x = bounds.min.x + thickness * 0.5f + spacing * clampedSlotIndex;
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
                EncapsulateTransformedBounds(ref result, ref hasBounds, section.transform, shelf.transform, sectionBounds);
            }

            return result;
        }

        private static int GetSectionCapacity(ShelfSectionView section, float bookThickness)
        {
            if (section == null)
            {
                return 0;
            }

            var thickness = Mathf.Max(MinBookThickness, bookThickness);
            var bounds = section.GetLocalBounds();
            return Mathf.Max(1, Mathf.FloorToInt((bounds.size.x + BookGap) / (thickness + BookGap)));
        }

        private static Vector3 GetClosestWorldPoint(Transform sourceTransform, Bounds sourceLocalBounds, Vector3 worldPoint)
        {
            var localPoint = sourceTransform.InverseTransformPoint(worldPoint);
            var closestLocalPoint = new Vector3(
                Mathf.Clamp(localPoint.x, sourceLocalBounds.min.x, sourceLocalBounds.max.x),
                Mathf.Clamp(localPoint.y, sourceLocalBounds.min.y, sourceLocalBounds.max.y),
                Mathf.Clamp(localPoint.z, sourceLocalBounds.min.z, sourceLocalBounds.max.z));

            return sourceTransform.TransformPoint(closestLocalPoint);
        }

        private static void EncapsulateTransformedBounds(
            ref Bounds targetBounds,
            ref bool hasBounds,
            Transform sourceTransform,
            Transform targetTransform,
            Bounds sourceLocalBounds)
        {
            var min = sourceLocalBounds.min;
            var max = sourceLocalBounds.max;

            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(min.x, min.y, min.z));
            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(min.x, min.y, max.z));
            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(min.x, max.y, min.z));
            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(min.x, max.y, max.z));
            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(max.x, min.y, min.z));
            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(max.x, min.y, max.z));
            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(max.x, max.y, min.z));
            EncapsulatePoint(ref targetBounds, ref hasBounds, sourceTransform, targetTransform, new Vector3(max.x, max.y, max.z));
        }

        private static void EncapsulatePoint(
            ref Bounds targetBounds,
            ref bool hasBounds,
            Transform sourceTransform,
            Transform targetTransform,
            Vector3 sourceLocalPoint)
        {
            var worldPoint = sourceTransform.TransformPoint(sourceLocalPoint);
            var targetLocalPoint = targetTransform.InverseTransformPoint(worldPoint);

            if (!hasBounds)
            {
                targetBounds = new Bounds(targetLocalPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            targetBounds.Encapsulate(targetLocalPoint);
        }
    }
}

