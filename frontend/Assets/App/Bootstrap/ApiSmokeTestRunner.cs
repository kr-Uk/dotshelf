// MR #98 review #2: production 빌드 포함 + smoke test 사용자 데이터 변경 위험 차단.
// 클래스 전체를 UNITY_EDITOR 가드 — 릴리즈 빌드에서는 컴파일 자체에서 제외되어 reflection
// 으로도 도달 불가. dev 환경에서만 진단 도구로 사용.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using App.Domain;
using App.Domain.Auth;
using App.Infrastructure.Api;
using App.Infrastructure.Api.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace App.Bootstrap
{
    /// <summary>
    /// 로그인 후 저장된 accessToken 으로 미확인 엔드포인트가 정상 응답/파싱되는지 확인하는 진단 러너.
    /// 빈 GameObject 에 부착 후 인스펙터의 ContextMenu 로 실행.
    ///
    /// 실제 서비스 흐름과 동일한 APIManager 경로(Authorization, baseUrl, envelope 파싱) 를 통과시킨다.
    ///
    /// **UNITY_EDITOR 전용** — 릴리즈/플레이어 빌드에는 포함되지 않는다.
    /// **DELETE /v1/users/me 는 절대 포함하지 않는다** (회원 탈퇴, irreversible).
    ///
    /// ContextMenu 분리:
    ///  - "Run Smoke Test"           : 일반 9개 테스트 (토큰 영향 없음)
    ///  - "Run Token Refresh"        : POST /v1/auth/token/refresh — 토큰 갱신 (현재 토큰 교체됨)
    ///  - "Run Logout"               : POST /v1/auth/logout       — 토큰 무효화 (재로그인 필요)
    /// </summary>
    public sealed class ApiSmokeTestRunner : MonoBehaviour
    {
        [Header("실행 옵션")]
        [SerializeField] private bool runOnStart = false;

        // 기본값 false: 로그인 후 저장된 accessToken 을 그대로 써서 인증된 호출이 통과하는지 검증.
        [SerializeField] private bool clearTokenBeforeRun = false;

        [SerializeField] private int bodyLogMaxChars = 1500;
        [SerializeField] private float perRequestTimeoutSec = 12f;

        [Header("테스트 파라미터 — 비워두면 해당 테스트 skip")]
        [Tooltip("타인 또는 본인 spaceId. GET /v1/spaces/{spaceId}, visits, like/unlike 에 사용.")]
        [SerializeField] private long testSpaceId;

        [Tooltip("타인 userId. POST/DELETE /v1/users/{userId}/follow 에 사용.")]
        [SerializeField] private long testUserId;

        [Tooltip("내 inventoryId. POST /v1/spaces/me/theme/{inventoryId}/apply 에 사용.")]
        [SerializeField] private long testInventoryId;

        // BE 가 받는 mode 값. enum 으로 두면 Inspector dropdown 으로 typo 방지 + 새 값 추가 시 단일 지점 갱신.
        public enum ThemeApplyMode
        {
            RESET,
            KEEP_BLOCKERS_ONLY,
        }

        [Tooltip("theme apply mode. 일반적으로 RESET. KEEP_BLOCKERS_ONLY 는 BE 가 501.")]
        [SerializeField] private ThemeApplyMode testThemeMode = ThemeApplyMode.RESET;

        private int total;
        private int ok2xx;
        private int authBlocked;
        private int otherFail;
        private int skipped;

        // PrintIdHints 가 채움. follow 8/9 에서 testUserId == myUserId 일 때 self-follow 차단 (BE 가 거절하는 시나리오 미리 컷).
        // 0 이면 미확보 — 가드 미적용 (PrintIdHints 가 응답 실패한 상태이므로 테스트는 그냥 진행).
        private long _myUserIdCache;

        private void Start()
        {
            if (runOnStart)
            {
                StartCoroutine(RunAll());
            }
        }

        [ContextMenu("Run Smoke Test")]
        public void RunFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunAll());
        }

        [ContextMenu("Run Token Refresh (분리)")]
        public void RunTokenRefreshFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunTokenRefresh());
        }

        [ContextMenu("Run Logout (분리)")]
        public void RunLogoutFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunLogout());
        }

        [ContextMenu("Run Theme Apply Test")]
        public void RunThemeApplyTestFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunThemeApplyTest());
        }

        [ContextMenu("Run Layout Save Test")]
        public void RunLayoutSaveTestFromInspector()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[ApiSmokeTest] Editor Play 모드에서만 실행됩니다.");
                return;
            }
            StartCoroutine(RunLayoutSaveTest());
        }

        // ─────────────────────────────────────────────────────────────────────
        // 일반 테스트 (토큰 비파괴)
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator RunAll()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            total = ok2xx = authBlocked = otherFail = skipped = 0;

            if (clearTokenBeforeRun)
            {
                api.SetAccessToken(null);
                Debug.Log("[ApiSmokeTest] clearTokenBeforeRun=true → in-memory accessToken cleared.");
            }

            LogTokenStatus(api);

            // ID hint: testInventoryId/testSpaceId/testUserId 가 미입력일 때 사용자가 Inspector 에
            // 채울 후보 값을 Console 로 안내. read-only 호출(GET /v1/users/me + GET /v1/spaces/me)
            // 만 발사하므로 부작용 없음. 결과를 testXxxId 에 자동 대입하지는 않음 — 사용자가 의도한
            // 대상(본인 vs 타인) 명시적으로 선택해야 함.
            yield return PrintIdHints();

            // 1. GET /v1/assets/me/inventory
            yield return ProbeGet<InventoryResponseDto>(
                "1 GET /v1/assets/me/inventory",
                "/v1/assets/me/inventory");

            // 2. POST /v1/spaces/me/theme/{inventoryId}/apply (BE 의 실제 POST 경로 — 사용자 요청 표기는
            //    "POST /v1/spaces/me/theme" 였지만 BE 는 inventoryId path 를 받음. testInventoryId 필요)
            if (testInventoryId <= 0)
            {
                LogSkip("2 POST /v1/spaces/me/theme/{inventoryId}/apply", "testInventoryId 미입력");
            }
            else
            {
                string themePath =
                    $"/v1/spaces/me/theme/{testInventoryId}/apply" +
                    $"?mode={Uri.EscapeDataString(testThemeMode.ToString())}";
                yield return ProbePost<SpaceMutationResponseDto>(
                    $"2 POST {themePath}",
                    themePath);
            }

            // 3. PATCH /v1/users/me/notification-settings
            yield return ProbePatch<NotificationSettingRequestDto, NotificationSettingResponseDto>(
                "3 PATCH /v1/users/me/notification-settings",
                "/v1/users/me/notification-settings",
                new NotificationSettingRequestDto { NotifyNewGuestbook = true });

            // 4. GET /v1/spaces/{spaceId}
            if (testSpaceId <= 0)
            {
                LogSkip("4 GET /v1/spaces/{spaceId}", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbeGet<SpaceResponseDto>(
                    $"4 GET /v1/spaces/{testSpaceId}",
                    $"/v1/spaces/{testSpaceId}");
            }

            // 5. POST /v1/spaces/{spaceId}/visits
            if (testSpaceId <= 0)
            {
                LogSkip("5 POST /v1/spaces/{spaceId}/visits", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbePost<ToggleResponseDto>(
                    $"5 POST /v1/spaces/{testSpaceId}/visits",
                    $"/v1/spaces/{testSpaceId}/visits");
            }

            // 6. POST /v1/spaces/{spaceId}/like
            if (testSpaceId <= 0)
            {
                LogSkip("6 POST /v1/spaces/{spaceId}/like", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbePost<ToggleResponseDto>(
                    $"6 POST /v1/spaces/{testSpaceId}/like",
                    $"/v1/spaces/{testSpaceId}/like");
            }

            // 7. DELETE /v1/spaces/{spaceId}/like
            if (testSpaceId <= 0)
            {
                LogSkip("7 DELETE /v1/spaces/{spaceId}/like", "testSpaceId 미입력");
            }
            else
            {
                yield return ProbeDelete<ToggleResponseDto>(
                    $"7 DELETE /v1/spaces/{testSpaceId}/like",
                    $"/v1/spaces/{testSpaceId}/like");
            }

            // 8/9. follow — self-follow 가드: testUserId == myUserId 면 BE 가 self-follow 차단 (의미 없는 4xx).
            // PrintIdHints 가 myUserId 확보 못 했으면 (_myUserIdCache == 0) 가드 미적용 — 사용자 입력 신뢰.
            bool selfFollowBlocked = _myUserIdCache > 0 && testUserId == _myUserIdCache;

            // 8. POST /v1/users/{userId}/follow
            if (testUserId <= 0)
            {
                LogSkip("8 POST /v1/users/{userId}/follow", "testUserId 미입력");
            }
            else if (selfFollowBlocked)
            {
                LogSkip("8 POST /v1/users/{userId}/follow", $"testUserId({testUserId}) == myUserId — BE self-follow 차단으로 의미 없음");
            }
            else
            {
                yield return ProbePost<ToggleResponseDto>(
                    $"8 POST /v1/users/{testUserId}/follow",
                    $"/v1/users/{testUserId}/follow");
            }

            // 9. DELETE /v1/users/{userId}/follow
            if (testUserId <= 0)
            {
                LogSkip("9 DELETE /v1/users/{userId}/follow", "testUserId 미입력");
            }
            else if (selfFollowBlocked)
            {
                LogSkip("9 DELETE /v1/users/{userId}/follow", $"testUserId({testUserId}) == myUserId — BE self-follow 차단으로 의미 없음");
            }
            else
            {
                yield return ProbeDelete<ToggleResponseDto>(
                    $"9 DELETE /v1/users/{testUserId}/follow",
                    $"/v1/users/{testUserId}/follow");
            }

            PrintSummary();
        }

        // RunAll 진입 시 Inspector 에 채울 ID 후보를 Console 로 노출.
        // 사용자 요구 형식: 각 응답의 raw body 한 줄 + 파싱된 핵심 필드들을 평탄하게 한 줄씩.
        // 자동 대입 없음 — 본 값 보고 사용자가 Inspector 직접 입력해야 함.
        private IEnumerator PrintIdHints()
        {
            APIManager api = APIManager.Instance;
            if (api == null)
            {
                Debug.LogWarning("[ApiSmokeTest][ID-HINT] APIManager.Instance null — hint 불가.");
                yield break;
            }

            Debug.Log("[ApiSmokeTest][ID-HINT] ===== START =====");

            JObject userMe = null;
            yield return FetchRaw("/v1/users/me", x => userMe = x);

            JObject spaceMe = null;
            yield return FetchRaw("/v1/spaces/me", x => spaceMe = x);

            // 각 응답 raw body 출력 (envelope 통째). Console 의 message 길이 제한 회피용으로
            // 별도 Debug.Log 로 분리.
            Debug.Log(
                "[ApiSmokeTest][ID-HINT] /v1/users/me raw body:\n" +
                (userMe != null
                    ? ApiManagerProvider.TrimForLog(userMe.ToString(Formatting.None), bodyLogMaxChars)
                    : "(null — 호출 실패 또는 빈 응답)"));

            Debug.Log(
                "[ApiSmokeTest][ID-HINT] /v1/spaces/me raw body:\n" +
                (spaceMe != null
                    ? ApiManagerProvider.TrimForLog(spaceMe.ToString(Formatting.None), bodyLogMaxChars)
                    : "(null — 호출 실패 또는 빈 응답)"));

            // 핵심 필드 직접 추출 + 사용자 요구 포맷 그대로 출력.
            JToken userResult = userMe != null ? userMe["result"] : null;
            JToken spaceResult = spaceMe != null ? spaceMe["result"] : null;

            string myUserId = ReadScalar(userResult, "id");
            string mySpaceId = ReadScalar(spaceResult, "id");
            string ownerUserId = ReadScalar(spaceResult, "ownerUserId");

            // self-follow 가드용 캐시. parse 실패/sentinel 이면 0 유지 → guard 미적용.
            _myUserIdCache = long.TryParse(myUserId, out long parsedMyUserId) ? parsedMyUserId : 0;

            // 카테고리별 inventoryId 한 번 추출. 첫 non-null 을 "추천" 으로 따로 표시해 사용자가
            // Inspector testInventoryId 에 그대로 복사 가능하게 한다.
            (string category, string value)[] invCandidates = new[]
            {
                ("theme", ReadScalar(spaceResult, "currentThemeInventoryId")),
                ("wallpaper", ReadScalar(spaceResult, "currentWallpaperInventoryId")),
                ("floor", ReadScalar(spaceResult, "currentFloorInventoryId")),
                ("desk", ReadScalar(spaceResult, "currentDeskInventoryId")),
                ("dashboard", ReadScalar(spaceResult, "currentDashboardInventoryId")),
                ("interiorLight", ReadScalar(spaceResult, "currentInteriorLightInventoryId")),
                ("exteriorLight", ReadScalar(spaceResult, "currentExteriorLightInventoryId")),
            };

            string recommendedId = null;
            string recommendedCategory = null;
            foreach (var (category, value) in invCandidates)
            {
                if (IsUsableInventoryId(value))
                {
                    recommendedId = value;
                    recommendedCategory = category;
                    break;
                }
            }

            StringBuilder lines = new StringBuilder();
            lines.AppendLine("[ApiSmokeTest][ID-HINT]");
            lines.AppendLine($"myUserId={myUserId}");
            lines.AppendLine($"mySpaceId={mySpaceId}");
            lines.AppendLine($"spaceOwnerUserId={ownerUserId}");
            lines.AppendLine("inventory candidates:");
            foreach (var (category, value) in invCandidates)
            {
                lines.AppendLine($"  {category}={value}");
            }
            lines.AppendLine();
            if (recommendedId != null)
            {
                lines.AppendLine("──────────────────────────────────────────────");
                lines.AppendLine($"✅ 추천 testInventoryId = {recommendedId}  (category={recommendedCategory})");
                lines.AppendLine("   → Inspector 의 testInventoryId 필드에 이 값을 입력 후 'Run Smoke Test' 재실행하면");
                lines.AppendLine($"     POST /v1/spaces/me/theme/{recommendedId}/apply?mode={testThemeMode} 가 발사됨.");
                lines.AppendLine("──────────────────────────────────────────────");
            }
            else
            {
                lines.AppendLine("──────────────────────────────────────────────");
                lines.AppendLine("⚠ 사용 가능한 inventoryId 후보 없음 (모두 null/missing).");
                lines.AppendLine("  → 본인 계정으로 wallpaper/floor 등 한 번 적용 후 재시도 또는 다른 계정 inventoryId 사용.");
                lines.AppendLine("──────────────────────────────────────────────");
            }
            Debug.Log(lines.ToString());

            // raw body 에서 핵심 키가 안 보이면 envelope/필드명 mismatch 진단 hint.
            if (mySpaceId == "(missing)" && spaceResult is JObject so)
            {
                StringBuilder keys = new StringBuilder();
                keys.Append("[ApiSmokeTest][ID-HINT] /v1/spaces/me result.id 가 'missing' — result 필드명 후보: ");
                foreach (var kv in so)
                {
                    keys.Append(kv.Key).Append(", ");
                }
                Debug.LogWarning(keys.ToString());
            }

            Debug.Log(
                "[ApiSmokeTest][ID-HINT] 가이드:\n" +
                "  • testUserId 는 *다른 사용자* — myUserId 그대로 쓰면 BE 가 self-follow 차단.\n" +
                "    GET /v1/spaces (popular) 의 ownerUserId, 또는 GET /v1/users/me/followers 에서 확보.\n" +
                "  • testSpaceId 는 본인/타인 모두 가능. self-like 정책은 BE 응답으로 확인.\n" +
                "  • testInventoryId 는 위 inventory candidates 중 null 아닌 값.\n" +
                "[ApiSmokeTest][ID-HINT] ===== END =====");
        }

        // /path 를 GET 호출하여 응답 envelope 을 JObject 로 받아 sink 에 전달. APIManager.Get<JObject>
        // 가 deserialize 해주므로 BE field 명/타입과 무관하게 트리 그대로 보존됨.
        private IEnumerator FetchRaw(string path, Action<JObject> sink)
        {
            APIManager api = APIManager.Instance;
            if (api == null)
            {
                sink(null);
                yield break;
            }

            bool done = false;
            JObject got = null;
            api.Get<JObject>(
                path,
                obj => { done = true; got = obj; },
                err =>
                {
                    done = true;
                    Debug.LogWarning(
                        $"[ApiSmokeTest][ID-HINT] {path} onError: status={(err != null ? err.StatusCode.ToString() : "?")} " +
                        $"code={err?.Code} message={err?.Message} body={ApiManagerProvider.TrimForLog(err?.Body, bodyLogMaxChars)}");
                });

            yield return WaitForDone(() => done, "ID-HINT " + path);
            sink(got);
        }

        // ReadScalar 결과가 inventoryId 로 사용 가능한 양의 정수인지 판정.
        // null / missing / no result / 0 등은 모두 false.
        private static bool IsUsableInventoryId(string scalar)
        {
            if (string.IsNullOrEmpty(scalar)) return false;
            if (scalar.StartsWith("(") || scalar == "null") return false;
            return long.TryParse(scalar, out long v) && v > 0;
        }

        // result JObject 에서 단일 스칼라 값을 안전하게 꺼냄. 키 부재 또는 result 자체 null 이면 명시적
        // sentinel 반환해서 사용자가 어디서 끊겼는지 즉시 인지 가능.
        private static string ReadScalar(JToken result, string key)
        {
            if (result == null) return "(no result)";
            if (result.Type == JTokenType.Null) return "(result is null)";
            JToken v = result[key];
            if (v == null) return "(missing)";
            if (v.Type == JTokenType.Null) return "null";
            return v.ToString(Formatting.None);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 토큰 영향 작업 — 별도 ContextMenu 로 격리
        // ─────────────────────────────────────────────────────────────────────

        // POST /v1/auth/token/refresh — refreshToken 으로 새 accessToken 발급. 응답 success 시
        // APIManager 가 자체적으로 토큰 교체하지는 않음(이 러너에선 단지 응답 검증만). 실서비스
        // 흐름은 SessionRefresh 가 담당.
        private IEnumerator RunTokenRefresh()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest][refresh] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            string refreshToken = PlayerPrefs.GetString(AuthStorageKeys.RefreshToken, string.Empty);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Debug.LogWarning("[ApiSmokeTest][refresh] PlayerPrefs 에 refreshToken 없음 — 로그인 먼저.");
                yield break;
            }

            Debug.Log(
                $"[ApiSmokeTest][refresh] 시작 — masked refreshToken={MaskToken(refreshToken)}. " +
                "이 호출은 PlayerPrefs/APIManager 토큰 자체는 건드리지 않음 (응답만 로그).");

            yield return ProbePost<TokenRefreshRequestDto, AuthTokenResponseDto>(
                "TOKEN-REFRESH POST /v1/auth/token/refresh",
                "/v1/auth/token/refresh",
                new TokenRefreshRequestDto { RefreshToken = refreshToken });
        }

        // POST /v1/auth/logout — refreshToken 무효화. 응답 후 다시 로그인 필요.
        // 이 러너 자체는 PlayerPrefs/APIManager 토큰을 정리하지 않음 — 호출 효과만 검증.
        // 실서비스 로그아웃 흐름은 SettingsScreenController.Logout 이 담당 (AuthSession.Clear 등).
        private IEnumerator RunLogout()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest][logout] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            string refreshToken = PlayerPrefs.GetString(AuthStorageKeys.RefreshToken, string.Empty);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                Debug.LogWarning("[ApiSmokeTest][logout] PlayerPrefs 에 refreshToken 없음 — 로그인 먼저.");
                yield break;
            }

            Debug.LogWarning(
                "[ApiSmokeTest][logout] 시작 — 이 호출 후 BE 의 refreshToken 이 무효화됨. " +
                "다시 정상 흐름을 쓰려면 재로그인 필요.");

            yield return ProbePost<LogoutRequestDto, VoidResponseDto>(
                "LOGOUT POST /v1/auth/logout",
                "/v1/auth/logout",
                new LogoutRequestDto { RefreshToken = refreshToken });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Theme apply / Layout save 전용 흐름 (사용자 spec)
        // ─────────────────────────────────────────────────────────────────────

        // POST /v1/spaces/me/theme/{inventoryId}/apply?mode=RESET (body 없음)
        // 흐름: GET /v1/assets/me/inventory → assetIds[0] 추출 → 해당 값을 path 의 inventoryId 로 그대로 사용 → POST 호출
        // 추가 sub-test:
        //   • inventoryId = -1 → 4xx 기대 (BE validation 미흡으로 현재는 500 반환되지만 로그만 남김)
        //   • clearTokenBeforeRun=true 로 재실행하면 401 검증 (토큰 영향 작업 → 별도 토글)
        private IEnumerator RunThemeApplyTest()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest][theme-apply] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            total = ok2xx = authBlocked = otherFail = skipped = 0;

            if (clearTokenBeforeRun)
            {
                api.SetAccessToken(null);
                Debug.Log("[ApiSmokeTest][theme-apply] clearTokenBeforeRun=true → in-memory accessToken cleared (401 검증 모드).");
            }

            LogTokenStatus(api);

            string mode = testThemeMode.ToString();

            // (1) theme apply id 확보. Inspector testInventoryId 가 있으면 그 값을 path 의 inventoryId 로 사용.
            // 없으면 GET /v1/assets/me/inventory 의 items[].inventoryId 를 가져와 사용한다.
            // BE 는 /theme/{inventoryId}/apply 에서 UserAssetInventory.id 를 검증하므로 assetIds[] 값을
            // path variable 로 보내면 안 된다.
            long idForThemeApply = testInventoryId;
            if (idForThemeApply <= 0)
            {
                Debug.Log("[ApiSmokeTest][theme-apply] testInventoryId 미입력 → GET /v1/assets/me/inventory 호출하여 items[].inventoryId 사용.");
                long fetched = 0;
                yield return FetchFirstThemeInventoryId(id => fetched = id);
                if (fetched <= 0)
                {
                    Debug.LogWarning("[ApiSmokeTest][theme-apply] inventory items 배열에 usable inventoryId가 없음 — Theme Apply 본 호출 SKIP. bad-id sub-test 만 진행.");
                }
                else
                {
                    Debug.Log($"[ApiSmokeTest][theme-apply] inventoryId={fetched} 값을 path variable에 사용 (mode={mode})");
                    idForThemeApply = fetched;
                }
            }
            else
            {
                Debug.Log($"[ApiSmokeTest][theme-apply] Inspector testInventoryId 사용 → path inventoryId={idForThemeApply} (mode={mode})");
            }

            // (2) 정상 호출 — POST /v1/spaces/me/theme/{inventoryId}/apply?mode=RESET, body 없음.
            //     path/query 가 BE 의 실제 endpoint shape. body 가 없으므로 ProbePost<TResponse>(path-only) 사용.
            if (idForThemeApply > 0)
            {
                string okPath = $"/v1/spaces/me/theme/{idForThemeApply}/apply?mode={Uri.EscapeDataString(mode)}";
                yield return ProbePost<SpaceMutationResponseDto>(
                    $"THEME-APPLY POST {okPath} (no body)",
                    okPath);
            }

            // (3) 잘못된 inventoryId sub-test — path variable 에 -1 을 박아 4xx 기대.
            string badPath = $"/v1/spaces/me/theme/-1/apply?mode={Uri.EscapeDataString(mode)}";
            yield return ProbePost<SpaceMutationResponseDto>(
                $"THEME-APPLY[bad-id] POST {badPath} → 4xx 기대",
                badPath);

            PrintSummary();
        }

        // PUT /v1/spaces/me/layout body{layoutPayload, baseLayoutVersion}
        // 흐름: 더미 layoutPayload(JSON 문자열) 생성 → PUT 호출
        // 추가 sub-test:
        //   • layoutPayload = null → 실패 기대
        //   • layoutPayload = "" (빈 문자열) → 실패 기대
        //   • clearTokenBeforeRun=true 로 재실행하면 401 검증
        private IEnumerator RunLayoutSaveTest()
        {
            APIManager api = ApiManagerProvider.EnsureInstance();
            if (api == null)
            {
                Debug.LogError("[ApiSmokeTest][layout-save] APIManager 초기화 실패.");
                yield break;
            }
            yield return null;

            total = ok2xx = authBlocked = otherFail = skipped = 0;

            if (clearTokenBeforeRun)
            {
                api.SetAccessToken(null);
                Debug.Log("[ApiSmokeTest][layout-save] clearTokenBeforeRun=true → in-memory accessToken cleared (401 검증 모드).");
            }

            LogTokenStatus(api);

            // 더미 payload — 사용자 예시 그대로. JObject 로 조립 후 string 화 (수동 escape 회피).
            JObject dummy = new JObject(
                new JProperty("objects", new JArray(
                    new JObject(
                        new JProperty("id", "chair_1"),
                        new JProperty("x", 0),
                        new JProperty("z", 0),
                        new JProperty("rot", 0)),
                    new JObject(
                        new JProperty("id", "lamp_1"),
                        new JProperty("x", 1),
                        new JProperty("z", 2),
                        new JProperty("rot", 90)))));
            string dummyPayload = dummy.ToString(Formatting.None);

            Debug.Log($"[ApiSmokeTest][layout-save] dummy layoutPayload = {dummyPayload}");

            // (1) 정상 payload 호출
            LayoutSaveRequestDto request = new LayoutSaveRequestDto
            {
                LayoutPayload = dummyPayload,
                BaseLayoutVersion = null
            };
            yield return ProbePut<LayoutSaveRequestDto, LayoutSaveResponseDto>(
                "LAYOUT-SAVE PUT /v1/spaces/me/layout (dummy payload)",
                "/v1/spaces/me/layout",
                request);

            // (2) null payload sub-test — 실패 기대.
            LayoutSaveRequestDto nullRequest = new LayoutSaveRequestDto
            {
                LayoutPayload = null,
                BaseLayoutVersion = null
            };
            yield return ProbePut<LayoutSaveRequestDto, LayoutSaveResponseDto>(
                "LAYOUT-SAVE[null] PUT /v1/spaces/me/layout (layoutPayload=null) → 실패 기대",
                "/v1/spaces/me/layout",
                nullRequest);

            // (3) 빈 string payload sub-test — 실패 기대.
            LayoutSaveRequestDto emptyRequest = new LayoutSaveRequestDto
            {
                LayoutPayload = string.Empty,
                BaseLayoutVersion = null
            };
            yield return ProbePut<LayoutSaveRequestDto, LayoutSaveResponseDto>(
                "LAYOUT-SAVE[empty] PUT /v1/spaces/me/layout (layoutPayload=\"\") → 실패 기대",
                "/v1/spaces/me/layout",
                emptyRequest);

            PrintSummary();
        }

        // GET /v1/assets/me/inventory → THEME inventoryId 우선 추출. 응답이 비어 있거나 호출 실패 시 0 반환.
        private IEnumerator FetchFirstThemeInventoryId(Action<long> sink)
        {
            APIManager api = APIManager.Instance;
            if (api == null)
            {
                sink(0);
                yield break;
            }

            bool done = false;
            long picked = 0;
            api.Get<ApiEnvelopeDto<InventoryResponseDto>>(
                "/v1/assets/me/inventory",
                env =>
                {
                    done = true;
                    if (env != null && env.Succeeded && env.Result != null && env.Result.Items != null && env.Result.Items.Count > 0)
                    {
                        for (int i = 0; i < env.Result.Items.Count; i++)
                        {
                            var item = env.Result.Items[i];
                            if (item == null || item.InventoryId <= 0L)
                            {
                                continue;
                            }

                            if (string.Equals(item.Asset?.AssetType, "THEME", StringComparison.OrdinalIgnoreCase))
                            {
                                picked = item.InventoryId;
                                Debug.Log(
                                    $"[ApiSmokeTest][theme-apply] THEME inventory item 사용 → inventoryId={item.InventoryId}, assetId={item.AssetId}");
                                break;
                            }
                        }

                        if (picked <= 0L)
                        {
                            Debug.LogWarning(
                                "[ApiSmokeTest][theme-apply] THEME assetType inventory item이 없어 Theme Apply 본 호출을 SKIP합니다.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[ApiSmokeTest][theme-apply] inventory envelope 성공이지만 items 배열이 비어 있음.");
                    }
                },
                err =>
                {
                    done = true;
                    Debug.LogWarning(
                        $"[ApiSmokeTest][theme-apply] GET /v1/assets/me/inventory 실패: status={(err != null ? err.StatusCode.ToString() : "?")} " +
                        $"code={err?.Code} message={err?.Message} body={ApiManagerProvider.TrimForLog(err?.Body, bodyLogMaxChars)}");
                });

            yield return WaitForDone(() => done, "GET /v1/assets/me/inventory");
            sink(picked);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Probe helpers
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator ProbeGet<T>(string label, string path)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Get<ApiEnvelopeDto<T>>(
                path,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<T>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "GET", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbePost<T>(string label, string path)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Post<ApiEnvelopeDto<T>>(
                path,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<T>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "POST", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbePost<TRequest, TResponse>(string label, string path, TRequest request)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            // 요청 body 미리 로그 — LogResult 의 BODY 라인은 응답 body 라 따로 분리.
            Debug.Log($"[ApiSmokeTest] {label}\n  REQUEST BODY: {ApiManagerProvider.TrimForLog(SafeSerialize(request), bodyLogMaxChars)}");

            api.Post<TRequest, ApiEnvelopeDto<TResponse>>(
                path,
                request,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<TResponse>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "POST", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbePut<TRequest, TResponse>(string label, string path, TRequest request)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            // 요청 body 미리 로그 — LogResult 의 BODY 라인은 응답 body 라 따로 분리.
            Debug.Log($"[ApiSmokeTest] {label}\n  REQUEST BODY: {ApiManagerProvider.TrimForLog(SafeSerialize(request), bodyLogMaxChars)}");

            api.Put<TRequest, ApiEnvelopeDto<TResponse>>(
                path,
                request,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<TResponse>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "PUT", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbePatch<TRequest, TResult>(string label, string path, TRequest request)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Patch<TRequest, ApiEnvelopeDto<TResult>>(
                path,
                request,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<TResult>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "PATCH", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator ProbeDelete<T>(string label, string path)
        {
            APIManager api = APIManager.Instance;
            bool authAttached = api != null && api.HasAccessToken;
            bool done = false;
            bool success = false;
            long status = 0;
            string body = null;
            string envParse = "(not attempted)";

            api.Delete<ApiEnvelopeDto<T>>(
                path,
                e =>
                {
                    done = true; success = true;
                    body = SafeSerialize(e);
                    envParse = ClassifyEnvelope(e);
                },
                err =>
                {
                    done = true;
                    status = err != null ? err.StatusCode : 0;
                    body = err != null ? err.Body : null;
                    envParse = TryParseEnvelopeFromBody<T>(body);
                });

            yield return WaitForDone(() => done, label);
            LogResult(label, "DELETE", path, authAttached, success, status, body, envParse);
        }

        private IEnumerator WaitForDone(Func<bool> isDone, string label)
        {
            float t = 0f;
            while (!isDone() && t < perRequestTimeoutSec)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!isDone())
            {
                Debug.LogWarning($"[ApiSmokeTest] {label}: timeout {perRequestTimeoutSec:0.0}s — 응답 없음.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 로깅
        // ─────────────────────────────────────────────────────────────────────

        private void LogSkip(string label, string reason)
        {
            total++;
            skipped++;
            Debug.LogWarning($"[ApiSmokeTest] {label} → SKIP ({reason}).");
        }

        private void LogResult(
            string label,
            string method,
            string path,
            bool authAttached,
            bool success,
            long status,
            string body,
            string envParse)
        {
            total++;
            string assumedUrl = ApiEndpointDefaults.DefaultBaseUrl.TrimEnd('/') + path;

            string statusStr;
            string verdict;
            if (success)
            {
                statusStr = "2xx (onSuccess)";
                verdict = "OK";
                ok2xx++;
            }
            else if (status == 401 || status == 403)
            {
                statusStr = status.ToString();
                verdict = "AUTH-FAIL — 권한 문제 (토큰 만료/누락/대상 리소스에 대한 권한 없음)";
                authBlocked++;
            }
            else if (status == 400)
            {
                statusStr = "400";
                verdict = "요청값 문제 — 요청 body/path 파라미터 형식 또는 validation 실패";
                otherFail++;
            }
            else if (status == 404)
            {
                statusStr = "404";
                verdict = "요청값 문제 — 대상 리소스 부재 (testSpaceId/testUserId/testInventoryId 잘못됨)";
                otherFail++;
            }
            else if (status == 409)
            {
                statusStr = "409";
                verdict = "충돌 — 이미 follow/like 되어 있거나, BE 가 self-action 차단";
                otherFail++;
            }
            else if (status >= 400 && status < 500)
            {
                statusStr = status.ToString();
                verdict = "4xx 기타 클라이언트 에러 — code/message 확인";
                otherFail++;
            }
            else if (status >= 500)
            {
                statusStr = status.ToString();
                verdict = "5xx 서버 에러 — BE 로그 확인 필요";
                otherFail++;
            }
            else
            {
                statusStr = status > 0 ? status.ToString() : "(no response)";
                verdict = "FAIL (network/timeout 등)";
                otherFail++;
            }

            string authLine = authAttached
                ? "attached (Bearer " + MaskToken(ReadStoredToken()) + ")"
                : "NOT attached";

            string trimmedBody = ApiManagerProvider.TrimForLog(body, bodyLogMaxChars);

            // Unity Console 이 single multi-line Debug.Log 의 일부만 표시하는 케이스 회피.
            // 각 라인을 개별 Debug.Log/LogWarning 으로 emit → 한 줄도 가려지지 않음.
            // 사용자가 STATUS/BODY 만 따로 골라 보거나 grep 하기도 쉬워짐.
            void Emit(string s) { if (success) Debug.Log(s); else Debug.LogWarning(s); }

            // [OK] / [FAIL] 태그 — 사용자 spec. Console grep 으로 한 눈에 결과 분리 가능.
            string tag = success ? "[OK]" : "[FAIL]";
            Emit($"[ApiSmokeTest] {tag} {label}");
            Emit($"  METHOD: {method}");
            Emit($"  URL: {assumedUrl}");
            Emit($"  AUTH: {authLine}");
            Emit($"  STATUS: {statusStr}  → {verdict}");
            Emit($"  BODY: {trimmedBody}");
            Emit($"  PARSE: {envParse}");
        }

        private void PrintSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[ApiSmokeTest] ===== Summary =====");
            sb.AppendLine($"  total={total}  ok={ok2xx}  auth-blocked={authBlocked}  other-fail={otherFail}  skipped={skipped}");
            sb.AppendLine($"  testSpaceId={testSpaceId}  testUserId={testUserId}  testInventoryId={testInventoryId}");
            Debug.Log(sb.ToString());
        }

        private void LogTokenStatus(APIManager api)
        {
            string raw = ReadStoredToken();
            bool hasPrefs = !string.IsNullOrWhiteSpace(raw);
            Debug.Log(
                $"[ApiSmokeTest] token state: prefs={(hasPrefs ? "present" : "absent")} " +
                $"masked={MaskToken(raw)} | api.HasAccessToken={api.HasAccessToken}");
        }

        private static string ReadStoredToken()
        {
            return PlayerPrefs.GetString(AuthStorageKeys.AccessToken, string.Empty);
        }

        private static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "(none)";
            if (token.Length <= 12) return $"***(len={token.Length})";
            return $"{token.Substring(0, 8)}...{token.Substring(token.Length - 4)} (len={token.Length})";
        }

        private static string SafeSerialize(object obj)
        {
            if (obj == null) return "(null)";
            try { return JsonConvert.SerializeObject(obj); }
            catch (Exception ex) { return $"(serialize error: {ex.Message})"; }
        }

        private static string ClassifyEnvelope<T>(ApiEnvelopeDto<T> envelope)
        {
            if (envelope == null) return "envelope-null";
            if (!envelope.Succeeded) return $"envelope-fail (code={envelope.Code})";
            if (envelope.Result == null) return "envelope-ok-result-null";
            return "envelope-ok-result-parsed";
        }

        private static string TryParseEnvelopeFromBody<T>(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return "(empty body)";
            try
            {
                ApiEnvelopeDto<T> env = JsonConvert.DeserializeObject<ApiEnvelopeDto<T>>(body);
                return ClassifyEnvelope(env);
            }
            catch (Exception ex)
            {
                return $"PARSE-ERROR: {ex.Message}";
            }
        }
    }
}
#endif
