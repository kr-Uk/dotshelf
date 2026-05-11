using System.Collections.Generic;
using UnityEngine;

namespace App.Infrastructure.Catalogs
{
    /// <summary>
    /// 외부 옵션 카탈로그. RoomThemeCatalogAsset 과 동일 패턴.
    /// Tier 1: defaultExterior 1개 + 빈 entries.
    /// Tier 2: BE assets table 의 EXTERIOR 카테고리 row 와 entry 매핑.
    /// </summary>
    [CreateAssetMenu(menuName = "Dotshelf/Exterior Catalog", fileName = "ExteriorCatalog")]
    public sealed class ExteriorCatalogAsset : ScriptableObject
    {
        [Tooltip("base APK 내장 default 외부. assetId == 0 또는 lookup 실패 시 fallback.")]
        [SerializeField] private ExteriorAsset defaultExterior;

        [Tooltip("선택 가능한 외부 옵션 list. Tier 2 에서 BE catalog 와 동기화.")]
        [SerializeField] private List<ExteriorAsset> entries = new();

        public ExteriorAsset DefaultExterior => defaultExterior;
        public IReadOnlyList<ExteriorAsset> Entries => entries;

        public ExteriorAsset FindByAssetId(long assetId)
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

        /// <summary>
        /// assetId 로 entry 찾기. 없으면 default fallback. assetId == 0 이면 default 직행.
        /// </summary>
        public ExteriorAsset Resolve(long assetId)
        {
            if (assetId == 0) return defaultExterior;
            return FindByAssetId(assetId) ?? defaultExterior;
        }
    }
}
