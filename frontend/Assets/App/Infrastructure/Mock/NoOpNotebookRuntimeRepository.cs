using System;
using App.Application.Notebook;

namespace App.Infrastructure.Mock
{
    public sealed class NoOpNotebookRuntimeRepository : INotebookRuntimeRepository
    {
        private const string DisabledCode = "NOTEBOOK_API_DISABLED";
        private const string DisabledMessage = "Notebook runtime API repository is disabled.";

        public void LoadWallet(Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void ChargePages(int pagesAmount, Action<NotebookWalletResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadAssets(string category, string sort, int limit, Action<NotebookAssetListResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void WishAsset(long assetId, bool wished, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void PurchaseAsset(long assetId, Action<NotebookAssetPurchaseResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadSpaces(int limit, Action<NotebookSpaceListResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadDefaultShelfTarget(Action<NotebookShelfTarget> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LikeSpace(long spaceId, bool liked, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void FollowUser(long userId, bool following, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void LoadBookReviews(long bookId, Action<NotebookReviewListResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void CreateBookReview(long bookId, float rating, string content, Action<NotebookReviewItem> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        public void SetUserBookFavorite(long userBookId, bool favorite, Action<NotebookToggleResult> onSuccess, Action<NotebookApiError> onError)
        {
            onError?.Invoke(CreateDisabledError());
        }

        private static NotebookApiError CreateDisabledError()
        {
            return new NotebookApiError
            {
                Code = DisabledCode,
                Message = DisabledMessage,
                Error = DisabledMessage
            };
        }
    }
}
