using System;
using System.Collections.Generic;
using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // 온보딩 Step2 에서 사용하는 한글 themeId 와 실제 RoomThemeAsset 자산 간 매핑.
    //
    // 분리 이유:
    //   - OnboardingScreenController 는 단순 string 으로 선택값을 다루지만, RoomThemeApplier /
    //     RoomEditModeController.ApplyThemeSelected 는 RoomThemeAsset reference 를 받는다.
    //   - 매핑이 분기/lookup 으로 흩어지면 silent fail (자산 이름이 한글 displayName 과 mismatch)
    //     이 자주 발생 → 단일 진입점 + 명시 로그.
    //
    // 매핑 정책:
    //   - "내추럴"  → RoomTheme_Natural   (displayName="Natural")
    //   - "클래식"  → RoomTheme_Classic   (displayName="Classic")
    //   - "모던"    → RoomTheme_Modern    (displayName="Modern")
    //   - "빈티지"  → RoomTheme_Vintage2  (displayName="빈티지"; vintage_shelf/dashboard/lamp 등
    //                                      풀 자산 보유; 영문 RoomTheme_Vintage 는 legacy)
    //   - "파스텔"  → RoomTheme_Pastel    (displayName="Pastel")
    //   - 5개 모두 isLocalPreview=true → PersistLastAppliedLocalPreviewTheme 호환.
    //
    // lookup 경로: Resources/RoomThemeCatalog.asset → Entries 에서 ScriptableObject.name 매칭.
    // catalog 미등록 시 LogWarning + null. silent fail 없음.
    internal static class OnboardingThemeAssetResolver
    {
        // 온보딩 themeId → RoomThemeAsset 자산 이름 (= ScriptableObject.name).
        // OnboardingThemePreviewProvider.OrderedThemeIds 와 키 동기화 필수.
        private static readonly Dictionary<string, string> AssetNameByOnboardingId =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "내추럴", "RoomTheme_Natural"  },
                { "클래식", "RoomTheme_Classic"  },
                { "모던",   "RoomTheme_Modern"   },
                { "빈티지", "RoomTheme_Vintage2" },
                { "파스텔", "RoomTheme_Pastel"   },
            };

        private const string CatalogResourcePath = "RoomThemeCatalog";

        // null 반환 시 호출자는 적용 skip. 모든 실패 분기에서 LogWarning 출력 → silent fail 없음.
        public static RoomThemeAsset Resolve(string onboardingThemeId)
        {
            if (string.IsNullOrWhiteSpace(onboardingThemeId))
            {
                Debug.LogWarning("[OnboardingThemeResolver] themeId 비어있음 — Resolve skip.");
                return null;
            }

            if (!AssetNameByOnboardingId.TryGetValue(onboardingThemeId, out var expectedAssetName))
            {
                Debug.LogWarning(
                    $"[OnboardingThemeResolver] 매핑 없음 themeId='{onboardingThemeId}'. " +
                    "OnboardingThemeAssetResolver.AssetNameByOnboardingId 에 추가하거나 " +
                    "OnboardingThemePreviewProvider.OrderedThemeIds 와 일치하는지 확인.");
                return null;
            }

            var catalog = Resources.Load<RoomThemeCatalogAsset>(CatalogResourcePath);
            if (catalog == null)
            {
                Debug.LogWarning(
                    $"[OnboardingThemeResolver] Resources/{CatalogResourcePath} 로드 실패 — " +
                    "RoomThemeCatalog.asset 이 Assets/Resources/ 아래에 있는지 확인. themeId='{onboardingThemeId}'");
                return null;
            }

            if (catalog.Entries != null)
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    var theme = catalog.Entries[i];
                    if (theme == null) continue;
                    if (string.Equals(theme.name, expectedAssetName, StringComparison.Ordinal))
                    {
                        return theme;
                    }
                }
            }

            Debug.LogWarning(
                $"[OnboardingThemeResolver] catalog 에 '{expectedAssetName}' 자산이 등록돼 있지 않음. " +
                $"themeId='{onboardingThemeId}' → RoomThemeCatalog.Entries 에 추가하거나 자산 이름 확인.");
            return null;
        }
    }
}
