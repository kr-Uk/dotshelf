using System;

namespace App.Domain.Dashboard
{
    public sealed class Guestbook
    {
        public long Id { get; }
        public long SpaceId { get; }
        public long WriterUserId { get; }
        public string DisplayedNickname { get; }
        public bool Anonymous { get; }
        public long PostItInventoryId { get; }
        public string Content { get; }
        public VisibilityScope Visibility { get; }
        public GuestbookReply Reply { get; }
        public DateTimeOffset CreatedAt { get; }

        public Guestbook(
            long id,
            long spaceId,
            long writerUserId,
            string displayedNickname,
            bool anonymous,
            long postItInventoryId,
            string content,
            VisibilityScope visibility,
            DateTimeOffset createdAt,
            GuestbookReply reply = null)
        {
            Id = id;
            SpaceId = spaceId;
            WriterUserId = writerUserId;
            DisplayedNickname = displayedNickname ?? string.Empty;
            Anonymous = anonymous;
            PostItInventoryId = postItInventoryId;
            Content = content ?? string.Empty;
            Visibility = visibility;
            CreatedAt = createdAt;
            Reply = reply;
        }

        public bool HasReply => Reply != null;
    }
}
