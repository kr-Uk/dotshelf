using System.Collections.Generic;
using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    [CreateAssetMenu(menuName = "Dotshelf/Room Theme Catalog", fileName = "RoomThemeCatalog")]
    public sealed class RoomThemeCatalogAsset : ScriptableObject
    {
        [SerializeField] private RoomThemeAsset defaultTheme;
        [SerializeField] private List<RoomThemeAsset> entries = new List<RoomThemeAsset>();

        public RoomThemeAsset DefaultTheme => defaultTheme;
        public IReadOnlyList<RoomThemeAsset> Entries => entries;

        public RoomThemeAsset FindByAssetId(long assetId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].AssetId == assetId)
                {
                    return entries[i];
                }
            }
            return null;
        }

        public RoomThemeAsset Resolve(long? assetId)
        {
            if (!assetId.HasValue) return defaultTheme;
            return FindByAssetId(assetId.Value) ?? defaultTheme;
        }
    }
}
