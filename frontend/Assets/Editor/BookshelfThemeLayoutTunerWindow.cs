#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using BookshelfPorting.Runtime;
using UnityEditor;
using UnityEngine;

namespace BookshelfPorting.Editor
{
    public sealed class BookshelfThemeLayoutTunerWindow : EditorWindow
    {
        private const string DefaultLayoutPath = "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_UnderwaterShell.asset";
        private const float MinimumColumnWidth = 0.05f;
        private const float MinimumDepth = 0.05f;
        private static readonly LayoutOption[] LayoutOptions =
        {
            new LayoutOption("Pastel Cabin", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_PastelCabin.asset"),
            new LayoutOption("Underwater Shell", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_UnderwaterShell.asset"),
            new LayoutOption("Vintage", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_VintageShelf.asset"),
            new LayoutOption("Spooky", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_Spooky.asset"),
            new LayoutOption("Fantasy", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_Fantasy.asset"),
            new LayoutOption("Space", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_Space.asset"),
            new LayoutOption("Good Vibes", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_GoodVibes.asset"),
            new LayoutOption("Deep Sea", "Assets/Resources/BookshelfThemeLayouts/BookshelfThemeLayout_DeepSea.asset")
        };

        private BookshelfThemeLayout layout;
        private readonly RowTuning[] rows = new RowTuning[4];
        private readonly ColumnTuning[] columns = new ColumnTuning[4];
        private readonly DepthTuning depth = new DepthTuning();
        private Texture2D previewTexture;
        private string previewPath;
        private string previewSummary;
        private Vector2 scroll;
        private bool autoPreview = true;
        private bool autoPreviewPending;
        private double autoPreviewAt;

        [MenuItem("Tools/DotShelf/Bookshelf/Open Layout Tuner")]
        public static void Open()
        {
            var window = GetWindow<BookshelfThemeLayoutTunerWindow>("Bookshelf Layout Tuner");
            window.minSize = new Vector2(520f, 640f);
            window.LoadInitialLayout();
        }

        [MenuItem("Tools/Dotshelf/Bookshelf/Open Layout Tuner")]
        public static void OpenDotshelfAlias()
        {
            Open();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            for (var i = 0; i < rows.Length; i++)
            {
                rows[i] = new RowTuning();
            }

            for (var i = 0; i < columns.Length; i++)
            {
                columns[i] = new ColumnTuning();
            }

            if (layout == null)
            {
                LoadInitialLayout();
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var i = 0; i < LayoutOptions.Length; i++)
                {
                    if (i > 0 && i % 4 == 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }

                    var option = LayoutOptions[i];
                    if (GUILayout.Button(option.Label, GUILayout.Height(28f)))
                    {
                        LoadLayout(option.Path);
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            layout = (BookshelfThemeLayout)EditorGUILayout.ObjectField("Layout Asset", layout, typeof(BookshelfThemeLayout), false);
            if (EditorGUI.EndChangeCheck())
            {
                ReadTuningFromLayout();
                ClearPreview();
            }

            if (layout == null)
            {
                EditorGUILayout.HelpBox("Select a BookshelfThemeLayout asset.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("Rows are bottom to top. shelfTopY moves the row floor. sectionHeight controls the safe vertical space. bookYOffset moves only books relative to that row.", MessageType.None);
            autoPreview = EditorGUILayout.ToggleLeft("Auto Preview", autoPreview);

            var changed = false;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
            for (var i = 0; i < rows.Length; i++)
            {
                changed |= DrawRow(i);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Columns", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Columns are left to right. leftBound/rightBound control the horizontal safe area for every row in that column.", MessageType.None);
            for (var i = 0; i < columns.Length; i++)
            {
                changed |= DrawColumn(i);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("frontBoundZ controls where books align in depth. Out moves books toward the camera; In moves books deeper into the bookshelf.", MessageType.None);
            changed |= DrawDepth();

            EditorGUILayout.EndScrollView();

            if (changed)
            {
                QueueAutoPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply", GUILayout.Height(32f)))
                {
                    autoPreviewPending = false;
                    ApplyTuningToLayout();
                }

                if (GUILayout.Button("Apply & Preview", GUILayout.Height(32f)))
                {
                    autoPreviewPending = false;
                    ApplyTuningToLayout();
                    RenderPreview();
                }

                if (GUILayout.Button("Reload", GUILayout.Height(32f)))
                {
                    autoPreviewPending = false;
                    ReadTuningFromLayout();
                    ClearPreview();
                }
            }

            if (autoPreviewPending)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox("Auto preview queued...", MessageType.None);
            }

            if (!string.IsNullOrEmpty(previewSummary))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(previewSummary, MessageType.Info);
            }

            if (previewTexture != null)
            {
                var availableWidth = Mathf.Max(1f, position.width - 24f);
                var aspect = (float)previewTexture.height / previewTexture.width;
                var rect = GUILayoutUtility.GetRect(availableWidth, availableWidth * aspect, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit);
            }

            if (!string.IsNullOrEmpty(previewPath))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(previewPath, GUILayout.Height(18f));
                    if (GUILayout.Button("Reveal", GUILayout.Width(70f)))
                    {
                        EditorUtility.RevealInFinder(previewPath);
                    }
                }
            }
        }

        private bool DrawRow(int rowIndex)
        {
            var row = rows[rowIndex];
            var changed = false;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Row {rowIndex} ({(rowIndex == 0 ? "bottom" : rowIndex == rows.Length - 1 ? "top" : "middle")})", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var shelfTopY = EditorGUILayout.FloatField("shelfTopY", row.ShelfTopY);
                var sectionHeight = EditorGUILayout.FloatField("sectionHeight", row.SectionHeight);
                var bookYOffset = EditorGUILayout.FloatField("bookYOffset", row.BookYOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    row.ShelfTopY = shelfTopY;
                    row.SectionHeight = sectionHeight;
                    row.BookYOffset = bookYOffset;
                    changed = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("-0.01 Y"))
                    {
                        row.ShelfTopY -= 0.01f;
                        changed = true;
                    }

                    if (GUILayout.Button("+0.01 Y"))
                    {
                        row.ShelfTopY += 0.01f;
                        changed = true;
                    }

                    if (GUILayout.Button("Book -0.005"))
                    {
                        row.BookYOffset -= 0.005f;
                        changed = true;
                    }

                    if (GUILayout.Button("Book +0.005"))
                    {
                        row.BookYOffset += 0.005f;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private bool DrawColumn(int columnIndex)
        {
            var column = columns[columnIndex];
            var changed = false;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Column {columnIndex} ({(columnIndex == 0 ? "left" : columnIndex == columns.Length - 1 ? "right" : "middle")})", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var leftBound = EditorGUILayout.FloatField("leftBound", column.LeftBound);
                var rightBound = EditorGUILayout.FloatField("rightBound", column.RightBound);
                EditorGUILayout.LabelField("width", Mathf.Max(0f, column.RightBound - column.LeftBound).ToString("0.###"));
                if (EditorGUI.EndChangeCheck())
                {
                    column.LeftBound = leftBound;
                    column.RightBound = rightBound;
                    EnsureMinimumColumnWidth(column);
                    changed = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("< 0.005"))
                    {
                        ShiftColumn(column, -0.005f);
                        changed = true;
                    }

                    if (GUILayout.Button("> 0.005"))
                    {
                        ShiftColumn(column, 0.005f);
                        changed = true;
                    }

                    if (GUILayout.Button("Shrink"))
                    {
                        ResizeColumn(column, -0.005f);
                        changed = true;
                    }

                    if (GUILayout.Button("Widen"))
                    {
                        ResizeColumn(column, 0.005f);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private bool DrawDepth()
        {
            var changed = false;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                var frontBoundZ = EditorGUILayout.FloatField("frontBoundZ", depth.FrontBoundZ);
                var backBoundZ = EditorGUILayout.FloatField("backBoundZ", depth.BackBoundZ);
                EditorGUILayout.LabelField("depth", Mathf.Max(0f, depth.BackBoundZ - depth.FrontBoundZ).ToString("0.###"));
                if (EditorGUI.EndChangeCheck())
                {
                    depth.FrontBoundZ = frontBoundZ;
                    depth.BackBoundZ = backBoundZ;
                    EnsureMinimumDepth(depth);
                    changed = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Out 0.005"))
                    {
                        ShiftDepth(depth, -0.005f);
                        changed = true;
                    }

                    if (GUILayout.Button("In 0.005"))
                    {
                        ShiftDepth(depth, 0.005f);
                        changed = true;
                    }

                    if (GUILayout.Button("Shallow"))
                    {
                        ResizeDepth(depth, -0.005f);
                        changed = true;
                    }

                    if (GUILayout.Button("Deepen"))
                    {
                        ResizeDepth(depth, 0.005f);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private void LoadLayout(string path)
        {
            layout = AssetDatabase.LoadAssetAtPath<BookshelfThemeLayout>(path);
            ReadTuningFromLayout();
            ClearPreview();
            Repaint();
        }

        private void LoadInitialLayout()
        {
            var selectedLayout = Selection.activeObject as BookshelfThemeLayout;
            if (selectedLayout != null)
            {
                var selectedPath = AssetDatabase.GetAssetPath(selectedLayout);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    LoadLayout(selectedPath);
                    return;
                }
            }

            LoadLayout(DefaultLayoutPath);
        }

        private void ReadTuningFromLayout()
        {
            ReadRowsFromLayout();
            ReadColumnsFromLayout();
            ReadDepthFromLayout();
        }

        private void ReadRowsFromLayout()
        {
            for (var i = 0; i < rows.Length; i++)
            {
                rows[i] ??= new RowTuning();
            }

            if (layout == null)
            {
                return;
            }

            var rowValues = new Dictionary<int, RowTuning>();
            var serialized = new SerializedObject(layout);
            var sections = serialized.FindProperty("sections");
            for (var i = 0; i < sections.arraySize; i++)
            {
                var section = sections.GetArrayElementAtIndex(i);
                var row = section.FindPropertyRelative("row").intValue;
                if (row < 0 || row >= rows.Length || rowValues.ContainsKey(row))
                {
                    continue;
                }

                rowValues[row] = new RowTuning
                {
                    ShelfTopY = section.FindPropertyRelative("shelfTopY").floatValue,
                    SectionHeight = section.FindPropertyRelative("sectionHeight").floatValue,
                    BookYOffset = section.FindPropertyRelative("bookYOffset").floatValue
                };
            }

            for (var row = 0; row < rows.Length; row++)
            {
                if (rowValues.TryGetValue(row, out var value))
                {
                    rows[row].ShelfTopY = value.ShelfTopY;
                    rows[row].SectionHeight = value.SectionHeight;
                    rows[row].BookYOffset = value.BookYOffset;
                }
            }
        }

        private void ReadColumnsFromLayout()
        {
            for (var i = 0; i < columns.Length; i++)
            {
                columns[i] ??= new ColumnTuning();
            }

            if (layout == null)
            {
                return;
            }

            var columnValues = new Dictionary<int, ColumnTuning>();
            var serialized = new SerializedObject(layout);
            var sections = serialized.FindProperty("sections");
            for (var i = 0; i < sections.arraySize; i++)
            {
                var section = sections.GetArrayElementAtIndex(i);
                var column = section.FindPropertyRelative("column").intValue;
                if (column < 0 || column >= columns.Length || columnValues.ContainsKey(column))
                {
                    continue;
                }

                columnValues[column] = new ColumnTuning
                {
                    LeftBound = section.FindPropertyRelative("leftBound").floatValue,
                    RightBound = section.FindPropertyRelative("rightBound").floatValue
                };
            }

            for (var column = 0; column < columns.Length; column++)
            {
                if (columnValues.TryGetValue(column, out var value))
                {
                    columns[column].LeftBound = value.LeftBound;
                    columns[column].RightBound = value.RightBound;
                    EnsureMinimumColumnWidth(columns[column]);
                }
            }
        }

        private void ReadDepthFromLayout()
        {
            if (layout == null)
            {
                return;
            }

            var serialized = new SerializedObject(layout);
            var sections = serialized.FindProperty("sections");
            for (var i = 0; i < sections.arraySize; i++)
            {
                var section = sections.GetArrayElementAtIndex(i);
                depth.FrontBoundZ = section.FindPropertyRelative("frontBoundZ").floatValue;
                depth.BackBoundZ = section.FindPropertyRelative("backBoundZ").floatValue;
                EnsureMinimumDepth(depth);
                return;
            }
        }

        private void ApplyTuningToLayout()
        {
            if (layout == null)
            {
                return;
            }

            var serialized = new SerializedObject(layout);
            var sections = serialized.FindProperty("sections");
            for (var i = 0; i < sections.arraySize; i++)
            {
                var section = sections.GetArrayElementAtIndex(i);
                var row = section.FindPropertyRelative("row").intValue;
                if (row < 0 || row >= rows.Length)
                {
                    continue;
                }

                section.FindPropertyRelative("shelfTopY").floatValue = rows[row].ShelfTopY;
                section.FindPropertyRelative("sectionHeight").floatValue = rows[row].SectionHeight;
                section.FindPropertyRelative("bookYOffset").floatValue = rows[row].BookYOffset;

                var column = section.FindPropertyRelative("column").intValue;
                if (column >= 0 && column < columns.Length)
                {
                    EnsureMinimumColumnWidth(columns[column]);
                    section.FindPropertyRelative("leftBound").floatValue = columns[column].LeftBound;
                    section.FindPropertyRelative("rightBound").floatValue = columns[column].RightBound;
                }

                EnsureMinimumDepth(depth);
                section.FindPropertyRelative("frontBoundZ").floatValue = depth.FrontBoundZ;
                section.FindPropertyRelative("backBoundZ").floatValue = depth.BackBoundZ;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }

        private void RenderPreview()
        {
            if (layout == null)
            {
                return;
            }

            var ok = BookshelfThemeLayoutAssetBuilder.RenderLayoutPreviewToVerificationFolder(layout, out previewPath, out previewSummary);
            previewSummary = $"{(ok ? "PASS" : "FAIL")}\n{previewSummary}";
            LoadPreviewTexture();
            Repaint();
        }

        private void QueueAutoPreview()
        {
            if (!autoPreview || layout == null)
            {
                return;
            }

            autoPreviewPending = true;
            autoPreviewAt = EditorApplication.timeSinceStartup + 0.25d;
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!autoPreviewPending || EditorApplication.timeSinceStartup < autoPreviewAt)
            {
                return;
            }

            autoPreviewPending = false;
            ApplyTuningToLayout();
            RenderPreview();
        }

        private static void ShiftColumn(ColumnTuning column, float amount)
        {
            if (column == null)
            {
                return;
            }

            column.LeftBound += amount;
            column.RightBound += amount;
        }

        private static void ResizeColumn(ColumnTuning column, float amount)
        {
            if (column == null)
            {
                return;
            }

            column.LeftBound -= amount;
            column.RightBound += amount;
            EnsureMinimumColumnWidth(column);
        }

        private static void EnsureMinimumColumnWidth(ColumnTuning column)
        {
            if (column == null || column.RightBound - column.LeftBound >= MinimumColumnWidth)
            {
                return;
            }

            var center = (column.LeftBound + column.RightBound) * 0.5f;
            column.LeftBound = center - MinimumColumnWidth * 0.5f;
            column.RightBound = center + MinimumColumnWidth * 0.5f;
        }

        private static void ShiftDepth(DepthTuning targetDepth, float amount)
        {
            if (targetDepth == null)
            {
                return;
            }

            targetDepth.FrontBoundZ += amount;
            targetDepth.BackBoundZ += amount;
        }

        private static void ResizeDepth(DepthTuning targetDepth, float amount)
        {
            if (targetDepth == null)
            {
                return;
            }

            targetDepth.FrontBoundZ -= amount;
            targetDepth.BackBoundZ += amount;
            EnsureMinimumDepth(targetDepth);
        }

        private static void EnsureMinimumDepth(DepthTuning targetDepth)
        {
            if (targetDepth == null || targetDepth.BackBoundZ - targetDepth.FrontBoundZ >= MinimumDepth)
            {
                return;
            }

            var center = (targetDepth.FrontBoundZ + targetDepth.BackBoundZ) * 0.5f;
            targetDepth.FrontBoundZ = center - MinimumDepth * 0.5f;
            targetDepth.BackBoundZ = center + MinimumDepth * 0.5f;
        }

        private void LoadPreviewTexture()
        {
            if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath))
            {
                previewTexture = null;
                return;
            }

            var bytes = File.ReadAllBytes(previewPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            previewTexture = texture;
        }

        private void ClearPreview()
        {
            previewTexture = null;
            previewPath = string.Empty;
            previewSummary = string.Empty;
        }

        private sealed class RowTuning
        {
            public float ShelfTopY;
            public float SectionHeight;
            public float BookYOffset;
        }

        private sealed class ColumnTuning
        {
            public float LeftBound;
            public float RightBound;
        }

        private sealed class DepthTuning
        {
            public float FrontBoundZ;
            public float BackBoundZ;
        }

        private readonly struct LayoutOption
        {
            public readonly string Label;
            public readonly string Path;

            public LayoutOption(string label, string path)
            {
                Label = label;
                Path = path;
            }
        }
    }
}
#endif
