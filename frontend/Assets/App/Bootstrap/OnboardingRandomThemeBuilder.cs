using System.Collections.Generic;
using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    internal static class OnboardingRandomThemeBuilder
    {

        private static IReadOnlyList<string> CandidateThemeIds => OnboardingThemePreviewProvider.OrderedThemeIds;

        public static OnboardingRandomThemeSelection BuildRandomSelection(int? overrideSeed = null)
        {
            int seed = overrideSeed ?? Random.Range(int.MinValue, int.MaxValue);
            var rng = new System.Random(seed);

            var resolvedThemes = new List<RoomThemeAsset>(CandidateThemeIds.Count);
            for (int i = 0; i < CandidateThemeIds.Count; i++)
            {
                var resolved = OnboardingThemeAssetResolver.Resolve(CandidateThemeIds[i]);
                if (resolved == null)
                {
                    continue;
                }

                resolvedThemes.Add(resolved);
            }

            if (resolvedThemes.Count == 0)
            {
                Debug.LogWarning("[OnboardingRandomTheme] No candidate themes resolved.");
                return null;
            }

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

            foreach (var kv in perThemeNoneCount)
            {
                if (kv.Value > 0)
                {
                    Debug.LogWarning(
                        $"[OnboardingRandomTheme] {kv.Key} has {kv.Value} spec(s) with ThemeSlot.None.");
                }
            }

            foreach (var kv in perThemeIneligibleCount)
            {
                if (kv.Value > 0)
                {
                    App.Core.AppLog.Onboarding(
                        $"[OnboardingRandomTheme] {kv.Key} has {kv.Value} spec(s) with randomEligible=false.");
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[OnboardingRandomTheme] No random-eligible slot candidates were found.");
                return null;
            }

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
                    perThemePickCount.TryGetValue(picked.OwnerTheme, out var count);
                    perThemePickCount[picked.OwnerTheme] = count + 1;
                }
            }

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

            if (baseTheme == null && resolvedThemes.Count > 0)
            {
                baseTheme = resolvedThemes[0];
            }

            selection.BaseTheme = baseTheme;
            selection.Description = $"여러 테마의 가구가 섞인 조합 ({selection.SelectedSpecs.Count}개 슬롯)";

            App.Core.AppLog.Onboarding(
                $"[OnboardingRandomTheme] Build OK ??{selection.BuildSummary()} " +
                $"themesContributed={themeContributedSlotsCount}/{resolvedThemes.Count}");
            return selection;
        }

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

                if (!string.IsNullOrEmpty(baseName))
                {
                    for (int i = 0; i < CandidateThemeIds.Count; i++)
                    {
                        var theme = OnboardingThemeAssetResolver.Resolve(CandidateThemeIds[i]);
                        if (theme != null && string.Equals(theme.name, baseName, System.StringComparison.Ordinal))
                        {
                            selection.BaseTheme = theme;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(specsPart))
                {
                    var entries = specsPart.Split(';');
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var entryText = entries[i];
                        if (string.IsNullOrEmpty(entryText)) continue;

                        var fields = entryText.Split(':');
                        if (fields.Length < 4) continue;
                        if (!int.TryParse(fields[0], out var slotInt)) continue;
                        if (!int.TryParse(fields[3], out var categoryInt)) continue;

                        var slot = (ThemeSlot)slotInt;
                        var category = (OnboardingRandomThemeSelection.DecoCategory)categoryInt;
                        var themeName = fields[1];
                        var assetKey = fields[2];

                        RoomThemeAsset ownerTheme = null;
                        RoomThemeDecoSpec spec = null;
                        for (int t = 0; t < CandidateThemeIds.Count; t++)
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
                                $"[OnboardingRandomTheme] restore skipped missing spec '{themeName}/{assetKey}' (slot={slot}).");
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

                selection.Description = $"여러 테마의 가구가 섞인 조합 ({selection.SelectedSpecs.Count}개 슬롯, 복원)";
                return selection.SelectedSpecs.Count > 0 ? selection : null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[OnboardingRandomTheme] BuildFromSerialized failed ??{ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static RoomThemeDecoSpec FindSpecByKey(
            RoomThemeAsset theme,
            OnboardingRandomThemeSelection.DecoCategory category,
            string assetKey)
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
                var spec = list[i];
                if (spec != null && string.Equals(spec.AssetKey, assetKey, System.StringComparison.Ordinal))
                {
                    return spec;
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
                    perThemeNoneCount.TryGetValue(themeName, out var noneCount);
                    perThemeNoneCount[themeName] = noneCount + 1;
                    continue;
                }

                if (!spec.RandomEligible)
                {
                    perThemeIneligibleCount.TryGetValue(themeName, out var ineligibleCount);
                    perThemeIneligibleCount[themeName] = ineligibleCount + 1;
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

        private static int TotalCandidateCount(
            Dictionary<ThemeSlot, List<OnboardingRandomThemeSelection.Entry>> candidates)
        {
            int sum = 0;
            foreach (var kv in candidates)
            {
                sum += kv.Value.Count;
            }

            return sum;
        }
    }
}
