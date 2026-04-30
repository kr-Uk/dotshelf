using System;
using System.Collections.Generic;
using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    [CreateAssetMenu(menuName = "Dotshelf/PostIt Style Catalog", fileName = "PostItStyleCatalog")]
    public sealed class PostItStyleCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<PostItStyleEntry> entries = new List<PostItStyleEntry>();

        public IReadOnlyList<PostItStyleEntry> Entries => entries;

        public PostItStyleEntry FindByAssetId(long assetId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].AssetId == assetId)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class PostItStyleEntry
    {
        [SerializeField] private long assetId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite previewSprite;

        public long AssetId => assetId;
        public string DisplayName => displayName;
        public Sprite PreviewSprite => previewSprite;
    }
}
