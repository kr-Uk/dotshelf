using System;

namespace App.Domain.Dashboard
{
    public sealed class DashboardSnapshot
    {
        public Space Space { get; }
        public string OwnerNickname { get; }
        public string OwnerProfileImageUrl { get; }
        public string OwnerIntroduction { get; }
        public long FollowerCount { get; }
        public long FollowingCount { get; }
        public long BookshelfCount { get; }
        public long BookCount { get; }
        public long ReadingRecordCount { get; }
        public long WeeklyVisitorCount { get; }

        public DashboardSnapshot(
            Space space,
            string ownerNickname,
            string ownerProfileImageUrl,
            string ownerIntroduction,
            long followerCount,
            long followingCount,
            long bookshelfCount,
            long bookCount,
            long readingRecordCount,
            long weeklyVisitorCount)
        {
            Space = space ?? throw new ArgumentNullException(nameof(space));
            OwnerNickname = ownerNickname ?? string.Empty;
            OwnerProfileImageUrl = ownerProfileImageUrl;
            OwnerIntroduction = ownerIntroduction ?? string.Empty;
            FollowerCount = followerCount;
            FollowingCount = followingCount;
            BookshelfCount = bookshelfCount;
            BookCount = bookCount;
            ReadingRecordCount = readingRecordCount;
            WeeklyVisitorCount = weeklyVisitorCount;
        }
    }
}
