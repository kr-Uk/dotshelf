using System;
using App.Application.Notebook;
using NUnit.Framework;

namespace App.Tests.EditMode.Notebook
{
    public sealed class NotebookRuntimeUseCaseTests
    {
        [Test]
        public void LoadSpaces_WithKeyword_DelegatesSearchParameters()
        {
            var repo = new FakeNotebookRuntimeRepository();
            var useCase = new NotebookRuntimeUseCase(repo);
            NotebookSpaceListResult captured = null;

            useCase.LoadSpaces(
                keyword: "창가",
                cursor: "10",
                limit: 20,
                onSuccess: result => captured = result,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual("창가", repo.LastSpaceKeyword);
            Assert.AreEqual("10", repo.LastSpaceCursor);
            Assert.AreEqual(20, repo.LastSpaceLimit);
        }

        [Test]
        public void LoadUserFollowers_DelegatesCursorAndLimit()
        {
            var repo = new FakeNotebookRuntimeRepository();
            var useCase = new NotebookRuntimeUseCase(repo);
            NotebookUserListResult captured = null;

            useCase.LoadUserFollowers(
                userId: 77,
                cursor: "30",
                limit: 30,
                onSuccess: result => captured = result,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual(77, repo.LastFollowersUserId);
            Assert.AreEqual("30", repo.LastFollowersCursor);
            Assert.AreEqual(30, repo.LastFollowersLimit);
        }

        [Test]
        public void LoadUserFollowings_DelegatesCursorAndLimit()
        {
            var repo = new FakeNotebookRuntimeRepository();
            var useCase = new NotebookRuntimeUseCase(repo);
            NotebookUserListResult captured = null;

            useCase.LoadUserFollowings(
                userId: 88,
                cursor: "60",
                limit: 15,
                onSuccess: result => captured = result,
                onError: _ => Assert.Fail("should not error"));

            Assert.IsNotNull(captured);
            Assert.AreEqual(88, repo.LastFollowingsUserId);
            Assert.AreEqual("60", repo.LastFollowingsCursor);
            Assert.AreEqual(15, repo.LastFollowingsLimit);
        }

        private sealed class FakeNotebookRuntimeRepository : INotebookRuntimeRepository
        {
            public string LastSpaceKeyword;
            public string LastSpaceCursor;
            public int LastSpaceLimit;
            public long LastFollowersUserId;
            public string LastFollowersCursor;
            public int LastFollowersLimit;
            public long LastFollowingsUserId;
            public string LastFollowingsCursor;
            public int LastFollowingsLimit;

            public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookWalletResult());
            public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookWalletResult());
            public void LoadPurchaseHistory(Action<NotebookPurchaseHistoryResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookPurchaseHistoryResult(null));
            public void LoadPaymentMethods(Action<NotebookPaymentMethodResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookPaymentMethodResult(null));
            public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookAssetListResult(null));
            public void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookToggleResult { Active = wished });
            public void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookAssetPurchaseResult { AssetId = assetId });
            public void LoadSpaces(int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError) => LoadSpaces(null, null, limit, onSuccess, onError);

            public void LoadSpaces(string keyword, string cursor, int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
            {
                LastSpaceKeyword = keyword;
                LastSpaceCursor = cursor;
                LastSpaceLimit = limit;
                onSuccess?.Invoke(new NotebookSpaceListResult(null));
            }

            public void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookShelfTarget());
            public void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookToggleResult { Active = liked });
            public void FollowUser(long userId, bool following, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookToggleResult { Active = following });

            public void LoadUserFollowers(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
            {
                LastFollowersUserId = userId;
                LastFollowersCursor = cursor;
                LastFollowersLimit = limit;
                onSuccess?.Invoke(new NotebookUserListResult(null));
            }

            public void LoadUserFollowings(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
            {
                LastFollowingsUserId = userId;
                LastFollowingsCursor = cursor;
                LastFollowingsLimit = limit;
                onSuccess?.Invoke(new NotebookUserListResult(null));
            }

            public void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookReviewListResult(null));
            public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookReviewItem { Id = bookId, Rating = rating, Content = content });
            public void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError) => onSuccess?.Invoke(new NotebookToggleResult { Active = favorite });
        }
    }
}
