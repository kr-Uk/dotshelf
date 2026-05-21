using System;

namespace App.Domain.Dashboard
{
    public sealed class GuestbookReply
    {
        public long Id { get; }
        public long GuestbookId { get; }
        public long WriterUserId { get; }
        public string WriterNickname { get; }
        public long PostItInventoryId { get; }
        public string Content { get; }
        public DateTimeOffset CreatedAt { get; }

        public GuestbookReply(
            long id,
            long guestbookId,
            long writerUserId,
            string writerNickname,
            long postItInventoryId,
            string content,
            DateTimeOffset createdAt)
        {
            Id = id;
            GuestbookId = guestbookId;
            WriterUserId = writerUserId;
            WriterNickname = writerNickname ?? string.Empty;
            PostItInventoryId = postItInventoryId;
            Content = content ?? string.Empty;
            CreatedAt = createdAt;
        }
    }
}
