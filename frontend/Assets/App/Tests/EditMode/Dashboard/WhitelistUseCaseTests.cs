using App.Application.Dashboard;
using App.Tests.EditMode.Dashboard.Helpers;
using NUnit.Framework;

namespace App.Tests.EditMode.Dashboard
{
    public sealed class WhitelistUseCaseTests
    {
        [Test]
        public void LoadCurrent_DelegatesToRepository()
        {
            long[] expected = new long[] { 10, 20, 30 };
            FakeSpaceSettingsRepository repo = new FakeSpaceSettingsRepository
            {
                WhitelistToReturn = new GuestbookPostItWhitelist(false, expected)
            };
            UpdateGuestbookPostItWhitelistUseCase useCase = new UpdateGuestbookPostItWhitelistUseCase(repo);
            GuestbookPostItWhitelist captured = null;

            useCase.LoadCurrent(
                onSuccess: w => captured = w,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.IsFalse(captured.AllowAll);
            CollectionAssert.AreEqual(expected, captured.AllowedAssetIds);
            Assert.AreEqual(1, repo.LoadWhitelistCalls);
        }

        [Test]
        public void Execute_AllowAllTrue_PersistsToRepository()
        {
            FakeSpaceSettingsRepository repo = new FakeSpaceSettingsRepository();
            UpdateGuestbookPostItWhitelistUseCase useCase = new UpdateGuestbookPostItWhitelistUseCase(repo);

            useCase.Execute(
                new GuestbookPostItWhitelist(true, null),
                onSuccess: () => { },
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(repo.LastWhitelist);
            Assert.IsTrue(repo.LastWhitelist.AllowAll);
        }

        [Test]
        public void Execute_PartialList_PersistsAllowedIds()
        {
            FakeSpaceSettingsRepository repo = new FakeSpaceSettingsRepository();
            UpdateGuestbookPostItWhitelistUseCase useCase = new UpdateGuestbookPostItWhitelistUseCase(repo);
            long[] ids = new long[] { 1, 2, 3 };

            useCase.Execute(
                new GuestbookPostItWhitelist(false, ids),
                onSuccess: () => { },
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(repo.LastWhitelist);
            Assert.IsFalse(repo.LastWhitelist.AllowAll);
            CollectionAssert.AreEqual(ids, repo.LastWhitelist.AllowedAssetIds);
        }
    }
}
