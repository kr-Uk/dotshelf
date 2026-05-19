using System;
using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// 오브젝트 편집 모드 인벤토리에 적용되는 "상단 메뉴 → 테마" 필터의 공유 상태.
    ///
    /// 상단 메뉴(Editor/RoomEditThemeFilterMenu) 가 ActiveTheme 을 set 하면 Changed 가 발사되며,
    /// RoomEditModeController 가 이를 받아 인벤토리를 다시 그린다. 카테고리 필터(selectedToolbarKey)
    /// 와는 별개 상태로 관리된다 — 두 필터는 AND 로 결합된다.
    ///
    /// 빈 문자열은 "전체 테마" (필터 해제) 를 의미한다. 그 외 값은 entry.ThemeTag 와 정확히 일치하는
    /// entry, 또는 entry.ThemeTag 가 빈 (= 무테마 공통 자산) entry 만 노출한다.
    ///
    /// 정적 상태 — Editor 와 Runtime 양쪽에서 같은 Assembly-CSharp 어셈블리를 통해 읽힌다.
    /// CoreCLR 전환 시 도메인 리로드로 초기화 (현재 Mono 와 동일 동작).
    /// </summary>
    public static class RoomEditThemeFilter
    {
        public const string AllThemesValue = "";

        private static string activeTheme = AllThemesValue;

        public static string ActiveTheme
        {
            get => activeTheme;
            set
            {
                var normalized = value ?? AllThemesValue;
                if (string.Equals(activeTheme, normalized, StringComparison.Ordinal))
                {
                    return;
                }
                activeTheme = normalized;
                Debug.Log($"[ThemePreview] Selected theme filter: {DescribeActive()}");
                Changed?.Invoke();
            }
        }

        public static event Action Changed;

        public static bool IsActive => !string.IsNullOrEmpty(activeTheme);

        // entry 가 현재 테마 필터를 통과하는지 판정. 빈 ThemeTag (legacy / 무테마 공통 자산) 는 항상 통과.
        // Active theme filters are strict: untagged entries are hidden while a full theme is selected.
        public static bool MatchesActive(string entryThemeTag)
        {
            if (!IsActive) return true;
            if (string.IsNullOrWhiteSpace(entryThemeTag)) return false;
            return string.Equals(entryThemeTag.Trim(), activeTheme, StringComparison.OrdinalIgnoreCase);
        }

        public static string DescribeActive()
        {
            return IsActive ? activeTheme : "전체";
        }
    }
}
