using System;
using System.Collections.Generic;
using App.Domain.Dashboard;

namespace App.Application.Dashboard
{
    public sealed class CursorPage<T>
    {
        public CursorPage(IReadOnlyList<T> items, string nextCursor)
        {
            Items = items ?? Array.Empty<T>();
            NextCursor = nextCursor;
        }

        public IReadOnlyList<T> Items { get; }
        public string NextCursor { get; }
        public bool HasMore => !string.IsNullOrEmpty(NextCursor);
    }

    public sealed class GuestbookListPage
    {
        public GuestbookListPage(IReadOnlyList<Guestbook> items, string nextCursor)
        {
            Items = items ?? Array.Empty<Guestbook>();
            NextCursor = nextCursor;
        }

        public IReadOnlyList<Guestbook> Items { get; }
        public string NextCursor { get; }
        public bool HasMore => !string.IsNullOrEmpty(NextCursor);
    }

    public sealed class UserListPage
    {
        public UserListPage(IReadOnlyList<UserSummary> items, string nextCursor)
        {
            Items = items ?? Array.Empty<UserSummary>();
            NextCursor = nextCursor;
        }

        public IReadOnlyList<UserSummary> Items { get; }
        public string NextCursor { get; }
        public bool HasMore => !string.IsNullOrEmpty(NextCursor);
    }

    public sealed class WriteGuestbookCommand
    {
        public long SpaceId { get; set; }
        public long PostItInventoryId { get; set; }
        public string Content { get; set; }
        public bool Anonymous { get; set; }
    }

    public sealed class WriteGuestbookReplyCommand
    {
        public long GuestbookId { get; set; }
        public long PostItInventoryId { get; set; }
        public string Content { get; set; }
    }

    public enum GuestbookReportTarget
    {
        Guestbook,
        Reply
    }

    public sealed class ReportGuestbookCommand
    {
        public GuestbookReportTarget Target { get; set; }
        public long TargetId { get; set; }
        public string ReasonType { get; set; }
        public string ReasonDetail { get; set; }
    }

    public sealed class GuestbookPostItWhitelist
    {
        public GuestbookPostItWhitelist(bool allowAll, IReadOnlyList<long> allowedAssetIds)
        {
            AllowAll = allowAll;
            AllowedAssetIds = allowedAssetIds ?? Array.Empty<long>();
        }

        public bool AllowAll { get; }
        public IReadOnlyList<long> AllowedAssetIds { get; }
    }

    public sealed class LikeToggleResult
    {
        public bool Liked { get; set; }
        public long LikeCount { get; set; }
    }

    public sealed class FollowToggleResult
    {
        public bool Following { get; set; }
        public bool Mutual { get; set; }
    }

    public sealed class BlockToggleResult
    {
        public bool Blocked { get; set; }
    }

    public enum OwnedBookSort
    {
        AddedDesc,        // 추가한 날짜 순 (최신 추가가 위)
        PublishedDesc,    // 출판일 최신순
        PublishedAsc,     // 출판일 과거순
        TitleAsc          // 가나다순
    }

    public enum ReadingRecordSort
    {
        Latest,
        Oldest,
        MostLiked,
        MostViewed,
        MostCommented
    }

    public sealed class OwnedBookItem
    {
        public long UserBookId { get; set; }
        public long BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Publisher { get; set; }
        public string PublishedDate { get; set; }
        public string CoverImageUrl { get; set; }
        public DateTimeOffset AddedAt { get; set; }
    }

    public sealed class ReadingRecordItem
    {
        public long Id { get; set; }
        public long BookId { get; set; }
        public string BookTitle { get; set; }
        public string Content { get; set; }
        public long LikeCount { get; set; }
        public long CommentCount { get; set; }
        public long ViewCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class OwnedBookListPage
    {
        public OwnedBookListPage(IReadOnlyList<OwnedBookItem> items, string nextCursor)
        {
            Items = items ?? Array.Empty<OwnedBookItem>();
            NextCursor = nextCursor;
        }

        public IReadOnlyList<OwnedBookItem> Items { get; }
        public string NextCursor { get; }
        public bool HasMore => !string.IsNullOrEmpty(NextCursor);
    }

    public sealed class ReadingRecordListPage
    {
        public ReadingRecordListPage(IReadOnlyList<ReadingRecordItem> items, string nextCursor)
        {
            Items = items ?? Array.Empty<ReadingRecordItem>();
            NextCursor = nextCursor;
        }

        public IReadOnlyList<ReadingRecordItem> Items { get; }
        public string NextCursor { get; }
        public bool HasMore => !string.IsNullOrEmpty(NextCursor);
    }
}
