using System;
using System.Collections.Generic;

namespace App.Application.Dashboard
{
    public sealed class ThemeContents
    {
        public long ThemeAssetId { get; set; }
        public long? Wallpaper { get; set; }
        public long? Floor { get; set; }
        public long? Desk { get; set; }
        public long? Dashboard { get; set; }
        public long? InteriorLight { get; set; }
        public long? ExteriorLight { get; set; }
        public long? BookshelfSkinDefault { get; set; }
        public IReadOnlyList<ThemeContentItem> Children { get; set; } = Array.Empty<ThemeContentItem>();
    }

    public sealed class ThemeContentItem
    {
        public long AssetId { get; set; }
        public string AssetType { get; set; }
        public string Role { get; set; }
        public bool Required { get; set; }
        public string DefaultTransform { get; set; }
    }
}
