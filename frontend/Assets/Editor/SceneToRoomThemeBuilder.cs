#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using App.Bootstrap;
using App.Infrastructure.Catalogs;
using UnityEditor;
using UnityEngine;

namespace App.EditorTools
{
    // Scene 에 배치한 DecorationItemMarker 들을 읽어 RoomTheme_*.asset 의 floorDecoSpecs /
    // wallDecoSpecs 에 저장하는 Editor 도구. 디자이너가 Editor 모드에서 가구를 직접 배치 →
    // 메뉴 클릭 → 선택한 RoomTheme 자산에 자동 write-back.
    //
    // 사용법:
    //   1. Tools → DotShelf → Theme → Save Scene Placement to RoomTheme... 클릭
    //   2. EditorWindow 의 "Target Theme" 슬롯에 저장할 RoomTheme_*.asset 드래그
    //   3. "Source Root" 슬롯에 marker 들을 모은 부모 Transform 지정
    //      (default: GameObject.Find("PlacedDecorations") — 런타임 placedItemsRoot 와 동명)
    //   4. "Preview" 클릭 → 콘솔에 수집 결과 / ThemeSlot 매핑 미리보기
    //   5. "Save to RoomTheme" 클릭 → 실제 asset 에 SerializedObject 로 write + SaveAssets
    //
    // 정책 (MVP, 2026-05-14):
    //   - **수집**: source root 의 모든 자손 (include inactive) 중 DecorationItemMarker 보유 GameObject.
    //   - **prefab reference**: PrefabUtility.GetCorrespondingObjectFromOriginalSource() 로 역추적.
    //     실패 시 LogWarning + skip (저장 시점에 prefab 없는 spec 은 의미 없음).
    //   - **assetKey**: marker.InventoryKey 의 "path:" prefix 제거 → "Assets/Arts/Models/...glb".
    //     InventoryKey 비어있으면 AssetDatabase.GetAssetPath(prefab) fallback.
    //   - **ThemeSlot**: filename 휴리스틱 (table_mini → TableMini, shelf → Shelf, lamp → Lamp, ...).
    //     매칭 실패 시 ThemeSlot.None + LogWarning (사용자가 Inspector 에서 수동 set).
    //   - **WallSide**: Frame / Window 슬롯은 WallSide.Back 디폴트, 나머지는 None.
    //   - **localPosition / localRotation / localScale**: source root 기준 local frame
    //     (sourceRoot.InverseTransformPoint / 회전 inverse). source root 가 world 원점에 있으면 곧 world.
    //   - **NotebookStation 자식 skip 가드**: marker 의 ancestor 이름에 "NotebookStation" /
    //     "DashboardScreenPanel" / "GuestbookBoard" 포함되면 skip — DeskVisualSkinApplier /
    //     DashboardVisualSkinApplier 흐름과 중복 spawn 회귀 (직전 vintage_table 케이스) 차단.
    //   - **저장 정책**: 전체 교체 (full replace). 기존 specs 는 모두 destroy 후 새 array 로 set.
    //     Bookshelf spec list (bookshelfDecoSpecs) 는 이번 도구에서 안 건드림 — Shelf 슬롯도
    //     floorDecoSpecs 로 통합 (기존 vintage_shelf 가 floorDecoSpecs[0] 에 등록된 패턴 유지).
    public sealed class SceneToRoomThemeBuilder : EditorWindow
    {
        private RoomThemeAsset targetTheme;
        private Transform sourceRoot;
        private bool skipFunctionalRoots = true;
        private Vector2 scrollPos;
        private string lastReport = string.Empty;

        [MenuItem("Tools/DotShelf/Theme/Save Scene Placement to RoomTheme...")]
        public static void OpenWindow()
        {
            var window = GetWindow<SceneToRoomThemeBuilder>("Scene → RoomTheme");
            window.minSize = new Vector2(420f, 380f);
            window.TryAutoFillSourceRoot();
            window.Show();
        }

