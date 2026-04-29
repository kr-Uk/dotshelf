using System;
using System.Collections.Generic;
using App.Application.Dashboard;

namespace App.Infrastructure.Mock.Dashboard
{
    public sealed class MockLibraryViewRepository : ILibraryViewRepository
    {
        private const int PageSize = 20;

        private static readonly string[] BookTitles =
        {
            "모순", "불편한 편의점", "아몬드", "작별인사", "파친코",
            "흐르는 강물처럼", "메리골드 마음 세탁소", "구의 증명", "천 개의 파랑",
            "달러구트 꿈 백화점", "지구 끝의 온실", "밝은 밤", "어서 오세요, 휴남동 서점입니다",
            "종의 기원", "여행의 이유", "스토너", "데미안", "1984",
            "인간 실격", "총, 균, 쇠"
        };

        private static readonly string[] Authors =
        {
            "양귀자", "김호연", "손원평", "김영하", "이민진",
            "파울로 코엘료", "윤정은", "최진영", "천선란", "이미예",
            "김초엽", "최은영", "황보름", "정유정", "유시민",
            "존 윌리엄스", "헤르만 헤세", "조지 오웰", "다자이 오사무", "재레드 다이아몬드"
        };

        private readonly List<OwnedBookItem> ownedBooks;
        private readonly List<ReadingRecordItem> readingRecords;

        public MockLibraryViewRepository()
        {
            DateTimeOffset baseTime = DateTimeOffset.UtcNow;

            ownedBooks = new List<OwnedBookItem>(BookTitles.Length);
            for (int i = 0; i < BookTitles.Length; i++)
            {
                ownedBooks.Add(new OwnedBookItem
                {
                    UserBookId = 10000 + i,
                    BookId = 100 + i,
                    Title = BookTitles[i],
                    Author = Authors[i % Authors.Length],
                    Publisher = "출판사" + (i % 5 + 1),
                    PublishedDate = $"202{i % 5}-0{i % 9 + 1}-15",
                    CoverImageUrl = null,
                    AddedAt = baseTime.AddDays(-i * 3)
                });
            }

            readingRecords = new List<ReadingRecordItem>(BookTitles.Length);
            for (int i = 0; i < BookTitles.Length; i++)
            {
                readingRecords.Add(new ReadingRecordItem
                {
                    Id = 20000 + i,
                    BookId = 100 + i,
                    BookTitle = BookTitles[i],
                    Content = $"{BookTitles[i]} 너무 좋았어요. 인상 깊은 문장이 많아서 자꾸만 곱씹게 됩니다. 등장인물의 감정선이 섬세하게 그려져 있어서 페이지를 넘기는 손이 멈추질 않더라고요...",
                    LikeCount = (i * 7) % 50,
                    CommentCount = (i * 3) % 17,
                    ViewCount = (i * 23) % 200,
                    CreatedAt = baseTime.AddDays(-i * 5)
                });
            }
        }

        public void LoadOwnedBooks(long spaceId, OwnedBookSort sort, string cursor, int limit, Action<OwnedBookListPage> onSuccess, Action<DashboardApiError> onError)
        {
            List<OwnedBookItem> sorted = SortOwned(ownedBooks, sort);
            onSuccess(SliceOwned(sorted, cursor, limit > 0 ? limit : PageSize));
        }

        public void LoadReadingRecords(long userId, ReadingRecordSort sort, string cursor, int limit, Action<ReadingRecordListPage> onSuccess, Action<DashboardApiError> onError)
        {
            List<ReadingRecordItem> sorted = SortRecords(readingRecords, sort);
            onSuccess(SliceRecords(sorted, cursor, limit > 0 ? limit : PageSize));
        }

        private static List<OwnedBookItem> SortOwned(List<OwnedBookItem> source, OwnedBookSort sort)
        {
            List<OwnedBookItem> copy = new List<OwnedBookItem>(source);
            switch (sort)
            {
                case OwnedBookSort.PublishedDesc:
                    copy.Sort((a, b) => string.Compare(b.PublishedDate ?? string.Empty, a.PublishedDate ?? string.Empty, StringComparison.Ordinal));
                    break;
                case OwnedBookSort.PublishedAsc:
                    copy.Sort((a, b) => string.Compare(a.PublishedDate ?? string.Empty, b.PublishedDate ?? string.Empty, StringComparison.Ordinal));
                    break;
                case OwnedBookSort.TitleAsc:
                    copy.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCulture));
                    break;
                case OwnedBookSort.AddedDesc:
                default:
                    copy.Sort((a, b) => b.AddedAt.CompareTo(a.AddedAt));
                    break;
            }
            return copy;
        }

        private static List<ReadingRecordItem> SortRecords(List<ReadingRecordItem> source, ReadingRecordSort sort)
        {
            List<ReadingRecordItem> copy = new List<ReadingRecordItem>(source);
            switch (sort)
            {
                case ReadingRecordSort.Oldest:
                    copy.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
                    break;
                case ReadingRecordSort.MostLiked:
                    copy.Sort((a, b) => b.LikeCount.CompareTo(a.LikeCount));
                    break;
                case ReadingRecordSort.MostViewed:
                    copy.Sort((a, b) => b.ViewCount.CompareTo(a.ViewCount));
                    break;
                case ReadingRecordSort.MostCommented:
                    copy.Sort((a, b) => b.CommentCount.CompareTo(a.CommentCount));
                    break;
                case ReadingRecordSort.Latest:
                default:
                    copy.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
                    break;
            }
            return copy;
        }

        private static OwnedBookListPage SliceOwned(List<OwnedBookItem> source, string cursor, int limit)
        {
            int startIndex = ParseCursor(cursor);
            int take = Math.Min(limit, Math.Max(0, source.Count - startIndex));
            if (take <= 0)
            {
                return new OwnedBookListPage(Array.Empty<OwnedBookItem>(), null);
            }

            List<OwnedBookItem> slice = source.GetRange(startIndex, take);
            string nextCursor = startIndex + take < source.Count ? (startIndex + take).ToString() : null;
            return new OwnedBookListPage(slice, nextCursor);
        }

        private static ReadingRecordListPage SliceRecords(List<ReadingRecordItem> source, string cursor, int limit)
        {
            int startIndex = ParseCursor(cursor);
            int take = Math.Min(limit, Math.Max(0, source.Count - startIndex));
            if (take <= 0)
            {
                return new ReadingRecordListPage(Array.Empty<ReadingRecordItem>(), null);
            }

            List<ReadingRecordItem> slice = source.GetRange(startIndex, take);
            string nextCursor = startIndex + take < source.Count ? (startIndex + take).ToString() : null;
            return new ReadingRecordListPage(slice, nextCursor);
        }

        private static int ParseCursor(string cursor)
        {
            if (string.IsNullOrEmpty(cursor))
            {
                return 0;
            }

            return int.TryParse(cursor, out int parsed) ? parsed : 0;
        }
    }
}
