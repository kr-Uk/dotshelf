#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using App.Bootstrap;
using App.Infrastructure.Catalogs;
using UnityEditor;
using UnityEngine;

namespace App.EditorTools
{
    public static class CustomizationPreviewGenerator
    {
        private const string CatalogRoot = "Assets/Resources/Catalogs";
        private const string EditableCatalogPath = "Assets/Resources/EditableDecorationCatalog.asset";
        private const string OutputRoot = "Assets/App/Composition/Previews/Customization";
        private const int PreviewSize = 256;
        private const float Padding = 1.18f;

        [MenuItem("Tools/DotShelf/Generate Customization Previews")]
        public static void GenerateMissingPreviews()
        {
            GeneratePreviews(overwriteExistingSprites: false, overwriteExistingPngs: false);
        }

        [MenuItem("Tools/DotShelf/Regenerate Customization Previews")]
        public static void RegeneratePreviews()
        {
            if (!EditorUtility.DisplayDialog(
                    "Regenerate Customization Previews",
                    "Existing PreviewSprite references and generated PNG files will be replaced.",
                    "Regenerate",
                    "Cancel"))
            {
                return;
            }

            GeneratePreviews(overwriteExistingSprites: true, overwriteExistingPngs: true);
        }

        private static void GeneratePreviews(bool overwriteExistingSprites, bool overwriteExistingPngs)
        {
            var catalogs = LoadCustomizationCatalogs();
            var editableCatalog = LoadEditableDecorationCatalog();
            if (catalogs.Count == 0 && editableCatalog == null)
            {
                Debug.LogWarning($"[CustomizationPreviewGenerator] No catalog found under {CatalogRoot} or {EditableCatalogPath}.");
                return;
            }

            EnsureFolder(OutputRoot);

            var generated = 0;
            var linked = 0;
            var skipped = 0;
            var failed = 0;
            var total = CountEntries(catalogs) + (editableCatalog?.Entries?.Count ?? 0);

            var context = PreviewContext.Create();
            try
            {
                var processed = 0;
                for (var c = 0; c < catalogs.Count; c++)
                {
                    var catalog = catalogs[c];
                    if (catalog == null || catalog.Entries == null)
                    {
                        continue;
                    }

                    var serializedCatalog = new SerializedObject(catalog);
                    var entriesProperty = serializedCatalog.FindProperty("entries");
                    if (entriesProperty == null || !entriesProperty.isArray)
                    {
                        Debug.LogWarning($"[CustomizationPreviewGenerator] Catalog entries field missing: {AssetDatabase.GetAssetPath(catalog)}");
                        continue;
                    }

                    for (var i = 0; i < catalog.Entries.Count; i++)
                    {
                        processed++;
                        var entry = catalog.Entries[i];
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Generate Customization Previews",
                                $"{catalog.Category} ({i + 1}/{catalog.Entries.Count})",
                                total <= 0 ? 1f : processed / (float)total))
                        {
                            Debug.Log("[CustomizationPreviewGenerator] Canceled.");
                            return;
                        }

                        if (entry == null)
                        {
                            skipped++;
                            continue;
                        }

                        var entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                        var previewProperty = entryProperty.FindPropertyRelative("previewSprite");
                        if (previewProperty == null)
                        {
                            failed++;
                            Debug.LogWarning($"[CustomizationPreviewGenerator] previewSprite field missing: {catalog.Category}/{entry.DisplayName}");
                            continue;
                        }

                        if (!overwriteExistingSprites && previewProperty.objectReferenceValue != null)
                        {
                            skipped++;
                            continue;
                        }

                        var outputPath = BuildOutputPath(catalog.Category, entry, i);
                        if (!overwriteExistingPngs && File.Exists(outputPath))
                        {
                            var existingSprite = ImportAsSprite(outputPath);
                            if (existingSprite != null)
                            {
                                previewProperty.objectReferenceValue = existingSprite;
                                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
                                EditorUtility.SetDirty(catalog);
                                linked++;
                                continue;
                            }
                        }

                        if (!TryRenderEntry(context, catalog.Category, entry, outputPath))
                        {
                            failed++;
                            continue;
                        }

                        var sprite = ImportAsSprite(outputPath);
                        if (sprite == null)
                        {
                            failed++;
                            Debug.LogWarning($"[CustomizationPreviewGenerator] Generated PNG could not be imported as Sprite: {outputPath}");
                            continue;
                        }

                        previewProperty.objectReferenceValue = sprite;
                        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(catalog);
                        generated++;
                    }
                }

