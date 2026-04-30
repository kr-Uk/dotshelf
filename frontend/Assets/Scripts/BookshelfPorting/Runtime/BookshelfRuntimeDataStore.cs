using System;
using System.Collections.Generic;
using App.Domain;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [Serializable]
    public class BookMetaData
    {
        public string bookId;
        public string title;
        public string author;
        public string publisher;
        public string description;
        public ReadingStatus readingStatus = ReadingStatus.Unread;
        public float ownerRating;
        public float averageRating;
        public int totalLikeCount;
        public bool isLiked;
        public bool isPaidCoverOwned;
        public bool isDecoration;
        public bool isRead;
        public bool isPublic = true;
        public VisibilityScope reportVisibility = VisibilityScope.Public;
        public string memoPreview;
        public string reportTitle;
        public string reportBody;
        public string reportUpdatedLabel;
    }

    [Serializable]
    public class BookPlacementData
    {
        public string bookId;
        public int row;
        public int column;
        public string orientation;
    }

    [Serializable]
    public class GuestbookEntryData
    {
        public string entryId;
        public string authorName;
        public string message;
        public int sortOrder;
        public string timestampLabel;
    }

    [Serializable]
    public class ExperienceStateData
    {
        public string currentFocusArea;
        public string selectedBookId;
        public string selectedTheme;
    }

    [Serializable]
    public class BookshelfLayoutData
    {
        public List<BookPlacementData> placements = new List<BookPlacementData>();
    }

    [Serializable]
    public class BookshelfRuntimeSnapshotData
    {
        public ExperienceStateData experience = new ExperienceStateData();
        public BookshelfLayoutData layout = new BookshelfLayoutData();
        public List<BookMetaData> books = new List<BookMetaData>();
        public List<GuestbookEntryData> guestbookEntries = new List<GuestbookEntryData>();
    }

    public class BookshelfRuntimeDataStore : MonoBehaviour
    {
        private readonly Dictionary<string, BookMetaData> bookMetaById = new Dictionary<string, BookMetaData>();
        private readonly List<GuestbookEntryData> guestbookEntries = new List<GuestbookEntryData>();

        private BookshelfState state;
        private MaterialFactory materialFactory;

        public void Configure(BookshelfState bookshelfState, MaterialFactory factory)
        {
            state = bookshelfState;
            materialFactory = factory;
            EnsureSeedData();
        }

        public BookMetaData GetOrCreateBookMeta(BookEntity book)
        {
            if (book == null)
            {
                return null;
            }

            if (!bookMetaById.TryGetValue(book.bookId, out var meta))
            {
                meta = CreateDefaultBookMeta(book);
                bookMetaById.Add(book.bookId, meta);
            }

            return meta;
        }

        public IReadOnlyList<GuestbookEntryData> GetGuestbookEntries()
        {
            EnsureSeedData();
            return guestbookEntries;
        }

        public BookshelfRuntimeSnapshotData CreateSnapshot(ExperienceFocusArea focusArea, BookEntity selectedBook)
        {
            EnsureSeedData();

            var snapshot = new BookshelfRuntimeSnapshotData();
            snapshot.experience.currentFocusArea = focusArea.ToString();
            snapshot.experience.selectedBookId = selectedBook != null ? selectedBook.bookId : string.Empty;
            snapshot.experience.selectedTheme = materialFactory != null ? materialFactory.CurrentTheme.ToString() : BookshelfThemeStyle.Default.ToString();

            foreach (var entry in guestbookEntries)
            {
                snapshot.guestbookEntries.Add(new GuestbookEntryData
                {
                    entryId = entry.entryId,
                    authorName = entry.authorName,
                    message = entry.message,
                    sortOrder = entry.sortOrder,
                    timestampLabel = entry.timestampLabel
                });
            }

            foreach (var pair in bookMetaById)
            {
                var meta = pair.Value;
                snapshot.books.Add(new BookMetaData
                {
                    bookId = meta.bookId,
                    title = meta.title,
                    author = meta.author,
                    publisher = meta.publisher,
                    description = meta.description,
                    readingStatus = meta.readingStatus,
                    ownerRating = meta.ownerRating,
                    averageRating = meta.averageRating,
                    totalLikeCount = meta.totalLikeCount,
                    isLiked = meta.isLiked,
                    isPaidCoverOwned = meta.isPaidCoverOwned,
                    isDecoration = meta.isDecoration,
                    isRead = meta.isRead,
                    isPublic = meta.isPublic,
                    reportVisibility = meta.reportVisibility,
                    memoPreview = meta.memoPreview,
                    reportTitle = meta.reportTitle,
                    reportBody = meta.reportBody,
                    reportUpdatedLabel = meta.reportUpdatedLabel
                });
            }

            if (state != null)
            {
                for (var i = 0; i < state.Sections.Count; i++)
                {
                    var section = state.Sections[i];
                    for (var j = 0; j < section.books.Count; j++)
                    {
                        var book = section.books[j];
                        GetOrCreateBookMeta(book);
                        snapshot.layout.placements.Add(new BookPlacementData
                        {
                            bookId = book.bookId,
                            row = section.row,
                            column = section.column,
                            orientation = book.orientation.ToString()
                        });
                    }
                }
            }

            return snapshot;
        }

        private void EnsureSeedData()
        {
            if (guestbookEntries.Count == 0)
            {
                guestbookEntries.Add(new GuestbookEntryData
                {
                    entryId = "entry-1",
                    authorName = "Minji",
                    message = "Leaving today's favorite sentence on the board.",
                    sortOrder = 0,
                    timestampLabel = "Today"
                });
                guestbookEntries.Add(new GuestbookEntryData
                {
                    entryId = "entry-2",
                    authorName = "Hyunwoo",
                    message = "Next time I want to change the room theme too.",
                    sortOrder = 1,
                    timestampLabel = "1h ago"
                });
                guestbookEntries.Add(new GuestbookEntryData
                {
                    entryId = "entry-3",
                    authorName = "Seoyeon",
                    message = "This room already feels like a shared reading archive.",
                    sortOrder = 2,
                    timestampLabel = "Yesterday"
                });
            }

            if (state == null)
            {
                return;
            }

            for (var i = 0; i < state.Sections.Count; i++)
            {
                var section = state.Sections[i];
                for (var j = 0; j < section.books.Count; j++)
                {
                    GetOrCreateBookMeta(section.books[j]);
                }
            }
        }

        private static BookMetaData CreateDefaultBookMeta(BookEntity book)
        {
            return new BookMetaData
            {
                bookId = book.bookId,
                title = ResolveBookTitle(book),
                author = string.IsNullOrWhiteSpace(book.author) ? "저자 미상" : book.author,
                publisher = string.IsNullOrWhiteSpace(book.publisher) ? "출판사 미상" : book.publisher,
                description = string.IsNullOrWhiteSpace(book.description) ? "책 설명이 없습니다." : book.description,
                readingStatus = book.readingStatus,
                ownerRating = book.ownerRating,
                averageRating = ResolveDefaultAverageRating(book),
                totalLikeCount = ResolveDefaultLikeCount(book),
                isLiked = book.isLiked,
                isPaidCoverOwned = book.isPaidCoverOwned,
                isDecoration = book.isDecoration,
                isRead = book.isRead,
                isPublic = book.reportVisibility == VisibilityScope.Public,
                reportVisibility = book.reportVisibility,
                memoPreview = ResolveMemoPreview(book),
                reportTitle = book.reportTitle ?? string.Empty,
                reportBody = book.reportBody ?? string.Empty,
                reportUpdatedLabel = book.reportUpdatedLabel ?? string.Empty
            };
        }

        public void ApplyBookMeta(BookEntity book, BookMetaData meta)
        {
            if (book == null || meta == null)
            {
                return;
            }

            book.bookId = string.IsNullOrWhiteSpace(meta.bookId) ? book.bookId : meta.bookId;
            book.title = meta.title;
            book.author = meta.author;
            book.publisher = meta.publisher;
            book.description = meta.description;
            book.memoPreview = meta.memoPreview;
            book.readingStatus = meta.readingStatus;
            book.ownerRating = Mathf.Clamp(meta.ownerRating, 0f, 5f);
            book.rating = Mathf.RoundToInt(Mathf.Clamp(meta.averageRating > 0f ? meta.averageRating : book.ownerRating, 0f, 5f));
            book.isLiked = meta.isLiked;
            book.isPaidCoverOwned = meta.isPaidCoverOwned;
            book.isDecoration = meta.isDecoration;
            book.isRead = meta.readingStatus == ReadingStatus.Read;
            meta.isPublic = meta.reportVisibility == VisibilityScope.Public;
            book.reportVisibility = meta.reportVisibility;
            book.isPublic = meta.isPublic;
            book.reportTitle = meta.reportTitle;
            book.reportBody = meta.reportBody;
            book.reportUpdatedLabel = meta.reportUpdatedLabel;
            if (string.IsNullOrWhiteSpace(meta.memoPreview) && !string.IsNullOrWhiteSpace(meta.reportBody))
            {
                meta.memoPreview = meta.reportBody;
            }

            bookMetaById[book.bookId] = meta;
        }

        private static string ResolveBookTitle(BookEntity book)
        {
            if (!string.IsNullOrWhiteSpace(book.title))
            {
                return book.title;
            }

            return string.IsNullOrWhiteSpace(book.bookId) ? "Untitled Book" : book.bookId.Replace('_', ' ');
        }

        private static string ResolveMemoPreview(BookEntity book)
        {
            if (!string.IsNullOrWhiteSpace(book.memoPreview))
            {
                return book.memoPreview;
            }

            if (!string.IsNullOrWhiteSpace(book.description))
            {
                return book.description;
            }

            return "No review yet. Add a short note in the next step.";
        }

        private static float ResolveDefaultAverageRating(BookEntity book)
        {
            if (book == null)
            {
                return 0f;
            }

            if (book.rating > 0)
            {
                return Mathf.Clamp(book.rating, 0f, 5f);
            }

            if (book.ownerRating > 0f)
            {
                return Mathf.Clamp(book.ownerRating, 0f, 5f);
            }

            return 0f;
        }

        private static int ResolveDefaultLikeCount(BookEntity book)
        {
            if (book == null)
            {
                return 0;
            }

            return 0;
        }
    }
}
