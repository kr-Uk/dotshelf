using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Bootstrap
{
    // Data-mapping layer for the onboarding theme-selection preview. Resolves
    // a stable themeId to a preview Sprite loaded from Resources/.
    //
    // This class is the swap point for a future 3D-scene preview: a parallel
    // GetPreviewRenderTexture(themeId) method backed by a dedicated camera
    // can be added here without touching OnboardingScreenController, as long
    // as the controller keeps calling the provider rather than reaching into
    // Resources directly.
    //
    // Sprite layout (the user must place 5 PNGs here):
    //   Assets/Resources/Onboarding/Themes/theme_natural.png
    //   Assets/Resources/Onboarding/Themes/theme_classic.png
    //   Assets/Resources/Onboarding/Themes/theme_modern.png
    //   Assets/Resources/Onboarding/Themes/theme_vintage.png
    //   Assets/Resources/Onboarding/Themes/theme_pastel.png
    // The .png import settings should be Texture Type = Sprite (2D and UI).
    internal sealed class OnboardingThemePreviewProvider
    {
        private const string ResourcesPathPrefix = "Onboarding/Themes/";

        // Stable theme identifiers. The Korean labels double as user-facing
        // button text — kept as-is to avoid a parallel "label vs id" indirection
        // when there is only one locale. Order here drives button order.
        public static readonly string[] OrderedThemeIds =
        {
            "내추럴",
            "클래식",
            "모던",
            "빈티지",
            "파스텔"
        };

        public const string DefaultThemeId = "내추럴";

        // themeId → Resources.Load asset name (no extension, no folder).
        // Stored separately from the user-facing label so theme labels can be
        // renamed/localized later without touching the asset folder layout.
        private static readonly Dictionary<string, string> SpriteNameByThemeId =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "내추럴", "theme_natural" },
                { "클래식", "theme_classic" },
                { "모던",   "theme_modern"  },
                { "빈티지", "theme_vintage" },
                { "파스텔", "theme_pastel"  }
            };

        private readonly Dictionary<string, Sprite> spriteCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<string> warnedThemeIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Sprite fallbackSprite;

        public OnboardingThemePreviewProvider(Sprite fallbackSprite)
        {
            this.fallbackSprite = fallbackSprite;
        }

        public Sprite FallbackSprite => fallbackSprite;

        // Returns true if the resolved sprite is the real preview (not the
        // fallback). The controller uses this to decide whether to hide the
        // procedural placeholder children — a fallback means we should keep
        // them visible because the real image is missing.
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

            if (!SpriteNameByThemeId.TryGetValue(themeId, out var spriteName))
            {
                LogMissingOnce(themeId, "no mapping in OnboardingThemePreviewProvider.SpriteNameByThemeId");
                return fallbackSprite;
            }

            var resourcePath = ResourcesPathPrefix + spriteName;
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
                $"[Onboarding] theme preview missing for '{themeId}' — {reason}. " +
                "Place the 5 onboarding theme PNGs under Assets/Resources/Onboarding/Themes/.");
        }
    }
}
