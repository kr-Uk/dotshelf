using System;
using System.Collections.Generic;
using UnityEngine;

namespace BookshelfPorting.Runtime
{
    [Serializable]
    public class BookMetaData
    {
        public string bookId;
        public string title;
        public bool isRead;
        public bool isPublic = true;
        public string memoPreview;
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
        public bool isEditMode;
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

        public BookshelfRuntimeSnapshotData CreateSnapshot(ExperienceFocusArea focusArea, bool isEditMode, BookEntity selectedBook)
        {
            EnsureSeedData();

            var snapshot = new BookshelfRuntimeSnapshotData();
            snapshot.experience.currentFocusArea = focusArea.ToString();
            snapshot.experience.isEditMode = isEditMode;
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
                    isRead = meta.isRead,
                    isPublic = meta.isPublic,
                    memoPreview = meta.memoPreview
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
                title = book.bookId.Replace('_', ' '),
                isRead = false,
                isPublic = true,
                memoPreview = "No review yet. Add a short note in the next step."
            };
        }
    }
}
