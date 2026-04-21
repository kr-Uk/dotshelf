using System;

namespace App.Domain
{
    public enum ContentStatus
    {
        Active,
        Deleted
    }

    public sealed class ReadingRecord
    {
        public string Id { get; }
        public string UserBookId { get; }
        public string Title { get; set; }
        public string Content { get; set; }
        public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Public;
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public ContentStatus Status { get; set; } = ContentStatus.Active;
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }

        public ReadingRecord(string id, string userBookId, DateTimeOffset? createdAt = null, DateTimeOffset? updatedAt = null)
        {
            Id = id;
            UserBookId = userBookId;
            CreatedAt = createdAt ?? DateTimeOffset.MinValue;
            UpdatedAt = updatedAt ?? DateTimeOffset.MinValue;
        }
    }
}
