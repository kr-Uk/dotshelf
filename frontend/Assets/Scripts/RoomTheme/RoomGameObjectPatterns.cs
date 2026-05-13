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
        // 고정 interactive 오브젝트 — Desk/Dashboard mesh swap 에서 hide 금지 대상.
        // RoomEditModeController.IsUnderNotebookStation 이 부모 체인 탐색 시 이 상수와 비교.
        // (MR #157 review simonjiho #1: hardcoded 문자열 통일 — prefab 이름 변경 시 silent break 방지.)
        public const string NotebookStation = "NotebookStation";
        // 노트북 본체 GameObject 이름 — procedural (BookshelfGenerator.BuildNotebookStation
        // line 4199) 과 prefab (NotebookStation.prefab line 4029) 모두 "Notebook". Desk
        // 패턴이 "Desk" parent 와 매칭되어 자식 renderer 를 walk 할 때, 이 이름의 ancestor 가
        // 있으면 hide skip — 노트북 시각 보존.
        //
        // 왜 필요한가: 실제 hierarchy 는 NotebookStation > Desk > Notebook 이라 IsUnderNotebookStation
        // 이 renderer 에서 matchedTransform(=Desk) 까지만 올라가면 NotebookStation 에 못 닿음.
        // "Notebook" 이름 자체로 보호 — Desk 가 matchedTransform 일 때도 laptop 보존됨.
        public const string Notebook = "Notebook";
        public static readonly string[] Wall = { "LeftWall", "RightWall", "BackWall", "FrontWall" };
        public static readonly string[] Bookshelf = { "BookshelfRoot", "Bookshelf_" };
        // Desk pattern — procedural BookshelfGenerator (line 4191) 과 prefab NotebookStation.prefab
        // (line 2357) 모두 "Desk" 라는 이름을 쓴다. 이전 패턴 "DeskRoot"/"Desk_" 는 실제 이름 어느
        // 것에도 매칭되지 않아 ApplyCategoryMeshSwap 가 vintage_table/GoodVibes_Desk mesh swap 을
        // silent skip 하는 회귀 (270512 vintage 테마 적용 시 책상 안 바뀜 보고). prefix 매칭이라
        // "Desk" 하나로 "Desk"/"Desktop"/"DeskLegA-D"/"DeskRoot"/"Desk_*" 모두 cover.
        //
        // 노트북 (laptop) 은 Desk 자식인 "Notebook" GameObject 로 들어 있어 본 pattern 이 자식 walk
        // 시 같이 잡힌다. RoomEditModeController.IsUnderNotebookStation 이 Notebook ancestor 검사로
        // hide skip 처리.
        public static readonly string[] Desk = { "Desk" };
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
