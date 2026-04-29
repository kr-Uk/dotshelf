using App.Application.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class UpdateIntroductionUseCaseTests
    {
        [Test]
        public void Execute_TooLong_ReturnsError()
        {
            FakeSpaceSettingsRepository repo = new FakeSpaceSettingsRepository();
            UpdateIntroductionUseCase useCase = new UpdateIntroductionUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                new string('a', UpdateIntroductionUseCase.MaxLength + 1),
                onSuccess: () => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("INTRODUCTION_TOO_LONG", captured.Code);
            Assert.IsNull(repo.LastIntroduction);
        }

        [Test]
        public void Execute_NullIntroduction_TreatsAsEmptyAndSucceeds()
        {
            FakeSpaceSettingsRepository repo = new FakeSpaceSettingsRepository();
            UpdateIntroductionUseCase useCase = new UpdateIntroductionUseCase(repo);
            bool ok = false;

            useCase.Execute(
                null,
                onSuccess: () => ok = true,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsTrue(ok);
            Assert.AreEqual(string.Empty, repo.LastIntroduction);
        }

        [Test]
        public void Execute_WithinLimit_DelegatesToRepository()
        {
            FakeSpaceSettingsRepository repo = new FakeSpaceSettingsRepository();
            UpdateIntroductionUseCase useCase = new UpdateIntroductionUseCase(repo);
            bool ok = false;

            useCase.Execute(
                "책 좋아하는 사람",
                onSuccess: () => ok = true,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsTrue(ok);
            Assert.AreEqual("책 좋아하는 사람", repo.LastIntroduction);
        }
    }
}
