using System.Collections.Generic;
using UnityEngine;

namespace App.Bootstrap
{
    // Data-mapping layer for the onboarding theme-selection preview. Resolves
    // a stable themeId to a preview Sprite loaded from Resources/.
    internal sealed class OnboardingThemePreviewProvider
    {
        private const string ResourcesPathPrefix = "Onboarding/ThemePreviews/";

        public static readonly string[] OrderedThemeIds = BuildOrderedThemeIds();

        public const string DefaultThemeId = OnboardingThemeRegistry.DefaultThemeId;

        private readonly Dictionary<string, Sprite> spriteCache =
            new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> resourceSpriteCache =
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

        public bool TryGetPreviewSpriteByResourceName(string resourceName, out Sprite sprite)
        {
            sprite = ResolveSpriteByResourceName(resourceName);
            return sprite != null && sprite != fallbackSprite;
        }

        public Sprite GetPreviewSprite(string themeId)
        {
            return ResolveSprite(themeId) ?? fallbackSprite;
        }

        public string GetResourceName(string themeId)
        {
            return OnboardingThemeRegistry.TryGet(themeId, out var definition)
                ? definition.SpriteName
                : null;
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

            var loaded = ResolveSpriteByResourceName(definition.SpriteName);
            if (loaded == fallbackSprite)
            {
                LogMissingOnce(themeId, $"Resources.Load<Sprite>(\"{ResourcesPathPrefix + definition.SpriteName}\") returned null");
            }

            if (loaded != null && loaded != fallbackSprite)
            {
                spriteCache[themeId] = loaded;
            }

            return loaded;
        }

        private Sprite ResolveSpriteByResourceName(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                return fallbackSprite;
            }

            if (resourceSpriteCache.TryGetValue(resourceName, out var cached) && cached != null)
            {
                return cached;
            }

            var resourcePath = ResourcesPathPrefix + resourceName;
            var loaded = Resources.Load<Sprite>(resourcePath);
            if (loaded != null)
            {
                resourceSpriteCache[resourceName] = loaded;
                return loaded;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                Debug.LogWarning(
                    $"[Onboarding] preview sprite load failed at '{resourcePath}'. " +
                    "Texture exists but Sprite load returned null. Check Texture Import Settings: Sprite (2D and UI).");

                var runtimeSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                runtimeSprite.name = texture.name + "_RuntimePreviewSprite";
                resourceSpriteCache[resourceName] = runtimeSprite;
                return runtimeSprite;
            }

            Debug.LogWarning($"[Onboarding] preview sprite load failed at '{resourcePath}'.");
            return fallbackSprite;
        }

        private void LogMissingOnce(string themeId, string reason)
        {
            if (!warnedThemeIds.Add(themeId))
            {
                return;
            }

            Debug.LogWarning(
                $"[Onboarding] theme preview missing for '{themeId}' ??{reason}. " +
                "Place the onboarding theme PNGs under Assets/Resources/Onboarding/ThemePreviews/.");
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
