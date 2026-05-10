#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using App.Bootstrap;
using App.Domain.Decoration;
using App.Infrastructure.Catalogs;
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

            var catalog = LoadOrCreateCatalog();

            // 기존 entries 의 manual fields 를 SourceAssetPath 키로 인덱싱.
            //   복원 대상 필드 — 디자이너가 Inspector 에서 수정했을 가능성이 있는 것 :
            //     PlacementCategory, AllowRotation, Kind, ScaleMultiplierOverride
            //   복원 비대상 — 파일 시스템 ground truth 인 것 :
            //     DisplayName, Prefab, SourceAssetPath
            // 이전 동작은 entries 를 매 refresh 마다 통째로 덮어써서 NeonSign 의 WallDeco / 회전
            // 비활성 같은 수동 설정이 회귀하던 회귀 버그가 있었다 (dev 머지 후 catalog reserialize
            // 시점에 자주 발생). 이번 변경으로 manual fields 는 보존되고, 파일이 실제로 추가/이동/
            // 삭제될 때만 entries 목록이 갱신된다.
            var existingByPath = new Dictionary<string, EditableDecorationCatalog.Entry>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var existing = catalog.Entries[i];
                if (existing != null && !string.IsNullOrEmpty(existing.SourceAssetPath))
                {
                    existingByPath[existing.SourceAssetPath] = existing;
                }
            }

            var entries = DiscoverEntries();
            for (var i = 0; i < entries.Count; i++)
            {
                var fresh = entries[i];
                if (existingByPath.TryGetValue(fresh.SourceAssetPath, out var prior))
                {
                    // 기존 entry 의 manual fields 복사 — 디자이너 Inspector 작업 보존.
                    fresh.PlacementCategory = prior.PlacementCategory;
                    fresh.AllowRotation = prior.AllowRotation;
                    fresh.Kind = prior.Kind;
                    fresh.ScaleMultiplierOverride = prior.ScaleMultiplierOverride;
                }
                else
                {
                    // 신규 entry — 파일명 기반 휴리스틱으로 Kind/PlacementCategory/AllowRotation 추정.
                    // Inspector 에서 사용자가 나중에 덮어쓸 수 있고, 다음 refresh 에서 그 값이 보존됨.
                    ApplyFilenameHeuristics(fresh);
                }
            }

            entries.Sort((a, b) =>
                string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            catalog.EditorOnly_SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EditableDecorationCatalogBuilder] Refreshed catalog with {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}.");
        }

        // 파일명 / 폴더 토큰으로 신규 entry 의 Kind 와 placement 정책 초기 추정.
        // 디자이너가 Inspector 에서 수정한 값은 다음 refresh 부터 manual-field 보존 경로로 유지.
        private static void ApplyFilenameHeuristics(EditableDecorationCatalog.Entry entry)
        {
            var lowerPath = (entry.SourceAssetPath ?? string.Empty).ToLowerInvariant();
            var lowerName = Path.GetFileNameWithoutExtension(lowerPath) ?? string.Empty;

            // Wall placement 파일명 토큰 — 창문/네온사인/포스터 류는 벽에 붙는다.
            bool looksLikeWall = lowerPath.Contains("/window/") ||
                                 lowerName.Contains("window") ||
                                 lowerName.Contains("neon") ||
                                 lowerName.Contains("poster") ||
                                 lowerName.Contains("frame");
            if (looksLikeWall)
            {
                entry.PlacementCategory = CustomizationCategory.WallDeco;
                entry.AllowRotation = false;
            }

            // Kind 추정 — 가장 구체적인 토큰부터 매칭.
            if (lowerName.Contains("window")) entry.Kind = DecorationObjectKind.Window;
            else if (lowerName.Contains("bed")) entry.Kind = DecorationObjectKind.Bed;
            else if (lowerName.Contains("desk") || lowerName.Contains("table")) entry.Kind = DecorationObjectKind.Desk;
            else if (lowerName.Contains("chair")) entry.Kind = DecorationObjectKind.Chair;
            else if (lowerName.Contains("shelf")) entry.Kind = DecorationObjectKind.Bookshelf;
            else if (lowerName.Contains("storage") || lowerName.Contains("drawer") || lowerName.Contains("cabinet")) entry.Kind = DecorationObjectKind.Storage;
            else if (lowerName.Contains("rug") || lowerName.Contains("carpet") || lowerName.Contains("mat")) entry.Kind = DecorationObjectKind.Rug;
            else if (lowerName.Contains("lamp") || lowerName.Contains("light")) entry.Kind = DecorationObjectKind.Lamp;
            else if (lowerName.Contains("plant") || lowerName.Contains("pot") || lowerName.Contains("flower")) entry.Kind = DecorationObjectKind.Plant;
            else if (lowerName.Contains("pillow") || lowerName.Contains("cushion")) entry.Kind = DecorationObjectKind.Pillow;
            else if (lowerName.Contains("monitor") || lowerName.Contains("laptop") || lowerName.Contains("screen")) entry.Kind = DecorationObjectKind.Monitor;
            else entry.Kind = DecorationObjectKind.Decoration;

            entry.ScaleMultiplierOverride = 0f;
        }

        // Whenever a Models/-rooted asset is added, deleted, or moved, refresh
        // the catalog automatically so the committed Resources asset doesn't
        // drift from the actual model set. Cheaply scans the changed-asset
        // arrays first and only re-runs the discovery pass when something
        // under ModelsRoot actually moved.
        private sealed class ModelsChangePostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                if (TouchesModels(importedAssets) || TouchesModels(deletedAssets) ||
                    TouchesModels(movedAssets) || TouchesModels(movedFromAssetPaths))
                {
                    RefreshCatalog();
                }
            }

            private static bool TouchesModels(string[] paths)
            {
                if (paths == null) return false;
                for (var i = 0; i < paths.Length; i++)
                {
                    if (!string.IsNullOrEmpty(paths[i]) &&
                        paths[i].StartsWith(ModelsRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
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
