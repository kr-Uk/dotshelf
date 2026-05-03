using System.Collections.Generic;
using System.IO;
using App.Infrastructure.Catalogs;
using UnityEditor;
using UnityEngine;

namespace Dotshelf.EditorTools
{
    /// <summary>
    /// 카테고리 카탈로그(Resources/Catalogs/*) placeholder 자산 자동 생성기.
    ///
    /// FE-4의 toolbar 1:1 카테고리 매핑이 동작하는지 시각 검증하기 위한 임시 자산.
    /// admin-web/Meshy 합류 전 디자인 검토용. 같은 메뉴 재실행 시 idempotent —
    /// 머티리얼/카탈로그 entries 자동 갱신.
    ///
    /// **`.gitignore`** 권장: Resources/Catalogs/, Resources/DesignPreview/ — 본인 로컬에서만
    /// 메뉴 실행. (또는 cleanup 메뉴로 일괄 삭제 후 commit.)
    /// </summary>
    public static class DesignPreviewPackCreatorUtility
    {
        const string MaterialsRoot = "Assets/App/Composition/Materials/DesignPreview";
        const string CatalogsRoot = "Assets/Resources/Catalogs";

        // FE-4 toolbar 1:1 매핑 4 카테고리. 묶음 카테고리는 후속.
        static readonly (CustomizationCategory category, (string name, Color color)[] swatches)[] PlaceholderSpecs =
        {
            (CustomizationCategory.Wallpaper, new (string, Color)[]
            {
                ("Cream",     HexColor("#FAF3E5")),
                ("Lavender",  HexColor("#E8DCEE")),
                ("Mint",      HexColor("#DCEBDC")),
                ("Beige",     HexColor("#E8DCC4")),
                ("OffWhite",  HexColor("#FAFAFA")),
            }),
            (CustomizationCategory.InteriorLight, null),  // 라이트는 색만 다름 — 머티리얼 X
            (CustomizationCategory.ExteriorLight, null),
        };

        static readonly (string name, Color color, float kelvin, float intensity)[] InteriorLightSwatches =
        {
            ("Warm 5200K",   HexColor("#FFF1D8"), 5200f, 1.0f),
            ("Neutral 6500K", Color.white,         6500f, 1.0f),
            ("Cool 7500K",   HexColor("#DCE7FF"),  7500f, 0.9f),
        };

        static readonly (string name, Color color, float kelvin, float intensity)[] ExteriorLightSwatches =
        {
            ("Morning 5500K", HexColor("#FFF6E0"), 5500f, 1.1f),
            ("Noon 6500K",    Color.white,          6500f, 1.4f),
            ("Evening 3500K", HexColor("#FFCC99"),  3500f, 0.9f),
        };

