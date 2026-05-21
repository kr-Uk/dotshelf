namespace App.Auth
{
    public readonly struct LoginResult
    {
        public bool IsSuccess { get; }
        public long UserId { get; }
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public long ExpiresIn { get; }
        public bool NewUser { get; }
        public bool Onboarded { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        private LoginResult(
            bool isSuccess,
            long userId,
            string accessToken,
            string refreshToken,
            long expiresIn,
            bool newUser,
            bool onboarded,
            string errorCode,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            UserId = userId;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresIn = expiresIn;
            NewUser = newUser;
            Onboarded = onboarded;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public static LoginResult Success(
            long userId,
            string accessToken,
            string refreshToken,
            long expiresIn,
            bool newUser,
            bool onboarded)
        {
            return new LoginResult(true, userId, accessToken, refreshToken, expiresIn, newUser, onboarded, null, null);
        }

        public static LoginResult Failure(string errorCode, string errorMessage)
        {
            return new LoginResult(false, 0L, null, null, 0L, false, false, errorCode, errorMessage);
        }
    }
}
