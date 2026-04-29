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

        public Space(
            long id,
            long ownerUserId,
            VisibilityScope visibility,
            VisibilityScope guestbookVisibility,
            long visitCount,
            long likeCount,
            bool liked,
            long layoutVersion,
            long? currentThemeInventoryId = null)
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
        }
    }
}
