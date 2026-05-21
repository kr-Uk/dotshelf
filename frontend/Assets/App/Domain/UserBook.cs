using System;

namespace App.Domain
{
    public enum ReadingStatus
    {
        Unread,
        Reading,
        Read
    }

    public enum StorageStatus
    {
        OnShelf,
        Archived,
        Deleted
    }

    public enum VisibilityScope
    {
        Public,
        Followers,
        Private
    }

    public sealed class UserBook
    {
        public string Id { get; }
        public string UserBookId => Id;
        public string UserId { get; }
        public string BookId { get; }
        public string SelectedCoverProductId { get; set; }
        public ReadingStatus ReadingStatus { get; set; } = ReadingStatus.Unread;
        public StorageStatus StorageStatus { get; set; } = StorageStatus.OnShelf;
        public decimal? OwnerRating { get; set; }
        public bool IsLiked { get; set; }
        public DateTimeOffset AddedAt { get; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }

        public UserBook(string id, string userId, string bookId, DateTimeOffset? addedAt = null, DateTimeOffset? updatedAt = null)
        {
            Id = id;
            UserId = userId;
            BookId = bookId;
            AddedAt = addedAt ?? DateTimeOffset.MinValue;
            UpdatedAt = updatedAt ?? DateTimeOffset.MinValue;
        }
    }
}

