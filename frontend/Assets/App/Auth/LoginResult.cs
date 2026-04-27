namespace App.Auth
{
    public readonly struct LoginResult
    {
        public bool IsSuccess { get; }
        public string UserId { get; }
        public string ErrorMessage { get; }

        private LoginResult(bool isSuccess, string userId, string errorMessage)
        {
            IsSuccess = isSuccess;
            UserId = userId;
            ErrorMessage = errorMessage;
        }

        public static LoginResult Success(string userId)
        {
            return new LoginResult(true, userId, null);
        }

        public static LoginResult Failure(string errorMessage)
        {
            return new LoginResult(false, null, errorMessage);
        }
    }
}
