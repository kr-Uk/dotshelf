using System.IO;
using System.Collections.Generic;
using App.Infrastructure.Catalogs;
using Dotshelf.Scripts.RoomTheme;
using UnityEditor;
using UnityEngine;

namespace Dotshelf.EditorTools
{
    public static class DeepSeaThemeCreatorUtility
    {
        private const string MaterialsRoot = "Assets/App/Composition/Materials/Themes/DeepSea";
        private const string ThemesRoot = "Assets/App/Composition/Configs/RoomThemes";
        private const string CatalogPath = "Assets/Resources/RoomThemeCatalog.asset";
        private const string DeskCatalogPath = "Assets/Resources/Catalogs/DeskCatalog.asset";
        private const string BookshelfSkinCatalogPath = "Assets/Resources/Catalogs/BookshelfSkinCatalog.asset";
        private const string DashboardSkinCatalogPath = "Assets/Resources/Catalogs/DashboardSkinCatalog.asset";

        private const long DeepSeaBookshelfAssetId = 300020L;
        private const long DeepSeaDeskAssetId = 300030L;
        private const long DeepSeaDashboardAssetId = 300040L;

        private const string WallTexturePath = "Assets/Arts/Textures/Environment/Wallpaper/deepsea_wallpaper2.png";
        private const string FloorTexturePath = "Assets/Arts/Textures/Environment/Floor/deepsea_floor2.png";

        private const string ModelRoot = "Assets/Arts/Models";
        private const string GoodVibesMonitorPath = ModelRoot + "/GoodVibes_Monitor.glb";

        [MenuItem("Dotshelf/Theme/Create Or Refresh DeepSea Pack")]
        public static void CreateOrRefreshDeepSeaPack()
        {
            EnsureDir(MaterialsRoot);
            EnsureDir(ThemesRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var wallTexture = LoadRequired<Texture2D>(WallTexturePath);
            var floorTexture = LoadRequired<Texture2D>(FloorTexturePath);

            var wall = CreateOrUpdateMaterial(
                "DeepSea_Wall",
                HexColor("#C7EDF2"),
                0.12f,
                wallTexture,
                new Vector2(1.25f, 1.25f));
            var floor = CreateOrUpdateMaterial(
                "DeepSea_Floor",
                HexColor("#D6F4F5"),
                0.34f,
                floorTexture,
                new Vector2(2.4f, 2.4f));
            var bookshelf = CreateOrUpdateSolidMaterial("DeepSea_Bookshelf", HexColor("#1E5F73"), 0.4f);
            var furniture = CreateOrUpdateSolidMaterial("DeepSea_Furniture", HexColor("#184C66"), 0.36f);
            var accent = CreateOrUpdateSolidMaterial("DeepSea_Accent", HexColor("#F2A7A0"), 0.26f);
            var dashboard = CreateOrUpdateSolidMaterial("DeepSea_Dashboard", HexColor("#123A52"), 0.18f);

            EnsureDeepSeaBookshelfEntry();
            EnsureDeepSeaDeskEntry();
            EnsureDeepSeaDashboardEntry();

            var theme = CreateOrLoadTheme(
                wall,
                floor,
                bookshelf,
                furniture,
                accent,
                dashboard);
            EnsureCatalogContainsTheme(theme);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DeepSeaThemeCreator] DeepSea theme pack refreshed. Select '깊은바다' in 전체테마 to apply wall, floor, desk, and default placements.");
        }

        [MenuItem("Dotshelf/Theme/Capture Current DeepSea Placement")]
        public static void CaptureCurrentDeepSeaPlacement()
        {
            const string themePath = ThemesRoot + "/RoomTheme_DeepSea.asset";
            var theme = LoadRequired<RoomThemeAsset>(themePath);
            var capturedSpecs = new List<DecoSpec>();

            foreach (var spec in FloorSpecs())
            {
                var sceneObject = FindSceneGameObject("ThemeDeco_Floor_" + spec.PrefabName);
                if (sceneObject == null)
                {
                    Debug.LogWarning($"[DeepSeaThemeCreator] Theme deco not found in scene: {spec.PrefabName}. Keeping existing placement.");
                    capturedSpecs.Add(spec);
                    continue;
                }

                capturedSpecs.Add(spec.WithTransform(
                    sceneObject.transform.position,
                    sceneObject.transform.eulerAngles,
                    sceneObject.transform.localScale));
            }

            var themeObject = new SerializedObject(theme);
            SetDecoSpecs(themeObject.FindProperty("floorDecoSpecs"), capturedSpecs.ToArray());
            SetDecoSpecs(themeObject.FindProperty("wallDecoSpecs"), WallSpecs());
            themeObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);

