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

        public void AddBookToShelf(
            long bookshelfId,
            NotebookBookAddRequest request,
            Action<NotebookBookAddResult> onSuccess,
            Action<NotebookApiError> onError)
        {
            if (request == null)
            {
                onError?.Invoke(new NotebookApiError { Message = "Book add request is required." });
                return;
            }

            if (string.Equals(request.Source, "KAKAO", StringComparison.OrdinalIgnoreCase))
            {
                client.AddKakaoBookToShelf(
                    bookshelfId,
                    MapKakaoAddRequest(request),
                    response => onSuccess?.Invoke(MapAddResult(response)),
                    error => onError?.Invoke(MapError(error)));
                return;
            }

            client.AddBookToShelf(
                bookshelfId,
                MapLocalAddRequest(request),
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
                    LikeCount = item.LikeCount,
                    PurchaseCount = item.PurchaseCount,
                    RecordCount = item.RecordCount,
                    PlacementCount = item.PlacementCount,
                    HasPaidCover = item.HasPaidCover
                });
            }

            return new NotebookBookSearchResult(items);
        }

        private static AddBookToShelfRequestDto MapLocalAddRequest(NotebookBookAddRequest request)
        {
            return new AddBookToShelfRequestDto
            {
                BookId = request.BookId.GetValueOrDefault(),
                SectionId = request.SectionId,
                SlotNo = request.SlotNo,
                DisplayStyle = request.DisplayStyle
            };
        }

        private static AddKakaoBookToShelfRequestDto MapKakaoAddRequest(NotebookBookAddRequest request)
        {
            return new AddKakaoBookToShelfRequestDto
            {
                Isbn13 = request.Isbn13,
                Isbn10 = request.Isbn10,
                Title = request.Title,
                Author = request.Author,
                Publisher = request.Publisher,
                PublishedDate = request.PublishedDate,
                Description = request.Description,
                CoverImageUrl = request.CoverImageUrl,
                SectionId = request.SectionId,
                SlotNo = request.SlotNo,
                DisplayStyle = request.DisplayStyle
            };
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
