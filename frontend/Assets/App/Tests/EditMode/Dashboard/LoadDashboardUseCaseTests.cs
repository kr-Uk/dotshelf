using App.Application.Dashboard;
using App.Domain.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class LoadDashboardUseCaseTests
    {
        [Test]
        public void ExecuteForMine_DelegatesToLoadMyDashboard()
        {
            FakeDashboardRepository repo = new FakeDashboardRepository();
            LoadDashboardUseCase useCase = new LoadDashboardUseCase(repo);
            DashboardSnapshot captured = null;

            useCase.ExecuteForMine(
                onSuccess: s => captured = s,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual(1, repo.LoadMyDashboardCalls);
            Assert.AreEqual(0, repo.LoadDashboardCalls);
        }

        [Test]
        public void Execute_DelegatesToLoadDashboardWithSpaceId()
        {
            FakeDashboardRepository repo = new FakeDashboardRepository();
            LoadDashboardUseCase useCase = new LoadDashboardUseCase(repo);
            DashboardSnapshot captured = null;

            useCase.Execute(
                spaceId: 7,
                onSuccess: s => captured = s,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual(1, repo.LoadDashboardCalls);
            Assert.AreEqual(0, repo.LoadMyDashboardCalls);
        }

        [Test]
        public void Execute_RepositoryError_PropagatesError()
        {
            FakeDashboardRepository repo = new FakeDashboardRepository
            {
                ErrorToReturn = new DashboardApiError { Code = "NOT_FOUND", Message = "공간 없음" }
            };
            LoadDashboardUseCase useCase = new LoadDashboardUseCase(repo);
            DashboardApiError captured = null;

            useCase.Execute(
                spaceId: 99,
                onSuccess: _ => Assert.Fail("should not succeed"),
                onError: e => captured = e);

            Assert.IsNotNull(captured);
            Assert.AreEqual("NOT_FOUND", captured.Code);
        }
    }
}
