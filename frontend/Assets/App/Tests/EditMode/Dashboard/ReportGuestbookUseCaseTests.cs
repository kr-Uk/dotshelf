using App.Application.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class ReportGuestbookUseCaseTests
    {
        [Test]
        public void Execute_MissingReason_ReturnsError()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            ReportGuestbookUseCase useCase = new ReportGuestbookUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                new ReportGuestbookCommand { Target = GuestbookReportTarget.Guestbook, TargetId = 1, ReasonType = "" },
                onSuccess: () => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("REPORT_REASON_REQUIRED", captured.Code);
            Assert.IsNull(repo.LastReportCommand);
        }

        [Test]
        public void Execute_ValidReason_Delegates()
        {
            FakeGuestbookRepository repo = new FakeGuestbookRepository();
            ReportGuestbookUseCase useCase = new ReportGuestbookUseCase(repo);
            bool ok = false;

            useCase.Execute(
                new ReportGuestbookCommand
                {
                    Target = GuestbookReportTarget.Reply,
                    TargetId = 5,
                    ReasonType = "SPAM",
                    ReasonDetail = "스팸"
                },
                onSuccess: () => ok = true,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsTrue(ok);
            Assert.IsNotNull(repo.LastReportCommand);
            Assert.AreEqual(GuestbookReportTarget.Reply, repo.LastReportCommand.Target);
        }
    }
}
