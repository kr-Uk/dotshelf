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

    public readonly struct GuestSessionResult
    {
        public bool IsSuccess { get; }
        public string GuestSessionId { get; }
        public string GuestToken { get; }
        public long ExpiresInSeconds { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        private GuestSessionResult(
            bool isSuccess,
            string guestSessionId,
            string guestToken,
            long expiresInSeconds,
            string errorCode,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            GuestSessionId = guestSessionId;
            GuestToken = guestToken;
            ExpiresInSeconds = expiresInSeconds;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public static GuestSessionResult Success(string guestSessionId, string guestToken, long expiresInSeconds)
        {
            return new GuestSessionResult(true, guestSessionId, guestToken, expiresInSeconds, null, null);
        }

        public static GuestSessionResult Failure(string errorCode, string errorMessage)
        {
            return new GuestSessionResult(false, null, null, 0L, errorCode, errorMessage);
        }
    }
}