            CaptureCatalogEntryTransform(DeskCatalogPath, DeepSeaDeskAssetId, "MeshSwap_Desk");
            CaptureCatalogEntryTransform(BookshelfSkinCatalogPath, DeepSeaBookshelfAssetId, "MeshSwap_BookshelfSkin");
            CaptureCatalogEntryTransform(DashboardSkinCatalogPath, DeepSeaDashboardAssetId, "MeshSwap_DashboardSkin");
            CaptureDeskMonitorTransform();
            CaptureNotebookStationOverlayTransform();
            CaptureDashboardBoardOverlayTransform();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DeepSeaThemeCreator] Captured current DeepSea scene placement into RoomTheme_DeepSea.asset and DeepSea catalog entries.");
        }

        [MenuItem("Dotshelf/Theme/Capture Current Fantasy Placement")]
        public static void CaptureCurrentFantasyPlacement()
        {
            CaptureCurrentThemePlacement("Fantasy", ThemesRoot + "/RoomTheme_Fantasy.asset");
        }

        [MenuItem("Dotshelf/Theme/Capture Current Space Placement")]
        public static void CaptureCurrentSpacePlacement()
        {
            CaptureCurrentThemePlacement("Space", ThemesRoot + "/RoomTheme_Space.asset");
        }

        [MenuItem("Dotshelf/Theme/Capture Current Spooky Placement")]
        public static void CaptureCurrentSpookyPlacement()
        {
            CaptureCurrentThemePlacement("Spooky", ThemesRoot + "/RoomTheme_Spooky.asset");
        }

        private static void CaptureCurrentThemePlacement(string themeName, string themePath)
        {
            var theme = LoadRequired<RoomThemeAsset>(themePath);
            var themeObject = new SerializedObject(theme);
            var roomThemeApplier = FindSceneComponent<RoomThemeApplier>();

            CaptureDecoSpecArray(
                themeObject.FindProperty("floorDecoSpecs"),
                "ThemeDeco_Floor_",
                false,
                roomThemeApplier,
                themeName);
            CaptureDecoSpecArray(
                themeObject.FindProperty("wallDecoSpecs"),
                "ThemeDeco_Wall_",
                true,
                roomThemeApplier,
                themeName);

            themeObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DeepSeaThemeCreator] Captured current {themeName} scene placement into {Path.GetFileName(themePath)}.");
        }

        private static void CaptureDecoSpecArray(
            SerializedProperty specs,
            string objectNamePrefix,
            bool convertWallAnchorSpace,
            RoomThemeApplier roomThemeApplier,
            string themeName)
        {
            if (specs == null) return;

            for (int i = 0; i < specs.arraySize; i++)
            {
                var spec = specs.GetArrayElementAtIndex(i);
                var prefabName = ResolveDecoSpecPrefabName(spec);
                if (string.IsNullOrWhiteSpace(prefabName))
                {
                    Debug.LogWarning($"[DeepSeaThemeCreator] {themeName} deco spec has no prefab at index {i}. Keeping existing placement.");
                    continue;
                }

                var sceneObjectName = objectNamePrefix + SanitizeName(prefabName);
                var sceneObject = FindSceneGameObject(sceneObjectName);
                if (sceneObject == null)
                {
                    Debug.LogWarning($"[DeepSeaThemeCreator] {themeName} theme deco not found in scene: {sceneObjectName}. Keeping existing placement.");
                    continue;
                }

                if (convertWallAnchorSpace && TryCaptureWallLocalTransform(spec, sceneObject.transform, roomThemeApplier))
                {
                    SetVector(spec, "localScale", sceneObject.transform.localScale);
                    continue;
                }

                SetVector(spec, "localPosition", sceneObject.transform.position);
                SetVector(spec, "localRotation", sceneObject.transform.eulerAngles);
                SetVector(spec, "localScale", sceneObject.transform.localScale);
            }
        }

        private static string ResolveDecoSpecPrefabName(SerializedProperty spec)
        {
            var prefabProp = spec.FindPropertyRelative("prefab");
            if (prefabProp?.objectReferenceValue is GameObject prefab)
            {
                return prefab.name;
            }

            var assetKey = spec.FindPropertyRelative("assetKey")?.stringValue;
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                return null;
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetKey);
            if (prefab != null)
            {
                SetObject(prefabProp, prefab);
                return prefab.name;
            }

            return Path.GetFileNameWithoutExtension(assetKey);
        }

        private static bool TryCaptureWallLocalTransform(
            SerializedProperty spec,
            Transform sceneTransform,
            RoomThemeApplier roomThemeApplier)
        {
            if (roomThemeApplier == null) return false;

            var wallSideProp = spec.FindPropertyRelative("wallSide");
            if (wallSideProp == null) return false;

            var wallSide = (WallSide)wallSideProp.enumValueIndex;
            if (!roomThemeApplier.TryGetWallSurfaceAnchor(wallSide, out var anchorPosition, out var anchorRotation))
            {
                return false;
            }

            var inverseAnchorRotation = Quaternion.Inverse(anchorRotation);
            SetVector(spec, "localPosition", inverseAnchorRotation * (sceneTransform.position - anchorPosition));
            SetVector(spec, "localRotation", (inverseAnchorRotation * sceneTransform.rotation).eulerAngles);
            return true;
        }

        private static RoomThemeAsset CreateOrLoadTheme(
            Material wall,
            Material floor,
            Material bookshelf,
            Material furniture,
            Material accent,
            Material dashboard)
        {
            const string path = ThemesRoot + "/RoomTheme_DeepSea.asset";
            var theme = AssetDatabase.LoadAssetAtPath<RoomThemeAsset>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<RoomThemeAsset>();
                AssetDatabase.CreateAsset(theme, path);
            }

            var so = new SerializedObject(theme);
            SetLong(so, "assetId", 0L);
            SetString(so, "displayName", "깊은바다");
            SetString(so, "description", "깊은 바다 질감과 산호, 해양 생물 소품을 배치하는 로컬 프리뷰 테마.");
            SetInt(so, "bookshelfStyleIndex", 0);
            SetObject(so, "wallMaterial", wall);
            SetObject(so, "floorMaterial", floor);
            SetObject(so, "bookshelfMaterial", bookshelf);
            SetObject(so, "furnitureMaterial", furniture);
            SetObject(so, "deskTopMaterial", furniture);
            SetObject(so, "chairMaterial", furniture);
            SetObject(so, "dashboardMaterial", dashboard);
            SetObject(so, "accentMaterial", accent);
            SetColor(so, "interiorLightColor", HexColor("#BDEFFF"));
            SetFloat(so, "interiorLightKelvin", 7200f);
            SetColor(so, "ambientTint", HexColor("#A9E8F7"));
            SetFloat(so, "ambientIntensity", 1.12f);
            SetColor(so, "exteriorLightColor", HexColor("#89CFE8"));
            SetFloat(so, "exteriorLightKelvin", 7600f);
            SetFloat(so, "exteriorLightIntensity", 1.25f);
            SetBool(so, "allowsCustomization", true);
            SetBool(so, "isLocalPreview", true);
            SetColor(so, "accentPrimary", HexColor("#42C9E8"));
            SetColor(so, "accentSecondary", HexColor("#F2A7A0"));
            SetColor(so, "accentTertiary", HexColor("#E6D07A"));

            SetDefaultEntries(so.FindProperty("defaultEntries"));
            SetDecoSpecs(so.FindProperty("bookshelfDecoSpecs"), System.Array.Empty<DecoSpec>());
            SetDecoSpecs(so.FindProperty("floorDecoSpecs"), FloorSpecs());
            SetDecoSpecs(so.FindProperty("wallDecoSpecs"), WallSpecs());

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static void EnsureDeepSeaDeskEntry()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(DeskCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CustomizationCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, DeskCatalogPath);
            }

            var deskPrefab = LoadRequired<GameObject>(ModelRoot + "/deepsea_desk.glb");
            var monitorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoodVibesMonitorPath);

            var so = new SerializedObject(catalog);
            var categoryProp = so.FindProperty("category");
            if (categoryProp != null) categoryProp.enumValueIndex = (int)CustomizationCategory.Desk;

            var entries = so.FindProperty("entries");
            int index = FindEntryIndex(entries, DeepSeaDeskAssetId);
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }

            var entry = entries.GetArrayElementAtIndex(index);
            SetLong(entry, "assetId", DeepSeaDeskAssetId);
            SetString(entry, "displayName", "깊은바다 책상");
            SetString(entry, "description", "깊은바다 테마 전용 책상. 테마 적용 시 기본 책상으로 교체된다.");
            SetObject(entry, "previewSprite", null);
            SetString(entry, "themeTag", "DeepSea");
            SetBool(entry, "isLocalPreview", true);
            var materials = entry.FindPropertyRelative("materials");
            if (materials != null) materials.arraySize = 0;
            SetColor(entry, "lightColor", Color.white);
            SetFloat(entry, "lightIntensity", 1f);
            SetFloat(entry, "colorTemperatureKelvin", 6500f);
            SetObject(entry, "prefab", deskPrefab);
            SetVector(entry, "placementAnchor", new Vector3(-2.03f, 0.35f, -0.36f));
            SetVector(entry, "placementRotation", new Vector3(0f, 270f, 0f));
            SetVector(entry, "placementScale", new Vector3(0.8f, 0.8f, 0.8f));
            SetBool(entry, "overrideNotebookStation", true);
            SetBool(entry, "lockNotebookStationToMonitor", true);
            SetVector(entry, "notebookStationPosition", new Vector3(0f, -0.012f, 0f));
            SetVector(entry, "notebookStationRotation", Vector3.zero);
            SetVector(entry, "notebookStationScale", new Vector3(0.3f, 0.3f, 0.3f));
            SetObject(entry, "monitorPrefab", monitorPrefab);
            SetVector(entry, "monitorPosition", new Vector3(-2.004f, 0.932f, -0.29f));
            SetVector(entry, "monitorRotation", new Vector3(0f, 90f, 0f));
            SetVector(entry, "monitorScale", new Vector3(0.3f, 0.3f, 0.3f));

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureDeepSeaBookshelfEntry()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(BookshelfSkinCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CustomizationCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, BookshelfSkinCatalogPath);
            }

            var shelfPrefab = LoadRequired<GameObject>(ModelRoot + "/deepsea_shelf.glb");

            var so = new SerializedObject(catalog);
            var categoryProp = so.FindProperty("category");
            if (categoryProp != null) categoryProp.enumValueIndex = (int)CustomizationCategory.BookshelfSkin;

            var entries = so.FindProperty("entries");
            int index = FindEntryIndex(entries, DeepSeaBookshelfAssetId);
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }

            var entry = entries.GetArrayElementAtIndex(index);
            SetLong(entry, "assetId", DeepSeaBookshelfAssetId);
            SetString(entry, "displayName", "깊은바다 책장");
            SetString(entry, "description", "깊은바다 테마 전용 책장. 기존 BookshelfRoot 위치를 교체한다.");
            SetObject(entry, "previewSprite", null);
            SetString(entry, "themeTag", "DeepSea");
            SetBool(entry, "isLocalPreview", true);
            var materials = entry.FindPropertyRelative("materials");
            if (materials != null) materials.arraySize = 0;
            SetColor(entry, "lightColor", Color.white);
            SetFloat(entry, "lightIntensity", 1f);
            SetFloat(entry, "colorTemperatureKelvin", 6500f);
            SetObject(entry, "prefab", shelfPrefab);
            SetVector(entry, "placementAnchor", new Vector3(0f, 0.82f, 1.36f));
            SetVector(entry, "placementRotation", new Vector3(0f, 180f, 0f));
            SetVector(entry, "placementScale", new Vector3(0.86f, 0.86f, 0.55f));
            SetBool(entry, "overrideNotebookStation", false);
            SetBool(entry, "lockNotebookStationToMonitor", false);
            SetVector(entry, "notebookStationPosition", Vector3.zero);
            SetVector(entry, "notebookStationRotation", Vector3.zero);
            SetVector(entry, "notebookStationScale", Vector3.one);
            SetObject(entry, "monitorPrefab", null);
            SetVector(entry, "monitorPosition", Vector3.zero);
            SetVector(entry, "monitorRotation", Vector3.zero);
            SetVector(entry, "monitorScale", Vector3.one);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureDeepSeaDashboardEntry()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(DashboardSkinCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CustomizationCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, DashboardSkinCatalogPath);
            }

            var dashboardPrefab = LoadRequired<GameObject>(ModelRoot + "/deepsea_board.glb");

            var so = new SerializedObject(catalog);
            var categoryProp = so.FindProperty("category");
            if (categoryProp != null) categoryProp.enumValueIndex = (int)CustomizationCategory.DashboardSkin;

            var entries = so.FindProperty("entries");
            int index = FindEntryIndex(entries, DeepSeaDashboardAssetId);
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }

            var entry = entries.GetArrayElementAtIndex(index);
            SetLong(entry, "assetId", DeepSeaDashboardAssetId);
            SetString(entry, "displayName", "깊은바다 대시보드");
            SetString(entry, "description", "깊은바다 테마 전용 대시보드 보드. 기존 GuestbookBoard 위치를 교체한다.");
            SetObject(entry, "previewSprite", null);
            SetString(entry, "themeTag", "DeepSea");
            SetBool(entry, "isLocalPreview", true);
            var materials = entry.FindPropertyRelative("materials");
            if (materials != null) materials.arraySize = 0;
            SetColor(entry, "lightColor", Color.white);
            SetFloat(entry, "lightIntensity", 1f);
            SetFloat(entry, "colorTemperatureKelvin", 6500f);
            SetObject(entry, "prefab", dashboardPrefab);
            SetVector(entry, "placementAnchor", new Vector3(-2.382f, 1.497f, 0.12f));
            SetVector(entry, "placementRotation", new Vector3(0f, 90f, 0f));
            SetVector(entry, "placementScale", new Vector3(0.73f, 0.72f, 0.18f));
            SetBool(entry, "overrideNotebookStation", true);
            SetBool(entry, "lockNotebookStationToMonitor", false);
            SetVector(entry, "notebookStationPosition", new Vector3(-2.382f, 1.497f, 0.12f));
            SetVector(entry, "notebookStationRotation", Vector3.zero);
            SetVector(entry, "notebookStationScale", Vector3.one);
            SetObject(entry, "monitorPrefab", null);
            SetVector(entry, "monitorPosition", Vector3.zero);
            SetVector(entry, "monitorRotation", Vector3.zero);
            SetVector(entry, "monitorScale", Vector3.one);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void CaptureCatalogEntryTransform(string catalogPath, long assetId, string sceneObjectName)
        {
            var sceneObject = FindSceneGameObject(sceneObjectName);
            if (sceneObject == null)
            {
                Debug.LogWarning($"[DeepSeaThemeCreator] Mesh swap object not found in scene: {sceneObjectName}. Keeping catalog placement.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(catalogPath);
            if (catalog == null)
            {
                Debug.LogWarning($"[DeepSeaThemeCreator] Catalog not found: {catalogPath}");
                return;
            }

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            var index = FindEntryIndex(entries, assetId);
            if (index < 0)
            {
                Debug.LogWarning($"[DeepSeaThemeCreator] Catalog entry not found: {catalogPath} assetId={assetId}");
                return;
            }

            var entry = entries.GetArrayElementAtIndex(index);
            SetVector(entry, "placementAnchor", sceneObject.transform.position);
            SetVector(entry, "placementRotation", sceneObject.transform.eulerAngles);
            SetVector(entry, "placementScale", sceneObject.transform.localScale);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void CaptureDeskMonitorTransform()
        {
            var sceneObject = FindSceneGameObject("MeshSwap_DeskMonitor");
            if (sceneObject == null) return;

            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(DeskCatalogPath);
            if (catalog == null) return;

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            var index = FindEntryIndex(entries, DeepSeaDeskAssetId);
            if (index < 0) return;

            var entry = entries.GetArrayElementAtIndex(index);
            SetVector(entry, "monitorPosition", sceneObject.transform.position);
            SetVector(entry, "monitorRotation", sceneObject.transform.eulerAngles);
            SetVector(entry, "monitorScale", sceneObject.transform.localScale);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void CaptureNotebookStationOverlayTransform()
        {
            var sceneObject = FindSceneGameObject("NotebookStation");
            if (sceneObject == null)
            {
                Debug.LogWarning("[DeepSeaThemeCreator] NotebookStation not found in scene. Keeping notebook overlay placement.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(DeskCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning($"[DeepSeaThemeCreator] Catalog not found: {DeskCatalogPath}");
                return;
            }

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            var index = FindEntryIndex(entries, DeepSeaDeskAssetId);
            if (index < 0)
            {
                Debug.LogWarning($"[DeepSeaThemeCreator] Catalog entry not found: {DeskCatalogPath} assetId={DeepSeaDeskAssetId}");
                return;
            }

            var monitorObject = FindSceneGameObject("MeshSwap_DeskMonitor");
            var dashboardBoard = FindSceneGameObject("GuestbookBoard");
            var entry = entries.GetArrayElementAtIndex(index);
            SetBool(entry, "overrideNotebookStation", true);
            SetBool(entry, "lockNotebookStationToMonitor", monitorObject != null);
            if (monitorObject != null)
            {
                SetVector(entry, "notebookStationPosition", monitorObject.transform.InverseTransformPoint(sceneObject.transform.position));
                var localRotation = Quaternion.Inverse(monitorObject.transform.rotation) * sceneObject.transform.rotation;
                SetVector(entry, "notebookStationRotation", localRotation.eulerAngles);
            }
            else if (dashboardBoard != null)
            {
                SetVector(entry, "notebookStationPosition", dashboardBoard.transform.InverseTransformPoint(sceneObject.transform.position));
                var localRotation = Quaternion.Inverse(dashboardBoard.transform.rotation) * sceneObject.transform.rotation;
                SetVector(entry, "notebookStationRotation", localRotation.eulerAngles);
            }
            else
            {
                SetVector(entry, "notebookStationPosition", sceneObject.transform.localPosition);
                SetVector(entry, "notebookStationRotation", sceneObject.transform.localEulerAngles);
            }
            SetVector(entry, "notebookStationScale", sceneObject.transform.localScale);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void CaptureDashboardBoardOverlayTransform()
        {
            var boardObject = FindSceneGameObject("MeshSwap_DashboardSkin");
            if (boardObject == null)
            {
                Debug.LogWarning("[DeepSeaThemeCreator] DeepSea dashboard board not found in scene. Keeping dashboard overlay placement.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(DashboardSkinCatalogPath);
            if (catalog == null) return;

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            var index = FindEntryIndex(entries, DeepSeaDashboardAssetId);
            if (index < 0) return;

            var entry = entries.GetArrayElementAtIndex(index);
            SetBool(entry, "overrideNotebookStation", true);
            SetBool(entry, "lockNotebookStationToMonitor", false);
            SetVector(entry, "notebookStationPosition", boardObject.transform.position);
            SetVector(entry, "notebookStationRotation", Vector3.zero);
            SetVector(entry, "notebookStationScale", Vector3.one);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static GameObject FindSceneGameObject(string objectName)
        {
            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                var go = objects[i];
                if (go == null) continue;
                if (!go.scene.IsValid()) continue;
                if (EditorUtility.IsPersistent(go)) continue;
                if (go.name == objectName) return go;
            }
            return null;
        }

        private static T FindSceneComponent<T>() where T : Component
        {
            var components = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || component.gameObject == null) continue;
                if (!component.gameObject.scene.IsValid()) continue;
                if (EditorUtility.IsPersistent(component)) continue;
                return component;
            }
            return null;
        }

        private static string SanitizeName(string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? "decoration" : raw.Replace(' ', '_');
        }

        private static void EnsureCatalogContainsTheme(RoomThemeAsset theme)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RoomThemeCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RoomThemeCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("entries");
            bool exists = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).objectReferenceValue == theme)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = theme;
            }

            var defaultTheme = so.FindProperty("defaultTheme");
            if (defaultTheme != null && defaultTheme.objectReferenceValue == null)
            {
                defaultTheme.objectReferenceValue = theme;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void SetDefaultEntries(SerializedProperty entries)
        {
            if (entries == null) return;
            entries.arraySize = 3;
            var entry = entries.GetArrayElementAtIndex(0);
            var category = entry.FindPropertyRelative("category");
            if (category != null) category.enumValueIndex = (int)CustomizationCategory.BookshelfSkin;
            SetLong(entry, "assetId", DeepSeaBookshelfAssetId);

            entry = entries.GetArrayElementAtIndex(1);
            category = entry.FindPropertyRelative("category");
            if (category != null) category.enumValueIndex = (int)CustomizationCategory.Desk;
            SetLong(entry, "assetId", DeepSeaDeskAssetId);

            entry = entries.GetArrayElementAtIndex(2);
            category = entry.FindPropertyRelative("category");
            if (category != null) category.enumValueIndex = (int)CustomizationCategory.DashboardSkin;
            SetLong(entry, "assetId", DeepSeaDashboardAssetId);
        }

        private static DecoSpec[] FloorSpecs()
        {
            return new[]
            {
                new DecoSpec("deepsea_bed", WallSide.None, new Vector3(1.38f, 0.46f, 0.12f), new Vector3(0f, 180f, 0f), Vector3.one, "bed"),
                new DecoSpec("deepsea_chair", WallSide.None, new Vector3(-0.72f, 0.41f, -0.10f), new Vector3(0f, 35f, 0f), new Vector3(0.43f, 0.43f, 0.43f), "chair"),
                new DecoSpec("deepsea_dreamlamp", WallSide.None, new Vector3(-1.58f, 0.65f, 0.93f), new Vector3(0f, -25f, 0f), new Vector3(0.68f, 0.68f, 0.68f), "dreamLamp"),
                new DecoSpec("deepsea_coral", WallSide.None, new Vector3(1.70f, 0.52f, 1.10f), new Vector3(0f, -20f, 0f), new Vector3(0.28f, 0.55f, 0.28f), "coralSmall"),
                new DecoSpec("deepsea_coral2", WallSide.None, new Vector3(1.12f, 0.23f, 1.43f), new Vector3(0f, 35f, 0f), new Vector3(0.24f, 0.24f, 0.24f), "coralCluster"),
                new DecoSpec("deepsea_shell", WallSide.None, new Vector3(-1.25f, 0.13f, 1.36f), new Vector3(0f, -15f, 0f), new Vector3(0.16f, 0.16f, 0.16f), "shell"),
                new DecoSpec("deepsea_aquarium", WallSide.None, new Vector3(-1.45f, 0.475f, 0.52f), Vector3.zero, new Vector3(0.62f, 0.62f, 0.62f), "aquarium"),
                new DecoSpec("deepsea_shark", WallSide.None, new Vector3(0.18f, 0.13f, 1.12f), new Vector3(0f, -28f, 0f), new Vector3(0.28f, 0.28f, 0.28f), "shark"),
                new DecoSpec("deepsea_dolphin", WallSide.None, new Vector3(-0.35f, 0.145f, 1.42f), new Vector3(0f, 32f, 0f), new Vector3(0.30f, 0.30f, 0.30f), "dolphin"),
            };
        }

        private static DecoSpec[] WallSpecs()
        {
            return System.Array.Empty<DecoSpec>();
        }

        private static void SetDecoSpecs(SerializedProperty specs, DecoSpec[] values)
        {
            if (specs == null) return;
            specs.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                var spec = specs.GetArrayElementAtIndex(i);
                SetLong(spec, "assetId", 0L);
                SetObject(spec, "prefab", LoadRequired<GameObject>(ModelRoot + "/" + values[i].PrefabName + ".glb"));
                var wallSide = spec.FindPropertyRelative("wallSide");
                if (wallSide != null) wallSide.enumValueIndex = (int)values[i].WallSide;
                SetVector(spec, "localPosition", values[i].LocalPosition);
                SetVector(spec, "localRotation", values[i].LocalRotation);
                SetVector(spec, "localScale", values[i].LocalScale);
                SetString(spec, "anchorTag", values[i].AnchorTag);
            }
        }

        private static Material CreateOrUpdateSolidMaterial(string name, Color color, float smoothness)
        {
            return CreateOrUpdateMaterial(name, color, smoothness, null, Vector2.one);
        }

        private static Material CreateOrUpdateMaterial(string name, Color color, float smoothness, Texture2D texture, Vector2 tiling)
        {
            var path = MaterialsRoot + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", texture);
                    mat.SetTextureScale("_BaseMap", tiling);
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", texture);
                    mat.SetTextureScale("_MainTex", tiling);
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static int FindEntryIndex(SerializedProperty entries, long assetId)
        {
            if (entries == null) return -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entryAssetId = entries.GetArrayElementAtIndex(i).FindPropertyRelative("assetId");
                if (entryAssetId != null && entryAssetId.longValue == assetId) return i;
            }
            return -1;
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException("Required DeepSea asset is missing: " + path);
            }
            return asset;
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
        }

        private static void SetLong(SerializedObject so, string name, long value) => SetLong(so.FindProperty(name), value);
        private static void SetInt(SerializedObject so, string name, int value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.intValue = value;
        }
        private static void SetFloat(SerializedObject so, string name, float value) => SetFloat(so.FindProperty(name), value);
        private static void SetBool(SerializedObject so, string name, bool value) => SetBool(so.FindProperty(name), value);
        private static void SetString(SerializedObject so, string name, string value) => SetString(so.FindProperty(name), value);
        private static void SetColor(SerializedObject so, string name, Color value) => SetColor(so.FindProperty(name), value);
        private static void SetObject(SerializedObject so, string name, Object value) => SetObject(so.FindProperty(name), value);

        private static void SetLong(SerializedProperty owner, string name, long value) => SetLong(owner?.FindPropertyRelative(name), value);
        private static void SetFloat(SerializedProperty owner, string name, float value) => SetFloat(owner?.FindPropertyRelative(name), value);
        private static void SetBool(SerializedProperty owner, string name, bool value) => SetBool(owner?.FindPropertyRelative(name), value);
        private static void SetString(SerializedProperty owner, string name, string value) => SetString(owner?.FindPropertyRelative(name), value);
        private static void SetColor(SerializedProperty owner, string name, Color value) => SetColor(owner?.FindPropertyRelative(name), value);
        private static void SetObject(SerializedProperty owner, string name, Object value) => SetObject(owner?.FindPropertyRelative(name), value);
        private static void SetVector(SerializedProperty owner, string name, Vector3 value)
        {
            var prop = owner?.FindPropertyRelative(name);
            if (prop != null) prop.vector3Value = value;
        }

        private static void SetLong(SerializedProperty prop, long value)
        {
            if (prop != null) prop.longValue = value;
        }

        private static void SetFloat(SerializedProperty prop, float value)
        {
            if (prop != null) prop.floatValue = value;
        }

        private static void SetBool(SerializedProperty prop, bool value)
        {
            if (prop != null) prop.boolValue = value;
        }

        private static void SetString(SerializedProperty prop, string value)
        {
            if (prop != null) prop.stringValue = value ?? string.Empty;
        }

        private static void SetColor(SerializedProperty prop, Color value)
        {
            if (prop != null) prop.colorValue = value;
        }

        private static void SetObject(SerializedProperty prop, Object value)
        {
            if (prop != null) prop.objectReferenceValue = value;
        }

        private readonly struct DecoSpec
        {
            public DecoSpec(string prefabName, WallSide wallSide, Vector3 localPosition, Vector3 localRotation, Vector3 localScale, string anchorTag)
            {
                PrefabName = prefabName;
                WallSide = wallSide;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
                AnchorTag = anchorTag;
            }

            public string PrefabName { get; }
            public WallSide WallSide { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalRotation { get; }
            public Vector3 LocalScale { get; }
            public string AnchorTag { get; }

            public DecoSpec WithTransform(Vector3 position, Vector3 rotation, Vector3 scale)
            {
                return new DecoSpec(PrefabName, WallSide, position, rotation, scale, AnchorTag);
            }
        }
    }
}
