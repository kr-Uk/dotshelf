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
    /// <summary>
    /// Loads the public book catalog used by the room add-book/search UI.
    /// Authenticated bookshelf placement is handled separately by BookshelfApiSyncController.
    /// </summary>
    public sealed class ApiBookCatalogRepository : IBookCatalogRepository
    {

        private const int Rows = 4;
        private const int Columns = 4;
        private const int BooksPerSection = 3;
        private const string DefaultShelfId = "bs-api-1";
        private const string DefaultUserId = "me";

        private readonly string baseUrl;
        private readonly int timeoutSeconds;

        public ApiBookCatalogRepository(string baseUrl, int timeoutSeconds)
        {
            this.baseUrl = NormalizeBaseUrl(baseUrl);
            this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public IReadOnlyList<BookCatalogItem> GetMyBookshelfBooks()
        {
            try
            {
                var response = GetBookList();
                var items = response?.EffectiveItems;
                if (items != null && items.Count > 0)
                {
                    App.Core.AppLog.BookshelfSync($"Book catalog API loaded {items.Count} item(s): {baseUrl}");
                    return MapToCatalog(items);
                }

                Debug.LogWarning($"Book catalog API returned no items: {baseUrl}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Book catalog API request failed: {baseUrl} ({exception.Message})");
            }

            return Array.Empty<BookCatalogItem>();
        }

        private BookListResponseDto GetBookList()
        {
            using (var client = CreateClient())
            {
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
            var bookId = FirstNonEmpty(dto.BookId, dto.Id > 0 ? dto.Id.ToString() : $"b-api-{index + 1:000}");
            var userBookId = $"catalog-{bookId}";
            var recordCount = Math.Max(1, dto.RecordCount);
            var isbn = FirstNonEmpty(dto.Isbn13, dto.Isbn);

            var book = new Book(
                bookId,
                FirstNonEmpty(dto.Title, "제목 없음"),
                FirstNonEmpty(dto.Author, "저자 미상"),
                publisher: dto.Publisher,
                description: FirstNonEmpty(dto.Description, "책 설명이 없습니다."),
                coverImageUrl: dto.CoverImageUrl,
                isbn: isbn,
                isbn13: isbn,
                likeCount: dto.LikeCount,
                bookshelfAddCount: dto.EffectiveBookshelfAddCount,
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
                    ? $"공개 리뷰가 {dto.RecordCount}개 등록된 책입니다."
                    : "아직 등록된 공개 리뷰가 없습니다.",
                VisibilityScope = VisibilityScope.Public,
                LikeCount = dto.LikeCount,
                CommentCount = 0,
                Status = ContentStatus.Active
            };

            var placement = new BookPlacement(
                $"catalog-item-{bookId}",
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

        private HttpClient CreateClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        private static string FirstNonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ApiEndpointDefaults.DefaultBaseUrl;
            }

            return value.Trim().TrimEnd('/');
        }
    }
}
