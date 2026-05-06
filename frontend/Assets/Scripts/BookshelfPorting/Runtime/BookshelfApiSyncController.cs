using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using App.Auth;
using App.Bootstrap;
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
        // Lifetime: per-controller instance. BE visit endpoint is not strictly
        // idempotent (visit count) so we de-dupe at the FE level — re-entering
        // the same other-user space within a single play session counts once.
        // Resets when MainScene unloads (controller is destroyed with scene).
        private readonly HashSet<long> visitedSpaceIds = new HashSet<long>();

        public bool IsConfigured => configured && client != null && generator != null;

        private bool CanUseServerApi => IsConfigured && !IsLocalDevModeActive();

        public bool IsServerSyncEnabled => CanUseServerApi;

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

        private static bool IsLocalDevModeActive()
        {
#if UNITY_EDITOR
            return DevMainSceneMode.IsActive;
#else
            return false;
#endif
        }

        public void RefreshFromServer()
        {
            if (!IsConfigured || isRefreshing)
            {
                return;
            }

            // /v1/spaces/me requires a Bearer token. Skip when anonymous
            // (token wiped on logout, or never set on first launch) so we
            // do not 401-spam BE and the local fallback catalog stays
            // visible. Replaces the former IsGuestSession gate (MR #88).
            if (APIManager.Instance == null || !APIManager.Instance.HasAccessToken)
            {
                return;
            }

            isRefreshing = true;
            client.GetMySpace(
                space =>
                {
                    currentSpaceId = space?.Id;
                    // Forward to the visitor overlay so it can hide on own
                    // space (the common case here — GetMySpace always returns
                    // the local user's space). When a future "visit other
                    // user's space" flow lands it should call SetCurrentSpace
                    // directly with the GET /v1/spaces/{spaceId} response.
                    SpaceVisitorOverlayController.Instance?.SetCurrentSpace(space);
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
                error =>
                {
#if UNITY_EDITOR
                    // Dev shortcut entry: BE auth is currently relaxed but may
                    // partially 401 on /v1/spaces/me. Soft-fall-back to the
                    // local book catalog so MainScene still renders a populated
                    // bookshelf instead of breaking the dev flow.
                    if (DevMainSceneMode.IsActive && IsAuthFailure(error))
                    {
                        Debug.Log("[DEV MODE] server bookshelf unavailable, using local fallback");
                        ApplyDevLocalFallback();
                        FinishRefresh(true, null);
                        return;
                    }
#endif
                    FinishRefresh(false, FormatError(error));
                });
        }

        // External entry point for "visit another user's space" flows
        // (search result click, dashboard, deep link, etc.). Caller must
        // resolve the spaceId — this method does NOT take a hardcoded ID.
        // Pipeline mirrors RefreshFromServer except it hits /v1/spaces/{id}
        // instead of /v1/spaces/me, then routes the response through the
        // same overlay forward + bookshelf load + visit record path.
        //
        // Own-space detection: SpaceResponse.OwnerUserId == AuthSession.UserId.
        public void EnterSpaceById(long spaceId)
        {
            if (!IsConfigured || isRefreshing)
            {
                return;
            }
            if (spaceId <= 0)
            {
                Debug.LogWarning($"[BookshelfApiSync] EnterSpaceById ignored — invalid spaceId={spaceId}.");
                return;
            }

            isRefreshing = true;
            client.GetSpace(
                spaceId,
                space =>
                {
                    if (space == null || space.Id <= 0)
                    {
                        FinishRefresh(false, "공간 정보를 불러오지 못했습니다.");
                        return;
                    }

                    currentSpaceId = space.Id;
                    // Forward to overlay regardless of own/other — it
                    // self-hides for own space and self-shows for other
                    // user. Same hook BookshelfApiSyncController already
                    // uses on /v1/spaces/me responses.
                    SpaceVisitorOverlayController.Instance?.SetCurrentSpace(space);

                    if (!IsOwnSpace(space.OwnerUserId))
                    {
                        // Visit record is "best-effort" — failure must not
                        // block the user from seeing the space. The de-dupe
                        // also moves to record-on-attempt to avoid retry
                        // floods if BE is slow.
                        RecordVisitOnce(space.Id);
                    }

                    client.GetBookshelves(
                        space.Id,
                        LoadBookshelfItems,
                        error => FinishRefresh(false, FormatError(error)));
                },
                error =>
                {
                    Debug.LogWarning(
                        $"[BookshelfApiSync] GetSpace({spaceId}) failed: code={error?.Code} message={error?.Message}");
                    FinishRefresh(false, "공간 정보를 불러오지 못했습니다.");
                });
        }

        private bool IsOwnSpace(long ownerUserId)
        {
            var myUserId = AuthSession.UserId;
            // Anonymous (pre-login) caller has myUserId == 0 from PlayerPrefs
            // default. They cannot own any BE space record, so the not-own
            // check passes — guarded callers should already short-circuit on
            // !HasAccessToken before reaching here (see RecordVisitOnce).
            return myUserId != 0 && ownerUserId == myUserId;
        }

        private void RecordVisitOnce(long spaceId)
        {
            // Visit recording requires a Bearer token: BE recordVisit calls
            // userService.requireUserId(), which 401s on anonymous calls.
            // Skipping here also keeps visitedSpaceIds untouched so a later
            // login in the same play session can still record the visit on
            // re-entry.
            if (APIManager.Instance == null || !APIManager.Instance.HasAccessToken)
            {
                return;
            }

            // Mark BEFORE the call (record-on-attempt) so a slow BE round-trip
            // cannot cause the user to navigate away and re-enter, racing two
            // visit POSTs. Worst case: BE call fails and we never retry — that
            // is preferable to inflating visit counts on a flaky network.
            if (!visitedSpaceIds.Add(spaceId))
            {
                return;
            }

            client.RecordSpaceVisit(
                spaceId,
                _ => { /* fire-and-forget on success */ },
                error =>
                {
                    Debug.LogWarning(
                        $"[BookshelfApiSync] RecordSpaceVisit({spaceId}) failed: code={error?.Code} message={error?.Message}");
                });
        }

#if UNITY_EDITOR
        private static bool IsAuthFailure(ApiErrorResponseDto error)
        {
            var statusCode = error?.StatusCode ?? 0L;
            return statusCode == 401L || statusCode == 403L;
        }

        // Flip the generator into local-catalog mode and rebuild. Generate()
        // does ClearGenerated → BuildRoom → BuildBookshelf, so the brief
        // empty-shelf state created by the initial server-mode Generate gets
        // replaced with a populated shelf using the public /v1/books catalog
        // (which does not require auth).
        private void ApplyDevLocalFallback()
        {
            if (generator == null)
            {
                return;
            }

            generator.ConfigureServerBookshelfSnapshots(false);
            generator.Generate();
        }
#endif

        public bool CreateBookshelf(string name, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi)
            {
                return false;
            }

            if (!currentSpaceId.HasValue)
            {
                client.GetMySpace(
                    space =>
                    {
                        currentSpaceId = space?.Id;
                        if (!currentSpaceId.HasValue || currentSpaceId.Value <= 0)
                        {
                            onFailed?.Invoke("책장을 추가할 공간 정보를 찾을 수 없습니다.");
                            return;
                        }

                        CreateBookshelf(name, onSucceeded, onFailed);
                    },
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Bookshelf create skipped: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            client.CreateBookshelf(
                new CreateBookshelfRequestDto
                {
                    SpaceId = currentSpaceId.Value,
                    Name = string.IsNullOrWhiteSpace(name) ? "새 책장" : name,
                    VisibilityScope = "PUBLIC"
                },
                _ =>
                {
                    onSucceeded?.Invoke();
                    RefreshFromServer();
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf create failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool UpdateBookshelf(int index, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || !TryParseId(generator.GetBookshelfServerId(index), out var bookshelfId))
            {
                return false;
            }

            client.UpdateBookshelf(
                bookshelfId,
                new UpdateBookshelfRequestDto
                {
                    Name = generator.GetBookshelfName(index),
                    VisibilityScope = ToApiVisibility(generator.GetBookshelfVisibility(index)),
                    SkinInventoryId = BookshelfSkinResolver.ResolveInventoryId(generator.GetBookshelfDesignStyle(index))
                },
                _ => onSucceeded?.Invoke(),
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf update failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool DeleteBookshelf(string bookshelfServerId, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || !TryParseId(bookshelfServerId, out var bookshelfId))
            {
                return false;
            }

            client.DeleteBookshelf(
                bookshelfId,
                _ =>
                {
                    onSucceeded?.Invoke();
                    RefreshFromServer();
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf delete failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool ReorderBookshelves(Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi)
            {
                return false;
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

            if (ids.Count <= 1 || ids.Count != rawIds.Count)
            {
                return false;
            }

            client.ReorderBookshelves(
                new ReorderBookshelvesRequestDto { BookshelfIds = ids },
                _ => onSucceeded?.Invoke(),
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Bookshelf reorder failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool SyncBookState(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return false;
            }

            client.UpdateUserBook(
                userBookId,
                new UpdateUserBookRequestDto
                {
                    ReadingStatus = ToApiReadingStatus(book.readingStatus),
                    OwnerRating = book.ownerRating > 0f ? (decimal?)Math.Round((decimal)book.ownerRating, 1) : null
                },
                response =>
                {
                    ApplyUserBookResponse(book, response);
                    onSucceeded?.Invoke(book);
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"User book update failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public bool SyncBookLike(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return false;
            }

            var desiredLiked = book.isLiked;
            Action<ToggleResponseDto> success = response =>
            {
                book.isLiked = response != null ? response.Active : desiredLiked;
                if (response != null)
                {
                    book.totalLikeCount = Mathf.Max(0, (int)response.Count);
                    book.hasAggregateBookStats = true;
                }

                onSucceeded?.Invoke(book);
            };
            Action<ApiErrorResponseDto> failure = error =>
            {
                var message = FormatUserFacingError(error);
                Debug.LogWarning($"User book like update failed: {FormatError(error)}");
                onFailed?.Invoke(message);
            };

            if (book.isLiked)
            {
                client.LikeUserBook(userBookId, success, failure);
                return true;
            }

            client.UnlikeUserBook(userBookId, success, failure);
            return true;
        }

        public bool SyncBookPlacement(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
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
                return false;
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
                    response =>
                    {
                        ApplyBookshelfItemResponse(book, response);
                        onSucceeded?.Invoke(book);
                    },
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Bookshelf item move failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
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
                    response =>
                    {
                        ApplyUserBookResponse(book, response);
                        onSucceeded?.Invoke(book);
                    },
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"User book move failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            return false;
        }

        public void SyncSectionAlignment(ShelfSection section)
        {
            if (!CanUseServerApi || section == null)
            {
                return;
            }

            var bookshelfIdText = generator.GetActiveBookshelfServerId();
            var sectionIdText = generator.GetSectionServerId(generator.ActiveBookshelfIndex, section.row, section.column);
            if (!TryParseId(bookshelfIdText, out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                return;
            }

            client.UpdateSectionAlignment(
                bookshelfId,
                sectionId,
                new SectionAlignmentRequestDto { Alignment = ToApiAlignment(section.alignment) },
                _ => { },
                error => Debug.LogWarning($"Bookshelf section alignment update failed: {FormatError(error)}"));
        }

        public void FetchBookDetails(BookEntity book, Action<BookEntity> onUpdated = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.bookId, out var bookId))
            {
                return;
            }

            client.GetBook(
                bookId,
                response =>
                {
                    ApplyBookResponse(book, response);
                    onUpdated?.Invoke(book);
                },
                error => Debug.LogWarning($"Book detail load failed: {FormatError(error)}"));
        }

        public bool FetchReadingRecord(BookEntity book, Action<BookEntity, bool> onCompleted = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
            }

            if (TryParseId(book.readingRecordId, out var recordId))
            {
                client.GetReadingRecord(
                    recordId,
                    response =>
                    {
                        ApplyReadingRecordResponse(book, response);
                        onCompleted?.Invoke(book, response != null);
                    },
                    error =>
                    {
                        Debug.LogWarning($"Reading record load failed: {FormatError(error)}");
                        onCompleted?.Invoke(book, false);
                    });
                return true;
            }

            if (TryParseId(book.userBookId, out var userBookId))
            {
                client.GetUserBookReadingRecord(
                    userBookId,
                    response =>
                    {
                        ApplyReadingRecordResponse(book, response);
                        onCompleted?.Invoke(book, response != null);
                    },
                    error =>
                    {
                        onCompleted?.Invoke(book, false);
                    });
                return true;
            }

            return false;
        }

        public bool DeleteReadingRecord(BookEntity book, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.readingRecordId, out var recordId))
            {
                return false;
            }

            client.DeleteReadingRecord(
                recordId,
                _ =>
                {
                    book.readingRecordId = null;
                    book.reportTitle = string.Empty;
                    book.reportBody = string.Empty;
                    book.reportUpdatedLabel = string.Empty;
                    book.memoPreview = book.description;
                    onSucceeded?.Invoke();
                },
                error =>
                {
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Reading record delete failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        public void ToggleReadingRecordLike(BookEntity book, bool liked, Action<BookEntity> onUpdated = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.readingRecordId, out var recordId))
            {
                return;
            }

            Action<ToggleResponseDto> success = response =>
            {
                if (response != null)
                {
                    book.reportLiked = response.Active;
                    book.reportLikeCount = Mathf.Max(0, (int)response.Count);
                }
                else
                {
                    book.reportLiked = liked;
                }

                onUpdated?.Invoke(book);
            };
            Action<ApiErrorResponseDto> failure = error => Debug.LogWarning($"Reading record like update failed: {FormatError(error)}");
            if (liked)
            {
                client.LikeReadingRecord(recordId, success, failure);
            }
            else
            {
                client.UnlikeReadingRecord(recordId, success, failure);
            }
        }

        public void FetchReadingRecordComments(BookEntity book, Action<IReadOnlyList<CommentResponseDto>> onLoaded = null)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.readingRecordId, out var recordId))
            {
                return;
            }

            client.GetReadingRecordComments(
                recordId,
                response =>
                {
                    var comments = response?.Comments ?? new List<CommentResponseDto>();
                    book.reportCommentCount = comments.Count;
                    onLoaded?.Invoke(comments);
                },
                error => Debug.LogWarning($"Reading record comments load failed: {FormatError(error)}"));
        }

        public void CreateReadingRecordComment(BookEntity book, string content, Action<CommentResponseDto> onCreated = null)
        {
            if (!CanUseServerApi ||
                book == null ||
                string.IsNullOrWhiteSpace(content) ||
                !TryParseId(book.readingRecordId, out var recordId))
            {
                return;
            }

            client.CreateReadingRecordComment(
                recordId,
                new CommentRequestDto { Content = content.Trim() },
                response =>
                {
                    book.reportCommentCount = Mathf.Max(0, book.reportCommentCount + 1);
                    onCreated?.Invoke(response);
                },
                error => Debug.LogWarning($"Reading record comment create failed: {FormatError(error)}"));
        }

        public void DeleteReadingRecordComment(long commentId, Action onDeleted = null)
        {
            if (!CanUseServerApi || commentId <= 0L)
            {
                return;
            }

            client.DeleteComment(
                commentId,
                _ => onDeleted?.Invoke(),
                error => Debug.LogWarning($"Reading record comment delete failed: {FormatError(error)}"));
        }

        public void ToggleReadingRecordCommentLike(long commentId, bool liked, Action<ToggleResponseDto> onUpdated = null)
        {
            if (!CanUseServerApi || commentId <= 0L)
            {
                return;
            }

            Action<ToggleResponseDto> success = response => onUpdated?.Invoke(response);
            Action<ApiErrorResponseDto> failure = error => Debug.LogWarning($"Reading record comment like update failed: {FormatError(error)}");
            if (liked)
            {
                client.LikeComment(commentId, success, failure);
            }
            else
            {
                client.UnlikeComment(commentId, success, failure);
            }
        }

        public bool AddBookToActiveShelf(
            BookEntity book,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null)
        {
            return AddBookToShelf(book, generator != null ? generator.ActiveBookshelfIndex : -1, onSucceeded, onFailed);
        }

        public bool AddBookToShelf(
            BookEntity book,
            int bookshelfIndex,
            Action<BookEntity> onSucceeded = null,
            Action<string> onFailed = null,
            Action<BookEntity, string> onPostAddStateFailed = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
            }

            var targetBookshelfIndex = bookshelfIndex >= 0 ? bookshelfIndex : generator.ActiveBookshelfIndex;
            var section = book.CurrentSection;
            var sectionIdText = section != null
                ? generator.GetSectionServerId(targetBookshelfIndex, section.row, section.column)
                : book.bookshelfSectionId;
            var bookshelfIdText = string.IsNullOrWhiteSpace(book.bookshelfId)
                ? generator.GetBookshelfServerId(targetBookshelfIndex)
                : book.bookshelfId;
            if (!TryParseId(bookshelfIdText, out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                return false;
            }

            var previousBookshelfId = book.bookshelfId;
            var previousBookshelfSectionId = book.bookshelfSectionId;
            book.bookshelfId = bookshelfId.ToString();
            book.bookshelfSectionId = sectionId.ToString();
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
                        CompleteBookAddStateSync(book, desiredReadingStatus, desiredOwnerRating, onSucceeded, onFailed, onPostAddStateFailed);
                    },
                    error =>
                    {
                        RestoreBookAddPlacementIds(book, previousBookshelfId, previousBookshelfSectionId);
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Book add failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            client.AddKakaoBookToShelf(
                bookshelfId,
                new AddKakaoBookToShelfRequestDto
                {
                    Title = book.title,
                    Author = book.author,
                    Publisher = book.publisher,
                    Isbn13 = book.isbn13,
                    Isbn10 = book.isbn10,
                    PublishedDate = book.publishedDate,
                    Description = book.description,
                    CoverImageUrl = book.coverImageUrl,
                    SourceUrl = book.sourceUrl,
                    SectionId = sectionId,
                    SlotNo = slotNo,
                    FacingDirection = facingDirection,
                    DisplayStyle = displayStyle
                },
                response =>
                {
                    book.bookId = response?.BookId > 0 ? response.BookId.ToString() : book.bookId;
                    ApplyUserBookResponse(book, response);
                    CompleteBookAddStateSync(book, desiredReadingStatus, desiredOwnerRating, onSucceeded, onFailed, onPostAddStateFailed);
                },
                error =>
                {
                    RestoreBookAddPlacementIds(book, previousBookshelfId, previousBookshelfSectionId);
                    var message = FormatUserFacingError(error);
                    Debug.LogWarning($"Kakao/manual book add failed: {FormatError(error)}");
                    onFailed?.Invoke(message);
                });
            return true;
        }

        private void CompleteBookAddStateSync(
            BookEntity book,
            ReadingStatus desiredReadingStatus,
            float desiredOwnerRating,
            Action<BookEntity> onSucceeded,
            Action<string> onFailed,
            Action<BookEntity, string> onPostAddStateFailed)
        {
            if (book == null)
            {
                onFailed?.Invoke("추가한 책 정보를 찾을 수 없습니다.");
                return;
            }

            var responseReadingStatus = book.readingStatus;
            var responseOwnerRating = book.ownerRating;
            book.readingStatus = desiredReadingStatus;
            book.isRead = desiredReadingStatus == ReadingStatus.Read;
            book.ownerRating = desiredOwnerRating;

            var needsStateSync = responseReadingStatus != desiredReadingStatus ||
                                 !Mathf.Approximately(responseOwnerRating, desiredOwnerRating);
            if (!needsStateSync)
            {
                onSucceeded?.Invoke(book);
                return;
            }

            if (!SyncBookState(
                    book,
                    onSucceeded,
                    error => HandlePostAddStateSyncFailure(book, error, onFailed, onPostAddStateFailed)))
            {
                HandlePostAddStateSyncFailure(
                    book,
                    "추가한 책의 독서 상태를 저장하는 데 필요한 서버 식별자를 찾을 수 없습니다.",
                    onFailed,
                    onPostAddStateFailed);
            }
        }

        private static void HandlePostAddStateSyncFailure(
            BookEntity book,
            string errorMessage,
            Action<string> onFailed,
            Action<BookEntity, string> onPostAddStateFailed)
        {
            if (onPostAddStateFailed != null)
            {
                onPostAddStateFailed.Invoke(book, errorMessage);
            }
            else
            {
                onFailed?.Invoke(errorMessage);
            }
        }

        private static void RestoreBookAddPlacementIds(BookEntity book, string bookshelfId, string bookshelfSectionId)
        {
            if (book == null)
            {
                return;
            }

            book.bookshelfId = bookshelfId;
            book.bookshelfSectionId = bookshelfSectionId;
        }

        public bool RemoveBook(BookEntity book, Action onSucceeded = null, Action<string> onFailed = null)
        {
            if (!CanUseServerApi || book == null)
            {
                return false;
            }

            if (TryParseId(book.bookshelfItemId, out var itemId))
            {
                client.RemoveBookshelfItem(
                    itemId,
                    _ => onSucceeded?.Invoke(),
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"Bookshelf item remove failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            if (TryParseId(book.userBookId, out var userBookId))
            {
                client.DeleteUserBook(
                    userBookId,
                    _ => onSucceeded?.Invoke(),
                    error =>
                    {
                        var message = FormatUserFacingError(error);
                        Debug.LogWarning($"User book delete failed: {FormatError(error)}");
                        onFailed?.Invoke(message);
                    });
                return true;
            }

            return false;
        }

        public void ArchiveBook(BookEntity book)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
            {
                return;
            }

            client.ArchiveUserBook(
                userBookId,
                response => ApplyUserBookResponse(book, response),
                error => Debug.LogWarning($"User book archive failed: {FormatError(error)}"));
        }

        public void FetchArchivedUserBooks(Action<IReadOnlyList<UserBookResponseDto>> onLoaded = null)
        {
            if (!CanUseServerApi)
            {
                return;
            }

            client.GetArchivedUserBooks(
                response =>
                {
                    IReadOnlyList<UserBookResponseDto> books = Array.Empty<UserBookResponseDto>();
                    if (response?.Books != null)
                    {
                        books = response.Books;
                    }

                    onLoaded?.Invoke(books);
                },
                error => Debug.LogWarning($"Archived user books load failed: {FormatError(error)}"));
        }

        public void FetchUserBooks(
            string storageStatus = null,
            string readingStatus = null,
            long? bookshelfId = null,
            long? sectionId = null,
            string cursor = null,
            int limit = 50,
            Action<UserBookPlacementListResponseDto> onLoaded = null)
        {
            if (!CanUseServerApi)
            {
                return;
            }

            client.GetUserBooks(
                storageStatus,
                readingStatus,
                bookshelfId,
                sectionId,
                cursor,
                limit,
                onLoaded,
                error => Debug.LogWarning($"User books load failed: {FormatError(error)}"));
        }

        public void FetchAssetInventory(Action<InventoryResponseDto> onLoaded = null)
        {
            if (!CanUseServerApi)
            {
                return;
            }

            client.GetAssetInventory(
                onLoaded,
                error => Debug.LogWarning($"Asset inventory load failed: {FormatError(error)}"));
        }

        public void AddInteriorObjectToActiveShelf(long assetInventoryId, ShelfSection section, int slotNo = 1)
        {
            if (section == null)
            {
                return;
            }

            AddInteriorObjectToActiveShelf(assetInventoryId, section.row, section.column, slotNo);
        }

        public void AddInteriorObjectToActiveShelf(long assetInventoryId, int row, int column, int slotNo = 1)
        {
            if (!CanUseServerApi || assetInventoryId <= 0L)
            {
                return;
            }

            var sectionIdText = generator.GetSectionServerId(generator.ActiveBookshelfIndex, row, column);
            if (!TryParseId(generator.GetActiveBookshelfServerId(), out var bookshelfId) ||
                !TryParseId(sectionIdText, out var sectionId))
            {
                Debug.LogWarning("Interior object add skipped: section id is required.");
                return;
            }

            client.AddInteriorObject(
                bookshelfId,
                new InteriorObjectRequestDto
                {
                    AssetInventoryId = assetInventoryId,
                    SectionId = sectionId,
                    SlotNo = Mathf.Max(1, slotNo),
                    FacingDirection = "FRONT",
                    DisplayStyle = "SPINE"
                },
                _ => RefreshFromServer(),
                error => Debug.LogWarning($"Interior object add failed: {FormatError(error)}"));
        }

        public void SaveReadingRecord(BookEntity book)
        {
            if (!CanUseServerApi || book == null || !TryParseId(book.userBookId, out var userBookId))
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
                    AverageRating = ResolveAverageRating(item.Book),
                    TotalLikeCount = ResolveLikeCount(item.Book),
                    ReviewCount = ResolveReviewCount(item.Book),
                    HasAggregateBookStats = HasAggregateBookStats(item.Book),
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
            var snapshot = new BookshelfRuntimeApiSnapshot
            {
                BookshelfId = shelf?.Id.ToString(),
                Name = shelf?.Name,
                Description = shelf?.Description,
                VisibilityScope = ParseVisibility(shelf?.VisibilityScope),
                DisplayOrder = shelf?.DisplayOrder ?? 0,
                BodyInventoryId = shelf?.BodyInventoryId?.ToString(),
                SkinInventoryId = shelf?.SkinInventoryId?.ToString()
            };

            if (BookshelfSkinResolver.TryResolveThemeStyle(shelf?.SkinInventoryId, out var style))
            {
                snapshot.HasThemeStyle = true;
                snapshot.ThemeStyle = style;
            }

            return snapshot;
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

        private static void ApplyBookResponse(BookEntity book, BookResponseDto response)
        {
            if (book == null || response == null)
            {
                return;
            }

            book.bookId = ResolveBookId(response.BookId, response.Id, book.bookId);
            book.source = response.Source;
            book.isbn13 = response.Isbn13;
            book.isbn10 = response.Isbn10;
            book.publishedDate = response.PublishedDate;
            book.coverImageUrl = response.CoverImageUrl;
            book.sourceUrl = response.SourceUrl;
            book.title = string.IsNullOrWhiteSpace(response.Title) ? book.title : response.Title;
            book.author = string.IsNullOrWhiteSpace(response.Author) ? book.author : response.Author;
            book.publisher = string.IsNullOrWhiteSpace(response.Publisher) ? book.publisher : response.Publisher;
            book.description = string.IsNullOrWhiteSpace(response.Description) ? book.description : response.Description;
            book.memoPreview = string.IsNullOrWhiteSpace(book.memoPreview) ? book.description : book.memoPreview;
            book.isPaidCoverOwned = response.HasPaidCover;
            book.averageRating = Mathf.Clamp((float)response.AvgRating, 0f, 5f);
            book.totalLikeCount = Mathf.Max(0, response.LikeCount);
            book.reviewCount = Mathf.Max(0, response.ReviewCount);
            book.hasAggregateBookStats = true;
            book.rating = Mathf.RoundToInt(Mathf.Clamp(book.averageRating, 0f, 5f));
        }

        private static string ResolveBookId(string responseBookId, long responseId, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(responseBookId))
            {
                return responseBookId;
            }

            return responseId > 0L ? responseId.ToString() : fallback;
        }

        private static bool HasAggregateBookStats(BookSummaryResponseDto book)
        {
            return book != null &&
                   (book.AvgRating.HasValue ||
                    book.AverageRating.HasValue ||
                    book.LikeCount.HasValue ||
                    book.ReviewCount.HasValue ||
                    book.RecordCount.HasValue);
        }

        private static float ResolveAverageRating(BookSummaryResponseDto book)
        {
            if (book == null)
            {
                return 0f;
            }

            var value = book.AverageRating ?? book.AvgRating ?? 0m;
            return Mathf.Clamp((float)value, 0f, 5f);
        }

        private static int ResolveLikeCount(BookSummaryResponseDto book)
        {
            return book == null ? 0 : Mathf.Max(0, book.LikeCount ?? 0);
        }

        private static int ResolveReviewCount(BookSummaryResponseDto book)
        {
            if (book == null)
            {
                return 0;
            }

            return Mathf.Max(0, book.ReviewCount ?? book.RecordCount ?? 0);
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
            book.reportLikeCount = response.LikeCount;
            book.reportCommentCount = response.CommentCount;
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

        private static string ToApiAlignment(ShelfSectionAlignment alignment)
        {
            switch (alignment)
            {
                case ShelfSectionAlignment.Center:
                    return "CENTER";
                case ShelfSectionAlignment.Right:
                    return "RIGHT";
                default:
                    return "LEFT";
            }
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

        private static string FormatUserFacingError(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "네트워크 또는 서버 응답을 확인해 주세요.";
            }

            if (error.StatusCode == 401L || error.StatusCode == 403L)
            {
                return "권한이 없거나 로그인 정보가 만료되었습니다.";
            }

            if (error.StatusCode >= 500L)
            {
                return "서버가 요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.";
            }

            return "요청을 완료하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        }
    }

    internal static class BookshelfSkinResolver
    {
        private const string CatalogResourcePath = "Catalogs/BookshelfSkinCatalog";
        private static ScriptableObject cachedCatalog;
        private static bool didLoadCatalog;
        private static bool didWarnMissingCatalog;
        private static bool didWarnMissingStyle;

        public static long? ResolveInventoryId(BookshelfThemeStyle style)
        {
            var entry = FindEntry(style);
            var assetId = ResolveEntryAssetId(entry);
            if (assetId > 0L)
            {
                return assetId;
            }

            WarnMissingStyle(style);
            return null;
        }

        public static bool TryResolveThemeStyle(long? skinInventoryId, out BookshelfThemeStyle style)
        {
            style = BookshelfThemeStyle.Default;
            if (!skinInventoryId.HasValue || skinInventoryId.Value <= 0L)
            {
                return false;
            }

            var entries = GetCatalogEntries();
            if (entries == null)
            {
                return false;
            }

            var index = 0;
            foreach (var entry in entries)
            {
                if (entry != null && ResolveEntryAssetId(entry) == skinInventoryId.Value)
                {
                    style = ResolveStyleFromEntry(entry, index);
                    return true;
                }

                index++;
            }

            return false;
        }

        private static object FindEntry(BookshelfThemeStyle style)
        {
            var entries = GetCatalogEntries();
            if (entries == null)
            {
                return null;
            }

            var index = 0;
            foreach (var entry in entries)
            {
                if (entry != null && ResolveStyleFromEntry(entry, index) == style)
                {
                    return entry;
                }

                index++;
            }

            return null;
        }

        private static ScriptableObject LoadCatalog()
        {
            if (didLoadCatalog)
            {
                return cachedCatalog;
            }

            didLoadCatalog = true;
            cachedCatalog = Resources.Load<ScriptableObject>(CatalogResourcePath);
            if (cachedCatalog == null && !didWarnMissingCatalog)
            {
                didWarnMissingCatalog = true;
                Debug.LogWarning($"Bookshelf skin catalog not found at Resources/{CatalogResourcePath}. SkinInventoryId sync will be skipped.");
            }

            return cachedCatalog;
        }

        private static IEnumerable GetCatalogEntries()
        {
            var catalog = LoadCatalog();
            if (catalog == null)
            {
                return null;
            }

            var field = catalog.GetType().GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(catalog) as IEnumerable : null;
        }

        private static long ResolveEntryAssetId(object entry)
        {
            if (entry == null)
            {
                return 0L;
            }

            var property = entry.GetType().GetProperty("AssetId", BindingFlags.Instance | BindingFlags.Public);
            var value = property != null ? property.GetValue(entry) : null;
            return value is long id ? id : 0L;
        }

        private static string ResolveEntryDisplayName(object entry)
        {
            if (entry == null)
            {
                return null;
            }

            var property = entry.GetType().GetProperty("DisplayName", BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(entry) as string : null;
        }

        private static BookshelfThemeStyle ResolveStyleFromEntry(object entry, int index)
        {
            var key = ResolveEntryDisplayName(entry);
            if (!string.IsNullOrWhiteSpace(key))
            {
                key = key.Trim().ToLowerInvariant();
                if (key.Contains("dark") || key.Contains("black") || key.Contains("night"))
                {
                    return BookshelfThemeStyle.Dark;
                }

                if (key.Contains("warm") || key.Contains("wood") || key.Contains("brown"))
                {
                    return BookshelfThemeStyle.Warm;
                }

                if (key.Contains("default") || key.Contains("basic"))
                {
                    return BookshelfThemeStyle.Default;
                }
            }

            return index switch
            {
                1 => BookshelfThemeStyle.Warm,
                2 => BookshelfThemeStyle.Dark,
                _ => BookshelfThemeStyle.Default
            };
        }

        private static void WarnMissingStyle(BookshelfThemeStyle style)
        {
            if (didWarnMissingStyle)
            {
                return;
            }

            didWarnMissingStyle = true;
            Debug.LogWarning($"Bookshelf skin inventory id for style '{style}' could not be resolved. Add Resources/{CatalogResourcePath}.asset or keep the design change local-only.");
        }
    }
}
