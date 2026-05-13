#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BookshelfPorting.Runtime;
using UnityEditor;
using UnityEngine;

namespace BookshelfPorting.Editor
{
    public static class BookshelfThemeLayoutAssetBuilder
    {
        private const float TargetWidth = 1.60f;
        private const float TargetHeight = 1.60f;
        private const float MinimumSectionDepth = 0.19f;
        private static readonly bool DrawSectionGuidesInPreview = false;
        private const string ThemeLayoutFolder = "Assets/Resources/BookshelfThemeLayouts";
        private const string CatalogPath = "Assets/Resources/BookshelfThemeLayoutCatalog.asset";
        private const string VerificationFolderName = "BookshelfThemeVerification";

        private static readonly ThemeSpec[] ThemeSpecs =
        {
            new ThemeSpec(
                BookshelfThemeStyle.Warm,
                "PastelCabin",
                "Assets/Arts/Models/Bookshelves/Meshy_AI_Pastel_16_Cubby_Cabin_0512011805_texture.glb",
                $"{ThemeLayoutFolder}/BookshelfThemeLayout_PastelCabin.asset",
                4,
                4,
                1f,
                Vector3.zero,
                0.14f,
                0.20f,
                0.08f,
                0.040f,
                0.025f,
                0.04f,
                0.04f,
                -0.025f,
                0f,
                new[] { 0.150f, 0.486f, 0.822f, 1.157f },
                new[] { 0.308f, 0.308f, 0.308f, 0.308f },
                new[] { 0.006f, 0.006f, 0.006f, 0.006f }),

            new ThemeSpec(
                BookshelfThemeStyle.Dark,
                "UnderwaterShell",
                "Assets/Arts/Models/Bookshelves/Meshy_AI_Underwater_Shell_Grid_0512011829_texture.glb",
                $"{ThemeLayoutFolder}/BookshelfThemeLayout_UnderwaterShell.asset",
                4,
                4,
                1f,
                new Vector3(0f, 180f, 0f),
                0.23f,
                0.22f,
                0.24f,
                0.045f,
                0.030f,
                0.06f,
                0.05f,
                -0.035f,
                0f,
                new[] { 0.160f, 0.453f, 0.747f, 1.040f },
                new[] { 0.269f, 0.268f, 0.267f, 0.257f },
                new[] { 0.011f, 0.010f, 0.010f, 0.010f }),

            new ThemeSpec(
                BookshelfThemeStyle.Vintage,
                "VintageShelf",
                "Assets/Arts/Models/Bookshelves/vintage_shelf.glb",
                $"{ThemeLayoutFolder}/BookshelfThemeLayout_VintageShelf.asset",
                4,
                4,
                1f,
                Vector3.zero,
                0.13f,
                0.18f,
                0.13f,
                0.065f,
                0.030f,
                0.04f,
                0.04f,
                -0.020f,
                0f,
                new[] { 0.165f, 0.535f, 0.820f, 1.145f },
                new[] { 0.305f, 0.295f, 0.310f, 0.315f },
                new[] { 0.006f, 0.006f, 0.006f, 0.006f })
        };

        [MenuItem("Tools/DotShelf/Bookshelf/Create Meshy Theme Layouts")]
        public static void CreateMeshyThemeLayouts()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(ThemeLayoutFolder);

            var generatedLayouts = new List<BookshelfThemeLayout>();
            for (var i = 0; i < ThemeSpecs.Length; i++)
            {
                var spec = ThemeSpecs[i];
                AssetDatabase.ImportAsset(spec.AssetPath, ImportAssetOptions.ForceUpdate);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath);
                if (prefab == null)
                {
                    Debug.LogError($"[BookshelfThemeLayoutAssetBuilder] Missing bookshelf GLB prefab: {spec.AssetPath}");
                    continue;
                }

