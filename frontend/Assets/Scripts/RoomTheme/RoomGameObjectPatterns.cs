namespace Dotshelf.Scripts.RoomTheme
{
    /// <summary>
    /// 룸 GameObject 이름 매칭 패턴. RoomThemeApplier(테마 일괄 swap)와
    /// RoomEditModeController(카테고리별 단일 swap) 두 곳에서 같은 패턴을 써야
    /// BookshelfGenerator의 GameObject 이름 변경 시 한 곳만 갱신하면 됨.
    /// </summary>
    public static class RoomGameObjectPatterns
    {
        public const string Floor = "Floor";
        public static readonly string[] Wall = { "LeftWall", "RightWall", "BackWall", "FrontWall" };
        public static readonly string[] Bookshelf = { "BookshelfRoot", "Bookshelf_" };
        public static readonly string[] Desk = { "DeskRoot", "NotebookStation", "Desk_" };
        public static readonly string[] Dashboard = { "DashboardScreenPanel", "GuestbookBoard", "Dashboard_" };

        public static bool IsWallName(string name)
        {
            for (int i = 0; i < Wall.Length; i++)
            {
                if (name == Wall[i]) return true;
            }
            return false;
        }

        public static bool MatchesPrefix(string name, string[] patterns)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                if (name.StartsWith(patterns[i])) return true;
            }
            return false;
        }
    }
}