        [MenuItem("Dotshelf/Design Preview/Generate Placeholder Catalogs")]
        public static void GeneratePlaceholders()
        {
            EnsureDir(MaterialsRoot);
            EnsureDir(CatalogsRoot);

            // Wallpaper 카탈로그 — 머티리얼 5색
            var wallpaperEntries = new List<CustomizationEntry>();
            int idCounter = 100001;
            foreach (var (name, color) in PlaceholderSpecs[0].swatches)
            {
                var mat = CreateOrUpdateMaterial($"Wallpaper_{name}", color, 0.08f);
                wallpaperEntries.Add(BuildMaterialEntry(idCounter++, $"벽지 {name}", mat));
            }
            CreateOrUpdateCategoryCatalog(CustomizationCategory.Wallpaper, wallpaperEntries);

            // InteriorLight — color/kelvin/intensity 프리셋 3종 (머티리얼 없음)
            var interiorEntries = new List<CustomizationEntry>();
            idCounter = 100021;
            foreach (var swatch in InteriorLightSwatches)
            {
                interiorEntries.Add(BuildLightEntry(idCounter++, $"내부조명 {swatch.name}", swatch.color, swatch.kelvin, swatch.intensity));
            }
            CreateOrUpdateCategoryCatalog(CustomizationCategory.InteriorLight, interiorEntries);

            // ExteriorLight — 시간대 프리셋 3종
            var exteriorEntries = new List<CustomizationEntry>();
            idCounter = 100031;
            foreach (var swatch in ExteriorLightSwatches)
            {
                exteriorEntries.Add(BuildLightEntry(idCounter++, $"외부조명 {swatch.name}", swatch.color, swatch.kelvin, swatch.intensity));
            }
            CreateOrUpdateCategoryCatalog(CustomizationCategory.ExteriorLight, exteriorEntries);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DesignPreviewPack] Placeholder catalogs generated. " +
                      "Resources/Catalogs/{Wallpaper,InteriorLight,ExteriorLight}Catalog.asset 확인. " +
                      "Play → 꾸미기 → 벽지/내부조명/외부조명 탭에서 그리드 + visual swap 테스트.");
        }

        [MenuItem("Dotshelf/Design Preview/Cleanup Placeholder Catalogs")]
        public static void Cleanup()
        {
            if (Directory.Exists(MaterialsRoot))
            {
                AssetDatabase.DeleteAsset(MaterialsRoot);
            }
            // CatalogsRoot 자체는 RoomThemeCatalog 등 다른 자산이 있을 수 있어 통째 삭제 X
            foreach (CustomizationCategory cat in System.Enum.GetValues(typeof(CustomizationCategory)))
            {
                var path = $"{CatalogsRoot}/{cat}Catalog.asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DesignPreviewPack] Placeholder catalogs cleaned up.");
        }

        static Material CreateOrUpdateMaterial(string name, Color color, float smoothness)
        {
            var path = $"{MaterialsRoot}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[DesignPreviewPack] URP/Lit shader 못 찾음 — material 생성 skip.");
                return mat;
            }
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static CustomizationEntry BuildMaterialEntry(long assetId, string displayName, Material material)
        {
            var entry = new CustomizationEntry();
            var so = new SerializedObject(ScriptableObject.CreateInstance<CustomizationCatalogAsset>());
            // SerializedObject로 entry private 필드를 set 못하므로(struct/class wrapper 안 됨),
            // 대신 reflection으로 직접 set. 단순 placeholder라 reflection 비용 무시.
            SetPrivate(entry, "assetId", assetId);
            SetPrivate(entry, "displayName", displayName);
            SetPrivate(entry, "materials", new[] { material });
            return entry;
        }

        static CustomizationEntry BuildLightEntry(long assetId, string displayName, Color color, float kelvin, float intensity)
        {
            var entry = new CustomizationEntry();
            SetPrivate(entry, "assetId", assetId);
            SetPrivate(entry, "displayName", displayName);
            SetPrivate(entry, "lightColor", color);
            SetPrivate(entry, "colorTemperatureKelvin", kelvin);
            SetPrivate(entry, "lightIntensity", intensity);
            return entry;
        }

        static void SetPrivate(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f == null)
            {
                Debug.LogWarning($"[DesignPreviewPack] private field '{fieldName}' 못 찾음 — entry 일부 비어있을 수 있음.");
                return;
            }
            f.SetValue(target, value);
        }

        static CustomizationCatalogAsset CreateOrUpdateCategoryCatalog(CustomizationCategory category, List<CustomizationEntry> entries)
        {
            var path = $"{CatalogsRoot}/{category}Catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CustomizationCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            var so = new SerializedObject(catalog);
            so.FindProperty("category").enumValueIndex = (int)category;
            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            so.ApplyModifiedPropertiesWithoutUndo();
            // entries는 List<CustomizationEntry>인데 SerializedProperty로 row별 set 어려움.
            // 대신 reflection으로 직접 list 교체 후 setdirty.
            SetPrivate(catalog, "entries", entries);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
