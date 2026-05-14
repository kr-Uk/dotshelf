using System;
using System.Collections.Generic;
using App.Application.Dashboard;
using App.Domain.Dashboard;

namespace App.Infrastructure.Catalogs
{
    public sealed class PostItStyleCatalogAdapter : IPostItStyleCatalog
    {
        private readonly Dictionary<long, PostItStyle> styles;
        private readonly List<PostItStyle> orderedStyles;

        public PostItStyleCatalogAdapter(PostItStyleCatalogAsset catalogAsset)
        {
            if (catalogAsset == null)
            {
                throw new ArgumentNullException(nameof(catalogAsset));
            }

            IReadOnlyList<PostItStyleEntry> entries = catalogAsset.Entries;
            styles = new Dictionary<long, PostItStyle>(entries.Count);
            orderedStyles = new List<PostItStyle>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                PostItStyleEntry entry = entries[i];
                if (entry == null || entry.AssetId <= 0)
                {
                    continue;
                }

                string hex = ColorToHex(catalogAsset.Entries[i].CellColor);
                PostItStyle style = new PostItStyle(entry.AssetId, entry.DisplayName, hex);
                if (styles.ContainsKey(entry.AssetId))
                {
                    continue;
                }

                styles.Add(entry.AssetId, style);
                orderedStyles.Add(style);
            }
        }

        public IReadOnlyList<PostItStyle> GetAllStyles()
        {
            return orderedStyles;
        }

        public PostItStyle GetStyle(long assetId)
        {
            return styles.TryGetValue(assetId, out PostItStyle style) ? style : null;
        }

        public bool TryGetStyle(long assetId, out PostItStyle style)
        {
            return styles.TryGetValue(assetId, out style);
        }

        private static string ColorToHex(UnityEngine.Color c)
        {
            return $"{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";
        }
    }
}
