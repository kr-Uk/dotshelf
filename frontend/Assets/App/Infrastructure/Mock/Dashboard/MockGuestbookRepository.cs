using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;

namespace App.Infrastructure.Mock.Dashboard
{
    public sealed class MockGuestbookRepository : IGuestbookRepository
    {
        private const int PageSize = 15;

        private readonly List<Guestbook> guestbooks = new List<Guestbook>();
        private long nextId = 1000;
        private long nextReplyId = 5000;

        public MockGuestbookRepository()
        {
            SeedSample();
        }

        public void LoadPage(long spaceId, string cursor, int limit, Action<GuestbookListPage> onSuccess, Action<DashboardApiError> onError)
        {
            int startIndex = ParseCursor(cursor);
            int take = Math.Min(limit > 0 ? limit : PageSize, guestbooks.Count - startIndex);
            if (take <= 0)
            {
                onSuccess(new GuestbookListPage(Array.Empty<Guestbook>(), null));
                return;
            }

            List<Guestbook> page = guestbooks.GetRange(startIndex, take);
            string nextCursor = startIndex + take < guestbooks.Count ? (startIndex + take).ToString() : null;
            onSuccess(new GuestbookListPage(page, nextCursor));
        }

        public void Write(WriteGuestbookCommand command, Action<Guestbook> onSuccess, Action<DashboardApiError> onError)
        {
            long id = nextId++;
            Guestbook gb = new Guestbook(
                id: id,
                spaceId: command.SpaceId,
                writerUserId: 99,
                displayedNickname: command.Anonymous ? "익명" : "방문자닉",
                anonymous: command.Anonymous,
                postItInventoryId: command.PostItInventoryId,
                content: command.Content,
                visibility: VisibilityScope.Public,
                createdAt: DateTimeOffset.UtcNow);
            guestbooks.Insert(0, gb);
            onSuccess(gb);
        }

        public void WriteReply(WriteGuestbookReplyCommand command, Action<GuestbookReply> onSuccess, Action<DashboardApiError> onError)
        {
            int index = guestbooks.FindIndex(g => g.Id == command.GuestbookId);
            if (index < 0)
            {
                onError(new DashboardApiError { Code = "GUESTBOOK_NOT_FOUND", Message = "방명록을 찾을 수 없습니다." });
                return;
            }

            if (guestbooks[index].HasReply)
            {
                onError(new DashboardApiError { Code = "GUESTBOOK_REPLY_ALREADY_EXISTS", Message = "이미 답장한 방명록입니다." });
                return;
            }

            GuestbookReply reply = new GuestbookReply(
                id: nextReplyId++,
                guestbookId: command.GuestbookId,
                writerUserId: 1,
                writerNickname: "내 닉네임",
                postItInventoryId: command.PostItInventoryId,
                content: command.Content,
                createdAt: DateTimeOffset.UtcNow);

            Guestbook original = guestbooks[index];
            guestbooks[index] = new Guestbook(
                id: original.Id,
                spaceId: original.SpaceId,
                writerUserId: original.WriterUserId,
                displayedNickname: original.DisplayedNickname,
                anonymous: original.Anonymous,
                postItInventoryId: original.PostItInventoryId,
                content: original.Content,
                visibility: original.Visibility,
                createdAt: original.CreatedAt,
                reply: reply);

            onSuccess(reply);
        }

        public void Delete(long guestbookId, Action onSuccess, Action<DashboardApiError> onError)
        {
            int removed = guestbooks.RemoveAll(g => g.Id == guestbookId);
            if (removed == 0)
            {
                onError(new DashboardApiError { Code = "GUESTBOOK_NOT_FOUND", Message = "방명록을 찾을 수 없습니다." });
                return;
            }
            onSuccess();
        }

        public void DeleteReply(long guestbookId, long replyId, Action onSuccess, Action<DashboardApiError> onError)
        {
            int index = guestbooks.FindIndex(g => g.Id == guestbookId);
            if (index < 0 || !guestbooks[index].HasReply || guestbooks[index].Reply.Id != replyId)
            {
                onError(new DashboardApiError { Code = "GUESTBOOK_REPLY_NOT_FOUND", Message = "답장을 찾을 수 없습니다." });
                return;
            }

            Guestbook original = guestbooks[index];
            guestbooks[index] = new Guestbook(
                id: original.Id,
                spaceId: original.SpaceId,
                writerUserId: original.WriterUserId,
                displayedNickname: original.DisplayedNickname,
                anonymous: original.Anonymous,
                postItInventoryId: original.PostItInventoryId,
                content: original.Content,
                visibility: original.Visibility,
                createdAt: original.CreatedAt,
                reply: null);

            onSuccess();
        }

        public void Report(ReportGuestbookCommand command, Action onSuccess, Action<DashboardApiError> onError)
        {
            onSuccess();
        }

        private void SeedSample()
        {
            string[] sampleContents =
            {
                "책 추천 감사합니다 :)",
                "오늘 비 와요 우산 챙기세요",
                "이 책 정말 좋았어요!",
                "다음 추천도 부탁드려요",
                "프로필 사진 너무 귀여워요",
                "팔로우했어요!",
                "독후감 잘 읽었습니다",
                "또 놀러올게요",
                "안녕하세요 :)",
                "공감되는 글이 많네요"
            };

            DateTimeOffset baseTime = DateTimeOffset.UtcNow;
            for (int i = 0; i < 12; i++)
            {
                string content = sampleContents[i % sampleContents.Length];
                bool anonymous = i % 3 == 0;
                Guestbook gb = new Guestbook(
                    id: nextId++,
                    spaceId: 1,
                    writerUserId: 100 + i,
                    displayedNickname: anonymous ? "익명" : $"방문자{i + 1}",
                    anonymous: anonymous,
                    postItInventoryId: 1 + (i % 5),
                    content: content,
                    visibility: VisibilityScope.Public,
                    createdAt: baseTime.AddHours(-i * 3),
                    reply: i % 4 == 0 ? CreateSampleReply(nextId - 1, baseTime.AddHours(-i * 3 + 1)) : null);
                guestbooks.Add(gb);
            }
        }

        private GuestbookReply CreateSampleReply(long guestbookId, DateTimeOffset createdAt)
        {
            return new GuestbookReply(
                id: nextReplyId++,
                guestbookId: guestbookId,
                writerUserId: 1,
                writerNickname: "내 닉네임",
                postItInventoryId: 2,
                content: "감사합니다!",
                createdAt: createdAt);
        }

        private static int ParseCursor(string cursor)
        {
            if (string.IsNullOrEmpty(cursor))
            {
                return 0;
            }

            return int.TryParse(cursor, out int parsed) ? parsed : 0;
        }
    }
}
