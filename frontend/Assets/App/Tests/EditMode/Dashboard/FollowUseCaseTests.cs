using App.Application.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class FollowUseCaseTests
    {
        [Test]
        public void Follow_DelegatesToRepository()
        {
            FakeFollowRepository repo = new FakeFollowRepository();
            FollowUserUseCase useCase = new FollowUserUseCase(repo);
            FollowToggleResult captured = null;

            useCase.Execute(
                targetUserId: 12,
                onSuccess: r => captured = r,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.IsTrue(captured.Following);
            Assert.AreEqual(12, repo.LastFollowTarget);
        }

        [Test]
        public void Unfollow_DelegatesToRepository()
        {
            FakeFollowRepository repo = new FakeFollowRepository();
            UnfollowUserUseCase useCase = new UnfollowUserUseCase(repo);
            FollowToggleResult captured = null;

            useCase.Execute(
                targetUserId: 33,
                onSuccess: r => captured = r,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.IsFalse(captured.Following);
            Assert.AreEqual(33, repo.LastUnfollowTarget);
        }

        [Test]
        public void Block_DelegatesToRepository()
        {
            FakeFollowRepository repo = new FakeFollowRepository();
            BlockUserUseCase useCase = new BlockUserUseCase(repo);
            BlockToggleResult captured = null;

            useCase.Execute(
                targetUserId: 99,
                onSuccess: r => captured = r,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.IsTrue(captured.Blocked);
            Assert.AreEqual(99, repo.LastBlockTarget);
        }

        [Test]
        public void LoadFollowersPage_PassesLimitFromConstant()
        {
            FakeFollowRepository repo = new FakeFollowRepository();
            LoadFollowersPageUseCase useCase = new LoadFollowersPageUseCase(repo);

            useCase.Execute(
                userId: 1,
                cursor: null,
                onSuccess: _ => { },
                onError: _ => Assert.Fail("should not error"));

            Assert.AreEqual(1, repo.LoadFollowersCalls);
        }
    }
}
