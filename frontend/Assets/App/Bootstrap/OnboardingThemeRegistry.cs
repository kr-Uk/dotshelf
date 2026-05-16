using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Bootstrap
{
    internal static class OnboardingThemeRegistry
    {
        internal sealed class ThemeDefinition
        {
            public ThemeDefinition(
                string themeId,
                string assetName,
                string debugName,
                string spriteName,
                string caption,
                Color previewColor)
            {
                ThemeId = themeId;
                AssetName = assetName;
                DebugName = debugName;
                SpriteName = spriteName;
                Caption = caption;
                PreviewColor = previewColor;
            }

            public string ThemeId { get; }
            public string AssetName { get; }
            public string DebugName { get; }
            public string SpriteName { get; }
            public string Caption { get; }
            public Color PreviewColor { get; }
        }

        private static readonly ThemeDefinition[] orderedThemes =
        {
            new ThemeDefinition("기본", "RoomTheme_Default", "Default", "theme_default",
                "현재 룸의 기본 분위기를 유지하는 안정적인 시작 테마입니다.",
                Hex("#E6DDD2")),
            new ThemeDefinition("깊은바다", "RoomTheme_DeepSea", "DeepSea", "theme_deepsea",
                "청록빛 조명과 해양 소품으로 채운 깊은 바다 분위기입니다.",
                Hex("#9FD8E6")),
            new ThemeDefinition("빈티지", "RoomTheme_Vintage", "Vintage", "theme_vintage",
                "오래된 목재와 따뜻한 조도로 정리한 빈티지 분위기입니다.",
                Hex("#E0D0C4")),
            new ThemeDefinition("판타지", "RoomTheme_Fantasy", "Fantasy", "theme_fantasy",
                "마법 소품과 보랏빛 톤이 살아있는 판타지 분위기입니다.",
                Hex("#D9CCF7")),
            new ThemeDefinition("우주", "RoomTheme_Space", "Space", "theme_space",
                "차가운 블루 톤과 우주 탐사 소품이 중심인 분위기입니다.",
                Hex("#B8C9F5")),
            new ThemeDefinition("스푸키", "RoomTheme_Spooky", "Spooky", "theme_spooky",
                "어두운 보랏빛과 할로윈 소품으로 꾸민 스푸키 분위기입니다.",
                Hex("#D2B4F0")),
        };

        private static readonly Dictionary<string, ThemeDefinition> themesById =
            BuildMap();

        public static IReadOnlyList<string> OrderedThemeIds { get; } = Array.ConvertAll(
            orderedThemes,
            definition => definition.ThemeId);

        public const string DefaultThemeId = "기본";

        public static bool TryGet(string themeId, out ThemeDefinition definition)
        {
            return themesById.TryGetValue(themeId ?? string.Empty, out definition);
        }

        public static string GetAssetName(string themeId)
        {
            return TryGet(themeId, out var definition) ? definition.AssetName : null;
        }

        public static string GetDebugName(string themeId)
        {
            return TryGet(themeId, out var definition) ? definition.DebugName : "Unknown";
        }

        public static string GetCaption(string themeId)
        {
            return TryGet(themeId, out var definition)
                ? definition.Caption
                : "테마를 선택하면 이 영역의 분위기가 변경됩니다.";
        }

        public static Color GetPreviewColor(string themeId, Color fallback)
        {
            return TryGet(themeId, out var definition) ? definition.PreviewColor : fallback;
        }

        public static IEnumerable<ThemeDefinition> Enumerate()
        {
            return orderedThemes;
        }

        private static Dictionary<string, ThemeDefinition> BuildMap()
        {
            var map = new Dictionary<string, ThemeDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < orderedThemes.Length; i++)
            {
                var definition = orderedThemes[i];
                map[definition.ThemeId] = definition;
            }

            return map;
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
        }
    }
}
