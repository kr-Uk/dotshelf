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
        // **NotebookStation 은 Desk 매칭에서 의도적으로 제외**. 노트북은 고정 interactive 오브젝트
        // (클릭 시 dashboard UI 진입) 라 Desk 테마 mesh swap 의 영향을 받으면 안 됨. 과거 매칭에
        // 포함됐을 땐 ApplyCategoryMeshSwap 가 NotebookStation 자식 renderer 까지 disable 해서
        // 편집 모드 진입 시 노트북이 사라지거나 다른 형태로 보이는 회귀 발생.
        public static readonly string[] Desk = { "DeskRoot", "Desk_" };
        public static readonly string[] Dashboard = { "DashboardScreenPanel", "GuestbookBoard", "Dashboard_" };

        // BackWall 자체는 BookshelfGenerator.BuildBackWallAlcove 가 책장 alcove 때문에
        // BackWall_LeftStrip / RightStrip / TopStrip 3 분할로만 생성. 정확 일치 검사하면
        // BackWall strips 가 wall 로 인식 안 돼 RoomThemeApplier.ApplyMaterials 가 그
        // strips 에 wall material (= stencil reader) 를 swap 안 함 → 다른 wall 만 hole
        // 보이는 비대칭 발생. 따라서 prefix 매칭으로 BackWall_* strips 모두 wall 인식.
        public static bool IsWallName(string name)
        {
            for (int i = 0; i < Wall.Length; i++)
            {
                if (name == Wall[i]) return true;
                if (name.StartsWith(Wall[i] + "_")) return true;
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
