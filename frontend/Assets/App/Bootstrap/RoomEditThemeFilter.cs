using System;
using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// Shared runtime state for the room-edit theme filter.
    /// Category builders opt into this filter; when active, only entries with the
    /// selected theme tag are visible. The empty value means "all themes".
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

        public static void ClearSilently()
        {
            activeTheme = AllThemesValue;
        }

        public static bool MatchesActive(string entryThemeTag)
        {
            if (!IsActive) return true;
            if (string.IsNullOrWhiteSpace(entryThemeTag)) return false;
            return string.Equals(entryThemeTag.Trim(), activeTheme, StringComparison.OrdinalIgnoreCase);
        }

        public static string DescribeActive()
        {
            return IsActive ? activeTheme : "All";
        }
    }
}
