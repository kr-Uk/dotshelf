using System;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [Serializable]
    public struct BookSizePreset
    {
        public BookSizePreset(string id, float height, float thickness, float depth, Color coverTint)
        {
            idValue = id;
            heightValue = height;
            thicknessValue = thickness;
            depthValue = depth;
            coverTintValue = coverTint;
        }

        private readonly string idValue;
        private readonly float heightValue;
        private readonly float thicknessValue;
        private readonly float depthValue;
        private readonly Color coverTintValue;

        public string Id => idValue;
        public float Height => heightValue;
        public float Thickness => thicknessValue;
        public float Depth => depthValue;
        public Color CoverTint => coverTintValue;
    }

    public static class BookSizePresets
    {
        public static readonly BookSizePreset SmallThin = new BookSizePreset("SmallThin", 0.205f, 0.024f, 0.155f, new Color(0.58f, 0.36f, 0.28f));
        public static readonly BookSizePreset SmallThick = new BookSizePreset("SmallThick", 0.210f, 0.040f, 0.158f, new Color(0.43f, 0.52f, 0.40f));
        public static readonly BookSizePreset Medium = new BookSizePreset("Medium", 0.240f, 0.032f, 0.162f, new Color(0.36f, 0.44f, 0.62f));
        public static readonly BookSizePreset TallThin = new BookSizePreset("TallThin", 0.270f, 0.026f, 0.165f, new Color(0.63f, 0.47f, 0.28f));
        public static readonly BookSizePreset TallThick = new BookSizePreset("TallThick", 0.275f, 0.044f, 0.168f, new Color(0.49f, 0.31f, 0.55f));

        public static readonly BookSizePreset[] All =
        {
            SmallThin,
            SmallThick,
            Medium,
            TallThin,
            TallThick
        };

        public static BookSizePreset Resolve(string sizePresetId, string deterministicSeed)
        {
            if (!string.IsNullOrWhiteSpace(sizePresetId))
            {
                for (var i = 0; i < All.Length; i++)
                {
                    if (string.Equals(All[i].Id, sizePresetId, StringComparison.OrdinalIgnoreCase))
                    {
                        return All[i];
                    }
                }
            }

            return ResolveDeterministic(deterministicSeed);
        }

        public static BookSizePreset ResolveDeterministic(string deterministicSeed)
        {
            var hash = GetStableHash(deterministicSeed);
            return All[hash % All.Length];
        }

        private static int GetStableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                if (!string.IsNullOrEmpty(value))
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }

                return Mathf.Abs(hash == int.MinValue ? int.MaxValue : hash);
            }
        }
    }
}
