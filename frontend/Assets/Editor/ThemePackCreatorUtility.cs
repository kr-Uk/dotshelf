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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ThemePackCreator] Pack ready. Catalog at {CatalogPath} (entries={catalog.Entries.Count}). " +
                      $"MainRoomScene RoomThemeApplier wired. 'THEME_FULL' toolbar 카테고리에서 동작.");
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
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader && shader != null)
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
            so.FindProperty("directionalLightColor").colorValue = lightColor;
            so.FindProperty("directionalLightKelvin").floatValue = kelvin;
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
