// Browser-based OAuth authorization-code flow for desktop standalone targets
// (Windows / macOS / Linux) and the Unity editor. Mobile platforms (iOS,
// Android) use platform SDKs instead and never run this flow at runtime.
//
// The flow follows RFC 8252 (OAuth 2.0 for Native Apps): a transient HttpListener
// bound to 127.0.0.1 on a free port receives the OAuth provider redirect, the
// authorization code is parsed out of the query string, and the listener is
// stopped immediately. BE then swaps the code for provider tokens server-side.
//
// The data classes (OAuthBrowserProviderConfig / OAuthBrowserResult) compile
// on every platform so MonoBehaviour fields can reference them unconditionally.
// Only the static flow runner is gated to desktop targets where HttpListener
// is available and does not get stripped by IL2CPP.
using System;
using UnityEngine;

namespace App.Auth
{
    // Per-provider authorize endpoint + client_id + scopes. Authored on the
    // LoginScreenController in the inspector; client_id is technically public
    // (the server keeps client_secret), so it is safe to ship in builds.
    [Serializable]
    public sealed class OAuthBrowserProviderConfig
    {
        public SocialProvider provider;

        // e.g. "https://accounts.google.com/o/oauth2/v2/auth",
        //      "https://kauth.kakao.com/oauth/authorize",
        //      "https://appleid.apple.com/auth/authorize".
        public string authorizeEndpoint;

        public string clientId;

        // Space-separated, e.g. "openid profile email". Default empty — provider scope는
        // OAuthBrowserDefaults.CreateDefault(provider)에서만 채우도록 단일 책임.
        // 클래스 필드 default가 non-empty면 신규 인스펙터 override(provider만 다른) 추가 시
        // Unity가 필드 default를 직렬화하고 MergeWithDefaults가 그걸 user override로 간주 →
        // 의도하지 않은 scope param이 authorize URL에 붙음 (예: Kakao provider override에
        // 'openid profile email' 새어들어가 KOE205 재발). MR #96 review skh1205 [P3] 반영.
        public string scope = string.Empty;

        public string responseType = "code";

        // OIDC providers (Google, Apple) require a nonce so the resulting
        // id_token can be verified server-side. Plain OAuth (Kakao) ignores it.
        public bool useNonce = true;

        // Provider-specific extras appended to the authorize URL, e.g.
        //   Google: "access_type=offline&prompt=consent"
        //   Apple : "response_mode=form_post"
        // Apple's form_post does not work with a loopback HttpListener and
        // needs a different transport; the LoginScreenController surfaces
        // the limitation rather than starting an unsatisfiable flow.
        public string extraQuery;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(authorizeEndpoint) && !string.IsNullOrWhiteSpace(clientId);
    }

    // Built-in authorize endpoints + recommended scopes for the providers we
    // ship login buttons for. The inspector array on LoginScreenController can
    // override any field (notably to inject a per-environment client_id);
    // anything left blank falls back to these defaults so the inspector only
    // needs the client_id to get a working flow.
    public static class OAuthBrowserDefaults
    {
        public static OAuthBrowserProviderConfig CreateDefault(SocialProvider provider)
        {
            switch (provider)
            {
                case SocialProvider.Google:
                    // Google Desktop Client ID hardcoded — public by design (server keeps client_secret).
                    // 인스펙터 비워두면 이 값 그대로 동작. 환경별로 다른 clientId 쓰려면 인스펙터 override.
                    // Source: SOCIAL_LOGIN_SETUP.md "Google Desktop Client ID".
                    return new OAuthBrowserProviderConfig
                    {
                        provider = SocialProvider.Google,
                        authorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                        clientId = "502970247403-j8f8ctqsdk9h1kog2ahiu2n9g0usb9ha.apps.googleusercontent.com",
                        scope = "openid email",
                        responseType = "code",
                        useNonce = true,
                        extraQuery = "access_type=offline&prompt=consent"
                    };
                case SocialProvider.Kakao:
                    // clientId is the Kakao REST API key — public by design (the
                    // server holds the secret). Hardcoded here so a fresh
                    // LoginScreenController without an inspector entry still has
                    // a working Kakao login. Inspector value still wins via
                    // MergeWithDefaults if a per-environment key needs override.
                    //
                    // scope: account_email만. 닉네임은 앱 자체(Onboarding 입력)로 관리하므로
                    // profile_nickname 동의 불필요. 이메일 거부 시 BE에서 null 처리 (User.email nullable).
                    return new OAuthBrowserProviderConfig
                    {
                        provider = SocialProvider.Kakao,
                        authorizeEndpoint = "https://kauth.kakao.com/oauth/authorize",
                        clientId = "d8c6ed5428758bd9a50e1f41a70d3d69",
                        scope = "account_email",
                        responseType = "code",
                        useNonce = false,
                        extraQuery = null
                    };
                case SocialProvider.Apple:
                    // Apple normally requires response_mode=form_post which is
                    // incompatible with the loopback HttpListener (POST body, not
                    // a query string). The flow will start so the developer can
                    // see the failure, but full Apple sign-in on desktop needs a
                    // different transport (e.g. a hosted redirect page on BE).
                    return new OAuthBrowserProviderConfig
                    {
                        provider = SocialProvider.Apple,
                        authorizeEndpoint = "https://appleid.apple.com/auth/authorize",
                        scope = "name email",
                        responseType = "code",
                        useNonce = true,
                        extraQuery = null
                    };
                default:
                    return new OAuthBrowserProviderConfig { provider = provider };
            }
        }