                var placement = CalculateVisualPlacement(prefab, spec);
                var sections = CreateGridSections(placement.TransformedBounds, spec);
                var focusCenter = new Vector3(
                    0f,
                    placement.TransformedBounds.center.y,
                    placement.TransformedBounds.min.z - 0.05f);
                var focusSize = new Vector3(
                    placement.TransformedBounds.size.x + 0.10f,
                    placement.TransformedBounds.size.y + 0.10f,
                    0.18f);

                var layout = LoadOrCreateAsset<BookshelfThemeLayout>(spec.LayoutPath);
                layout.SetGeneratedData(
                    spec.Style,
                    prefab,
                    placement.LocalPosition,
                    placement.LocalEulerAngles,
                    placement.LocalScale,
                    focusCenter,
                    focusSize,
                    sections);

                EditorUtility.SetDirty(layout);
                generatedLayouts.Add(layout);
                Debug.Log($"[BookshelfThemeLayoutAssetBuilder] Generated {spec.DisplayName}: {sections.Count} sections, bounds={FormatBounds(placement.TransformedBounds)}");
            }

            var catalog = LoadOrCreateAsset<BookshelfThemeLayoutCatalog>(CatalogPath);
            catalog.SetLayouts(generatedLayouts);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/DotShelf/Bookshelf/Verify Meshy Theme Layouts")]
        public static void VerifyMeshyThemeLayouts()
        {
            CreateMeshyThemeLayouts();

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VerificationFolderName));
            Directory.CreateDirectory(outputDirectory);

            var report = new StringBuilder();
            report.AppendLine("Bookshelf theme layout verification");
            report.AppendLine($"Generated at: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            for (var i = 0; i < ThemeSpecs.Length; i++)
            {
                var spec = ThemeSpecs[i];
                var layout = AssetDatabase.LoadAssetAtPath<BookshelfThemeLayout>(spec.LayoutPath);
                if (layout == null)
                {
                    report.AppendLine($"{spec.DisplayName}: missing layout asset");
                    continue;
                }

                var capturePath = Path.Combine(outputDirectory, $"{spec.DisplayName}_bookshelf_preview.png");
                var ok = RenderAndValidateLayout(layout, capturePath, out var summary);
                report.AppendLine($"{spec.DisplayName}: {(ok ? "PASS" : "FAIL")}");
                report.AppendLine(summary);
                report.AppendLine($"Capture: {capturePath}");
                report.AppendLine();
            }

            var reportPath = Path.Combine(outputDirectory, "bookshelf_layout_verification.txt");
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
            Debug.Log($"[BookshelfThemeLayoutAssetBuilder] Verification report written: {reportPath}");
        }

        public static bool RenderLayoutPreviewToVerificationFolder(BookshelfThemeLayout layout, out string capturePath, out string summary)
        {
            capturePath = string.Empty;
            summary = string.Empty;
            if (layout == null)
            {
                summary = "Missing layout.";
                return false;
            }

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VerificationFolderName));
            Directory.CreateDirectory(outputDirectory);
            capturePath = Path.Combine(outputDirectory, $"{layout.name}_live_preview.png");
            return RenderAndValidateLayout(layout, capturePath, out summary);
        }

        private static VisualPlacement CalculateVisualPlacement(GameObject prefab, ThemeSpec spec)
        {
            var rawBounds = CalculatePrefabLocalRendererBounds(prefab);
            var rawSize = rawBounds.size;
            var scale = Mathf.Min(
                SafeDivide(TargetWidth, rawSize.x),
                SafeDivide(TargetHeight, rawSize.y));

            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 1f;
            }

            scale *= Mathf.Max(0.01f, spec.VisualScaleMultiplier);
            var localScale = Vector3.one * scale;
            var rotation = Quaternion.Euler(spec.PrefabLocalEulerAngles);
            var rotatedBounds = TransformBounds(rawBounds, Vector3.zero, scale, rotation);
            var localPosition = new Vector3(
                -rotatedBounds.center.x,
                -rotatedBounds.min.y,
                -rotatedBounds.center.z);
            var transformedBounds = OffsetBounds(rotatedBounds, localPosition);
            return new VisualPlacement(localPosition, spec.PrefabLocalEulerAngles, localScale, transformedBounds);
        }

        private static List<BookshelfSectionLayout> CreateGridSections(Bounds visualBounds, ThemeSpec spec)
        {
            var sections = new List<BookshelfSectionLayout>();
            var left = visualBounds.min.x + spec.SideInset;
            var right = visualBounds.max.x - spec.SideInset;
            var bottom = visualBounds.min.y + spec.BottomInset;
            var top = visualBounds.max.y - spec.TopInset;

            if (right <= left || top <= bottom)
            {
                Debug.LogWarning($"[BookshelfThemeLayoutAssetBuilder] {spec.DisplayName} inset values are too large. Falling back to full visual bounds.");
                left = visualBounds.min.x;
                right = visualBounds.max.x;
                bottom = visualBounds.min.y;
                top = visualBounds.max.y;
            }

            var totalColumnGap = spec.ColumnGap * Mathf.Max(0, spec.Columns - 1);
            var totalRowGap = spec.RowGap * Mathf.Max(0, spec.Rows - 1);
            var sectionWidth = (right - left - totalColumnGap) / Mathf.Max(1, spec.Columns);
            var sectionHeight = (top - bottom - totalRowGap) / Mathf.Max(1, spec.Rows);

            var frontZ = visualBounds.min.z + spec.FrontInset;
            var backZ = visualBounds.max.z - spec.BackInset;
            if (backZ - frontZ < MinimumSectionDepth)
            {
                var centerZ = (frontZ + backZ) * 0.5f;
                frontZ = centerZ - MinimumSectionDepth * 0.5f;
                backZ = centerZ + MinimumSectionDepth * 0.5f;
            }

            for (var row = 0; row < spec.Rows; row++)
            {
                var shelfTopY = spec.GetRowShelfTopY(row, bottom + row * (sectionHeight + spec.RowGap) + spec.SectionYOffset);
                var rowSectionHeight = spec.GetRowSectionHeight(row, sectionHeight);
                var rowBookYOffset = spec.GetRowBookYOffset(row, spec.BookYOffset);
                for (var column = 0; column < spec.Columns; column++)
                {
                    var sectionLeft = left + column * (sectionWidth + spec.ColumnGap);
                    var sectionRight = sectionLeft + sectionWidth;
                    sections.Add(new BookshelfSectionLayout(
                        row,
                        column,
                        sectionLeft,
                        sectionRight,
                        shelfTopY,
                        rowSectionHeight,
                        rowBookYOffset,
                        frontZ,
                        backZ,
                        ShelfSectionAlignment.Left,
                        row == spec.Rows - 1 && column == spec.Columns - 1));
                }
            }

            return sections;
        }

        private static bool RenderAndValidateLayout(BookshelfThemeLayout layout, string capturePath, out string summary)
        {
            var root = new GameObject($"{layout.name}_VerificationRoot");
            var cameraObject = new GameObject($"{layout.name}_VerificationCamera");
            var lightObject = new GameObject($"{layout.name}_VerificationLight");
            var createdMaterials = new List<Material>();
            var bookColors = new[]
            {
                new Color32(242, 62, 48, 255),
                new Color32(44, 126, 232, 255),
                new Color32(250, 196, 42, 255),
                new Color32(80, 182, 72, 255)
            };
            var valid = true;
            var sectionCount = 0;
            var bookCount = 0;
            var visibleBookPixels = 0;
            var builder = new StringBuilder();

            try
            {
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                layout.InstantiateVisual(root.transform);

                var guideMaterial = CreateMaterial(new Color(0.18f, 0.72f, 0.62f, 0.65f));
                createdMaterials.Add(guideMaterial);
                var bookMaterials = new Material[bookColors.Length];
                for (var colorIndex = 0; colorIndex < bookColors.Length; colorIndex++)
                {
                    bookMaterials[colorIndex] = CreateUnlitMaterial(bookColors[colorIndex]);
                }
                createdMaterials.AddRange(bookMaterials);

                var sections = new List<ShelfSection>();
                layout.BuildSections(sections);
                sectionCount = sections.Count;

                for (var i = 0; i < sections.Count; i++)
                {
                    var section = sections[i];
                    if (section == null)
                    {
                        valid = false;
                        continue;
                    }

                    if (DrawSectionGuidesInPreview)
                    {
                        CreateGuide(root.transform, section, guideMaterial);
                    }
                    if (section.isAdvertisementSection)
                    {
                        continue;
                    }

                    for (var bookIndex = 0; bookIndex < 3; bookIndex++)
                    {
                        var bookSize = new Vector3(0.038f + 0.004f * bookIndex, Mathf.Min(0.25f, section.sectionHeight * 0.68f), 0.145f);
                        var x = section.leftBound + 0.025f + bookSize.x * 0.5f + bookIndex * 0.052f;
                        var y = section.BookBaseY + bookSize.y * 0.5f;
                        var z = section.frontBoundZ + 0.012f + bookSize.z * 0.5f;
                        if (x + bookSize.x * 0.5f > section.rightBound)
                        {
                            continue;
                        }

                        var book = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        book.name = $"VerifyBook_r{section.row}_c{section.column}_{bookIndex}";
                        book.transform.SetParent(root.transform, false);
                        book.transform.localPosition = new Vector3(x, y, z);
                        book.transform.localScale = bookSize;
                        book.GetComponent<MeshRenderer>().sharedMaterial = bookMaterials[(section.row + section.column + bookIndex) % bookMaterials.Length];
                        UnityEngine.Object.DestroyImmediate(book.GetComponent<Collider>());
                        bookCount++;

                        if (!IsPreviewBookInsideSection(book.transform.localPosition, bookSize, section))
                        {
                            valid = false;
                            builder.AppendLine($"  Book outside section row={section.row}, column={section.column}, index={bookIndex}");
                        }
                    }
                }

                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.92f, 0.91f, 0.88f);
                camera.fieldOfView = 30f;
                camera.nearClipPlane = 0.03f;
                camera.farClipPlane = 20f;
                camera.transform.position = new Vector3(0f, 0.86f, -3.15f);
                camera.transform.LookAt(new Vector3(0f, 0.82f, 0f));

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.52f, 0.52f, 0.52f);

                visibleBookPixels = RenderCameraToPng(camera, capturePath, bookColors);
                if (visibleBookPixels < 9000)
                {
                    valid = false;
                    builder.AppendLine($"  Visible book pixels too low: {visibleBookPixels}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lightObject);
                for (var i = 0; i < createdMaterials.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(createdMaterials[i]);
                }
            }

            builder.Insert(0, $"  sections={sectionCount}, previewBooks={bookCount}, visibleBookPixels={visibleBookPixels}\n");
            summary = builder.ToString().TrimEnd();
            return valid && sectionCount > 0 && bookCount > 0;
        }

        private static void CreateGuide(Transform root, ShelfSection section, Material material)
        {
            var guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = $"SectionGuide_r{section.row}_c{section.column}";
            guide.transform.SetParent(root, false);
            guide.transform.localPosition = new Vector3(
                (section.leftBound + section.rightBound) * 0.5f,
                section.shelfTopY + 0.003f,
                (section.frontBoundZ + section.backBoundZ) * 0.5f);
            guide.transform.localScale = new Vector3(
                section.Width,
                0.006f,
                Mathf.Max(0.02f, section.Depth));
            guide.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(guide.GetComponent<Collider>());
        }

        private static bool IsPreviewBookInsideSection(Vector3 center, Vector3 size, ShelfSection section)
        {
            const float epsilon = 0.0001f;
            var min = center - size * 0.5f;
            var max = center + size * 0.5f;
            return min.x >= section.leftBound - epsilon &&
                   max.x <= section.rightBound + epsilon &&
                   min.y >= section.BookBaseY - epsilon &&
                   max.y <= section.shelfTopY + section.sectionHeight + epsilon &&
                   min.z >= section.frontBoundZ - epsilon &&
                   max.z <= section.backBoundZ + epsilon;
        }

        private static int RenderCameraToPng(Camera camera, string path, Color32[] bookColors)
        {
            const int width = 1280;
            const int height = 960;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return CountBookPixels(texture.GetPixels32(), bookColors);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return CreateMaterial(color);
            }

            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static int CountBookPixels(Color32[] pixels, Color32[] bookColors)
        {
            if (pixels == null || bookColors == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (IsBookColor(pixels[i], bookColors))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsBookColor(Color32 pixel, Color32[] bookColors)
        {
            for (var i = 0; i < bookColors.Length; i++)
            {
                var color = bookColors[i];
                var distance =
                    Mathf.Abs(pixel.r - color.r) +
                    Mathf.Abs(pixel.g - color.g) +
                    Mathf.Abs(pixel.b - color.b);
                if (distance <= 80)
                {
                    return true;
                }
            }

            return false;
        }

        private static Bounds CalculatePrefabLocalRendererBounds(GameObject prefab)
        {
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    instance = UnityEngine.Object.Instantiate(prefab);
                }

                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                return CalculateLocalRendererBounds(instance.transform);
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static Bounds CalculateLocalRendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var hasBounds = false;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                EncapsulateRendererBounds(root, renderer.bounds, ref min, ref max);
                hasBounds = true;
            }

            if (!hasBounds)
            {
                return new Bounds(Vector3.zero, new Vector3(TargetWidth, TargetHeight, 0.25f));
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static void EncapsulateRendererBounds(Transform root, Bounds bounds, ref Vector3 min, ref Vector3 max)
        {
            var bMin = bounds.min;
            var bMax = bounds.max;
            EncapsulateLocalPoint(root, new Vector3(bMin.x, bMin.y, bMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(bMin.x, bMin.y, bMax.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(bMin.x, bMax.y, bMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(bMin.x, bMax.y, bMax.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(bMax.x, bMin.y, bMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(bMax.x, bMin.y, bMax.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(bMax.x, bMax.y, bMin.z), ref min, ref max);
            EncapsulateLocalPoint(root, new Vector3(bMax.x, bMax.y, bMax.z), ref min, ref max);
        }

        private static void EncapsulateLocalPoint(Transform root, Vector3 worldPoint, ref Vector3 min, ref Vector3 max)
        {
            var localPoint = root.InverseTransformPoint(worldPoint);
            min = Vector3.Min(min, localPoint);
            max = Vector3.Max(max, localPoint);
        }

        private static Bounds TransformBounds(Bounds bounds, Vector3 position, float scale, Quaternion rotation)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var bMin = bounds.min;
            var bMax = bounds.max;

            EncapsulateTransformedPoint(new Vector3(bMin.x, bMin.y, bMin.z), position, scale, rotation, ref min, ref max);
            EncapsulateTransformedPoint(new Vector3(bMin.x, bMin.y, bMax.z), position, scale, rotation, ref min, ref max);
            EncapsulateTransformedPoint(new Vector3(bMin.x, bMax.y, bMin.z), position, scale, rotation, ref min, ref max);
            EncapsulateTransformedPoint(new Vector3(bMin.x, bMax.y, bMax.z), position, scale, rotation, ref min, ref max);
            EncapsulateTransformedPoint(new Vector3(bMax.x, bMin.y, bMin.z), position, scale, rotation, ref min, ref max);
            EncapsulateTransformedPoint(new Vector3(bMax.x, bMin.y, bMax.z), position, scale, rotation, ref min, ref max);
            EncapsulateTransformedPoint(new Vector3(bMax.x, bMax.y, bMin.z), position, scale, rotation, ref min, ref max);
            EncapsulateTransformedPoint(new Vector3(bMax.x, bMax.y, bMax.z), position, scale, rotation, ref min, ref max);
            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static void EncapsulateTransformedPoint(Vector3 point, Vector3 position, float scale, Quaternion rotation, ref Vector3 min, ref Vector3 max)
        {
            var transformed = rotation * (point * scale) + position;
            min = Vector3.Min(min, transformed);
            max = Vector3.Max(max, transformed);
        }

        private static Bounds OffsetBounds(Bounds bounds, Vector3 offset)
        {
            return new Bounds(bounds.center + offset, bounds.size);
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) < 0.0001f ? 1f : numerator / denominator;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var normalized = folderPath.Replace('\\', '/');
            var parent = Path.GetDirectoryName(normalized);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            parent = parent.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
        }

        private static string FormatBounds(Bounds bounds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "center=({0:0.###},{1:0.###},{2:0.###}) size=({3:0.###},{4:0.###},{5:0.###})",
                bounds.center.x,
                bounds.center.y,
                bounds.center.z,
                bounds.size.x,
                bounds.size.y,
                bounds.size.z);
        }

        private struct ThemeSpec
        {
            public readonly BookshelfThemeStyle Style;
            public readonly string DisplayName;
            public readonly string AssetPath;
            public readonly string LayoutPath;
            public readonly int Rows;
            public readonly int Columns;
            public readonly float VisualScaleMultiplier;
            public readonly Vector3 PrefabLocalEulerAngles;
            public readonly float SideInset;
            public readonly float BottomInset;
            public readonly float TopInset;
            public readonly float ColumnGap;
            public readonly float RowGap;
            public readonly float FrontInset;
            public readonly float BackInset;
            public readonly float SectionYOffset;
            public readonly float BookYOffset;
            private readonly float[] rowShelfTopY;
            private readonly float[] rowSectionHeight;
            private readonly float[] rowBookYOffset;

            public ThemeSpec(
                BookshelfThemeStyle style,
                string displayName,
                string assetPath,
                string layoutPath,
                int rows,
                int columns,
                float visualScaleMultiplier,
                Vector3 prefabLocalEulerAngles,
                float sideInset,
                float bottomInset,
                float topInset,
                float columnGap,
                float rowGap,
                float frontInset,
                float backInset,
                float sectionYOffset,
                float bookYOffset,
                float[] rowShelfTopY,
                float[] rowSectionHeight,
                float[] rowBookYOffset)
            {
                Style = style;
                DisplayName = displayName;
                AssetPath = assetPath;
                LayoutPath = layoutPath;
                Rows = rows;
                Columns = columns;
                VisualScaleMultiplier = visualScaleMultiplier;
                PrefabLocalEulerAngles = prefabLocalEulerAngles;
                SideInset = sideInset;
                BottomInset = bottomInset;
                TopInset = topInset;
                ColumnGap = columnGap;
                RowGap = rowGap;
                FrontInset = frontInset;
                BackInset = backInset;
                SectionYOffset = sectionYOffset;
                BookYOffset = bookYOffset;
                this.rowShelfTopY = rowShelfTopY;
                this.rowSectionHeight = rowSectionHeight;
                this.rowBookYOffset = rowBookYOffset;
            }

            public float GetRowShelfTopY(int row, float fallback)
            {
                return TryGetRowValue(rowShelfTopY, row, out var value) ? value : fallback;
            }

            public float GetRowSectionHeight(int row, float fallback)
            {
                return TryGetRowValue(rowSectionHeight, row, out var value) ? value : fallback;
            }

            public float GetRowBookYOffset(int row, float fallback)
            {
                return TryGetRowValue(rowBookYOffset, row, out var value) ? value : fallback;
            }

            private static bool TryGetRowValue(float[] values, int row, out float value)
            {
                if (values != null && row >= 0 && row < values.Length)
                {
                    value = values[row];
                    return true;
                }

                value = 0f;
                return false;
            }
        }

        private struct VisualPlacement
        {
            public readonly Vector3 LocalPosition;
            public readonly Vector3 LocalEulerAngles;
            public readonly Vector3 LocalScale;
            public readonly Bounds TransformedBounds;

            public VisualPlacement(Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Bounds transformedBounds)
            {
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                LocalScale = localScale;
                TransformedBounds = transformedBounds;
            }
        }
    }
}
#endif
