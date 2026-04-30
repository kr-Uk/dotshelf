using App.Infrastructure.Api.Dtos;

namespace App.Auth
{
    internal static class AuthErrorMessages
    {
        public static string ResolveLoginMessage(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "\uB85C\uADF8\uC778\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.";
            }

            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }

            return MapByCode(error.Code, error.StatusCode, "\uB85C\uADF8\uC778\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.");
        }

        public static string ResolveGuestMessage(ApiErrorResponseDto error)
        {
            if (error == null)
            {
                return "\uAC8C\uC2A4\uD2B8 \uC138\uC158\uC744 \uC2DC\uC791\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB124\uD2B8\uC6CC\uD06C \uC0C1\uD0DC\uB97C \uD655\uC778\uD574\uC8FC\uC138\uC694.";
            }

            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }

            return MapByCode(error.Code, error.StatusCode, "\uAC8C\uC2A4\uD2B8 \uC138\uC158\uC744 \uC2DC\uC791\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB124\uD2B8\uC6CC\uD06C \uC0C1\uD0DC\uB97C \uD655\uC778\uD574\uC8FC\uC138\uC694.");
        }

        private static string MapByCode(string code, long statusCode, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                switch (code)
                {
                    case "A001":
                        return "\uC778\uC99D \uC815\uBCF4\uAC00 \uC62C\uBC14\uB974\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uB85C\uADF8\uC778\uD574\uC8FC\uC138\uC694.";
                    case "A002":
                        return "\uC774 \uC694\uCCAD\uC740 \uAD8C\uD55C\uC774 \uC5C6\uC5B4 \uCC98\uB9AC\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
                    case "A003":
                        return "\uC18C\uC15C \uB85C\uADF8\uC778 \uD1A0\uD070\uC774 \uC720\uD6A8\uD558\uC9C0 \uC54A\uAC70\uB098 \uB9CC\uB8CC\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.";
                    case "C001":
                        return "\uC694\uCCAD \uAC12\uC774 \uC62C\uBC14\uB974\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
                    case "C003":
                        return "\uC11C\uBC84 \uC624\uB958\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.";
                    case "U001":
                        return "\uC0AC\uC6A9\uC790 \uC815\uBCF4\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
                    case "U002":
                        return "\uC774\uBBF8 \uAC00\uC785\uB41C \uACC4\uC815\uC785\uB2C8\uB2E4. \uB2E4\uB978 \uB85C\uADF8\uC778 \uBC29\uC2DD\uC744 \uD655\uC778\uD574\uC8FC\uC138\uC694.";
                }
            }

            if (statusCode == 0)
            {
                return "\uC11C\uBC84\uC5D0 \uC5F0\uACB0\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB124\uD2B8\uC6CC\uD06C \uC0C1\uD0DC\uB97C \uD655\uC778\uD574\uC8FC\uC138\uC694.";
            }

            if (statusCode == 400)
            {
                return "\uB85C\uADF8\uC778 \uC694\uCCAD \uAC12\uC744 \uD655\uC778\uD574\uC8FC\uC138\uC694.";
            }

            if (statusCode == 401)
            {
                return "\uC18C\uC15C \uB85C\uADF8\uC778 \uD1A0\uD070\uC774 \uC720\uD6A8\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.";
            }

            if (statusCode == 403)
            {
                return "\uC774 \uC694\uCCAD\uC740 \uD5C8\uC6A9\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            }

            if (statusCode == 404)
            {
                return "\uC694\uCCAD\uD55C \uC778\uC99D \uC815\uBCF4\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            if (statusCode == 409)
            {
                return "\uC774\uBBF8 \uC5F0\uB3D9\uB41C \uACC4\uC815\uC774\uAC70\uB098 \uCC98\uB9AC\uD560 \uC218 \uC5C6\uB294 \uC694\uCCAD\uC785\uB2C8\uB2E4.";
            }

            if (statusCode >= 500)
            {
                return "\uC11C\uBC84 \uC624\uB958\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uC7A0\uC2DC \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD574\uC8FC\uC138\uC694.";
            }

            return fallback;
        }
    }
}