                ProcessEditableDecorationCatalog(
                    context,
                    editableCatalog,
                    overwriteExistingSprites,
                    overwriteExistingPngs,
                    total,
                    ref processed,
                    ref generated,
                    ref linked,
                    ref skipped,
                    ref failed);
            }
            finally
            {
                context.Dispose();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[CustomizationPreviewGenerator] Done. generated={generated}, linked={linked}, skipped={skipped}, failed={failed}, output={OutputRoot}");
        }

        private static List<CustomizationCatalogAsset> LoadCustomizationCatalogs()
        {
            var catalogs = new List<CustomizationCatalogAsset>();
            if (!AssetDatabase.IsValidFolder(CatalogRoot))
            {
                return catalogs;
            }

            var guids = AssetDatabase.FindAssets("t:CustomizationCatalogAsset", new[] { CatalogRoot });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var catalog = AssetDatabase.LoadAssetAtPath<CustomizationCatalogAsset>(path);
                if (catalog != null)
                {
                    catalogs.Add(catalog);
                }
            }

            catalogs.Sort((left, right) => string.Compare(
                AssetDatabase.GetAssetPath(left),
                AssetDatabase.GetAssetPath(right),
                StringComparison.OrdinalIgnoreCase));
            return catalogs;
        }

        private static EditableDecorationCatalog LoadEditableDecorationCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<EditableDecorationCatalog>(EditableCatalogPath);
        }

        private static int CountEntries(IReadOnlyList<CustomizationCatalogAsset> catalogs)
        {
            var count = 0;
            for (var i = 0; i < catalogs.Count; i++)
            {
                count += catalogs[i]?.Entries?.Count ?? 0;
            }

            return count;
        }

        private static void ProcessEditableDecorationCatalog(
            PreviewContext context,
            EditableDecorationCatalog catalog,
            bool overwriteExistingSprites,
            bool overwriteExistingPngs,
            int total,
            ref int processed,
            ref int generated,
            ref int linked,
            ref int skipped,
            ref int failed)
        {
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
            {
                return;
            }

            var serializedCatalog = new SerializedObject(catalog);
            var entriesProperty = serializedCatalog.FindProperty("entries");
            if (entriesProperty == null || !entriesProperty.isArray)
            {
                Debug.LogWarning($"[CustomizationPreviewGenerator] Editable decoration entries field missing: {EditableCatalogPath}");
                return;
            }

            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                processed++;
                var entry = catalog.Entries[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Generate Customization Previews",
                        $"Placement ({i + 1}/{catalog.Entries.Count})",
                        total <= 0 ? 1f : processed / (float)total))
                {
                    Debug.Log("[CustomizationPreviewGenerator] Canceled.");
                    return;
                }

                if (entry == null)
                {
                    skipped++;
                    continue;
                }

                var entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                var previewProperty = entryProperty.FindPropertyRelative("PreviewSprite") ??
                                      entryProperty.FindPropertyRelative("previewSprite");
                if (previewProperty == null)
                {
                    failed++;
                    Debug.LogWarning($"[CustomizationPreviewGenerator] PreviewSprite field missing: Placement/{entry.DisplayName}");
                    continue;
                }

                if (!overwriteExistingSprites && previewProperty.objectReferenceValue != null)
                {
                    skipped++;
                    continue;
                }

                var outputPath = BuildEditableOutputPath(entry, i);
                if (!overwriteExistingPngs && File.Exists(outputPath))
                {
                    var existingSprite = ImportAsSprite(outputPath);
                    if (existingSprite != null)
                    {
                        previewProperty.objectReferenceValue = existingSprite;
                        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(catalog);
                        linked++;
                        continue;
                    }
                }

                if (!TryRenderEditableEntry(context, entry, outputPath))
                {
                    failed++;
                    continue;
                }

                var sprite = ImportAsSprite(outputPath);
                if (sprite == null)
                {
                    failed++;
                    Debug.LogWarning($"[CustomizationPreviewGenerator] Generated placement PNG could not be imported as Sprite: {outputPath}");
                    continue;
                }

                previewProperty.objectReferenceValue = sprite;
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
                generated++;
            }
        }

        private static bool TryRenderEntry(
            PreviewContext context,
            CustomizationCategory category,
            CustomizationEntry entry,
            string outputPath)
        {
            GameObject subjectRoot = null;
            try
            {
                subjectRoot = BuildPreviewSubject(category, entry);
                if (subjectRoot == null)
                {
                    Debug.LogWarning($"[CustomizationPreviewGenerator] No preview source: {category}/{entry.DisplayName}");
                    return false;
                }

                subjectRoot.transform.SetParent(context.Root.transform, false);
                var renderers = subjectRoot.GetComponentsInChildren<Renderer>(true);
                var bounds = CalculateRendererBounds(renderers);
                if (!bounds.HasValue)
                {
                    Debug.LogWarning($"[CustomizationPreviewGenerator] No renderer bounds: {category}/{entry.DisplayName}");
                    return false;
                }

                Capture(context, bounds.Value, outputPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CustomizationPreviewGenerator] Failed: {category}/{entry.DisplayName} {ex.GetType().Name} {ex.Message}");
                return false;
            }
            finally
            {
                if (subjectRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(subjectRoot);
                }
            }
        }

        private static bool TryRenderEditableEntry(
            PreviewContext context,
            EditableDecorationCatalog.Entry entry,
            string outputPath)
        {
            GameObject subjectRoot = null;
            try
            {
                subjectRoot = BuildEditablePreviewSubject(entry);
                if (subjectRoot == null)
                {
                    Debug.LogWarning($"[CustomizationPreviewGenerator] No placement preview source: {entry.DisplayName}");
                    return false;
                }

                subjectRoot.transform.SetParent(context.Root.transform, false);
                var renderers = subjectRoot.GetComponentsInChildren<Renderer>(true);
                var bounds = CalculateRendererBounds(renderers);
                if (!bounds.HasValue)
                {
                    Debug.LogWarning($"[CustomizationPreviewGenerator] No placement renderer bounds: {entry.DisplayName}");
                    return false;
                }

                Capture(context, bounds.Value, outputPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CustomizationPreviewGenerator] Placement failed: {entry.DisplayName} {ex.GetType().Name} {ex.Message}");
                return false;
            }
            finally
            {
                if (subjectRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(subjectRoot);
                }
            }
        }

        private static GameObject BuildPreviewSubject(CustomizationCategory category, CustomizationEntry entry)
        {
            if (entry.Prefab != null)
            {
                var root = new GameObject("PreviewSubject");
                root.hideFlags = HideFlags.HideAndDontSave;

                var instance = PrefabUtility.InstantiatePrefab(entry.Prefab) as GameObject;
                if (instance == null)
                {
                    instance = UnityEngine.Object.Instantiate(entry.Prefab);
                }

                instance.name = entry.Prefab.name;
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(entry.PlacementRotation);
                instance.transform.localScale = SanitizeScale(entry.PlacementScale);
                root.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
                return root;
            }

            if (IsLightCategory(category))
            {
                return BuildLightPreviewSubject(entry);
            }

            if (entry.PrimaryMaterial != null)
            {
                return BuildMaterialPreviewSubject(category, entry.PrimaryMaterial);
            }

            return null;
        }

        private static GameObject BuildEditablePreviewSubject(EditableDecorationCatalog.Entry entry)
        {
            if (entry == null || entry.Prefab == null)
            {
                return null;
            }

            var root = new GameObject("PlacementPreviewSubject");
            root.hideFlags = HideFlags.HideAndDontSave;

            var instance = PrefabUtility.InstantiatePrefab(entry.Prefab) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(entry.Prefab);
            }

            instance.name = entry.Prefab.name;
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            root.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            return root;
        }

        private static GameObject BuildMaterialPreviewSubject(CustomizationCategory category, Material material)
        {
            var primitive = IsHorizontalMaterialCategory(category)
                ? GameObject.CreatePrimitive(PrimitiveType.Cube)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = "MaterialPreview";
            primitive.hideFlags = HideFlags.HideAndDontSave;

            var collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            primitive.transform.localScale = IsHorizontalMaterialCategory(category)
                ? new Vector3(1.7f, 0.08f, 1.15f)
                : new Vector3(1.55f, 1.05f, 0.08f);
            primitive.transform.localRotation = IsHorizontalMaterialCategory(category)
                ? Quaternion.Euler(0f, -35f, 0f)
                : Quaternion.Euler(12f, -35f, 0f);

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static GameObject BuildLightPreviewSubject(CustomizationEntry entry)
        {
            var root = new GameObject("LightPreview");
            root.hideFlags = HideFlags.HideAndDontSave;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "LightColor";
            sphere.hideFlags = HideFlags.HideAndDontSave;
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * 0.72f;

            var collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
            {
                name = "GeneratedLightPreviewMaterial",
                color = entry.LightColor
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", entry.LightColor);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", entry.LightColor * Mathf.Max(1f, entry.LightIntensity));
            }

            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = entry.LightColor;
            light.intensity = Mathf.Max(0.5f, entry.LightIntensity);
            light.range = 3f;

            root.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            return root;
        }

        private static void Capture(PreviewContext context, Bounds bounds, string outputPath)
        {
            var camera = context.Camera;
            var center = bounds.center;
            var viewOffset = new Vector3(2.2f, 1.55f, -2.35f).normalized;
            var distance = Mathf.Max(3f, bounds.extents.magnitude * 3f);
            camera.transform.position = center + viewOffset * distance;
            camera.transform.LookAt(center, Vector3.up);
            camera.orthographicSize = CalculateOrthographicSize(camera, bounds) * Padding;

            var previousActive = RenderTexture.active;
            try
            {
                RenderTexture.active = context.RenderTexture;
                camera.Render();
                context.Texture.ReadPixels(new Rect(0, 0, PreviewSize, PreviewSize), 0, 0);
                context.Texture.Apply(false, false);
                File.WriteAllBytes(outputPath, context.Texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private static float CalculateOrthographicSize(Camera camera, Bounds bounds)
        {
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minY = float.PositiveInfinity;
            var maxY = float.NegativeInfinity;

            var center = bounds.center;
            var extents = bounds.extents;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        var local = camera.transform.InverseTransformPoint(corner);
                        minX = Mathf.Min(minX, local.x);
                        maxX = Mathf.Max(maxX, local.x);
                        minY = Mathf.Min(minY, local.y);
                        maxY = Mathf.Max(maxY, local.y);
                    }
                }
            }

            var widthSize = (maxX - minX) * 0.5f / Mathf.Max(0.01f, camera.aspect);
            var heightSize = (maxY - minY) * 0.5f;
            return Mathf.Max(widthSize, heightSize, 0.45f);
        }

        private static Bounds? CalculateRendererBounds(IReadOnlyList<Renderer> renderers)
        {
            Bounds? bounds = null;
            for (var i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                bounds = bounds.HasValue ? Encapsulate(bounds.Value, renderer.bounds) : renderer.bounds;
            }

            return bounds;
        }

        private static Bounds Encapsulate(Bounds current, Bounds next)
        {
            current.Encapsulate(next.min);
            current.Encapsulate(next.max);
            return current;
        }

        private static Sprite ImportAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string BuildOutputPath(CustomizationCategory category, CustomizationEntry entry, int index)
        {
            var id = entry.AssetId > 0 ? entry.AssetId.ToString() : $"index_{index:D3}";
            return $"{OutputRoot}/{category}_{id}.png";
        }

        private static string BuildEditableOutputPath(EditableDecorationCatalog.Entry entry, int index)
        {
            var name = !string.IsNullOrWhiteSpace(entry.SourceAssetPath)
                ? Path.GetFileNameWithoutExtension(entry.SourceAssetPath)
                : entry.DisplayName;
            name = string.IsNullOrWhiteSpace(name) ? $"index_{index:D3}" : SanitizeFileName(name);
            return $"{OutputRoot}/Placement_{name}.png";
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return scale.sqrMagnitude > 1e-10f ? scale : Vector3.one;
        }

        private static bool IsHorizontalMaterialCategory(CustomizationCategory category)
        {
            return category == CustomizationCategory.Floor;
        }

        private static bool IsLightCategory(CustomizationCategory category)
        {
            return category == CustomizationCategory.InteriorLight ||
                   category == CustomizationCategory.ExteriorLight;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private sealed class PreviewContext : IDisposable
        {
            public GameObject Root { get; private set; }
            public Camera Camera { get; private set; }
            public RenderTexture RenderTexture { get; private set; }
            public Texture2D Texture { get; private set; }

            public static PreviewContext Create()
            {
                var context = new PreviewContext
                {
                    Root = new GameObject("CustomizationPreviewGeneratorRoot")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    },
                    RenderTexture = new RenderTexture(PreviewSize, PreviewSize, 24, RenderTextureFormat.ARGB32)
                    {
                        antiAliasing = 4
                    },
                    Texture = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGBA32, false)
                };

                var cameraObject = new GameObject("PreviewCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                cameraObject.transform.SetParent(context.Root.transform, false);
                context.Camera = cameraObject.AddComponent<Camera>();
                context.Camera.clearFlags = CameraClearFlags.SolidColor;
                context.Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                context.Camera.orthographic = true;
                context.Camera.nearClipPlane = 0.01f;
                context.Camera.farClipPlane = 100f;
                context.Camera.allowHDR = false;
                context.Camera.allowMSAA = true;
                context.Camera.targetTexture = context.RenderTexture;

                AddDirectionalLight(context.Root.transform, "KeyLight", new Vector3(45f, -35f, 0f), 1.35f);
                AddDirectionalLight(context.Root.transform, "FillLight", new Vector3(25f, 145f, 0f), 0.55f);
                return context;
            }

            public void Dispose()
            {
                if (Camera != null)
                {
                    Camera.targetTexture = null;
                }

                if (RenderTexture != null)
                {
                    RenderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(RenderTexture);
                }

                if (Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(Texture);
                }

                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                }
            }

            private static void AddDirectionalLight(Transform parent, string name, Vector3 euler, float intensity)
            {
                var lightObject = new GameObject(name)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.rotation = Quaternion.Euler(euler);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.color = Color.white;
            }
        }
    }
}
#endif
