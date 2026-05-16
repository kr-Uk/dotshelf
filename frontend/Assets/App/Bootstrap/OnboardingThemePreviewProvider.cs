using System.Collections.Generic;
using UnityEngine;

namespace App.Bootstrap
{
    // Data-mapping layer for the onboarding theme-selection preview. Resolves
    // a stable themeId to a preview Sprite loaded from Resources/.
    internal sealed class OnboardingThemePreviewProvider
    {
        private const string ResourcesPathPrefix = "Onboarding/Themes/";

        public static readonly string[] OrderedThemeIds = BuildOrderedThemeIds();

        public const string DefaultThemeId = OnboardingThemeRegistry.DefaultThemeId;

        private readonly Dictionary<string, Sprite> spriteCache =
            new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
        private readonly HashSet<string> warnedThemeIds =
            new HashSet<string>(System.StringComparer.Ordinal);
        private readonly Sprite fallbackSprite;

        public OnboardingThemePreviewProvider(Sprite fallbackSprite)
        {
            this.fallbackSprite = fallbackSprite;
        }

        public Sprite FallbackSprite => fallbackSprite;

        public bool TryGetPreviewSprite(string themeId, out Sprite sprite)
        {
            sprite = ResolveSprite(themeId);
            return sprite != null && sprite != fallbackSprite;
        }

        public Sprite GetPreviewSprite(string themeId)
        {
            return ResolveSprite(themeId) ?? fallbackSprite;
        }

        private Sprite ResolveSprite(string themeId)
        {
            if (string.IsNullOrEmpty(themeId))
            {
                return fallbackSprite;
            }

            if (spriteCache.TryGetValue(themeId, out var cached) && cached != null)
            {
                return cached;
            }

            if (!OnboardingThemeRegistry.TryGet(themeId, out var definition))
            {
                LogMissingOnce(themeId, "no mapping in OnboardingThemeRegistry");
                return fallbackSprite;
            }

            var resourcePath = ResourcesPathPrefix + definition.SpriteName;
            var loaded = Resources.Load<Sprite>(resourcePath);
            if (loaded == null)
            {
                LogMissingOnce(themeId, $"Resources.Load<Sprite>(\"{resourcePath}\") returned null");
                return fallbackSprite;
            }

            spriteCache[themeId] = loaded;
            return loaded;
        }

        private void LogMissingOnce(string themeId, string reason)
        {
            if (!warnedThemeIds.Add(themeId))
            {
                return;
            }

            Debug.LogWarning(
                $"[Onboarding] theme preview missing for '{themeId}' ??{reason}. " +
                "Place the onboarding theme PNGs under Assets/Resources/Onboarding/Themes/.");
        }

        private static string[] BuildOrderedThemeIds()
        {
            var ids = new List<string>();
            foreach (var definition in OnboardingThemeRegistry.Enumerate())
            {
                ids.Add(definition.ThemeId);
            }

            return ids.ToArray();
        }
    }
}
