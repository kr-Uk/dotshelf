using System;
using System.Collections.Generic;
using App.Application.Notebook;
using App.Infrastructure.Api.Dtos;

namespace App.Infrastructure.Api
{
    public sealed class ApiNotebookBookRepository : INotebookBookRepository
    {
        private readonly NotebookApiClient client;

        public ApiNotebookBookRepository(NotebookApiClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void SearchBooks(
            string keyword,
            int page,
            int size,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            client.SearchBooks(
                keyword,
                page,
                size,
                response => onSuccess?.Invoke(MapSearchResult(response)),
                error => onError?.Invoke(MapError(error)));
        }

        public void LoadMarketBooks(
            string category,
            int limit,
            Action<NotebookBookSearchResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            client.GetBooks(
                category,
                limit,
                response => onSuccess?.Invoke(MapSearchResult(response)),
                error => onError?.Invoke(MapError(error)));
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
                onError?.Invoke(new NotebookApiError { Message = "Book add command is required." });
                return;
            }

            if (placement == null)
            {
                onError?.Invoke(new NotebookApiError { Message = "Book shelf placement is required." });
                return;
            }

            if (string.Equals(command.Source, "KAKAO", StringComparison.OrdinalIgnoreCase))
            {
                client.AddKakaoBookToShelf(
                    bookshelfId,
                    MapKakaoAddRequest(command, placement),
                    response => onSuccess?.Invoke(MapAddResult(response)),
                    error => onError?.Invoke(MapError(error)));
                return;
            }

            client.AddBookToShelf(
                bookshelfId,
                MapLocalAddRequest(command, placement),
                response => onSuccess?.Invoke(MapAddResult(response)),
                error => onError?.Invoke(MapError(error)));
        }

        private static NotebookBookSearchResult MapSearchResult(BookListResponseDto response)
        {
            var sourceItems = response?.EffectiveItems;
            if (sourceItems == null || sourceItems.Count == 0)
            {
                return new NotebookBookSearchResult(Array.Empty<NotebookBookSearchItem>());
            }

            var items = new List<NotebookBookSearchItem>(sourceItems.Count);
            foreach (var item in sourceItems)
            {
                if (item == null)
                {
                    continue;
                }

                items.Add(new NotebookBookSearchItem
                {
                    Id = item.Id,
                    Source = item.Source,
                    BookId = item.BookId,
                    Isbn = item.Isbn,
                    Isbn13 = item.Isbn13,
                    Isbn10 = item.Isbn10,
                    Title = item.Title,
                    Author = item.Author,
                    Publisher = item.Publisher,
                    PublishedDate = item.PublishedDate,
                    Description = item.Description,
                    CoverImageUrl = item.CoverImageUrl,
                    SourceUrl = item.SourceUrl,
                    AvgRating = item.AvgRating,
                    ReviewCount = item.ReviewCount,
                    ViewCount = item.ViewCount,
                    PricePages = item.PricePages,
                    Rank = item.Rank,
                    Owned = item.Owned,
                    UserBookId = item.UserBookId,
                    LikeCount = item.LikeCount,
                    PurchaseCount = item.PurchaseCount,
                    RecordCount = item.RecordCount,
                    PlacementCount = item.PlacementCount,
                    BookshelfAddCount = item.EffectiveBookshelfAddCount,
                    HasPaidCover = item.HasPaidCover
                });
            }

            return new NotebookBookSearchResult(items);
        }

        private static AddBookToShelfRequestDto MapLocalAddRequest(
            AddNotebookBookToShelfCommand command,
            NotebookShelfPlacement placement)
        {
            return new AddBookToShelfRequestDto
            {
                BookId = command.BookId.GetValueOrDefault(),
                SectionId = placement.SectionId,
                SlotNo = placement.SlotNo,
                DisplayStyle = MapDisplayStyle(placement.DisplayStyle)
            };
        }

        private static AddKakaoBookToShelfRequestDto MapKakaoAddRequest(
            AddNotebookBookToShelfCommand command,
            NotebookShelfPlacement placement)
        {
            return new AddKakaoBookToShelfRequestDto
            {
                Isbn13 = command.Isbn13,
                Isbn10 = command.Isbn10,
                Title = command.Title,
                Author = command.Author,
                Publisher = command.Publisher,
                PublishedDate = command.PublishedDate,
                Description = command.Description,
                CoverImageUrl = command.CoverImageUrl,
                SourceUrl = command.SourceUrl,
                SectionId = placement.SectionId,
                SlotNo = placement.SlotNo,
                DisplayStyle = MapDisplayStyle(placement.DisplayStyle)
            };
        }

        private static string MapDisplayStyle(NotebookShelfDisplayStyle displayStyle)
        {
            switch (displayStyle)
            {
                case NotebookShelfDisplayStyle.Spine:
                default:
                    return "SPINE";
            }
        }

        private static NotebookBookAddResult MapAddResult(UserBookResponseDto response)
        {
            return new NotebookBookAddResult
            {
                Id = response?.Id ?? 0,
                BookId = response?.BookId ?? 0
            };
        }

        private static NotebookApiError MapError(ApiErrorResponseDto error)
        {
            return new NotebookApiError
            {
                Code = error?.Code,
                Message = error?.Message,
                Error = error?.Error
            };
        }
    }
}
