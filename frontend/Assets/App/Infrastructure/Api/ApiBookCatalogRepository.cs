using System;
using System.Collections.Generic;
using System.Net.Http;
using App.Application.Books;
using App.Domain;
using App.Infrastructure.Api.Dtos;
using Newtonsoft.Json;
using UnityEngine;

namespace App.Infrastructure.Api
{
    public sealed class ApiBookCatalogRepository : IBookCatalogRepository
    {
        private const int Rows = 4;
        private const int Columns = 4;
        private const int BooksPerSection = 3;
        private const string DefaultShelfId = "bs-api-1";
        private const string DefaultUserId = "me";

        private readonly string baseUrl;
        private readonly int timeoutSeconds;
        private readonly IBookCatalogRepository fallbackRepository;

        public ApiBookCatalogRepository(string baseUrl, int timeoutSeconds, IBookCatalogRepository fallbackRepository = null)
        {
            this.baseUrl = NormalizeBaseUrl(baseUrl);
            this.timeoutSeconds = Math.Max(1, timeoutSeconds);
            this.fallbackRepository = fallbackRepository;
        }

        public IReadOnlyList<BookCatalogItem> GetMyBookshelfBooks()
        {
            try
            {
                var response = GetBookList();
                var items = response?.Items;
                if (items != null && items.Count > 0)
                {
                    return MapToCatalog(items);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Book catalog API fallback is active: {baseUrl} ({exception.Message})");
            }

            return fallbackRepository != null
                ? fallbackRepository.GetMyBookshelfBooks()
                : Array.Empty<BookCatalogItem>();
        }

        private BookListResponseDto GetBookList()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                var url = $"{baseUrl}/v1/books?category=RANKING&limit={Rows * Columns * BooksPerSection}";
                var json = client.GetStringAsync(url).GetAwaiter().GetResult();
                var envelope = JsonConvert.DeserializeObject<ApiEnvelopeDto<BookListResponseDto>>(json);
                if (envelope == null || !envelope.Succeeded || envelope.Result == null)
                {
                    return null;
                }

                return envelope.Result;
            }
        }

        private static IReadOnlyList<BookCatalogItem> MapToCatalog(IReadOnlyList<BookListItemDto> items)
        {
            var result = new List<BookCatalogItem>(items.Count);
            var now = DateTimeOffset.Now;

            for (var index = 0; index < items.Count; index++)
            {
                var dto = items[index];
                var row = index / (Columns * BooksPerSection);
                var column = index / BooksPerSection % Columns;
                var slot = index % BooksPerSection;
                result.Add(CreateCatalogItem(dto, index, row, column, slot, now));
            }

            return result;
        }

        private static BookCatalogItem CreateCatalogItem(BookListItemDto dto, int index, int row, int column, int slot, DateTimeOffset now)
        {
            var bookId = FirstNonEmpty(dto.BookId, $"b-api-{index + 1:000}");
            var userBookId = $"owned-{bookId}";
            var recordCount = Math.Max(1, dto.RecordCount);

            var book = new Book(
                bookId,
                FirstNonEmpty(dto.Title, "Untitled"),
                FirstNonEmpty(dto.Author, "Unknown"),
                publisher: dto.Publisher,
                description: "API /v1/books ranking item",
                coverImageUrl: dto.CoverImageUrl,
                isbn: dto.Isbn,
                isbn13: dto.Isbn,
                likeCount: dto.LikeCount,
                bookshelfAddCount: dto.PlacementCount,
                ratingSum: dto.AvgRating * recordCount,
                ratingCount: recordCount,
                createdAt: now,
                updatedAt: now);

            var userBook = new UserBook(userBookId, DefaultUserId, book.BookId, now, now)
            {
                ReadingStatus = ReadingStatus.Unread,
                StorageStatus = StorageStatus.OnShelf,
                OwnerRating = dto.AvgRating > 0m ? dto.AvgRating : null,
                IsLiked = dto.LikeCount > 0
            };

            var readingRecord = new ReadingRecord($"record-{bookId}", userBook.UserBookId, now, now)
            {
                Title = $"{book.Title} 기록",
                Content = dto.RecordCount > 0
                    ? $"공개 독후감 {dto.RecordCount}개가 등록된 책입니다."
                    : "아직 등록된 독후감이 없습니다.",
                VisibilityScope = VisibilityScope.Public,
                LikeCount = dto.LikeCount,
                CommentCount = 0,
                Status = ContentStatus.Active
            };

            var placement = new BookPlacement(
                $"item-{bookId}",
                DefaultShelfId,
                $"{DefaultShelfId}-section-{row}-{column}",
                userBook.UserBookId,
                row,
                column,
                slot + 1)
            {
                ItemType = ShelfItemType.Book,
                FacingDirection = FacingDirection.Front,
                DisplayStyle = DisplayStyle.Spine,
                RotationDeg = 0m,
                ScaleX = 1m,
                ScaleY = 1m,
                Status = ShelfItemStatus.Active
            };

            return new BookCatalogItem(book, userBook, placement, readingRecord);
        }

        private static string FirstNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "https://api.dotshelf.digital";
            }

            return value.Trim().TrimEnd('/');
        }
    }
}