        // Merge the inspector-supplied config (may be null or partial) with the
        // built-in default for the provider. Inspector wins where set; missing
        // fields fall back to defaults. `useNonce` is a bool so it cannot
        // distinguish "unset" from "explicitly false" — the inspector value is
        // taken as authoritative there.
        public static OAuthBrowserProviderConfig MergeWithDefaults(
            SocialProvider provider,
            OAuthBrowserProviderConfig userConfig)
        {
            var defaults = CreateDefault(provider);
            if (userConfig == null)
            {
                return defaults;
            }

            return new OAuthBrowserProviderConfig
            {
                provider = provider,
                authorizeEndpoint = !string.IsNullOrWhiteSpace(userConfig.authorizeEndpoint)
                    ? userConfig.authorizeEndpoint
                    : defaults.authorizeEndpoint,
                clientId = !string.IsNullOrWhiteSpace(userConfig.clientId)
                    ? userConfig.clientId
                    : defaults.clientId,
                scope = !string.IsNullOrWhiteSpace(userConfig.scope)
                    ? userConfig.scope
                    : defaults.scope,
                responseType = !string.IsNullOrWhiteSpace(userConfig.responseType)
                    ? userConfig.responseType
                    : defaults.responseType,
                useNonce = userConfig.useNonce,
                extraQuery = !string.IsNullOrWhiteSpace(userConfig.extraQuery)
                    ? userConfig.extraQuery
                    : defaults.extraQuery
            };
        }
    }

