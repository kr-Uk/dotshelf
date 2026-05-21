using System;

namespace App.Domain.Dashboard
{
    public sealed class PostItStyle
    {
        public const string DefaultColorHex = "FFF5C7";

        public long AssetId { get; }
        public string DisplayName { get; }
        public string ColorHex { get; }

        public PostItStyle(long assetId, string displayName, string colorHex = null)
        {
            if (assetId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(assetId));
            }

            AssetId = assetId;
            DisplayName = displayName ?? string.Empty;
            ColorHex = colorHex ?? DefaultColorHex;
        }
    }
}
