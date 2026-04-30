using App.Application.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class ToggleSpaceLikeUseCaseTests
    {
        [Test]
        public void Execute_LikeOn_PassesParametersToRepository()
        {
            FakeDashboardRepository repo = new FakeDashboardRepository
            {
                LikeResultToReturn = new LikeToggleResult { Liked = true, LikeCount = 42 }
            };
            ToggleSpaceLikeUseCase useCase = new ToggleSpaceLikeUseCase(repo);
            LikeToggleResult captured = null;

            useCase.Execute(
                spaceId: 11,
                liked: true,
                onSuccess: r => captured = r,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.IsTrue(captured.Liked);
            Assert.AreEqual(42, captured.LikeCount);
            Assert.AreEqual(11, repo.LastToggleSpaceId);
            Assert.IsTrue(repo.LastToggleLiked);
        }

        [Test]
        public void Execute_LikeOff_PassesFalse()
        {
            FakeDashboardRepository repo = new FakeDashboardRepository();
            ToggleSpaceLikeUseCase useCase = new ToggleSpaceLikeUseCase(repo);

            useCase.Execute(
                spaceId: 5,
                liked: false,
                onSuccess: _ => { },
                onError: _ => Assert.Fail("should not error"));

            Assert.AreEqual(5, repo.LastToggleSpaceId);
            Assert.IsFalse(repo.LastToggleLiked);
        }
    }
}