    public readonly struct OAuthBrowserResult
    {
        public bool IsSuccess { get; }
        public string Code { get; }
        public string RedirectUri { get; }
        public string State { get; }
        public string Nonce { get; }
        // PKCE code_verifier — authorize 시점에 생성되어 code_challenge=SHA256(code_verifier)로 OAuth 제공자에 전달됨.
        // BE는 token 교환 호출에 이 verifier를 그대로 forward해서 제공자가 challenge 검증함 (RFC 7636).
        // null이면 PKCE 미적용 (legacy fallback) — BE는 codeVerifier 없으면 client_secret 사용.
        public string CodeVerifier { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        private OAuthBrowserResult(
            bool isSuccess,
            string code,
            string redirectUri,
            string state,
            string nonce,
            string codeVerifier,
            string errorCode,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            Code = code;
            RedirectUri = redirectUri;
            State = state;
            Nonce = nonce;
            CodeVerifier = codeVerifier;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public static OAuthBrowserResult Success(string code, string redirectUri, string state, string nonce, string codeVerifier)
        {
            return new OAuthBrowserResult(true, code, redirectUri, state, nonce, codeVerifier, null, null);
        }

        public static OAuthBrowserResult Failure(string errorCode, string errorMessage)
        {
            return new OAuthBrowserResult(false, null, null, null, null, null, errorCode, errorMessage);
        }
    }
}

#if UNITY_STANDALONE || UNITY_EDITOR
namespace App.Auth
{
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public static class WindowsOAuthBrowserFlow
    {
        private const string LoopbackHost = "127.0.0.1";
        private const string CallbackPath = "/dotshelf/oauth/callback/";
        // Kakao OAuth는 redirect URI를 콘솔 등록값과 정확 일치시켜야 함 (와일드카드 X). 임의 ephemeral port를
        // 쓰면 등록 불가 → 고정 port range 5개를 사전 등록하고 차례로 시도. 18080~18084 선택 이유:
        // - 18080/18081은 흔히 Tomcat/IDE에 점유. 5개 range로 회복 가능성 확보.
        // - 1024 미만 reserved port 회피, 일반 사용자 PC에서 다른 앱과 충돌 가능성 낮은 대역.
        // Google은 RFC 8252대로 loopback 모든 port 자동 허용이라 이 range 그대로 동작.
        // 새 port 추가/제거 시 카카오 콘솔 redirect URI 등록도 동일하게 갱신해야 함 (SOCIAL_LOGIN_SETUP.md 참고).
        private static readonly int[] PreferredCallbackPorts = { 18080, 18081, 18082, 18083, 18084 };
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        public static bool IsSupported => true;

        public static async Task<OAuthBrowserResult> ExecuteAsync(
            OAuthBrowserProviderConfig providerConfig,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            // Editor/Build 차이 진단 가능하도록 stage 별 로그. 토큰/code/state 값 자체는 절대 안 남김
            // (presence/length/validity 만). AuthApiClient.Redact 와 동일 정책.
            var providerName = providerConfig != null ? providerConfig.provider.ToString() : "(null)";
            bool clientIdPresent = providerConfig != null && !string.IsNullOrWhiteSpace(providerConfig.clientId);
            bool authorizeEndpointPresent = providerConfig != null && !string.IsNullOrWhiteSpace(providerConfig.authorizeEndpoint);
            App.Core.AppLog.Auth(
                $"[OAuth] ExecuteAsync provider={providerName} " +
                $"clientIdPresent={clientIdPresent} authorizeEndpointPresent={authorizeEndpointPresent}");

            if (providerConfig == null || !providerConfig.IsConfigured)
            {
                var missingPiece = providerConfig == null || string.IsNullOrWhiteSpace(providerConfig.clientId)
                    ? "OAuth 클라이언트 ID"
                    : "Authorize 엔드포인트";
                Debug.LogWarning(
                    $"[OAuth] Config missing — provider={providerName} clientIdPresent={clientIdPresent} " +
                    $"authorizeEndpointPresent={authorizeEndpointPresent}");
                return OAuthBrowserResult.Failure(
                    "OAUTH_CONFIG_MISSING",
                    $"{missingPiece}가 설정되어 있지 않습니다. LoginScreenController 의 'Desktop Browser OAuth Providers' 에 값을 입력해주세요.");
            }

            HttpListener listener;
            string redirectUri;
            try
            {
                (listener, redirectUri) = StartListenerOnPreferredPort();
                App.Core.AppLog.Auth($"[OAuth] Listener started. prefix={redirectUri}");
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == ErrorAccessDenied)
            {
                // Windows URL ACL 거부 — 일반 사용자에게 \"관리자 권한\" 안내가 정확.
                Debug.LogWarning($"[Auth] HttpListener ACL denied: {ex.GetType().Name}: {ex.Message}");
                return OAuthBrowserResult.Failure(
                    "LISTENER_ACL_DENIED",
                    "운영체제 권한 문제로 로그인을 시작할 수 없습니다. 관리자 권한으로 실행하거나 관리자에게 문의해주세요.");
            }
            catch (Exception ex) when (ex is HttpListenerException || ex is SocketException)
            {
                // 일반 사용자에게 18080~18084 같은 port 숫자는 actionable 안 되므로 단순 안내.
                // 진단 정보는 LogWarning에만 남김.
                Debug.LogWarning($"[Auth] HttpListener start failed (all preferred ports unavailable): {ex.GetType().Name}: {ex.Message}");
                return OAuthBrowserResult.Failure(
                    "LISTENER_START_FAILED",
                    "로그인 콜백을 받을 수 없습니다. 앱을 재시작하거나 잠시 후 다시 시도해주세요.");
            }

            try
            {
                var state = GenerateRandomBase64Url(32);
                var nonce = providerConfig.useNonce ? GenerateRandomBase64Url(32) : null;
                // PKCE (RFC 7636): authorize 시점에 random code_verifier 생성, SHA256 hash → code_challenge.
                // 같은 code_verifier를 BE에 전달하면 BE가 token 교환 호출에 그대로 forward해서 OAuth 제공자가
                // 검증함. client_secret 없이도 code interception 공격 방어 가능 (native app OAuth 표준).
                var codeVerifier = GenerateRandomBase64Url(64);  // RFC 7636: 43~128 chars
                var codeChallenge = ComputeCodeChallengeS256(codeVerifier);
                var authorizeUrl = BuildAuthorizeUrl(providerConfig, redirectUri, state, nonce, codeChallenge);

                App.Core.AppLog.Auth($"[OAuth] Opening browser for {providerConfig.provider} (redirect={redirectUri})");
                // Fully-qualified — `App.Application` (UseCase layer) shadows
                // UnityEngine.Application here because both namespaces are in
                // scope, so `Application.OpenURL(...)` resolves to the wrong type.
                // try/catch — Build 환경에서 platform default browser 미설치/launcher 실패 시 침묵 회피.
                try
                {
                    UnityEngine.Application.OpenURL(authorizeUrl);
                    App.Core.AppLog.Auth("[OAuth] Browser opened.");
                }
                catch (Exception openEx)
                {
                    Debug.LogWarning($"[OAuth] Browser open failed: {openEx.GetType().Name}: {openEx.Message}");
                    return OAuthBrowserResult.Failure(
                        "BROWSER_OPEN_FAILED",
                        "기본 브라우저를 실행할 수 없습니다. 브라우저 설치 상태를 확인하고 다시 시도해주세요.");
                }

                var contextTask = listener.GetContextAsync();
                var effectiveTimeout = timeout ?? DefaultTimeout;
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    var delayTask = Task.Delay(effectiveTimeout, timeoutCts.Token);
                    var completed = await Task.WhenAny(contextTask, delayTask).ConfigureAwait(false);

                    if (completed != contextTask)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return OAuthBrowserResult.Failure("CANCELLED", "로그인이 취소되었습니다.");
                        }

                        return OAuthBrowserResult.Failure(
                            "TIMEOUT",
                            "브라우저 로그인 응답을 시간 안에 받지 못했습니다. 다시 시도해주세요.");
                    }

                    timeoutCts.Cancel();
                }

                var context = await contextTask.ConfigureAwait(false);
                var query = ParseQueryString(context.Request.Url?.Query);

                var hadError = query.TryGetValue("error", out var errorCode);
                bool codePresent = query.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code);
                bool receivedStatePresent = query.TryGetValue("state", out var receivedState) && !string.IsNullOrWhiteSpace(receivedState);
                bool stateValid = receivedStatePresent && FixedTimeEquals(receivedState, state);
                App.Core.AppLog.Auth(
                    $"[OAuth] Callback received. codePresent={codePresent} stateValid={stateValid} errorPresent={hadError}");

                WriteHtmlResponse(context.Response, hadError);

                if (hadError)
                {
                    query.TryGetValue("error_description", out var errorDescription);
                    return OAuthBrowserResult.Failure(
                        errorCode,
                        string.IsNullOrWhiteSpace(errorDescription)
                            ? $"OAuth 제공자가 오류를 반환했습니다: {errorCode}"
                            : errorDescription);
                }

                if (!codePresent)
                {
                    return OAuthBrowserResult.Failure(
                        "NO_CODE",
                        "Authorization code 가 응답에 포함되지 않았습니다.");
                }

                if (!stateValid)
                {
                    return OAuthBrowserResult.Failure(
                        "STATE_MISMATCH",
                        "State 검증에 실패했습니다. 다시 시도해주세요.");
                }

                return OAuthBrowserResult.Success(code, redirectUri, state, nonce, codeVerifier);
            }
            finally
            {
                try { listener.Stop(); } catch { /* listener may already be closed */ }
                try { ((IDisposable)listener).Dispose(); } catch { }
            }
        }

        // RFC 7636 §4.2: code_challenge = BASE64URL(SHA256(code_verifier)). S256 method.
        private static string ComputeCodeChallengeS256(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                return Convert.ToBase64String(hash)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
        }

        // PreferredCallbackPorts를 차례로 시도해서 비어있는 첫 port에 HttpListener를 시작.
        // 예외 분류:
        // - SocketException, HttpListenerException(ErrorCode != 5) — port 점유/race로 추정,
        //   다음 port로 fall-through. 5개 모두 실패 시 마지막 예외 throw.
        // - HttpListenerException(ErrorCode == 5, ERROR_ACCESS_DENIED) — Windows URL ACL
        //   문제. 다음 port도 같은 ACL 정책에 막힐 가능성 매우 높음 → 즉시 break (사용자에게
        //   관리자 권한 안내). 5번 시도 시간 + 방화벽 팝업 5번 회피.
        // - 그 외 예외는 throw — OOM/StackOverflow 같은 critical 예외를 swallow하면 안 됨.
        private const int ErrorAccessDenied = 5;
        private static (HttpListener listener, string redirectUri) StartListenerOnPreferredPort()
        {
            Exception lastException = null;
            foreach (var port in PreferredCallbackPorts)
            {
                var redirectUri = $"http://{LoopbackHost}:{port}{CallbackPath}";
                HttpListener listener = null;
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add(redirectUri);
                    listener.Start();
                    return (listener, redirectUri);
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == ErrorAccessDenied)
                {
                    // ACL 권한 부족 — 다른 port도 같은 결과 → 즉시 escalate.
                    try { ((IDisposable)listener)?.Dispose(); } catch { }
                    throw;
                }
                catch (HttpListenerException ex)
                {
                    lastException = ex;
                    try { ((IDisposable)listener)?.Dispose(); } catch { }
                }
                catch (SocketException ex)
                {
                    lastException = ex;
                    try { ((IDisposable)listener)?.Dispose(); } catch { }
                }
            }
            throw lastException ?? new InvalidOperationException("PreferredCallbackPorts 비어있음");
        }

        private static string BuildAuthorizeUrl(
            OAuthBrowserProviderConfig providerConfig,
            string redirectUri,
            string state,
            string nonce,
            string codeChallenge)
        {
            var builder = new StringBuilder();
            builder.Append(providerConfig.authorizeEndpoint);
            builder.Append(providerConfig.authorizeEndpoint.Contains("?") ? "&" : "?");
            AppendQueryParam(builder, "response_type", string.IsNullOrWhiteSpace(providerConfig.responseType) ? "code" : providerConfig.responseType);
            AppendQueryParam(builder, "client_id", providerConfig.clientId);
            AppendQueryParam(builder, "redirect_uri", redirectUri);
            if (!string.IsNullOrWhiteSpace(providerConfig.scope))
            {
                AppendQueryParam(builder, "scope", providerConfig.scope);
            }
            AppendQueryParam(builder, "state", state);
            if (!string.IsNullOrEmpty(nonce))
            {
                AppendQueryParam(builder, "nonce", nonce);
            }
            // PKCE — Google/Kakao 모두 S256 method 지원. 무시되는 환경에서도 무해 (extra query param).
            if (!string.IsNullOrEmpty(codeChallenge))
            {
                AppendQueryParam(builder, "code_challenge", codeChallenge);
                AppendQueryParam(builder, "code_challenge_method", "S256");
            }

            if (!string.IsNullOrWhiteSpace(providerConfig.extraQuery))
            {
                builder.Append('&');
                builder.Append(providerConfig.extraQuery.TrimStart('&', '?'));
            }

            return builder.ToString();
        }

        private static void AppendQueryParam(StringBuilder builder, string key, string value)
        {
            var lastChar = builder.Length > 0 ? builder[builder.Length - 1] : '\0';
            if (lastChar != '?' && lastChar != '&')
            {
                builder.Append('&');
            }
            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value ?? string.Empty));
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
            {
                return result;
            }

            var trimmed = query.StartsWith("?") ? query.Substring(1) : query;
            foreach (var pair in trimmed.Split('&'))
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex < 0)
                {
                    result[Uri.UnescapeDataString(pair)] = string.Empty;
                    continue;
                }

                var key = Uri.UnescapeDataString(pair.Substring(0, separatorIndex));
                var value = Uri.UnescapeDataString(pair.Substring(separatorIndex + 1));
                result[key] = value;
            }
            return result;
        }

        private static void WriteHtmlResponse(HttpListenerResponse response, bool isError)
        {
            try
            {
                var headerColor = isError ? "#C14E4E" : "#2B5C8A";
                var heading = isError ? "로그인에 실패했습니다" : "로그인 완료";
                var body = isError
                    ? "앱으로 돌아가 다시 시도해주세요."
                    : "이 창을 닫고 앱으로 돌아가주세요.";
                var html =
                    "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\">" +
                    "<title>Dotshelf 로그인</title>" +
                    "<style>body{font-family:'Apple SD Gothic Neo',sans-serif;background:#F4EFE6;color:#2D2418;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}" +
                    ".card{background:#FFFDF9;padding:40px 56px;border-radius:14px;box-shadow:0 8px 32px rgba(0,0,0,0.08);text-align:center}" +
                    "h1{margin:0 0 12px 0;font-size:22px;color:" + headerColor + "}p{margin:0;color:#7C6A56}</style>" +
                    "</head><body><div class=\"card\"><h1>" + heading + "</h1><p>" + body + "</p></div></body></html>";

                var buffer = Encoding.UTF8.GetBytes(html);
                response.StatusCode = isError ? 400 : 200;
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.LongLength;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Flush();
            }
            catch
            {
                // Browser already closed; nothing to recover.
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private static string GenerateRandomBase64Url(int byteLength)
        {
            var bytes = new byte[byteLength];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            // RFC 7636 / RFC 4648 base64url, no padding.
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
#endif
