#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using App.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace App.EditorTools
{
    // Scans Assets/Arts/Models (Furniture, Props, etc.) and writes the discovered
    // GameObject prefabs into a single EditableDecorationCatalog at
    // Assets/Resources/EditableDecorationCatalog.asset. RoomEditModeController loads
    // that asset at runtime to populate its bottom inventory panel.
    //
    // Run "Tools/Dotshelf/Refresh Decoration Catalog" any time models are added or
    // removed. The asset is created the first time this runs.
    public static class EditableDecorationCatalogBuilder
    {
        private const string ModelsRoot = "Assets/Arts/Models";
        private const string ResourcesDir = "Assets/Resources";
        private const string CatalogAssetPath = "Assets/Resources/EditableDecorationCatalog.asset";

        [MenuItem("Tools/Dotshelf/Refresh Decoration Catalog")]
        public static void RefreshCatalog()
        {
            if (!AssetDatabase.IsValidFolder(ModelsRoot))
            {
                Debug.LogWarning($"[EditableDecorationCatalogBuilder] {ModelsRoot} not found — nothing to refresh.");
                return;
            }

            var entries = DiscoverEntries();
            entries.Sort((a, b) =>
                string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            var catalog = LoadOrCreateCatalog();
            catalog.EditorOnly_SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EditableDecorationCatalogBuilder] Refreshed catalog with {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}.");
        }

        public static List<EditableDecorationCatalog.Entry> DiscoverEntries()
        {
            var entries = new List<EditableDecorationCatalog.Entry>();
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { ModelsRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".glb" && ext != ".fbx" && ext != ".prefab" && ext != ".obj")
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                entries.Add(new EditableDecorationCatalog.Entry
                {
                    DisplayName = ToDisplayName(Path.GetFileNameWithoutExtension(path)),
                    Prefab = prefab,
                    SourceAssetPath = path,
                });
            }
            return entries;
        }

        private static EditableDecorationCatalog LoadOrCreateCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EditableDecorationCatalog>(CatalogAssetPath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesDir))
            {
                Directory.CreateDirectory(ResourcesDir);
                AssetDatabase.Refresh();
            }

            var instance = ScriptableObject.CreateInstance<EditableDecorationCatalog>();
            AssetDatabase.CreateAsset(instance, CatalogAssetPath);
            return instance;
        }

        private static string ToDisplayName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "decoration";
            }

            var parts = raw.Replace('-', ' ').Replace('_', ' ').Split(' ');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) +
                           (parts[i].Length > 1 ? parts[i].Substring(1).ToLowerInvariant() : string.Empty);
            }
            return string.Join(" ", parts);
        }
    }
}
#endif
