using App.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Dotshelf.EditorTools
{
    public static class RoomEditThemeFilterMenu
    {
        private const string MenuPrefix = "Dotshelf/Theme/Filter Inventory By/";

        [MenuItem(MenuPrefix + "Default")] public static void ApplyDefault() => Set("Default");
        [MenuItem(MenuPrefix + "DeepSea")] public static void ApplyDeepSea() => Set("DeepSea");
        [MenuItem(MenuPrefix + "Vintage")] public static void ApplyVintage() => Set("Vintage");
        [MenuItem(MenuPrefix + "Fantasy")] public static void ApplyFantasy() => Set("Fantasy");
        [MenuItem(MenuPrefix + "Space")] public static void ApplySpace() => Set("Space");
        [MenuItem(MenuPrefix + "Spooky")] public static void ApplySpooky() => Set("Spooky");

        [MenuItem(MenuPrefix + "All Themes (Clear Filter)")]
        public static void ClearFilter() => Set(RoomEditThemeFilter.AllThemesValue);

        [MenuItem(MenuPrefix + "Default", validate = true)] private static bool VD() => Mark("Default");
        [MenuItem(MenuPrefix + "DeepSea", validate = true)] private static bool VDS() => Mark("DeepSea");
        [MenuItem(MenuPrefix + "Vintage", validate = true)] private static bool VV() => Mark("Vintage");
        [MenuItem(MenuPrefix + "Fantasy", validate = true)] private static bool VF() => Mark("Fantasy");
        [MenuItem(MenuPrefix + "Space", validate = true)] private static bool VS() => Mark("Space");
        [MenuItem(MenuPrefix + "Spooky", validate = true)] private static bool VSP() => Mark("Spooky");
        [MenuItem(MenuPrefix + "All Themes (Clear Filter)", validate = true)]
        private static bool VA() => MarkClear();

        private static bool Mark(string theme)
        {
            Menu.SetChecked(
                MenuPrefix + theme,
                string.Equals(RoomEditThemeFilter.ActiveTheme, theme, System.StringComparison.OrdinalIgnoreCase));
            return true;
        }

        private static bool MarkClear()
        {
            Menu.SetChecked(MenuPrefix + "All Themes (Clear Filter)", !RoomEditThemeFilter.IsActive);
            return true;
        }

        private static void Set(string theme)
        {
            RoomEditThemeFilter.ActiveTheme = theme;
            Debug.Log($"[ThemePreview] Theme filter active = '{RoomEditThemeFilter.DescribeActive()}'.");
        }
    }
}
