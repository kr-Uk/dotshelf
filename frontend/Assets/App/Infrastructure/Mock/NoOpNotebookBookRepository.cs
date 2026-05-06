using System;
using System.Collections.Generic;
using System.Linq;
using App.Application.Notebook;

namespace App.Infrastructure.Mock
{
    public sealed class NoOpNotebookBookRepository : INotebookBookRepository
    {
        private readonly HashSet<string> ownedBookIds = new HashSet<string> { "1003" };
        private readonly List<NotebookBookSearchItem> books = BuildBooks();
        private long nextUserBookId = 9000L;

        public void SearchBooks(
            string keyword,
            int page,
            int size,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            var normalized = string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim();
            var skip = Math.Max(0, page - 1) * Math.Max(1, size);
            var take = Math.Max(1, size);
            var items = books
                .Where(book => string.IsNullOrEmpty(normalized) ||
                               Contains(book.Title, normalized) ||
                               Contains(book.Author, normalized) ||
                               Contains(book.Publisher, normalized) ||
                               Contains(book.Description, normalized))
                .OrderByDescending(book => book.ViewCount)
                .ThenByDescending(book => book.AvgRating)
                .Skip(skip)
                .Take(take)
                .Select(CloneBook)
                .ToArray();

            onSuccess?.Invoke(new NotebookBookSearchResult(items));
        }

        public void LoadMarketBooks(
            string category,
            int limit,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            var normalized = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim().ToUpperInvariant();
            IEnumerable<NotebookBookSearchItem> source = books;
            if (normalized == "RECOMMENDED")
            {
                source = source
                    .OrderByDescending(book => book.AvgRating)
                    .ThenByDescending(book => book.ReviewCount);
            }
            else
            {
                source = source
                    .OrderBy(book => book.Rank <= 0 ? int.MaxValue : book.Rank)
                    .ThenByDescending(book => book.ViewCount);
            }

            var items = source
                .Take(Math.Max(1, limit))
                .Select(CloneBook)
                .ToArray();

            onSuccess?.Invoke(new NotebookBookSearchResult(items));
        }

        public void AddBookToShelf(
            long bookshelfId,
            AddNotebookBookToShelfCommand command,
            NotebookShelfPlacement placement,
            Action<NotebookBookAddResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            if (command == null)
            {
                onError?.Invoke(new NotebookApiError { Code = "NOTEBOOK_MOCK_INVALID_REQUEST", Message = "Book add command is required." });
                return;
            }

            var bookId = command.BookId.GetValueOrDefault();
            if (bookId <= 0 && long.TryParse(command.Isbn13, out var parsedIsbn))
            {
                bookId = parsedIsbn;
            }

            if (bookId <= 0)
            {
                bookId = Math.Abs((command.Title ?? string.Empty).GetHashCode());
            }

            ownedBookIds.Add(bookId.ToString());
            onSuccess?.Invoke(new NotebookBookAddResult
            {
                Id = ++nextUserBookId,
                BookId = bookId
            });
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   !string.IsNullOrWhiteSpace(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private NotebookBookSearchItem CloneBook(NotebookBookSearchItem source)
        {
            var owned = ownedBookIds.Contains(source.Id.ToString()) || source.Owned;
            return new NotebookBookSearchItem
            {
                Id = source.Id,
                Source = source.Source,
                BookId = source.BookId,
                Isbn = source.Isbn,
                Isbn13 = source.Isbn13,
                Isbn10 = source.Isbn10,
                Title = source.Title,
                Author = source.Author,
                Publisher = source.Publisher,
                PublishedDate = source.PublishedDate,
                Description = source.Description,
                CoverImageUrl = source.CoverImageUrl,
                SourceUrl = source.SourceUrl,
                AvgRating = source.AvgRating,
                ReviewCount = source.ReviewCount,
                ViewCount = source.ViewCount,
                PricePages = source.PricePages,
                Rank = source.Rank,
                Owned = owned,
                UserBookId = owned ? source.UserBookId ?? source.Id + 5000L : null,
                LikeCount = source.LikeCount,
                PurchaseCount = source.PurchaseCount,
                RecordCount = source.RecordCount,
                PlacementCount = source.PlacementCount,
                HasPaidCover = source.HasPaidCover
            };
        }

        private static List<NotebookBookSearchItem> BuildBooks()
        {
            return new List<NotebookBookSearchItem>
            {
                Book(1001, 1, "완벽하지 않은 여름", "서은재", "백지출판", "완벽하지 않은 계절 속에서 관계가 회복되는 이야기입니다.", "2026-04-01", 1400, 4.9m, 923, 18420, 820, 4112, true),
                Book(1002, 2, "마음의 방향을 묻는 밤", "Harun Ade", "Slowday Books", "흔들리는 마음의 방향을 천천히 되짚는 심리 에세이입니다.", "2026-03-18", 0, 4.8m, 612, 16880, 760, 3890, false),
                Book(1003, 3, "숲으로 간 편지", "박이도", "초록서재", "자연과 편지로 엮은 계간 소설, 작은 계절의 움직임을 따라갑니다.", "2026-02-11", 0, 4.9m, 421, 15210, 690, 3611, false, true),
                Book(1004, 4, "관계의 지도", "윤지수", "북노트", "사람 사이의 거리와 균형을 지도처럼 살피는 인문 기록입니다.", "2026-01-24", 1200, 4.7m, 388, 14322, 540, 3402, true),
                Book(1005, 5, "문장 수집가", "김아진", "밤과책", "좋은 문장을 모으는 사람들의 기록과 독서 노트를 담았습니다.", "2025-12-08", 0, 4.8m, 304, 13980, 510, 3280, false),
                Book(1006, 6, "별이 된 서점", "문경하", "초록서재", "오래된 서점과 별빛 도시에 관한 조용한 판타지입니다.", "2025-11-19", 1800, 4.6m, 287, 13210, 490, 3022, true),
                Book(1007, 7, "일요일의 경제학", "이재훈", "월요서재", "주말에 읽기 좋은 생활 경제 입문서입니다.", "2025-10-09", 1500, 4.4m, 221, 11880, 430, 2711, true),
                Book(1008, 8, "사소한 용기에 대하여", "Mira Koll", "Paperlight", "작은 선택과 회복의 순간을 따라가는 자기계발 에세이입니다.", "2025-09-15", 0, 4.5m, 198, 11020, 390, 2540, false),
            };
        }

        private static NotebookBookSearchItem Book(
            long id,
            int rank,
            string title,
            string author,
            string publisher,
            string description,
            string publishedDate,
            int pricePages,
            decimal rating,
            int reviews,
            int views,
            int records,
            int placements,
            bool paidCover,
            bool owned = false)
        {
            return new NotebookBookSearchItem
            {
                Id = id,
                Source = "LOCAL",
                BookId = id.ToString(),
                Isbn13 = $"97900000{id}",
                Title = title,
                Author = author,
                Publisher = publisher,
                PublishedDate = publishedDate,
                Description = description,
                AvgRating = rating,
                ReviewCount = reviews,
                ViewCount = views,
                PricePages = pricePages,
                Rank = rank,
                Owned = owned,
                UserBookId = owned ? id + 5000L : null,
                LikeCount = Math.Max(1, reviews * 3),
                PurchaseCount = paidCover ? Math.Max(1, reviews / 2) : 0,
                RecordCount = records,
                PlacementCount = placements,
                HasPaidCover = paidCover
            };
        }
    }
}
