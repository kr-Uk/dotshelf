namespace App.Domain.Dashboard
{
    public sealed class Space
    {
        public long Id { get; }
        public long OwnerUserId { get; }
        public VisibilityScope Visibility { get; }
        public VisibilityScope GuestbookVisibility { get; }
        public long VisitCount { get; }
        public long LikeCount { get; }
        public bool Liked { get; }
        public long LayoutVersion { get; }
        public long? CurrentThemeInventoryId { get; }

        // 카테고리별 개별 적용 항목 inventory id. CurrentThemeInventoryId(전체 테마)와는
        // 독립적으로 동작 — "테마1의 노트북 + 테마2의 바닥" 식 믹스 표현 가능.
        // 책장 외형은 인스턴스별로 Bookshelf.skinInventoryId에 저장(별도 도메인).
        public long? CurrentWallpaperInventoryId { get; }
        public long? CurrentFloorInventoryId { get; }
        public long? CurrentDeskInventoryId { get; }
        public long? CurrentBookshelfSkinInventoryId { get; }
        public long? CurrentDashboardInventoryId { get; }
        public long? CurrentNotebookInventoryId { get; }
        public long? CurrentInteriorLightInventoryId { get; }
        public long? CurrentExteriorLightInventoryId { get; }

        public Space(
            long id,
            long ownerUserId,
            VisibilityScope visibility,
            VisibilityScope guestbookVisibility,
            long visitCount,
            long likeCount,
            bool liked,
            long layoutVersion,
            long? currentThemeInventoryId = null,
            long? currentWallpaperInventoryId = null,
            long? currentFloorInventoryId = null,
            long? currentDeskInventoryId = null,
            long? currentBookshelfSkinInventoryId = null,
            long? currentDashboardInventoryId = null,
            long? currentNotebookInventoryId = null,
            long? currentInteriorLightInventoryId = null,
            long? currentExteriorLightInventoryId = null)
        {
            Id = id;
            OwnerUserId = ownerUserId;
            Visibility = visibility;
            GuestbookVisibility = guestbookVisibility;
            VisitCount = visitCount;
            LikeCount = likeCount;
            Liked = liked;
            LayoutVersion = layoutVersion;
            CurrentThemeInventoryId = currentThemeInventoryId;
            CurrentWallpaperInventoryId = currentWallpaperInventoryId;
            CurrentFloorInventoryId = currentFloorInventoryId;
            CurrentDeskInventoryId = currentDeskInventoryId;
            CurrentBookshelfSkinInventoryId = currentBookshelfSkinInventoryId;
            CurrentDashboardInventoryId = currentDashboardInventoryId;
            CurrentNotebookInventoryId = currentNotebookInventoryId;
            CurrentInteriorLightInventoryId = currentInteriorLightInventoryId;
            CurrentExteriorLightInventoryId = currentExteriorLightInventoryId;
        }
    }
}
