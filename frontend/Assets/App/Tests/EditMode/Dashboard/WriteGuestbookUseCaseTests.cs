using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class WriteGuestbookUseCaseTests
    {
        [Test]
        public void Execute_BlankContent_ReturnsBlankError()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookUseCase useCase = new WriteGuestbookUseCase(repo);
            DashboardApiError captured = null;
            Guestbook saved = null;

            useCase.Execute(
                new WriteGuestbookCommand { SpaceId = 1, PostItInventoryId = 5, Content = "  ", Anonymous = false },
                onSuccess: g => saved = g,
                onError: e => captured = e);

            Assert.IsNull(saved);
            Assert.IsNotNull(captured);
            Assert.AreEqual("GUESTBOOK_CONTENT_BLANK", captured.Code);
            Assert.IsNull(repo.LastWriteCommand);
        }

        [Test]
        public void Execute_TooLongContent_ReturnsTooLongError()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookUseCase useCase = new WriteGuestbookUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                new WriteGuestbookCommand
                {
                    SpaceId = 1,
                    PostItInventoryId = 5,
                    Content = new string('a', WriteGuestbookUseCase.MaxContentLength + 1),
                    Anonymous = false
                },
                onSuccess: _ => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("GUESTBOOK_CONTENT_TOO_LONG", captured.Code);
        }

        [Test]
        public void Execute_MissingPostIt_ReturnsRequiredError()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookUseCase useCase = new WriteGuestbookUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                new WriteGuestbookCommand { SpaceId = 1, PostItInventoryId = 0, Content = "hi", Anonymous = false },
                onSuccess: _ => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("GUESTBOOK_POSTIT_REQUIRED", captured.Code);
        }

        [Test]
        public void Execute_ValidCommand_DelegatesToRepositoryAndReturnsGuestbook()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookUseCase useCase = new WriteGuestbookUseCase(repo);
            Guestbook captured = null;

            useCase.Execute(
                new WriteGuestbookCommand { SpaceId = 7, PostItInventoryId = 3, Content = "안녕", Anonymous = true },
                onSuccess: g => captured = g,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual(7, captured.SpaceId);
            Assert.AreEqual("안녕", captured.Content);
            Assert.IsTrue(captured.Anonymous);
            Assert.IsNotNull(repo.LastWriteCommand);
        }

        [Test]
        public void Execute_ExactlyMaxLength_Succeeds()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            WriteGuestbookUseCase useCase = new WriteGuestbookUseCase(repo);
            Guestbook captured = null;

            useCase.Execute(
                new WriteGuestbookCommand
                {
                    SpaceId = 1,
                    PostItInventoryId = 1,
                    Content = new string('a', WriteGuestbookUseCase.MaxContentLength),
                    Anonymous = false
                },
                onSuccess: g => captured = g,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
        }
    }
}
