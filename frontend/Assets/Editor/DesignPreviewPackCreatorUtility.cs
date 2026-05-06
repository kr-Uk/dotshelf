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

        // 5 테마별 샘플 placeholder. 상단 메뉴 'Dotshelf > Theme > Filter Inventory By > {Theme}' 와
        // RoomEditThemeFilter 의 ActiveTheme 값과 일치하는 ThemeTag 로 entry 에 박힌다.
        // 각 테마당 카테고리별 1 entry 생성 — 무테마(legacy) entry 와 별개로 누적되며,
        // 같은 assetId 면 idempotent 로 갱신.
        static readonly string[] ThemeNames = { "Natural", "Classic", "Modern", "Vintage", "Pastel" };

        static readonly Color[] ThemeWallColors =
        {
            HexColor("#E5DCC5"),  // Natural — warm sand
            HexColor("#C9B79A"),  // Classic — mahogany cream
            HexColor("#ECECEC"),  // Modern — cool grey
            HexColor("#C9B099"),  // Vintage — sepia
            HexColor("#FCE4EC"),  // Pastel — pink
        };

        // 테마별 내부조명 spec — 요구사항 #3 분위기에 맞춰 color/kelvin/intensity 차별화.
        //   Natural   : 따뜻하고 부드러운 자연광 (warm cream 4500K, 표준 강도)
        //   Classic   : 따뜻한 샹들리에 (wheat 3400K, 살짝 강함)
        //   Modern    : 깔끔하고 약간 차가운 백색광 (cool white 6500K, 표준 강도)
        //   Vintage   : 주황빛 낮은 조도 (burlywood 3000K, 약함)
        //   Pastel    : 밝고 부드러운 확산광 (rose 5800K, 강함)
        static readonly (Color color, float kelvin, float intensity)[] ThemeInteriorLights =
        {
            (HexColor("#FFE4B5"), 4500f, 1.0f),  // Natural
            (HexColor("#F5DEB3"), 3400f, 1.2f),  // Classic — chandelier
            (HexColor("#F0F8FF"), 6500f, 1.1f),  // Modern
            (HexColor("#DEB887"), 3000f, 0.7f),  // Vintage — low orange
            (HexColor("#FFE4F0"), 5800f, 1.3f),  // Pastel — bright soft
        };

        // 테마별 외부조명 spec — Natural 은 외부광 영향 강함 (요구사항 #2/#3).
        //   Natural   : 1.6 (강함), 5200K
        //   Classic   : 0.9, 4200K (gold hour)
        //   Modern    : 1.2, 6800K (overcast cool)
        //   Vintage   : 0.7 (약함), 3300K
        //   Pastel    : 1.0, 5500K
        static readonly (Color color, float kelvin, float intensity)[] ThemeExteriorLights =
        {
            (HexColor("#FFF1D8"), 5200f, 1.6f),  // Natural — strong exterior
            (HexColor("#FFE7B0"), 4200f, 0.9f),  // Classic
            (HexColor("#E0EAFF"), 6800f, 1.2f),  // Modern
            (HexColor("#E8C99B"), 3300f, 0.7f),  // Vintage
            (HexColor("#FFD4E5"), 5500f, 1.0f),  // Pastel
        };

        [MenuItem("Dotshelf/Design Preview/Generate Placeholder Catalogs")]
        public static void GeneratePlaceholders()
        {
            EnsureDir(MaterialsRoot);
            EnsureDir(CatalogsRoot);

            // Wallpaper 카탈로그 — 머티리얼 5색 (무테마 — 기존 호환)
            var wallpaperEntries = new List<CustomizationEntry>();
            int idCounter = 100001;
            foreach (var (name, color) in PlaceholderSpecs[0].swatches)
            {
                var mat = CreateOrUpdateMaterial($"Wallpaper_{name}", color, 0.08f);
                wallpaperEntries.Add(BuildMaterialEntry(idCounter++, $"벽지 {name}", mat));
            }
            // 5 테마별 wallpaper sample 추가. assetId range 200001-200005, ThemeTag 박힘.
            for (int t = 0; t < ThemeNames.Length; t++)
            {
                var themeName = ThemeNames[t];
                var mat = CreateOrUpdateMaterial($"Wallpaper_{themeName}", ThemeWallColors[t], 0.08f);
                wallpaperEntries.Add(BuildMaterialEntry(200001 + t, $"{themeName} 벽지", mat, themeTag: themeName));
            }
            CreateOrUpdateCategoryCatalog(CustomizationCategory.Wallpaper, wallpaperEntries);

            // InteriorLight — color/kelvin/intensity 프리셋 3종 (무테마)
            var interiorEntries = new List<CustomizationEntry>();
            idCounter = 100021;
            foreach (var swatch in InteriorLightSwatches)
            {
                interiorEntries.Add(BuildLightEntry(idCounter++, $"내부조명 {swatch.name}", swatch.color, swatch.kelvin, swatch.intensity));
            }
            // 5 테마별 internal light sample. assetId 200011-200015. local preview 전용 (PATCH skip).
            for (int t = 0; t < ThemeNames.Length; t++)
            {
                var themeName = ThemeNames[t];
                var spec = ThemeInteriorLights[t];
                interiorEntries.Add(BuildLightEntry(200011 + t, $"{themeName} 내부조명", spec.color, spec.kelvin, spec.intensity, themeTag: themeName));
            }
            CreateOrUpdateCategoryCatalog(CustomizationCategory.InteriorLight, interiorEntries);

            // ExteriorLight — 시간대 프리셋 3종 (무테마)
            var exteriorEntries = new List<CustomizationEntry>();
            idCounter = 100031;
            foreach (var swatch in ExteriorLightSwatches)
            {
                exteriorEntries.Add(BuildLightEntry(idCounter++, $"외부조명 {swatch.name}", swatch.color, swatch.kelvin, swatch.intensity));
            }
            // 5 테마별 external light sample. assetId 200021-200025. Natural 은 외부광 영향 강함.
            for (int t = 0; t < ThemeNames.Length; t++)
            {
                var themeName = ThemeNames[t];
                var spec = ThemeExteriorLights[t];
                exteriorEntries.Add(BuildLightEntry(200021 + t, $"{themeName} 외부조명", spec.color, spec.kelvin, spec.intensity, themeTag: themeName));
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

        static CustomizationEntry BuildMaterialEntry(long assetId, string displayName, Material material, string themeTag = null)
        {
            var entry = new CustomizationEntry();
            var so = new SerializedObject(ScriptableObject.CreateInstance<CustomizationCatalogAsset>());
            // SerializedObject로 entry private 필드를 set 못하므로(struct/class wrapper 안 됨),
            // 대신 reflection으로 직접 set. 단순 placeholder라 reflection 비용 무시.
            SetPrivate(entry, "assetId", assetId);
            SetPrivate(entry, "displayName", displayName);
            SetPrivate(entry, "materials", new[] { material });
            if (!string.IsNullOrEmpty(themeTag))
            {
                SetPrivate(entry, "themeTag", themeTag);
            }
            // 이 utility 가 만드는 모든 entry 는 사용자 inventory 에 없는 placeholder — PATCH 시 403 회피용.
            SetPrivate(entry, "isLocalPreview", true);
            return entry;
        }

        static CustomizationEntry BuildLightEntry(long assetId, string displayName, Color color, float kelvin, float intensity, string themeTag = null)
        {
            var entry = new CustomizationEntry();
            SetPrivate(entry, "assetId", assetId);
            SetPrivate(entry, "displayName", displayName);
            SetPrivate(entry, "lightColor", color);
            SetPrivate(entry, "colorTemperatureKelvin", kelvin);
            SetPrivate(entry, "lightIntensity", intensity);
            if (!string.IsNullOrEmpty(themeTag))
            {
                SetPrivate(entry, "themeTag", themeTag);
            }
            SetPrivate(entry, "isLocalPreview", true);
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
