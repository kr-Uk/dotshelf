using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // DeskCatalog entry 의 prefab (예: vintage_table.glb) 을 일반 mesh swap (roomRoot 자식 +
    // hardcoded placementAnchor) 가 아니라 functional Desk transform 위의 "visual skin" 자식으로
    // attach 하는 static helper. BookshelfVisualSkinApplier 와 동일 정책.
    //
    // 정책 (특정 테마 하드코딩 없음 — CustomizationCategory.Desk 의 mesh swap 경로 전체에 일관 적용):
    //   - functional Desk root 자체는 절대 destroy/reparent 하지 않는다 — Notebook (laptop)
    //     interaction / NotebookHitArea collider / NotebookScreenController / NotebookStation
    //     자체의 GuestbookBoard-local 위치 모두 그대로 유지.
    //   - skin prefab 은 Desk 자식으로 instantiate, 이름 = SkinChildName.
    //   - functional desk bounds (Desk 자식 renderer 중 Notebook subtree 제외) 를 reference 로
    //     비등방 scale + bounds-center 정렬. pivot-agnostic — bottom-center / mesh-center / 임의
    //     좌표 어느 쪽이든 동일하게 정렬. entry.PlacementAnchor / PlacementScale 은 무시 (designer
    //     hardcoded 값이 prefab raw 크기에 따라 어긋남이 회귀 원인이었음).
    //   - skin 의 모든 Collider 는 disable — laptop NotebookHitArea 의 raycast / focus / drag
    //     판정을 가리지 않음.
    //   - Desk 자식 기존 mesh Renderer 들은 attach 시점에 disable 하고 marker 에 저장, clear 시
    //     enable 원복. Notebook subtree (laptop body / screen / keyboard / hit area) 는 hide
    //     대상에서 제외 — laptop visual 보존.
    //   - laptop Y 재정렬: skin world bounds.max.y → Desk-local y 변환 후 laptop.localPosition.y
    //     = deskTopLocalY + LaptopBottomOffset. X / Z 와 rotation 은 native 보존. native localPos
    //     는 marker 에 캡처해 clear 시 정확 복원.
    //   - 같은 위치에 이미 skin 이 있으면 destroy 후 새 skin 으로 교체 (idempotent re-apply).
    //   - 회귀 가드: attach 전/후 laptop world position 캡처 후 deltaY 만 의도된 변화, deltaX/deltaZ
    //     는 거의 0 이어야 정상. delta 가 의도와 다르면 warning.
    public static class DeskVisualSkinApplier
    {
        // procedural BookshelfGenerator.BuildNotebookStation (line 4201) 의 laptop y offset.
        // 책 두께 0.018 의 절반 (~0.009) — laptop 의 UnibodyBase 가 책상 위에 자연스럽게 닿는
        // hint. skin top + 이 offset 으로 laptop y 결정.
        private const float LaptopBottomOffset = 0.009f;

        // 호출 책임: category == CustomizationCategory.Desk 인 mesh swap entry 에 대해서만.
        // 반환: 부착 성공 여부.
        public static bool TryAttach(CustomizationEntry entry, Transform roomRoot, string source)
        {
            if (entry == null || entry.Prefab == null)
            {
                Debug.LogWarning($"[DeskSkin] TryAttach skip — entry 또는 entry.Prefab null. source={source}");
                return false;
            }

            Debug.Log($"[DeskSkin] TryAttach enter entryAssetId={entry.AssetId} prefab={entry.Prefab.name} source={source}");

            if (!TryFindDeskAndNotebook(roomRoot, out var deskTransform, out var notebookTransform, out var failReason))
            {
                Debug.LogWarning($"[DeskSkin] TryAttach FAIL reason={failReason} entryAssetId={entry.AssetId} source={source}. " +
                                 "BookshelfGenerator.BuildNotebookStation 이 NotebookStation > Desk > Notebook hierarchy 를 만든 후 호출되는지 확인.");
                return false;
            }

            // functional desk bounds — Desk 자식 renderer 중 Notebook subtree 제외 envelope.
            // 이 bounds 의 size 를 vintage_table 같은 skin GLB 의 raw bounds 에 fit 시키면 책상
            // 크기가 기존 default (table_natural ~ DeskTargetWidthX/HeightY/DepthZ) 와 비슷하게
            // 유지된다. center 정렬로 책상 위치 (NotebookStation 의 world 좌표) 도 그대로 유지.
            var functionalBounds = ComputeFunctionalDeskBoundsWorld(deskTransform, notebookTransform);
            if (!functionalBounds.HasValue ||
                functionalBounds.Value.size.x < 1e-4f ||
                functionalBounds.Value.size.y < 1e-4f ||
                functionalBounds.Value.size.z < 1e-4f)
            {
                Debug.LogWarning($"[DeskSkin] TryAttach FAIL reason=functional_desk_bounds_invalid (bounds={(functionalBounds.HasValue ? functionalBounds.Value.size.ToString() : "null")}) entryAssetId={entry.AssetId} source={source}.");
                return false;
            }

            // 기존 skin 제거 (이전 vintage 적용 후 다시 vintage 또는 다른 desk skin 적용)
            ClearSkinUnder(deskTransform, notebookTransform);

            // laptop 의 native localPosition 스냅샷 — clear 시 정확히 복원하기 위한 reference.
            // attach 직후 laptop y 를 skin top 으로 옮기는데, 다음 테마에서 default desk 로 돌아
            // 가면 procedural BuildDeskSurface 의 deskTopY 기준 native y 로 되돌려야 함.
            var nativeLaptopLocal = notebookTransform.localPosition;
            var laptopBeforeWorld = notebookTransform.position;

            // skin instantiate as child of functional Desk transform.
            // localPosition=zero / rotation=zero / scale=one 으로 reset 후 아래에서 scale 적용 +
            // bounds-center 정렬 translation 으로 functional desk envelope 와 정확히 일치시킴.
            // entry.PlacementAnchor / PlacementRotation / PlacementScale 은 사용 안 함 (designer
            // hardcoded 값이 prefab raw 크기에 따라 어긋남이 vintage 회귀 원인이었음).
            var go = Object.Instantiate(entry.Prefab, deskTransform);
            go.name = DeskVisualSkin.SkinChildName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localEulerAngles = Vector3.zero;
            go.transform.localScale = Vector3.one;

            DisableAllColliders(go);

            // 비등방 scale — functional bounds size 에 정확히 fit. skin GLB 의 raw 종횡비가
            // 어떻든 width/height/depth 각각 functional 과 일치. 그 위에 entry.SkinScaleMultiplier
            // 를 componentwise 곱해 디자이너 fine-tune 적용 (예: vintage_table (0.7, 0.5, 0.5)
            // — auto-fit 으로 functional 과 같아진 크기를 가로 70% / 높이·깊이 50% 로 축소).
            // multiplier 가 (1,1,1) 또는 zero 면 auto-fit 결과 그대로.
            var skinBoundsBefore = ComputeRendererBoundsSafe(go);
            Vector3 scaleFactor = Vector3.one;
            Vector3 multiplier = SanitizeMultiplier(entry.SkinScaleMultiplier);
            if (skinBoundsBefore.HasValue &&
                skinBoundsBefore.Value.size.x > 1e-4f &&
                skinBoundsBefore.Value.size.y > 1e-4f &&
                skinBoundsBefore.Value.size.z > 1e-4f)
            {
                scaleFactor = new Vector3(
                    Mathf.Clamp((functionalBounds.Value.size.x / skinBoundsBefore.Value.size.x) * multiplier.x, 0.01f, 100f),
                    Mathf.Clamp((functionalBounds.Value.size.y / skinBoundsBefore.Value.size.y) * multiplier.y, 0.01f, 100f),
                    Mathf.Clamp((functionalBounds.Value.size.z / skinBoundsBefore.Value.size.z) * multiplier.z, 0.01f, 100f));
                go.transform.localScale = scaleFactor;
            }

            // bounds-center 정렬 translation — scale 적용 직후 측정한 skin 의 world-space renderer
            // bounds.center 를 functional bounds.center 와 일치시킨다. pivot-agnostic 정렬.
            // BookshelfVisualSkinApplier 와 동일 패턴.
            var skinBoundsAfterScale = ComputeRendererBoundsSafe(go);
            Vector3 worldOffset = Vector3.zero;
            if (skinBoundsAfterScale.HasValue && skinBoundsAfterScale.Value.size.sqrMagnitude > 1e-8f)
            {
                worldOffset = functionalBounds.Value.center - skinBoundsAfterScale.Value.center;
                go.transform.position += worldOffset;
            }
            var skinBoundsAfter = ComputeRendererBoundsSafe(go);

            // marker 부착 + 보관 데이터 set
            var marker = go.AddComponent<DeskVisualSkin>();
            marker.EntryAssetId = entry.AssetId;
            marker.NativeLaptopLocalPosition = nativeLaptopLocal;
            marker.HasNativeLaptopCapture = true;

            // native desk renderer hide — Notebook subtree 는 제외 (laptop 시각 보존).
            HideNativeDeskRenderers(deskTransform, notebookTransform, marker);

            // laptop Y 재정렬 — skin world top 을 Desk-local 로 변환 후 LaptopBottomOffset 더함.
            // X / Z 는 native 그대로 보존 (놓이는 평면 anchor 의 designer 의도 유지).
            float deskTopLocalY = 0f;
            if (skinBoundsAfter.HasValue)
            {
                var deskTopWorld = new Vector3(notebookTransform.position.x, skinBoundsAfter.Value.max.y, notebookTransform.position.z);
                deskTopLocalY = deskTransform.InverseTransformPoint(deskTopWorld).y;
            }
            else
            {
                // skin bounds 못 잡으면 functional bounds 의 max.y 로 fallback
                var deskTopWorld = new Vector3(notebookTransform.position.x, functionalBounds.Value.max.y, notebookTransform.position.z);
                deskTopLocalY = deskTransform.InverseTransformPoint(deskTopWorld).y;
            }
            float newLaptopLocalY = deskTopLocalY + LaptopBottomOffset;
            notebookTransform.localPosition = new Vector3(nativeLaptopLocal.x, newLaptopLocalY, nativeLaptopLocal.z);

            var laptopAfterWorld = notebookTransform.position;
            var laptopDelta = laptopAfterWorld - laptopBeforeWorld;

            Debug.Log($"[DeskSkin] entryAssetId={entry.AssetId} prefab={entry.Prefab.name} deskRoot={deskTransform.name} notebook={notebookTransform.name} " +
                      $"functionalDeskBounds=(center={functionalBounds.Value.center}, size={functionalBounds.Value.size}) " +
                      $"skinBoundsBefore={(skinBoundsBefore.HasValue ? skinBoundsBefore.Value.size.ToString() : "null")} " +
                      $"skinBoundsAfter={(skinBoundsAfter.HasValue ? skinBoundsAfter.Value.size.ToString() : "null")} " +
                      $"skinBoundsCenter={(skinBoundsAfter.HasValue ? skinBoundsAfter.Value.center.ToString() : "null")} " +
                      $"scaleFactor=({scaleFactor.x:F3},{scaleFactor.y:F3},{scaleFactor.z:F3}) multiplier=({multiplier.x:F3},{multiplier.y:F3},{multiplier.z:F3}) worldOffset={worldOffset} " +
                      $"deskTopLocalY={deskTopLocalY:F4} laptopBottomOffset={LaptopBottomOffset:F4} " +
                      $"laptopBeforeWorld={laptopBeforeWorld} laptopAfterWorld={laptopAfterWorld} laptopDelta={laptopDelta} source={source}");

            // X / Z 변위는 0 이어야 정상 (Y 만 의도된 변화). 회귀 가드.
            if (Mathf.Abs(laptopDelta.x) > 1e-3f || Mathf.Abs(laptopDelta.z) > 1e-3f)
            {
                Debug.LogWarning($"[DeskSkin] LAPTOP XZ DRIFTED — laptopDelta=({laptopDelta.x:F4},{laptopDelta.y:F4},{laptopDelta.z:F4}) entryAssetId={entry.AssetId}. " +
                                 "X / Z 는 native anchor 유지여야 정상 — Desk transform 자체가 흔들리지 않았는지 확인.");
            }
            return true;
        }

        // 외부 (테마 전환 시) 명시 호출. functional Desk / Notebook 못 찾으면 no-op.
        public static void ClearSkin(Transform roomRoot, string source = null)
        {
            if (!TryFindDeskAndNotebook(roomRoot, out var deskTransform, out var notebookTransform, out _))
            {
                return;
            }
            var existing = deskTransform.Find(DeskVisualSkin.SkinChildName);
            bool exists = existing != null;
            ClearSkinUnder(deskTransform, notebookTransform);
            if (exists)
            {
                Debug.Log($"[DeskSkinRestore] entryAssetId=- visualExists={exists} functionalDeskPreserved=true laptopRestored={(notebookTransform != null ? "true" : "false")} source={source ?? "ClearSkin"}");
            }
        }

        private static void ClearSkinUnder(Transform deskTransform, Transform notebookTransform)
        {
            if (deskTransform == null) return;
            var existing = deskTransform.Find(DeskVisualSkin.SkinChildName);
            if (existing == null) return;

            var marker = existing.GetComponent<DeskVisualSkin>();
            if (marker != null)
            {
                // hidden native renderer 복원
                var hidden = marker.HiddenFunctionalRenderers;
                for (int i = 0; i < hidden.Count; i++)
                {
                    var r = hidden[i];
                    if (r != null) r.enabled = true;
                }
                hidden.Clear();

                // laptop native localPosition 복원 — attach 시 캡처한 값으로 정확히 되돌림.
                if (marker.HasNativeLaptopCapture && notebookTransform != null)
                {
                    notebookTransform.localPosition = marker.NativeLaptopLocalPosition;
                }
            }
            // FQN — App.Application namespace 와 충돌 회피.
            if (UnityEngine.Application.isPlaying) Object.Destroy(existing.gameObject);
            else Object.DestroyImmediate(existing.gameObject);
        }

        // roomRoot 아래에서 NotebookStation > Desk > Notebook 경로 탐색.
        // 두 경로 다 — procedural BookshelfGenerator.BuildNotebookStation (line 4178, 4191, 4199)
        // 및 NotebookStation.prefab (m_Name: NotebookStation/Desk/Notebook) — 같은 이름을 쓴다.
        //
        // failReason 으로 사용자 디버깅 hint 제공:
        //   "room_root_null" — roomRoot 자체가 null
        //   "notebook_station_not_found" — NotebookStation 이름 transform 없음
        //   "desk_not_found" — NotebookStation 자식 중 Desk 없음
        //   "notebook_not_found" — Desk 자식 중 Notebook 없음
        private static bool TryFindDeskAndNotebook(Transform roomRoot, out Transform desk, out Transform notebook, out string failReason)
        {
            desk = null;
            notebook = null;
            failReason = null;
            if (roomRoot == null) { failReason = "room_root_null"; return false; }

            Transform notebookStation = null;
            var all = roomRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name == Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.NotebookStation)
                {
                    notebookStation = all[i];
                    break;
                }
            }
            if (notebookStation == null) { failReason = "notebook_station_not_found"; return false; }

            for (int i = 0; i < notebookStation.childCount; i++)
            {
                var c = notebookStation.GetChild(i);
                if (c.gameObject.name == "Desk") { desk = c; break; }
            }
            if (desk == null) { failReason = "desk_not_found"; return false; }

            for (int i = 0; i < desk.childCount; i++)
            {
                var c = desk.GetChild(i);
                if (c.gameObject.name == Dotshelf.Scripts.RoomTheme.RoomGameObjectPatterns.Notebook) { notebook = c; break; }
            }
            if (notebook == null) { failReason = "notebook_not_found"; return false; }

            return true;
        }

        // functional Desk 자식 renderer 중 Notebook subtree 제외한 world-space AABB.
        // table_natural (GLB 경로) 와 Desktop/DeskLegA-D (procedural box) 두 변종 모두 cover.
        private static Bounds? ComputeFunctionalDeskBoundsWorld(Transform deskTransform, Transform notebookTransform)
        {
            if (deskTransform == null) return null;
            var renderers = deskTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return null;
            Bounds? acc = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (notebookTransform != null && IsDescendantOf(r.transform, notebookTransform)) continue;
                // 이미 attach 된 skin 의 자식 renderer 도 제외 (idempotent re-apply 안전)
                if (IsUnderNamedAncestor(r.transform, DeskVisualSkin.SkinChildName)) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-8f) continue;
                if (acc == null) acc = b;
                else { var aa = acc.Value; aa.Encapsulate(b); acc = aa; }
            }
            return acc;
        }

        // attach 시 native desk renderer hide — Notebook subtree + skin 본인 + 비활성 / particle
        // / trail / line 제외. marker.HiddenFunctionalRenderers 에 저장해 clear 시 원복.
        private static void HideNativeDeskRenderers(Transform deskTransform, Transform notebookTransform, DeskVisualSkin marker)
        {
            var renderers = deskTransform.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (notebookTransform != null && IsDescendantOf(r.transform, notebookTransform)) continue;
                if (IsUnderNamedAncestor(r.transform, DeskVisualSkin.SkinChildName)) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.enabled = false;
                marker.HiddenFunctionalRenderers.Add(r);
            }
        }

        // SkinScaleMultiplier 가 zero 또는 음수면 Vector3.one 으로 대체. ScriptableObject 신규 필드
        // deserialize 시 default zero 가 들어가 있을 수 있어 (1,1,1) 로 normalize. 음수는 mesh
        // mirror 가 의도된 경우가 아니므로 abs 처리.
        private static Vector3 SanitizeMultiplier(Vector3 m)
        {
            float x = Mathf.Abs(m.x) < 1e-4f ? 1f : m.x;
            float y = Mathf.Abs(m.y) < 1e-4f ? 1f : m.y;
            float z = Mathf.Abs(m.z) < 1e-4f ? 1f : m.z;
            return new Vector3(x, y, z);
        }

        private static bool IsDescendantOf(Transform t, Transform ancestor)
        {
            var cur = t;
            while (cur != null)
            {
                if (cur == ancestor) return true;
                cur = cur.parent;
            }
            return false;
        }

        private static bool IsUnderNamedAncestor(Transform t, string ancestorName)
        {
            var cur = t;
            while (cur != null)
            {
                if (cur.gameObject.name == ancestorName) return true;
                cur = cur.parent;
            }
            return false;
        }

        private static void DisableAllColliders(GameObject go)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }
        }

        // 비등방 scale / center 정렬 계산용 간이 bounds — BookshelfVisualSkinApplier 와 동일 정책.
        private static Bounds? ComputeRendererBoundsSafe(GameObject go)
        {
            if (go == null) return null;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return null;
            Bounds? acc = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-8f) continue;
                if (acc == null) acc = b;
                else { var aa = acc.Value; aa.Encapsulate(b); acc = aa; }
            }
            return acc;
        }
    }
}
