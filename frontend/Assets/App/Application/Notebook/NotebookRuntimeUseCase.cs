using System;

namespace App.Application.Notebook
{
    public sealed class NotebookRuntimeUseCase
    {
        private readonly INotebookRuntimeRepository repository;

        public NotebookRuntimeUseCase(INotebookRuntimeRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadWallet(onSuccess, onError);
        }

        public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.ChargePages(pagesAmount, onSuccess, onError);
        }

        public void LoadPurchaseHistory(Action<NotebookPurchaseHistoryResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadPurchaseHistory(onSuccess, onError);
        }

        public void LoadPaymentMethods(Action<NotebookPaymentMethodResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadPaymentMethods(onSuccess, onError);
        }

        public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadAssets(category, sort, limit, onSuccess, onError);
        }

        public void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.WishAsset(assetId, wished, onSuccess, onError);
        }

        public void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.PurchaseAsset(assetId, onSuccess, onError);
        }

        public void LoadSpaces(int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadSpaces(limit, onSuccess, onError);
        }

        public void LoadSpaces(string keyword, string cursor, int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadSpaces(keyword, cursor, limit, onSuccess, onError);
        }

        public void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadDefaultShelfTarget(onSuccess, onError);
        }

        public void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LikeSpace(spaceId, liked, onSuccess, onError);
        }

        public void FollowUser(long userId, bool following, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.FollowUser(userId, following, onSuccess, onError);
        }

        public void LoadUserFollowers(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadUserFollowers(userId, cursor, limit, onSuccess, onError);
        }

        public void LoadUserFollowings(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadUserFollowings(userId, cursor, limit, onSuccess, onError);
        }

        public void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.LoadBookReviews(bookId, onSuccess, onError);
        }

        public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError)
        {
            repository.CreateBookReview(bookId, rating, content, onSuccess, onError);
        }

        public void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            repository.SetUserBookFavorite(userBookId, favorite, onSuccess, onError);
        }
    }
}
