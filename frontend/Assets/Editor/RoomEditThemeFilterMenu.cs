using App.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Dotshelf.EditorTools
{
    /// <summary>
    /// 상단 메뉴 "Dotshelf > Theme > Filter Inventory By > ..." 진입점.
    /// 선택된 테마를 RoomEditThemeFilter.ActiveTheme 에 set 하고, 편집 모드 인벤토리가
    /// 자동 재그리기 되도록 한다 (RoomEditModeController 가 Changed 이벤트 구독).
    ///
    /// "All Themes (Clear Filter)" 는 필터를 해제 (빈 문자열).
    /// 메뉴는 Editor 어셈블리 전용 — Player 빌드에 포함되지 않음.
    /// </summary>
    public static class RoomEditThemeFilterMenu
    {
        private const string MenuPrefix = "Dotshelf/Theme/Filter Inventory By/";

        [MenuItem(MenuPrefix + "Natural")] public static void ApplyNatural() => Set("Natural");
        [MenuItem(MenuPrefix + "Classic")] public static void ApplyClassic() => Set("Classic");
        [MenuItem(MenuPrefix + "Modern")]  public static void ApplyModern()  => Set("Modern");
        [MenuItem(MenuPrefix + "Vintage")] public static void ApplyVintage() => Set("Vintage");
        [MenuItem(MenuPrefix + "Pastel")]  public static void ApplyPastel()  => Set("Pastel");

        [MenuItem(MenuPrefix + "All Themes (Clear Filter)")]
        public static void ClearFilter() => Set(RoomEditThemeFilter.AllThemesValue);

        // 메뉴 체크마크 — 현재 선택된 테마 옆에 ✓ 표시.
        [MenuItem(MenuPrefix + "Natural", validate = true)] private static bool VN() => Mark("Natural");
        [MenuItem(MenuPrefix + "Classic", validate = true)] private static bool VC() => Mark("Classic");
        [MenuItem(MenuPrefix + "Modern",  validate = true)] private static bool VM() => Mark("Modern");
        [MenuItem(MenuPrefix + "Vintage", validate = true)] private static bool VV() => Mark("Vintage");
        [MenuItem(MenuPrefix + "Pastel",  validate = true)] private static bool VP() => Mark("Pastel");
        [MenuItem(MenuPrefix + "All Themes (Clear Filter)", validate = true)]
        private static bool VA() => MarkClear();

        private static bool Mark(string theme)
        {
            Menu.SetChecked(MenuPrefix + theme,
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
            // RoomEditThemeFilter.ActiveTheme setter 가 [ThemePreview] 로그를 emit 하므로 중복 로그 회피.
            // 단 사용자에게 확정 피드백을 더 명확히 주기 위해 현재 라벨을 한 번 더 emit.
            Debug.Log($"[ThemePreview] Theme filter active = '{RoomEditThemeFilter.DescribeActive()}'. " +
                      "Play Mode 진입 + 오브젝트 편집 모드에서 인벤토리에 반영됨.");
        }
    }
}
