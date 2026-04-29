using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class WriteGuestbookReplyUseCaseTests
    {
        [Test]
        public void Execute_BlankContent_ReturnsBlankError()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookReplyUseCase useCase = new WriteGuestbookReplyUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                new WriteGuestbookReplyCommand { GuestbookId = 1, PostItInventoryId = 5, Content = "" },
                onSuccess: _ => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("GUESTBOOK_REPLY_CONTENT_BLANK", captured.Code);
        }

        [Test]
        public void Execute_TooLongContent_ReturnsTooLongError()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookReplyUseCase useCase = new WriteGuestbookReplyUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                new WriteGuestbookReplyCommand
                {
                    GuestbookId = 1,
                    PostItInventoryId = 5,
                    Content = new string('a', WriteGuestbookReplyUseCase.MaxContentLength + 1)
                },
                onSuccess: _ => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("GUESTBOOK_REPLY_CONTENT_TOO_LONG", captured.Code);
        }

        [Test]
        public void Execute_MissingPostIt_ReturnsRequiredError()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookReplyUseCase useCase = new WriteGuestbookReplyUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                new WriteGuestbookReplyCommand { GuestbookId = 1, PostItInventoryId = 0, Content = "ok" },
                onSuccess: _ => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("GUESTBOOK_REPLY_POSTIT_REQUIRED", captured.Code);
        }

        [Test]
        public void Execute_ValidCommand_DelegatesAndReturnsReply()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookReplyUseCase useCase = new WriteGuestbookReplyUseCase(repo);
            GuestbookReply captured = null;

            useCase.Execute(
                new WriteGuestbookReplyCommand { GuestbookId = 42, PostItInventoryId = 3, Content = "고마워요" },
                onSuccess: r => captured = r,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual(42, captured.GuestbookId);
            Assert.AreEqual("고마워요", captured.Content);
            Assert.IsNotNull(repo.LastReplyCommand);
        }
    }
}
