using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace App.Auth
{
    internal static class KakaoDesktopOAuthClient
    {
        public static Task<KakaoOAuthResult> RequestAuthorizationCodeAsync(
            string clientId,
            string redirectUri,
            TimeSpan timeout,
            Action<string> openAuthorizeUrl)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Task.FromResult(KakaoOAuthResult.Failure("Kakao REST API key is missing."));
            }

            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirect) ||
                redirect.Scheme != Uri.UriSchemeHttp ||
                !redirect.IsLoopback ||
                redirect.Port <= 0)
            {
                return Task.FromResult(KakaoOAuthResult.Failure("Kakao redirect URI must be a loopback HTTP URI with a fixed port."));
            }

            var state = Guid.NewGuid().ToString("N");
            var authorizeUrl = BuildAuthorizeUrl(clientId.Trim(), redirectUri.Trim(), state);

            var timeoutCts = new CancellationTokenSource(timeout);
            var listenerReady = new ManualResetEventSlim(false);
            var waitTask = Task.Run(() => WaitForCallback(redirect.Port, redirect.AbsolutePath, state, timeoutCts.Token, listenerReady));
            if (!listenerReady.Wait(TimeSpan.FromSeconds(1)) && waitTask.IsCompleted)
            {
                return AwaitCallbackAsync(waitTask, timeoutCts, listenerReady);
            }

            openAuthorizeUrl?.Invoke(authorizeUrl);
            return AwaitCallbackAsync(waitTask, timeoutCts, listenerReady);
        }

        private static async Task<KakaoOAuthResult> AwaitCallbackAsync(
            Task<KakaoOAuthResult> waitTask,
            CancellationTokenSource timeoutCts,
            ManualResetEventSlim listenerReady)
        {
            try
            {
                return await waitTask;
            }
            finally
            {
                listenerReady.Dispose();
                timeoutCts.Dispose();
            }
        }

        private static KakaoOAuthResult WaitForCallback(
            int port,
            string expectedPath,
            string expectedState,
            CancellationToken cancellationToken,
            ManualResetEventSlim listenerReady)
        {
            TcpListener listener = null;
            try
            {
                listener = CreateLoopbackCompatibleListener(port);
                listener.Start();
                listenerReady.Set();

                using (cancellationToken.Register(() => listener.Stop()))
                using (var client = listener.AcceptTcpClient())
                using (var stream = client.GetStream())
                {
                    if (!IsLoopback(client.Client.RemoteEndPoint))
                    {
                        var failure = KakaoOAuthResult.Failure("Kakao callback must come from localhost.");
                        WriteHttpResponse(stream, failure);
                        return failure;
                    }

                    var requestText = ReadHttpRequest(stream);
                    var result = ParseCallbackRequest(requestText, expectedPath, expectedState);
                    WriteHttpResponse(stream, result);
                    return result;
                }
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return KakaoOAuthResult.Failure("Kakao login timed out.");
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return KakaoOAuthResult.Failure("Kakao login timed out.");
            }
            catch (Exception exception)
            {
                return KakaoOAuthResult.Failure(exception.Message);
            }
            finally
            {
                listener?.Stop();
            }
        }

        private static TcpListener CreateLoopbackCompatibleListener(int port)
        {
            var listener = new TcpListener(IPAddress.IPv6Any, port);
            try
            {
                listener.Server.DualMode = true;
                return listener;
            }
            catch (NotSupportedException)
            {
                listener.Stop();
                return new TcpListener(IPAddress.Loopback, port);
            }
        }

        private static bool IsLoopback(EndPoint endPoint)
        {
            if (!(endPoint is IPEndPoint ipEndPoint))
            {
                return false;
            }

            var address = ipEndPoint.Address;
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            return IPAddress.IsLoopback(address);
        }

        private static string BuildAuthorizeUrl(string clientId, string redirectUri, string state)
        {
            return "https://kauth.kakao.com/oauth/authorize" +
                   "?response_type=code" +
                   "&client_id=" + Uri.EscapeDataString(clientId) +
                   "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                   "&state=" + Uri.EscapeDataString(state);
        }

        private static string ReadHttpRequest(NetworkStream stream)
        {
            var buffer = new byte[8192];
            using (var output = new MemoryStream())
            {
                while (true)
                {
                    var read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    output.Write(buffer, 0, read);
                    var requestText = Encoding.UTF8.GetString(output.ToArray());
                    if (requestText.Contains("\r\n\r\n"))
                    {
                        return requestText;
                    }

                    if (output.Length >= 32768)
                    {
                        return requestText;
                    }
                }

                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        private static KakaoOAuthResult ParseCallbackRequest(string requestText, string expectedPath, string expectedState)
        {
            var requestLine = FirstRequestLine(requestText);
            var parts = requestLine.Split(' ');
            if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase))
            {
                return KakaoOAuthResult.Failure("Invalid Kakao callback request.");
            }

            if (!Uri.TryCreate("http://localhost" + parts[1], UriKind.Absolute, out var callbackUri))
            {
                return KakaoOAuthResult.Failure("Invalid Kakao callback URI.");
            }

            if (!SamePath(callbackUri.AbsolutePath, expectedPath))
            {
                return KakaoOAuthResult.Failure("Unexpected Kakao callback path.");
            }

            var query = ParseQuery(callbackUri.Query);
            if (query.TryGetValue("error", out var error))
            {
                query.TryGetValue("error_description", out var description);
                return KakaoOAuthResult.Failure(string.IsNullOrWhiteSpace(description) ? error : description);
            }

            if (!query.TryGetValue("state", out var state) || !string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                return KakaoOAuthResult.Failure("Kakao login state did not match.");
            }

            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                return KakaoOAuthResult.Failure("Kakao authorization code was missing.");
            }

            return KakaoOAuthResult.Success(code);
        }

        private static string FirstRequestLine(string requestText)
        {
            if (string.IsNullOrWhiteSpace(requestText))
            {
                return string.Empty;
            }

            var lineEnd = requestText.IndexOf("\r\n", StringComparison.Ordinal);
            return lineEnd < 0 ? requestText.Trim() : requestText.Substring(0, lineEnd).Trim();
        }

        private static bool SamePath(string actual, string expected)
        {
            return string.Equals(
                actual.TrimEnd('/'),
                expected.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(query))
            {
                return result;
            }

            foreach (var pair in query.TrimStart('?').Split('&'))
            {
                if (string.IsNullOrWhiteSpace(pair))
                {
                    continue;
                }

                var separatorIndex = pair.IndexOf('=');
                var rawKey = separatorIndex < 0 ? pair : pair.Substring(0, separatorIndex);
                var rawValue = separatorIndex < 0 ? string.Empty : pair.Substring(separatorIndex + 1);
                result[DecodeQueryValue(rawKey)] = DecodeQueryValue(rawValue);
            }

            return result;
        }

        private static string DecodeQueryValue(string value)
        {
            return Uri.UnescapeDataString((value ?? string.Empty).Replace("+", " "));
        }

        private static void WriteHttpResponse(NetworkStream stream, KakaoOAuthResult result)
        {
            var status = result.IsSuccess ? "200 OK" : "400 Bad Request";
            var title = result.IsSuccess ? "Kakao login received" : "Kakao login failed";
            var message = result.IsSuccess
                ? "You can close this window and return to DotShelf."
                : WebUtility.HtmlEncode(result.ErrorMessage);
            var html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>" +
                       title +
                       "</title></head><body><h1>" +
                       title +
                       "</h1><p>" +
                       message +
                       "</p></body></html>";
            var body = Encoding.UTF8.GetBytes(html);
            var header = "HTTP/1.1 " + status + "\r\n" +
                         "Content-Type: text/html; charset=utf-8\r\n" +
                         "Content-Length: " + body.Length + "\r\n" +
                         "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
        }
    }

    internal sealed class KakaoOAuthResult
    {
        private KakaoOAuthResult(bool isSuccess, string code, string errorMessage)
        {
            IsSuccess = isSuccess;
            Code = code;
            ErrorMessage = errorMessage;
        }

        public bool IsSuccess { get; }
        public string Code { get; }
        public string ErrorMessage { get; }

        public static KakaoOAuthResult Success(string code)
        {
            return new KakaoOAuthResult(true, code, null);
        }

        public static KakaoOAuthResult Failure(string errorMessage)
        {
            return new KakaoOAuthResult(false, null, errorMessage);
        }
    }
}
