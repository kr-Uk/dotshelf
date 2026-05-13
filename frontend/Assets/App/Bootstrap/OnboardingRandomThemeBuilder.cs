using System.Collections.Generic;
using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // 온보딩 Step2 의 "랜덤" 버튼 클릭 시 호출되는 정적 헬퍼.
    //
    // 입력: 5개 한글 themeId ("내추럴" / "클래식" / "모던" / "빈티지" / "파스텔") — 내부적으로
    //   OnboardingThemeAssetResolver 로 RoomThemeAsset 자산 해상도.
    //
    // 처리:
    //   1) 5개 RoomThemeAsset 의 FloorDecoSpecs / WallDecoSpecs / BookshelfDecoSpecs 를 모두 합쳐
    //      ThemeSlot 별 후보 풀 build.
    //   2) ThemeSlot.None 또는 spec.RandomEligible == false 인 entry 는 제외 (per-theme summary 로그).
    //   3) 각 슬롯에서 1개 spec 만 random pick → 같은 슬롯 중복 0 (위치 겹침 1차 가드).
    //   4) baseTheme — slot mix 에서 가장 많은 spec 을 기여한 theme. 같으면 5개 themeId 의 첫 valid.
    //
    // 출력: OnboardingRandomThemeSelection. 호출자가 selectedRandomSelection 멤버에 저장 + UI 갱신.
    //
    // ThemeSlot.None 정책: 슬롯 의미 없는 spec 은 위치 겹침 정책의 단위가 없어 랜덤 후보에서 제외.
    //   *theme 별 summary 로그* 로 디자이너 확인 가능 — spec 한 줄씩 LogWarning 출력하면 5×N 줄
    //   spam 되므로 theme 마다 카운트만 한 줄.
    internal static class OnboardingRandomThemeBuilder
    {
        // 본 빌더가 후보로 수집하는 5개 themeId. OnboardingThemePreviewProvider.OrderedThemeIds 와 동기화.
        private static readonly string[] CandidateThemeIds = new[]
        {
            "내추럴",
            "클래식",
            "모던",
            "빈티지",
            "파스텔",
        };

        public static OnboardingRandomThemeSelection BuildRandomSelection(int? overrideSeed = null)
        {
            int seed = overrideSeed ?? Random.Range(int.MinValue, int.MaxValue);
            var rng = new System.Random(seed);

            // (1) 5개 RoomThemeAsset 해상도
            var resolvedThemes = new List<RoomThemeAsset>(CandidateThemeIds.Length);
            for (int i = 0; i < CandidateThemeIds.Length; i++)
            {
                var resolved = OnboardingThemeAssetResolver.Resolve(CandidateThemeIds[i]);
                if (resolved == null)
                {
                    // Resolver 가 이미 LogWarning — 본 빌더는 추가 noise 없이 skip.
                    continue;
                }
                resolvedThemes.Add(resolved);
            }
            if (resolvedThemes.Count == 0)
            {
                Debug.LogWarning("[OnboardingRandomTheme] resolved 0 themes — 5-pick fallback 권장. 본 빌더는 null 반환.");
                return null;
            }

            // (2) ThemeSlot 별 후보 풀 build. excludedNone 카운트도 per-theme 로 모아 summary 출력.
            var candidates = new Dictionary<ThemeSlot, List<OnboardingRandomThemeSelection.Entry>>();
            var perThemeNoneCount = new Dictionary<string, int>();
            var perThemeIneligibleCount = new Dictionary<string, int>();
            int themeContributedSlotsCount = 0;
            for (int i = 0; i < resolvedThemes.Count; i++)
            {
                var theme = resolvedThemes[i];
                int beforeSlotsCount = TotalCandidateCount(candidates);
                AddSpecs(theme, theme.FloorDecoSpecs, OnboardingRandomThemeSelection.DecoCategory.Floor,
                    candidates, perThemeNoneCount, perThemeIneligibleCount);
                AddSpecs(theme, theme.WallDecoSpecs, OnboardingRandomThemeSelection.DecoCategory.Wall,
                    candidates, perThemeNoneCount, perThemeIneligibleCount);
                AddSpecs(theme, theme.BookshelfDecoSpecs, OnboardingRandomThemeSelection.DecoCategory.Bookshelf,
                    candidates, perThemeNoneCount, perThemeIneligibleCount);
                int after = TotalCandidateCount(candidates);
                if (after > beforeSlotsCount) themeContributedSlotsCount++;
            }

            // per-theme summary (None / ineligible) — 한 줄씩.
            foreach (var kv in perThemeNoneCount)
            {
                if (kv.Value > 0)
                {
                    Debug.LogWarning(
                        $"[OnboardingRandomTheme] {kv.Key} has {kv.Value} spec(s) with ThemeSlot.None. " +
                        "Excluded from slot random (슬롯 의미 없는 spec — 위치 겹침 가드 단위 부재).");
                }
            }
            foreach (var kv in perThemeIneligibleCount)
            {
                if (kv.Value > 0)
                {
                    Debug.Log(
                        $"[OnboardingRandomTheme] {kv.Key} has {kv.Value} spec(s) with randomEligible=false. " +
                        "Excluded from slot random (디자이너 옵트아웃).");
                }
            }

            // (3) 슬롯 별 후보 풀 디버그 로그 — 어느 슬롯에 몇 개가 모였는지 한 줄.
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[OnboardingRandomTheme] 후보 풀 비어있음 — 5개 RoomThemeAsset 의 spec 들에 ThemeSlot 이 모두 None 이거나 RandomEligible=false. 랜덤 선택 skip.");
                return null;
            }
            var poolSummary = new System.Text.StringBuilder("[OnboardingRandomTheme] Build candidates:");
            foreach (var kv in candidates)
            {
                poolSummary.Append(' ').Append(kv.Key).Append('=').Append(kv.Value.Count);
            }
            Debug.Log(poolSummary.ToString());

            // (4) 슬롯 별 1개 pick + baseTheme 결정
            var selection = new OnboardingRandomThemeSelection { Seed = seed };
            var perThemePickCount = new Dictionary<RoomThemeAsset, int>();
            foreach (var kv in candidates)
            {
                var pool = kv.Value;
                if (pool.Count == 0) continue;
                int idx = rng.Next(0, pool.Count);
                var picked = pool[idx];
                selection.SelectedSpecs[kv.Key] = picked;
                if (picked.OwnerTheme != null)
                {
                    perThemePickCount.TryGetValue(picked.OwnerTheme, out var c);
                    perThemePickCount[picked.OwnerTheme] = c + 1;
                }
                Debug.Log(
                    $"[OnboardingRandomTheme] Selected {kv.Key}: " +
                    $"{(picked.ThemeAssetName ?? "?")} / {(picked.AssetKey ?? "?")} " +
                    $"(category={picked.Category}, poolSize={pool.Count})");
            }

            // baseTheme — 가장 많은 spec 을 기여한 theme. tie 시 첫 valid.
            RoomThemeAsset baseTheme = null;
            int bestCount = -1;
            foreach (var kv in perThemePickCount)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    baseTheme = kv.Key;
                }
            }
            if (baseTheme == null && resolvedThemes.Count > 0) baseTheme = resolvedThemes[0];
            selection.BaseTheme = baseTheme;
            selection.Description = $"여러 테마의 가구가 섞인 조합 ({selection.SelectedSpecs.Count}개 슬롯)";

            Debug.Log(
                $"[OnboardingRandomTheme] Build OK — {selection.BuildSummary()} " +
                $"themesContributed={themeContributedSlotsCount}/{resolvedThemes.Count}");
            return selection;
        }

        // PlayerPrefs 로부터 복원. 직렬화 형식은 OnboardingRandomThemeSelection.SerializeToPrefs 와 1:1.
        // 자산 이름 + assetKey 로 catalog 에서 spec 다시 lookup — 자산이 삭제/이름 변경됐으면 entry skip.
        public static OnboardingRandomThemeSelection BuildFromSerialized(string serialized)
        {
            if (string.IsNullOrEmpty(serialized)) return null;
            try
            {
                var selection = new OnboardingRandomThemeSelection();
                var parts = serialized.Split('|');
                string baseName = null;
                string specsPart = null;
                int seed = 0;
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    if (p.StartsWith("base:", System.StringComparison.Ordinal)) baseName = p.Substring(5);
                    else if (p.StartsWith("seed:", System.StringComparison.Ordinal)) int.TryParse(p.Substring(5), out seed);
                    else if (p.StartsWith("specs:", System.StringComparison.Ordinal)) specsPart = p.Substring(6);
                }
                selection.Seed = seed;

                // base theme — 5개 candidates 중 name 매칭. 없으면 null (apply 시 fallback 동작).
                if (!string.IsNullOrEmpty(baseName))
                {
                    for (int i = 0; i < CandidateThemeIds.Length; i++)
                    {
                        var t = OnboardingThemeAssetResolver.Resolve(CandidateThemeIds[i]);
                        if (t != null && string.Equals(t.name, baseName, System.StringComparison.Ordinal))
                        {
                            selection.BaseTheme = t;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(specsPart))
                {
                    var entries = specsPart.Split(';');
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var e = entries[i];
                        if (string.IsNullOrEmpty(e)) continue;
                        var fields = e.Split(':');
                        if (fields.Length < 4) continue;
                        if (!int.TryParse(fields[0], out var slotInt)) continue;
                        var themeName = fields[1];
                        var assetKey = fields[2];
                        if (!int.TryParse(fields[3], out var catInt)) continue;

                        var slot = (ThemeSlot)slotInt;
                        var category = (OnboardingRandomThemeSelection.DecoCategory)catInt;

                        // 자산 + spec 재해상도
                        RoomThemeAsset ownerTheme = null;
                        RoomThemeDecoSpec spec = null;
                        for (int t = 0; t < CandidateThemeIds.Length; t++)
                        {
                            var candidate = OnboardingThemeAssetResolver.Resolve(CandidateThemeIds[t]);
                            if (candidate == null) continue;
                            if (!string.Equals(candidate.name, themeName, System.StringComparison.Ordinal)) continue;
                            ownerTheme = candidate;
                            spec = FindSpecByKey(candidate, category, assetKey);
                            break;
                        }
                        if (spec == null)
                        {
                            Debug.LogWarning(
                                $"[OnboardingRandomTheme] restore: '{themeName}/{assetKey}' (slot={slot}) " +
                                "자산 또는 spec 못 찾음 — entry skip. catalog 변경 가능성.");
                            continue;
                        }
                        selection.SelectedSpecs[slot] = new OnboardingRandomThemeSelection.Entry
                        {
                            Slot = slot,
                            ThemeAssetName = themeName,
                            AssetKey = assetKey,
                            OwnerTheme = ownerTheme,
                            Spec = spec,
                            Category = category,
                        };
                    }
                }
                selection.Description = $"여러 테마의 가구가 섞인 조합 ({selection.SelectedSpecs.Count}개 슬롯, 복원됨)";
                return selection.SelectedSpecs.Count > 0 ? selection : null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OnboardingRandomTheme] BuildFromSerialized 실패 — {ex.GetType().Name}: {ex.Message}. 복원 skip.");
                return null;
            }
        }

        private static RoomThemeDecoSpec FindSpecByKey(RoomThemeAsset theme, OnboardingRandomThemeSelection.DecoCategory category, string assetKey)
        {
            if (theme == null || string.IsNullOrEmpty(assetKey)) return null;
            IReadOnlyList<RoomThemeDecoSpec> list = category switch
            {
                OnboardingRandomThemeSelection.DecoCategory.Floor => theme.FloorDecoSpecs,
                OnboardingRandomThemeSelection.DecoCategory.Wall => theme.WallDecoSpecs,
                OnboardingRandomThemeSelection.DecoCategory.Bookshelf => theme.BookshelfDecoSpecs,
                _ => null,
            };
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s != null && string.Equals(s.AssetKey, assetKey, System.StringComparison.Ordinal))
                {
                    return s;
                }
            }
            return null;
        }

        private static void AddSpecs(
            RoomThemeAsset theme,
            IReadOnlyList<RoomThemeDecoSpec> specs,
            OnboardingRandomThemeSelection.DecoCategory category,
            Dictionary<ThemeSlot, List<OnboardingRandomThemeSelection.Entry>> candidates,
            Dictionary<string, int> perThemeNoneCount,
            Dictionary<string, int> perThemeIneligibleCount)
        {
            if (specs == null) return;
            var themeName = theme.name;
            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (spec == null) continue;

                if (spec.ThemeSlot == ThemeSlot.None)
                {
                    perThemeNoneCount.TryGetValue(themeName, out var c);
                    perThemeNoneCount[themeName] = c + 1;
                    continue;
                }
                if (!spec.RandomEligible)
                {
                    perThemeIneligibleCount.TryGetValue(themeName, out var c);
                    perThemeIneligibleCount[themeName] = c + 1;
                    continue;
                }

                if (!candidates.TryGetValue(spec.ThemeSlot, out var pool))
                {
                    pool = new List<OnboardingRandomThemeSelection.Entry>();
                    candidates[spec.ThemeSlot] = pool;
                }
                pool.Add(new OnboardingRandomThemeSelection.Entry
                {
                    Slot = spec.ThemeSlot,
                    ThemeAssetName = themeName,
                    AssetKey = spec.AssetKey,
                    OwnerTheme = theme,
                    Spec = spec,
                    Category = category,
                });
            }
        }

        private static int TotalCandidateCount(Dictionary<ThemeSlot, List<OnboardingRandomThemeSelection.Entry>> candidates)
        {
            int sum = 0;
            foreach (var kv in candidates) sum += kv.Value.Count;
            return sum;
        }
    }
}
