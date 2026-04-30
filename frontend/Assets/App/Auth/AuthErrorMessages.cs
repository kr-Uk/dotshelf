using App.Infrastructure.Api.Dtos;

namespace App.Auth
{
    // Maps BE error envelopes to user-facing Korean copy. The server already
    // ships Korean messages for known cases via ErrorCode, but we keep a
    // local override so transport-level failures (no network, timeout) and
    // unknown payloads also produce sensible copy.
    internal static class AuthErrorMessages
    {
        public static string ResolveLoginMessage(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "로그인에 실패했습니다. 잠시 후 다시 시도해주세요.";
            }

            // Prefer explicit BE-shipped Korean message when available.
            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }

            return MapByCode(error.Code, error.StatusCode, fallback:
                "로그인에 실패했습니다. 잠시 후 다시 시도해주세요.");
        }

        public static string ResolveGuestMessage(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "게스트 세션을 시작하지 못했습니다. 네트워크 상태를 확인해주세요.";
            }

            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }

            return MapByCode(error.Code, error.StatusCode, fallback:
                "게스트 세션을 시작하지 못했습니다. 네트워크 상태를 확인해주세요.");
        }

        private static string MapByCode(string code, long statusCode, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                switch (code)
                {
                    case "A001":
                        return "인증 정보가 올바르지 않습니다. 다시 로그인해주세요.";
                    case "A003":
                        return "토큰이 만료되었습니다. 다시 로그인해주세요.";
                    case "C001":
                        return "요청 값이 올바르지 않습니다.";
                    case "U001":
                        return "사용자 정보를 찾을 수 없습니다.";
                    case "C003":
                        return "서버 오류가 발생했습니다. 잠시 후 다시 시도해주세요.";
                }
            }

            if (statusCode == 0)
            {
                return "서버에 연결할 수 없습니다. 네트워크 상태를 확인해주세요.";
            }

            if (statusCode >= 500)
            {
                return "서버 오류가 발생했습니다. 잠시 후 다시 시도해주세요.";
            }

            return fallback;
        }
    }
}