        private void TryAutoFillSourceRoot()
        {
            if (sourceRoot != null) return;
            var found = GameObject.Find("PlacedDecorations");
            if (found != null) sourceRoot = found.transform;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Scene → RoomTheme Spec Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scene 에 배치한 DecorationItemMarker 들을 읽어 RoomTheme 자산의 " +
                "floorDecoSpecs / wallDecoSpecs 에 저장합니다. 기존 specs 는 전체 교체됩니다.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            targetTheme = (RoomThemeAsset)EditorGUILayout.ObjectField(
                new GUIContent("Target Theme", "저장 대상 RoomTheme_*.asset"),
                targetTheme, typeof(RoomThemeAsset), false);

            sourceRoot = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Source Root", "DecorationItemMarker 들을 모은 부모 transform. " +
                                              "비어있으면 'PlacedDecorations' 이름 GameObject 자동 검색."),
                sourceRoot, typeof(Transform), allowSceneObjects: true);

            skipFunctionalRoots = EditorGUILayout.Toggle(
                new GUIContent("Skip functional roots",
                    "NotebookStation / DashboardScreenPanel / GuestbookBoard 자식은 skip — " +
                    "Desk/Dashboard VisualSkinApplier 흐름과 중복 spawn 회귀 차단."),
                skipFunctionalRoots);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledGroupScope(targetTheme == null || sourceRoot == null))
            {
                if (GUILayout.Button("Preview (dry run, 콘솔 + 아래 패널 출력)"))
                {
                    var collected = CollectAndInfer(out var report);
                    lastReport = report;
                    Debug.Log(report);
                    Debug.Log($"[SceneToRoomTheme] Preview — collected {collected.Count} markers " +
                              $"(target={targetTheme.name}, source={sourceRoot.name}). 저장은 'Save' 버튼 사용.");
                }

                if (GUILayout.Button("Save to RoomTheme (write + SaveAssets)"))
                {
                    if (EditorUtility.DisplayDialog(
                        "RoomTheme spec 전체 교체",
                        $"'{targetTheme.name}' 의 floorDecoSpecs / wallDecoSpecs 를 " +
                        $"'{sourceRoot.name}' 자손 marker 기반으로 전체 교체합니다. " +
                        $"기존 spec list 는 모두 사라집니다. 계속하시겠습니까?",
                        "Save", "Cancel"))
                    {
                        var collected = CollectAndInfer(out var report);
                        lastReport = report;
                        Debug.Log(report);
                        SaveToTheme(collected);
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Last report", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(160f));
            EditorGUILayout.TextArea(lastReport ?? string.Empty, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private struct CollectedSpec
        {
            public GameObject Prefab;
            public Vector3 LocalPosition;
            public Vector3 LocalRotation;
            public Vector3 LocalScale;
            public string AnchorTag;
            public ThemeSlot ThemeSlot;
            public WallSide WallSide;
            public string AssetKey;
        }

        private List<CollectedSpec> CollectAndInfer(out string report)
        {
            var sb = new System.Text.StringBuilder();
            var result = new List<CollectedSpec>();
            if (targetTheme == null || sourceRoot == null)
            {
                report = "[SceneToRoomTheme] target theme 또는 source root 미지정 — collect skip.";
                return result;
            }

            var markers = sourceRoot.GetComponentsInChildren<DecorationItemMarker>(includeInactive: true);
            sb.AppendLine($"[SceneToRoomTheme] target={targetTheme.name}, source={sourceRoot.name}, " +
                          $"discovered markers={markers.Length}");

            int skipped = 0, unmappedSlot = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                var go = m.gameObject;

                if (skipFunctionalRoots && IsUnderFunctionalRoot(go.transform))
                {
                    sb.AppendLine($"  SKIP (functional root) — {go.name}");
                    skipped++;
                    continue;
                }

                var prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                if (prefab == null)
                {
                    prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                }
                if (prefab == null)
                {
                    sb.AppendLine($"  SKIP (no prefab source) — {go.name} (PrefabUtility 가 source 못 찾음)");
                    skipped++;
                    continue;
                }

                var assetKey = ResolveAssetKey(m.InventoryKey, prefab);
                var slot = InferThemeSlot(assetKey, prefab.name);
                var wallSide = InferWallSide(slot);

                if (slot == ThemeSlot.None)
                {
                    unmappedSlot++;
                    sb.AppendLine($"  WARN (ThemeSlot 미매칭, None 으로 저장) — {go.name} assetKey='{assetKey}'");
                }

                var localPos = sourceRoot.InverseTransformPoint(go.transform.position);
                var localRot = (Quaternion.Inverse(sourceRoot.rotation) * go.transform.rotation).eulerAngles;
                var localScale = go.transform.localScale; // sourceRoot scale 1 가정

                result.Add(new CollectedSpec
                {
                    Prefab = prefab,
                    LocalPosition = localPos,
                    LocalRotation = localRot,
                    LocalScale = localScale,
                    AnchorTag = MakeAnchorTag(prefab.name),
                    ThemeSlot = slot,
                    WallSide = wallSide,
                    AssetKey = assetKey,
                });

                sb.AppendLine($"  OK  {go.name} → slot={slot} wall={wallSide} pos={Format(localPos)} " +
                              $"rotY={localRot.y:F1}° scale={Format(localScale)} key={assetKey}");
            }

            int floorCount = 0, wallCount = 0;
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].WallSide == WallSide.None) floorCount++; else wallCount++;
            }
            sb.AppendLine();
            sb.AppendLine($"[Summary] collected={result.Count} skipped={skipped} unmappedSlot={unmappedSlot}");
            sb.AppendLine($"  → floorDecoSpecs candidates={floorCount}, wallDecoSpecs candidates={wallCount}");

            report = sb.ToString();
            return result;
        }

        private void SaveToTheme(List<CollectedSpec> collected)
        {
            if (targetTheme == null) return;

            var floor = new List<CollectedSpec>();
            var wall = new List<CollectedSpec>();
            for (int i = 0; i < collected.Count; i++)
            {
                if (collected[i].WallSide == WallSide.None) floor.Add(collected[i]);
                else wall.Add(collected[i]);
            }

            var so = new SerializedObject(targetTheme);
            WriteSpecArray(so.FindProperty("floorDecoSpecs"), floor);
            WriteSpecArray(so.FindProperty("wallDecoSpecs"), wall);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(targetTheme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneToRoomTheme] SAVED to '{targetTheme.name}': " +
                      $"floorDecoSpecs={floor.Count}, wallDecoSpecs={wall.Count}. " +
                      $"AssetDatabase.SaveAssets() 호출 완료.");
        }

        private static void WriteSpecArray(SerializedProperty arrayProp, List<CollectedSpec> entries)
        {
            if (arrayProp == null || !arrayProp.isArray) return;
            arrayProp.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var elem = arrayProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("assetId").longValue = 0;
                elem.FindPropertyRelative("prefab").objectReferenceValue = e.Prefab;
                elem.FindPropertyRelative("wallSide").enumValueIndex = (int)e.WallSide;
                elem.FindPropertyRelative("localPosition").vector3Value = e.LocalPosition;
                elem.FindPropertyRelative("localRotation").vector3Value = e.LocalRotation;
                elem.FindPropertyRelative("localScale").vector3Value = e.LocalScale;
                elem.FindPropertyRelative("anchorTag").stringValue = e.AnchorTag ?? string.Empty;
                elem.FindPropertyRelative("themeSlot").enumValueIndex = (int)e.ThemeSlot;
                elem.FindPropertyRelative("assetKey").stringValue = e.AssetKey ?? string.Empty;
                elem.FindPropertyRelative("randomEligible").boolValue = true;
                elem.FindPropertyRelative("previewPrefab").objectReferenceValue = null;
            }
        }

        private static bool IsUnderFunctionalRoot(Transform t)
        {
            var current = t.parent;
            while (current != null)
            {
                var name = current.gameObject.name;
                if (name.IndexOf("NotebookStation", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("DashboardScreenPanel", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("GuestbookBoard", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                current = current.parent;
            }
            return false;
        }

        // InventoryKey 형식 예: "path:Assets/Arts/Models/vintage_lamp_mint.glb" → "Assets/Arts/Models/..."
        // path: prefix 가 없으면 AssetDatabase.GetAssetPath(prefab) fallback.
        private static string ResolveAssetKey(string inventoryKey, GameObject prefab)
        {
            if (!string.IsNullOrEmpty(inventoryKey))
            {
                const string PathPrefix = "path:";
                if (inventoryKey.StartsWith(PathPrefix, System.StringComparison.Ordinal))
                {
                    return inventoryKey.Substring(PathPrefix.Length);
                }
            }
            if (prefab != null)
            {
                var path = AssetDatabase.GetAssetPath(prefab);
                if (!string.IsNullOrEmpty(path)) return path;
            }
            return inventoryKey ?? string.Empty;
        }

        // Filename 휴리스틱 — assetKey / prefab.name 중 어느 쪽이든 substring 매칭. mini 가 generic table
        // 의 substring 이라 mini 먼저 검사 (RoomEditModeController.TryFindVintageSpecByAssetKey 의
        // 매칭 우선순위와 동일 정책).
        private static ThemeSlot InferThemeSlot(string assetKey, string prefabName)
        {
            var hay = ((assetKey ?? string.Empty) + "|" + (prefabName ?? string.Empty)).ToLowerInvariant();

            if (Contains(hay, "table_mini") || Contains(hay, "tablemini") || Contains(hay, "mini_table"))
                return ThemeSlot.TableMini;
            if (Contains(hay, "shelf") || Contains(hay, "bookshelf") || Contains(hay, "bookcase") || Contains(hay, "cabinet"))
                return ThemeSlot.Shelf;
            if (Contains(hay, "table") || Contains(hay, "desk"))
                return ThemeSlot.Table;
            if (Contains(hay, "lamp") || Contains(hay, "lantern"))
                return ThemeSlot.Lamp;
            if (Contains(hay, "frame") || Contains(hay, "painting") || Contains(hay, "picture") || Contains(hay, "poster"))
                return ThemeSlot.Frame;
            if (Contains(hay, "rug") || Contains(hay, "carpet") || Contains(hay, "mat"))
                return ThemeSlot.Rug;
            if (Contains(hay, "bed"))
                return ThemeSlot.Bed;
            if (Contains(hay, "window"))
                return ThemeSlot.Window;
            if (Contains(hay, "plant") || Contains(hay, "tree") || Contains(hay, "flower") || Contains(hay, "pot"))
                return ThemeSlot.Plant;
            if (Contains(hay, "dashboard") || Contains(hay, "guestbook"))
                return ThemeSlot.Dashboard;
            return ThemeSlot.None;
        }

        private static bool Contains(string hay, string needle) =>
            hay.IndexOf(needle, System.StringComparison.Ordinal) >= 0;

        // 벽 부착 슬롯만 wallDecoSpecs 로 저장. 디자이너가 wall mount 의도가 다른 면이면
        // Inspector 에서 wallSide 수동 변경 (Back/Left/Right/Front).
        private static WallSide InferWallSide(ThemeSlot slot)
        {
            if (slot == ThemeSlot.Frame || slot == ThemeSlot.Window) return WallSide.Back;
            return WallSide.None;
        }

        private static string MakeAnchorTag(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return string.Empty;
            // CamelCase 변환: "vintage_lamp_mint" → "vintageLampMint"
            var parts = prefabName.Split('_');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                if (i == 0)
                {
                    sb.Append(char.ToLowerInvariant(parts[i][0]));
                    if (parts[i].Length > 1) sb.Append(parts[i].Substring(1));
                }
                else
                {
                    sb.Append(char.ToUpperInvariant(parts[i][0]));
                    if (parts[i].Length > 1) sb.Append(parts[i].Substring(1));
                }
            }
            return sb.ToString();
        }

        private static string Format(Vector3 v) =>
            $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
    }
}
#endif
