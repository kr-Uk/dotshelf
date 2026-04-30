using System;

namespace App.Domain.Dashboard
{
    public sealed class PostItStyle
    {
        public long AssetId { get; }
        public string DisplayName { get; }

        public PostItStyle(long assetId, string displayName)
        {
            if (assetId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(assetId));
            }

            AssetId = assetId;
            DisplayName = displayName ?? string.Empty;
        }
    }
}
