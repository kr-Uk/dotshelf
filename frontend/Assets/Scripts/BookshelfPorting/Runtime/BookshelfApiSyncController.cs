using System;
using System.Collections;
using System.Collections.Generic;
using App.Domain;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public sealed class BookshelfApiSyncController : MonoBehaviour
    {
        private NotebookApiClient client;
        private BookshelfGenerator generator;
        private BookshelfExperienceManager experienceManager;
        private long? currentSpaceId;
        private bool configured;
        private bool isRefreshing;

        public bool IsConfigured => configured && client != null && generator != null;

        public void Configure(
            NotebookApiClient apiClient,
            BookshelfGenerator bookshelfGenerator,
            BookshelfExperienceManager manager)
        {
            client = apiClient;
            generator = bookshelfGenerator;
            experienceManager = manager;
            configured = client != null && generator != null;

            if (configured && Application.isPlaying)
            {
                StartCoroutine(RefreshAfterSceneSettles());
            }
        }

        public void RefreshFromServer()
        {
            if (!IsConfigured || isRefreshing)
            {
                return;
            }

            isRefreshing = true;
            client.GetMySpace(
                space =>
                {
                    currentSpaceId = space?.Id;
                    if (!currentSpaceId.HasValue || currentSpaceId.Value <= 0)
                    {
                        FinishRefresh(false, "내 공간 정보를 찾을 수 없습니다.");
                        return;
                    }

                    client.GetBookshelves(
                        currentSpaceId.Value,
                        LoadBookshelfItems,
                        error => FinishRefresh(false, FormatError(error)));
                },
                error => FinishRefresh(false, FormatError(error)));
        }

        public void CreateBookshelf(string name)
        {
            if (!IsConfigured)
            {
                return;
            }

            if (!currentSpaceId.HasValue)
            {
                client.GetMySpace(
                    space =>
                    {
                        currentSpaceId = space?.Id;
                        CreateBookshelf(name);
                    },
                    error => Debug.LogWarning($"Bookshelf create skipped: {FormatError(error)}"));
                return;
            }

            client.CreateBookshelf(
                new CreateBookshelfRequestDto
                {
                    SpaceId = currentSpaceId.Value,
                    Name = string.IsNullOrWhiteSpace(name) ? "새 책장" : name,
                    VisibilityScope = "PUBLIC"
                },
                _ => RefreshFromServer(),
                error => Debug.LogWarning($"Bookshelf create failed: {FormatError(error)}"));
        }

        public void UpdateBookshelf(int index)
        {
            if (!IsConfigured || !TryParseId(generator.GetBookshelfServerId(index), out var bookshelfId))
            {
                return;
            }

            client.UpdateBookshelf(
                bookshelfId,
                new UpdateBookshelfRequestDto
                {
                    Name = generator.GetBookshelfName(index),
                    VisibilityScope = ToApiVisibility(generator.GetBookshelfVisibility(index))
                },
                _ => { },
                error => Debug.LogWarning($"Bookshelf update failed: {FormatError(error)}"));
        }

        public void DeleteBookshelf(string bookshelfServerId)
        {
            if (!IsConfigured || !TryParseId(bookshelfServerId, out var bookshelfId))
            {
                return;
            }

            client.DeleteBookshelf(
                bookshelfId,
                _ => RefreshFromServer(),
                error => Debug.LogWarning($"Bookshelf delete failed: {FormatError(error)}"));
        }

        public void ReorderBookshelves()
        {
            if (!IsConfigured)
            {
                return;
            }

            var rawIds = generator.GetBookshelfServerIdsInDisplayOrder();
            var ids = new List<long>();
            for (var i = 0; i < rawIds.Count; i++)
            {
                if (TryParseId(rawIds[i], out var id))
                {
                    ids.Add(id);
                }
            }

            if (ids.Count <= 1)
            {
                return;
            }

            client.ReorderBookshelves(
                new ReorderBookshelvesRequestDto { BookshelfIds = ids },
                _ => { },
                error => Debug.LogWarning($"Bookshelf reorder failed: {FormatError(error)}"));
        }

        public void SyncBookState(BookEntity book)
        {
            if (!IsConfigured || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return;
            }

            client.UpdateUserBook(
                userBookId,
                new UpdateUserBookRequestDto
                {
                    ReadingStatus = ToApiReadingStatus(book.readingStatus),
                    OwnerRating = book.ownerRating > 0f ? (decimal?)Math.Round((decimal)book.ownerRating, 1) : null
                },
                response => ApplyUserBookResponse(book, response),
                error => Debug.LogWarning($"User book update failed: {FormatError(error)}"));
        }

        public void SyncBookLike(BookEntity book)
        {
            if (!IsConfigured || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return;
            }

            if (book.isLiked)
            {
                client.LikeUserBook(userBookId, _ => { }, error => Debug.LogWarning($"User book like failed: {FormatError(error)}"));
                return;
            }

            client.UnlikeUserBook(userBookId, _ => { }, error => Debug.LogWarning($"User book unlike failed: {FormatError(error)}"));
        }

        public void SyncBookPlacement(BookEntity book)
        {
            if (!IsConfigured || book == null)
            {
                return;
            }

            var bookshelfIdText = string.IsNullOrWhiteSpace(book.bookshelfId)
                ? generator.GetActiveBookshelfServerId()
                : book.bookshelfId;
            var section = book.CurrentSection;
            var sectionIdText = string.IsNullOrWhiteSpace(book.bookshelfSectionId) && section != null
                ? generator.GetSectionServerId(generator.ActiveBookshelfIndex, section.row, section.column)
                : book.bookshelfSectionId;

            if (!TryParseId(bookshelfIdText, out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                return;
            }

            var request = new BookshelfItemRequestDto
            {
                BookshelfId = bookshelfId,
                SectionId = sectionId,
                SlotNo = generator.GetBookSlotNo(book),
                FacingDirection = "FRONT",
                DisplayStyle = ToApiDisplayStyle(book.orientation)
            };

            if (TryParseId(book.bookshelfItemId, out var itemId))
            {
                client.MoveBookshelfItem(
                    itemId,
                    request,
                    response => ApplyBookshelfItemResponse(book, response),
                    error => Debug.LogWarning($"Bookshelf item move failed: {FormatError(error)}"));
                return;
            }

            if (TryParseId(book.userBookId, out var userBookId))
            {
                client.MoveUserBook(
                    userBookId,
                    new MoveUserBookRequestDto
                    {
                        BookshelfId = bookshelfId,
                        SectionId = sectionId,
                        SlotNo = request.SlotNo,
                        FacingDirection = request.FacingDirection,
                        DisplayStyle = request.DisplayStyle
                    },
                    response => ApplyUserBookResponse(book, response),
                    error => Debug.LogWarning($"User book move failed: {FormatError(error)}"));
            }
        }

        public void AddBookToActiveShelf(BookEntity book)
        {
            if (!IsConfigured || book == null)
            {
                return;
            }

            var section = book.CurrentSection;
            var sectionIdText = section != null
                ? generator.GetSectionServerId(generator.ActiveBookshelfIndex, section.row, section.column)
                : book.bookshelfSectionId;
            if (!TryParseId(generator.GetActiveBookshelfServerId(), out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                return;
            }

            var slotNo = generator.GetBookSlotNo(book);
            var displayStyle = ToApiDisplayStyle(book.orientation);
            var facingDirection = "FRONT";
            var desiredReadingStatus = book.readingStatus;
            var desiredOwnerRating = book.ownerRating;
            if (TryParseId(book.bookId, out var bookId))
            {
                client.AddBookToShelf(
                    bookshelfId,
                    new AddBookToShelfRequestDto
                    {
                        BookId = bookId,
                        SectionId = sectionId,
                        SlotNo = slotNo,
                        FacingDirection = facingDirection,
                        DisplayStyle = displayStyle
                    },
                    response =>
                    {
                        ApplyUserBookResponse(book, response);
                        book.readingStatus = desiredReadingStatus;
                        book.isRead = desiredReadingStatus == ReadingStatus.Read;
                        book.ownerRating = desiredOwnerRating;
                        SyncBookState(book);
                    },
                    error => Debug.LogWarning($"Book add failed: {FormatError(error)}"));
                return;
            }

            client.AddKakaoBookToShelf(
                bookshelfId,
                new AddKakaoBookToShelfRequestDto
                {
                    Title = book.title,
                    Author = book.author,
                    Publisher = book.publisher,
                    Description = book.description,
                    SectionId = sectionId,
                    SlotNo = slotNo,
                    FacingDirection = facingDirection,
                    DisplayStyle = displayStyle
                },
                response =>
                {
                    book.bookId = response?.BookId > 0 ? response.BookId.ToString() : book.bookId;
                    ApplyUserBookResponse(book, response);
                    book.readingStatus = desiredReadingStatus;
                    book.isRead = desiredReadingStatus == ReadingStatus.Read;
                    book.ownerRating = desiredOwnerRating;
                    SyncBookState(book);
                },
                error => Debug.LogWarning($"Kakao/manual book add failed: {FormatError(error)}"));
        }

        public void RemoveBook(BookEntity book)
        {
            if (!IsConfigured || book == null)
            {
                return;
            }

            if (TryParseId(book.bookshelfItemId, out var itemId))
            {
                client.RemoveBookshelfItem(
                    itemId,
                    _ => { },
                    error => Debug.LogWarning($"Bookshelf item remove failed: {FormatError(error)}"));
                return;
            }

            if (TryParseId(book.userBookId, out var userBookId))
            {
                client.DeleteUserBook(
                    userBookId,
                    _ => { },
                    error => Debug.LogWarning($"User book delete failed: {FormatError(error)}"));
            }
        }

        public void SaveReadingRecord(BookEntity book)
        {
            if (!IsConfigured || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return;
            }

            var request = new ReadingRecordRequestDto
            {
                UserBookId = userBookId,
                Title = book.reportTitle,
                Content = book.reportBody,
                VisibilityScope = ToApiVisibility(book.reportVisibility)
            };

            if (TryParseId(book.readingRecordId, out var recordId))
            {
                client.UpdateReadingRecord(
                    recordId,
                    request,
                    response => ApplyReadingRecordResponse(book, response),
                    error => Debug.LogWarning($"Reading record update failed: {FormatError(error)}"));
                return;
            }

            client.CreateReadingRecord(
                request,
                response => ApplyReadingRecordResponse(book, response),
                error => Debug.LogWarning($"Reading record create failed: {FormatError(error)}"));
        }

        private IEnumerator RefreshAfterSceneSettles()
        {
            yield return null;
            RefreshFromServer();
        }

        private void LoadBookshelfItems(BookshelfListResponseDto response)
        {
            var shelves = response?.Bookshelves ?? new List<BookshelfResponseDto>();
            if (shelves.Count == 0)
            {
                FinishRefresh(false, "서버 책장 목록이 비어 있습니다.");
                return;
            }

            shelves.Sort((left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));
            var snapshots = new List<BookshelfRuntimeApiSnapshot>();
            LoadBookshelfItemAt(shelves, snapshots, 0);
        }

        private void LoadBookshelfItemAt(
            IReadOnlyList<BookshelfResponseDto> shelves,
            List<BookshelfRuntimeApiSnapshot> snapshots,
            int index)
        {
            if (index >= shelves.Count)
            {
                generator.ApplyServerBookshelves(snapshots);
                experienceManager?.NotifyBookshelfApiSynced();
                FinishRefresh(true, null);
                return;
            }

            var shelf = shelves[index];
            client.GetBookshelfItems(
                shelf.Id,
                render =>
                {
                    snapshots.Add(MapRender(render, shelf));
                    LoadBookshelfItemAt(shelves, snapshots, index + 1);
                },
                error =>
                {
                    Debug.LogWarning($"Bookshelf items load failed: {FormatError(error)}");
                    snapshots.Add(MapShelfOnly(shelf));
                    LoadBookshelfItemAt(shelves, snapshots, index + 1);
                });
        }

        private static BookshelfRuntimeApiSnapshot MapRender(
            BookshelfRenderResponseDto render,
            BookshelfResponseDto fallbackShelf)
        {
            var shelfDto = render?.Bookshelf ?? fallbackShelf;
            var snapshot = MapShelfOnly(shelfDto);
            var sections = render?.Sections ?? new List<SectionResponseDto>();
            for (var i = 0; i < sections.Count; i++)
            {
                snapshot.Sections.Add(new BookshelfRuntimeSectionSnapshot
                {
                    SectionId = sections[i].Id.ToString(),
                    Row = sections[i].RowNo,
                    Column = sections[i].ColumnNo,
                    Alignment = ParseAlignment(sections[i].Alignment)
                });
            }

            var sectionById = new Dictionary<long, BookshelfRuntimeSectionSnapshot>();
            for (var i = 0; i < snapshot.Sections.Count; i++)
            {
                if (long.TryParse(snapshot.Sections[i].SectionId, out var id))
                {
                    sectionById[id] = snapshot.Sections[i];
                }
            }

            var items = render?.Items ?? new List<BookshelfRenderItemResponseDto>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!string.Equals(item.ItemType, "BOOK", StringComparison.OrdinalIgnoreCase) || item.Book == null)
                {
                    continue;
                }

                var row = item.SectionRow ?? 0;
                var column = item.SectionColumn ?? 0;
                if (sectionById.TryGetValue(item.SectionId, out var section))
                {
                    row = section.Row;
                    column = section.Column;
                }

                snapshot.Books.Add(new BookshelfRuntimeBookSnapshot
                {
                    BookshelfItemId = item.BookshelfItemId.ToString(),
                    UserBookId = item.UserBookId?.ToString() ?? (item.UserBook != null ? item.UserBook.Id.ToString() : null),
                    BookId = item.Book.BookId.ToString(),
                    SectionId = item.SectionId.ToString(),
                    Row = row,
                    Column = column,
                    SlotNo = item.SlotNo ?? 1,
                    Orientation = ParseDisplayStyle(item.DisplayStyle),
                    Title = item.Book.Title,
                    Author = item.Book.Author,
                    Publisher = item.Book.Publisher,
                    Description = item.Book.Description,
                    MemoPreview = item.ReadingRecord != null && !string.IsNullOrWhiteSpace(item.ReadingRecord.Content)
                        ? item.ReadingRecord.Content
                        : item.Book.Description,
                    OwnerRating = item.UserBook?.OwnerRating.HasValue == true ? (float)item.UserBook.OwnerRating.Value : 0f,
                    ReadingStatus = ParseReadingStatus(item.UserBook?.ReadingStatus),
                    IsLiked = item.UserBook?.Liked == true,
                    ReadingRecordId = item.ReadingRecord?.Id.ToString(),
                    ReportTitle = item.ReadingRecord?.Title,
                    ReportBody = item.ReadingRecord?.Content,
                    ReportUpdatedLabel = FormatApiDate(item.ReadingRecord?.UpdatedAt),
                    ReportVisibility = ParseVisibility(item.ReadingRecord?.VisibilityScope)
                });
            }

            return snapshot;
        }

        private static BookshelfRuntimeApiSnapshot MapShelfOnly(BookshelfResponseDto shelf)
        {
            return new BookshelfRuntimeApiSnapshot
            {
                BookshelfId = shelf?.Id.ToString(),
                Name = shelf?.Name,
                Description = shelf?.Description,
                VisibilityScope = ParseVisibility(shelf?.VisibilityScope),
                DisplayOrder = shelf?.DisplayOrder ?? 0,
                BodyInventoryId = shelf?.BodyInventoryId?.ToString(),
                SkinInventoryId = shelf?.SkinInventoryId?.ToString()
            };
        }

        private void FinishRefresh(bool success, string message)
        {
            isRefreshing = false;
            if (!success && !string.IsNullOrWhiteSpace(message))
            {
                Debug.LogWarning($"Bookshelf API refresh skipped: {message}");
            }
        }

        private static void ApplyUserBookResponse(BookEntity book, UserBookResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.userBookId = response.Id.ToString();
            book.readingStatus = ParseReadingStatus(response.ReadingStatus);
            book.isRead = book.readingStatus == ReadingStatus.Read;
            book.ownerRating = response.OwnerRating.HasValue ? (float)response.OwnerRating.Value : book.ownerRating;
            book.isLiked = response.Liked;
            if (response.Placement != null)
            {
                book.bookshelfItemId = response.Placement.BookshelfItemId?.ToString() ?? book.bookshelfItemId;
                book.bookshelfId = response.Placement.BookshelfId?.ToString() ?? book.bookshelfId;
                book.bookshelfSectionId = response.Placement.SectionId?.ToString() ?? book.bookshelfSectionId;
            }
        }

        private static void ApplyBookshelfItemResponse(BookEntity book, BookshelfItemResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.bookshelfItemId = response.Id.ToString();
            book.bookshelfId = response.BookshelfId.ToString();
            book.bookshelfSectionId = response.SectionId.ToString();
        }

        private static void ApplyReadingRecordResponse(BookEntity book, ReadingRecordResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.readingRecordId = response.Id.ToString();
            book.reportTitle = response.Title;
            book.reportBody = response.Content;
            book.reportVisibility = ParseVisibility(response.VisibilityScope);
            book.reportUpdatedLabel = FormatApiDate(response.UpdatedAt);
            book.memoPreview = !string.IsNullOrWhiteSpace(response.Content) ? response.Content : book.memoPreview;
        }

        private static bool TryParseId(string value, out long id)
        {
            return long.TryParse(value, out id) && id > 0;
        }

        private static string ToApiReadingStatus(ReadingStatus status)
        {
            switch (status)
            {
                case ReadingStatus.Read:
                    return "READ";
                case ReadingStatus.Reading:
                    return "READING";
                default:
                    return "UNREAD";
            }
        }

        private static ReadingStatus ParseReadingStatus(string value)
        {
            if (string.Equals(value, "READ", StringComparison.OrdinalIgnoreCase))
            {
                return ReadingStatus.Read;
            }

            return string.Equals(value, "READING", StringComparison.OrdinalIgnoreCase)
                ? ReadingStatus.Reading
                : ReadingStatus.Unread;
        }

        private static string ToApiVisibility(VisibilityScope scope)
        {
            switch (scope)
            {
                case VisibilityScope.Private:
                    return "PRIVATE";
                case VisibilityScope.Followers:
                    return "FOLLOWERS";
                default:
                    return "PUBLIC";
            }
        }

        private static VisibilityScope ParseVisibility(string value)
        {
            if (string.Equals(value, "PRIVATE", StringComparison.OrdinalIgnoreCase))
            {
                return VisibilityScope.Private;
            }

            return string.Equals(value, "FOLLOWERS", StringComparison.OrdinalIgnoreCase)
                ? VisibilityScope.Followers
                : VisibilityScope.Public;
        }

        private static string ToApiDisplayStyle(BookOrientation orientation)
        {
            switch (orientation)
            {
                case BookOrientation.Front:
                    return "FRONT";
                case BookOrientation.Angled45:
                    return "ANGLED45";
                default:
                    return "SPINE";
            }
        }

        private static BookOrientation ParseDisplayStyle(string value)
        {
            if (string.Equals(value, "FRONT", StringComparison.OrdinalIgnoreCase))
            {
                return BookOrientation.Front;
            }

            return string.Equals(value, "ANGLED45", StringComparison.OrdinalIgnoreCase)
                ? BookOrientation.Angled45
                : BookOrientation.Spine;
        }

        private static ShelfSectionAlignment ParseAlignment(string value)
        {
            if (string.Equals(value, "CENTER", StringComparison.OrdinalIgnoreCase))
            {
                return ShelfSectionAlignment.Center;
            }

            return string.Equals(value, "RIGHT", StringComparison.OrdinalIgnoreCase)
                ? ShelfSectionAlignment.Right
                : ShelfSectionAlignment.Left;
        }

        private static string FormatApiDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return DateTimeOffset.TryParse(value, out var parsed)
                ? parsed.ToString("yyyy.MM.dd")
                : value;
        }

        private static string FormatError(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "unknown error";
            }

            return string.IsNullOrWhiteSpace(error.Message)
                ? error.Error
                : error.Message;
        }
    }
}
