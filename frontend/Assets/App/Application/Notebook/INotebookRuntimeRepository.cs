using System;

namespace App.Application.Notebook
{
    public interface INotebookRuntimeRepository
    {
        void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError);

        void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError);

        void LoadPurchaseHistory(Action<NotebookPurchaseHistoryResult> onSuccess, Action<NotebookApiError> onError);

        void LoadPaymentMethods(Action<NotebookPaymentMethodResult> onSuccess, Action<NotebookApiError> onError);

        void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError);

        void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError);

        void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError);

        void LoadSpaces(int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError);

        void LoadSpaces(string keyword, string cursor, int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError);

        void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError);

        void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError);

        void FollowUser(long userId, bool following, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError);

        void LoadUserFollowers(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError);

        void LoadUserFollowings(long userId, string cursor, int limit, Action<NotebookUserListResult> onSuccess, Action<NotebookApiError> onError);

        void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError);

        void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError);

        void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError);
    }
}
