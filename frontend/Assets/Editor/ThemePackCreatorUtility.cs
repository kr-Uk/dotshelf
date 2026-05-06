using System.IO;
using System.Linq;
using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
using Dotshelf.Scripts.RoomTheme;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dotshelf.EditorTools
{
    public static class ThemePackCreatorUtility
    {
        const string MainRoomScenePath = "Assets/Scenes/MainRoomScene.unity";
        const string MaterialsRoot = "Assets/App/Composition/Materials/Themes";
        const string ThemesRoot = "Assets/App/Composition/Configs/RoomThemes";
        const string ResourcesRoot = "Assets/Resources";
        const string CatalogPath = "Assets/Resources/RoomThemeCatalog.asset";

        // GoodVibes palette (reference.jpg)
        static readonly Color GoodVibesWall      = HexColor("#FAF3E5");
        static readonly Color GoodVibesFloor     = HexColor("#E8D4B8");
        static readonly Color GoodVibesBookshelf = HexColor("#FAFAFA");
        static readonly Color GoodVibesFurniture = HexColor("#FAFAFA");
        static readonly Color GoodVibesAccent    = HexColor("#FFD773");
        static readonly Color GoodVibesLightCol  = HexColor("#FFF1D8");
        static readonly Color GoodVibesAmbient   = HexColor("#FFF8EC");

        // ─────────────────────────────────────────────────────────────────────
        // Light Preview Themes — 가구/벽지/바닥 미제작 단계의 placeholder 테마.
        //
        // 목적: 오브젝트 편집 모드의 '전체 테마' 카테고리에 Natural/Classic/Modern/Vintage/Pastel
        // 6개 테마(+기본)를 노출하되, 클릭 시 백엔드 PATCH 를 발사하지 않고 RoomThemeApplier 의
        // 라이팅 swap 만 수행 (assetId=0 + isLocalPreview=true → RoomEditModeController 가 PATCH skip).
        //
        // 머티리얼 필드는 null — RoomThemeApplier 가 null 머티리얼은 현재 상태 유지 (가구/벽지/바닥은
        // 변경 안 됨). 라이팅 만 테마 분위기로 갱신.
        //
        // 같은 메뉴 재실행 시 idempotent — 같은 fileName 의 RoomThemeAsset 갱신 + catalog dedupe.
        [MenuItem("Dotshelf/Theme/Create Or Refresh Light Preview Themes")]
        public static void CreateLightPreviewPack()
        {
            EnsureDir(ThemesRoot);
            EnsureDir(ResourcesRoot);

            var newThemes = new System.Collections.Generic.List<RoomThemeAsset>();

            // '기본' 테마 — 카탈로그 첫 항목 보장. CreatePack(GoodVibes 메뉴)이 이미 머티리얼 포함 Default 를
            // 생성했으면 그 자산을 보존(load 만) — overwrite 안 함. 없으면 light-only placeholder 로 새로 생성.
            const string defaultThemePath = ThemesRoot + "/RoomTheme_Default.asset";
            var defaultTheme = AssetDatabase.LoadAssetAtPath<RoomThemeAsset>(defaultThemePath);
            if (defaultTheme == null)
            {
                defaultTheme = CreateOrLoadLightPreviewTheme(
                    fileName: "RoomTheme_Default",
                    displayName: "기본",
                    description: "기본 제공 테마 (placeholder — 라이팅 중립).",
                    interiorColor: Color.white, interiorKelvin: 6500f, ambientIntensity: 1.0f,
                    exteriorColor: Color.white, exteriorKelvin: 6500f, exteriorIntensity: 1.0f);
            }
            newThemes.Add(defaultTheme);

            // 5 placeholder 테마 — CreatePack 과 동일 spec (helper 재사용).
            newThemes.AddRange(BuildLightPreviewThemes());

            EnsureCatalogContainsThemes(newThemes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ThemePackCreator] Light preview themes ready: {string.Join(", ", newThemes.ConvertAll(t => t.DisplayName))}. " +
                      "Resources/RoomThemeCatalog.asset 에 추가됨. 클릭 시 라이팅만 적용 (PATCH skip).");
        }

        // 가구/벽지/바닥 미제작 placeholder 라 머티리얼 일체 null. 라이팅 + isLocalPreview 만 set.
        // assetId 는 0 — RoomEditModeController.ApplyThemeSelected 가 AssetId<=0 면 PATCH skip.
        static RoomThemeAsset CreateOrLoadLightPreviewTheme(
            string fileName, string displayName, string description,
            Color interiorColor, float interiorKelvin, float ambientIntensity,
            Color exteriorColor, float exteriorKelvin, float exteriorIntensity)
        {
            var path = $"{ThemesRoot}/{fileName}.asset";
            var theme = AssetDatabase.LoadAssetAtPath<RoomThemeAsset>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<RoomThemeAsset>();
                AssetDatabase.CreateAsset(theme, path);
            }

            var so = new SerializedObject(theme);
            so.FindProperty("assetId").longValue = 0;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("bookshelfStyleIndex").intValue = 0;
            // 머티리얼 필드는 의도적으로 건드리지 않음 (null 유지) — RoomThemeApplier 가 null 이면 기본값 fallback.
            so.FindProperty("interiorLightColor").colorValue = interiorColor;
            so.FindProperty("interiorLightKelvin").floatValue = interiorKelvin;
            // ambientTint = interiorColor (RoomThemeApplier 의 ApplyLighting 이 ambientTint 를 RenderSettings.ambientLight 에 넣음)
            so.FindProperty("ambientTint").colorValue = interiorColor;
            so.FindProperty("ambientIntensity").floatValue = ambientIntensity;
            so.FindProperty("exteriorLightColor").colorValue = exteriorColor;
            so.FindProperty("exteriorLightKelvin").floatValue = exteriorKelvin;
            so.FindProperty("exteriorLightIntensity").floatValue = exteriorIntensity;
            so.FindProperty("isLocalPreview").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        // 기존 catalog 의 entries 와 합쳐 dedupe. defaultTheme 은 보존 (없으면 첫 entry 로 set).
        static void EnsureCatalogContainsThemes(System.Collections.Generic.List<RoomThemeAsset> additions)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomThemeCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RoomThemeCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var so = new SerializedObject(catalog);
            var entriesProp = so.FindProperty("entries");

            // 기존 entries 수집
            var existing = new System.Collections.Generic.List<RoomThemeAsset>();
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i).objectReferenceValue as RoomThemeAsset;
                if (entry != null) existing.Add(entry);
            }

            // 추가 — reference dedupe
            foreach (var t in additions)
            {
                if (!existing.Contains(t)) existing.Add(t);
            }

            entriesProp.arraySize = existing.Count;
            for (int i = 0; i < existing.Count; i++)
            {
                entriesProp.GetArrayElementAtIndex(i).objectReferenceValue = existing[i];
            }

            // defaultTheme 미설정이면 첫 entry 를 default 로
            var defaultProp = so.FindProperty("defaultTheme");
            if (defaultProp.objectReferenceValue == null && existing.Count > 0)
            {
                defaultProp.objectReferenceValue = existing[0];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        [MenuItem("Dotshelf/Theme/Create Or Refresh GoodVibes Pack")]
        public static void CreatePack()
        {
            EnsureDir(MaterialsRoot + "/GoodVibes");
            EnsureDir(ThemesRoot);
            EnsureDir(ResourcesRoot);

            // 1) GoodVibes 머티리얼 5종
            var matWall      = CreateOrUpdateMaterial("GoodVibes_Wall",      GoodVibesWall,      0.08f);
            var matFloor     = CreateOrUpdateMaterial("GoodVibes_Floor",     GoodVibesFloor,     0.28f);
            var matBookshelf = CreateOrUpdateMaterial("GoodVibes_Bookshelf", GoodVibesBookshelf, 0.42f);
            var matFurniture = CreateOrUpdateMaterial("GoodVibes_Furniture", GoodVibesFurniture, 0.42f);
            var matAccent    = CreateOrUpdateMaterial("GoodVibes_Accent",    GoodVibesAccent,    0.30f);

            // 2) 기본 테마 — 머티리얼 모두 null이라 RoomThemeApplier가 swap skip → 현재 룸 색감 유지
            var defaultTheme = CreateOrLoadTheme(
                fileName: "RoomTheme_Default",
                displayName: "기본",
                description: "기본 제공 테마. 현재 룸 색감 그대로.",
                assetId: 0,
                bookshelfStyleIndex: 0,
                wall: null, floor: null, bookshelf: null, furniture: null, accent: null,
                lightColor: Color.white, kelvin: 6500f,
                ambient: Color.white, ambientIntensity: 1f,
                accentPrimary: HexColor("#D8CCBC"),
                accentSecondary: HexColor("#7C6A56"),
                accentTertiary: HexColor("#FFFFFF"));

            // 3) 산뜻 테마 — GoodVibes 머티리얼 + warm light
            var goodVibes = CreateOrLoadTheme(
                fileName: "RoomTheme_GoodVibes",
                displayName: "산뜻",
                description: "따뜻한 크림 + 화이트 + 노랑 액센트의 산뜻한 분위기.",
                assetId: 1001,
                bookshelfStyleIndex: 0,
                wall: matWall, floor: matFloor, bookshelf: matBookshelf, furniture: matFurniture, accent: matAccent,
                lightColor: GoodVibesLightCol, kelvin: 5200f,
                ambient: GoodVibesAmbient, ambientIntensity: 1.05f,
                accentPrimary: GoodVibesAccent,
                accentSecondary: HexColor("#FFC1B4"),
                accentTertiary: HexColor("#A8D5A2"));

            // 4) 카탈로그 (Resources/)
            var catalog = CreateOrLoadCatalog(defaultTheme, defaultTheme, goodVibes);

            // 5) MainRoomScene에 RoomThemeApplier 추가 + reference 연결
            EnsureApplierInScene();

            // 6) Natural / Classic / Modern / Vintage / Pastel placeholder 테마 보장.
            //    이전엔 별도 메뉴(Create Or Refresh Light Preview Themes) 에서만 생성됐으나, 사용자가 GoodVibes
            //    메뉴 한 번 클릭으로 7 테마(기본+산뜻+5) 가 모두 카탈로그에 반영되도록 통합. 가구/벽지/바닥 미적용
            //    (머티리얼 null) + isLocalPreview=true → 클릭 시 RoomThemeApplier 라이팅만 영향, PATCH skip.
            //    같은 메뉴 재실행 idempotent — 같은 fileName RoomThemeAsset 갱신 + EnsureCatalogContainsThemes dedupe.
            var themedPreviews = BuildLightPreviewThemes();
            EnsureCatalogContainsThemes(themedPreviews);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // catalog.Entries.Count 는 reload 후 정확. 7 테마 (Default + GoodVibes + 5 placeholder) 가 표준.
            var refreshedCatalog = AssetDatabase.LoadAssetAtPath<RoomThemeCatalogAsset>(CatalogPath);
            Debug.Log($"[ThemePackCreator] Pack ready. Catalog at {CatalogPath} (entries={refreshedCatalog?.Entries?.Count ?? -1}). " +
                      $"기본/산뜻/Natural/Classic/Modern/Vintage/Pastel 모두 등록. 'THEME_FULL' toolbar 카테고리에서 확인.");
        }

        // CreatePack + CreateLightPreviewPack 양쪽에서 재사용. Natural/Classic/Modern/Vintage/Pastel 5개 returns.
        static System.Collections.Generic.List<RoomThemeAsset> BuildLightPreviewThemes()
        {
            EnsureDir(ThemesRoot);
            var specs = new[]
            {
                ("Natural", "Natural",
                    HexColor("#FFE4B5"), 4500f, 1.0f,
                    HexColor("#FFF1D8"), 5200f, 1.6f),
                ("Classic", "Classic",
                    HexColor("#F5DEB3"), 3400f, 1.2f,
                    HexColor("#FFE7B0"), 4200f, 0.9f),
                ("Modern", "Modern",
                    HexColor("#F0F8FF"), 6500f, 1.1f,
                    HexColor("#E0EAFF"), 6800f, 1.2f),
                ("Vintage", "Vintage",
                    HexColor("#DEB887"), 3000f, 0.7f,
                    HexColor("#E8C99B"), 3300f, 0.7f),
                ("Pastel", "Pastel",
                    HexColor("#FFE4F0"), 5800f, 1.3f,
                    HexColor("#FFD4E5"), 5500f, 1.0f),
            };
            var list = new System.Collections.Generic.List<RoomThemeAsset>();
            foreach (var s in specs)
            {
                list.Add(CreateOrLoadLightPreviewTheme(
                    fileName: $"RoomTheme_{s.Item1}",
                    displayName: s.Item2,
                    description: $"{s.Item2} 라이트 프리뷰 (placeholder — 가구/벽지/바닥 미적용).",
                    interiorColor: s.Item3, interiorKelvin: s.Item4, ambientIntensity: s.Item5,
                    exteriorColor: s.Item6, exteriorKelvin: s.Item7, exteriorIntensity: s.Item8));
            }
            return list;
        }

        static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        static Material CreateOrUpdateMaterial(string name, Color color, float smoothness)
        {
            var path = $"{MaterialsRoot}/GoodVibes/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError($"[ThemePackCreator] 'Universal Render Pipeline/Lit' shader 못 찾음 — URP 패키지 미설치 또는 shader 이름 변경. 머티리얼 '{name}' 생성 skip.");
                return mat;
            }
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static RoomThemeAsset CreateOrLoadTheme(
            string fileName, string displayName, string description, long assetId, int bookshelfStyleIndex,
            Material wall, Material floor, Material bookshelf, Material furniture, Material accent,
            Color lightColor, float kelvin,
            Color ambient, float ambientIntensity,
            Color accentPrimary, Color accentSecondary, Color accentTertiary)
        {
            var path = $"{ThemesRoot}/{fileName}.asset";
            var theme = AssetDatabase.LoadAssetAtPath<RoomThemeAsset>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<RoomThemeAsset>();
                AssetDatabase.CreateAsset(theme, path);
            }

            var so = new SerializedObject(theme);
            so.FindProperty("assetId").longValue = assetId;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("bookshelfStyleIndex").intValue = bookshelfStyleIndex;
            so.FindProperty("wallMaterial").objectReferenceValue = wall;
            so.FindProperty("floorMaterial").objectReferenceValue = floor;
            so.FindProperty("bookshelfMaterial").objectReferenceValue = bookshelf;
            so.FindProperty("furnitureMaterial").objectReferenceValue = furniture;
            so.FindProperty("accentMaterial").objectReferenceValue = accent;
            // RoomThemeAsset 의 실제 SerializeField 명. 옛 'directionalLight*' 는 getter 만 alias 로 남아있어
            // SerializedObject 로는 접근 불가 → 실제 필드명 사용. 이 줄이 직접적으로 CreatePack 메뉴의 NRE 를 막음.
            so.FindProperty("exteriorLightColor").colorValue = lightColor;
            so.FindProperty("exteriorLightKelvin").floatValue = kelvin;
            so.FindProperty("ambientTint").colorValue = ambient;
            so.FindProperty("ambientIntensity").floatValue = ambientIntensity;
            so.FindProperty("accentPrimary").colorValue = accentPrimary;
            so.FindProperty("accentSecondary").colorValue = accentSecondary;
            so.FindProperty("accentTertiary").colorValue = accentTertiary;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        static RoomThemeCatalogAsset CreateOrLoadCatalog(RoomThemeAsset defaultTheme, params RoomThemeAsset[] entries)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomThemeCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RoomThemeCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var so = new SerializedObject(catalog);
            so.FindProperty("defaultTheme").objectReferenceValue = defaultTheme;

            var entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                entriesProp.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static void EnsureApplierInScene()
        {
            var scene = EnsureMainRoomSceneOpen();
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[ThemePackCreator] {MainRoomScenePath} 열기 실패 — applier 자동 추가 skip.");
                return;
            }

            var applier = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<RoomThemeApplier>(true))
                .FirstOrDefault();

            GameObject host = null;
            if (applier == null)
            {
                // BookshelfSystems GameObject 위에 추가 (BookshelfExperienceManager 옆)
                host = scene.GetRootGameObjects()
                    .FirstOrDefault(g => g.name == "BookshelfSystems");
                if (host == null)
                {
                    Debug.LogWarning("[ThemePackCreator] 'BookshelfSystems' GameObject 못 찾음 — applier 자동 추가 skip.");
                    return;
                }
                applier = host.AddComponent<RoomThemeApplier>();
                Debug.Log($"[ThemePackCreator] RoomThemeApplier 컴포넌트 추가 → {host.name}");
            }
            else
            {
                host = applier.gameObject;
            }

            // SerializedObject 통해 reference 연결
            var so = new SerializedObject(applier);

            // roomRoot — BookshelfGenerator.roomRoot 필드 따라가거나 'RoomRoot' 이름 GameObject
            var roomRoot = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "RoomRoot");
            if (roomRoot != null)
            {
                so.FindProperty("roomRoot").objectReferenceValue = roomRoot;
            }

            // materialFactory — BookshelfSystems의 MaterialFactory
            var factory = host.GetComponentInChildren<MaterialFactory>(true)
                       ?? Object.FindFirstObjectByType<MaterialFactory>();
            if (factory != null)
            {
                so.FindProperty("materialFactory").objectReferenceValue = factory;
            }

            // directionalLight — 씬의 첫 Directional Light
            var light = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(l => l.type == LightType.Directional);
            if (light != null)
            {
                so.FindProperty("directionalLight").objectReferenceValue = light;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(applier);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ThemePackCreator] Applier wired — roomRoot={roomRoot?.name}, factory={factory?.name}, light={light?.name}");
        }

        static Scene EnsureMainRoomSceneOpen()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == MainRoomScenePath && s.isLoaded) return s;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
            return EditorSceneManager.OpenScene(MainRoomScenePath, OpenSceneMode.Single);
        }

        static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
