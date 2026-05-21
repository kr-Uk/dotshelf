using System;
using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    internal static class OnboardingThemeAssetResolver
    {
        private const string CatalogResourcePath = "RoomThemeCatalog";

        public static RoomThemeAsset Resolve(string onboardingThemeId)
        {
            if (string.IsNullOrWhiteSpace(onboardingThemeId))
            {
                Debug.LogWarning("[OnboardingThemeResolver] themeId is empty. Resolve skipped.");
                return null;
            }

            var expectedAssetName = OnboardingThemeRegistry.GetAssetName(onboardingThemeId);
            if (string.IsNullOrWhiteSpace(expectedAssetName))
            {
                Debug.LogWarning(
                    $"[OnboardingThemeResolver] No registry mapping for themeId='{onboardingThemeId}'.");
                return null;
            }

            var catalog = Resources.Load<RoomThemeCatalogAsset>(CatalogResourcePath);
            if (catalog == null)
            {
                Debug.LogWarning(
                    $"[OnboardingThemeResolver] Resources/{CatalogResourcePath} load failed. " +
                    $"themeId='{onboardingThemeId}'.");
                return null;
            }

            var entries = catalog.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var theme = entries[i];
                    if (theme == null) continue;
                    if (string.Equals(theme.name, expectedAssetName, StringComparison.Ordinal))
                    {
                        return theme;
                    }
                }
            }

            Debug.LogWarning(
                $"[OnboardingThemeResolver] Theme asset '{expectedAssetName}' is missing from the catalog. " +
                $"themeId='{onboardingThemeId}'.");
            return null;
        }
    }
}
