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
    //   - 크기 정책 두 갈래:
    //       (a) entry.PreserveSkinNativeScale == false (default) — functional desk bounds 에
    //           비등방 auto-fit + bounds-center 정렬 + entry.SkinScaleMultiplier 적용. GLB 의
    //           raw 종횡비 무시하고 functional desk 와 같은 크기로 강제 정렬.
    //       (b) entry.PreserveSkinNativeScale == true — auto-fit/multiplier 모두 skip,
    //           localScale=one 유지 (GLB 의 원본 모델 크기/비율 그대로). 위치는 XZ 중심을 functional
    //           desk 의 XZ 중심에 맞추고 Y bottom 을 functional bottom 에 맞춰 floor 에 자연스럽게
    //           놓음. vintage_table 처럼 GLB 가 이미 적절한 크기로 제작된 자산에 사용.
    //     두 경우 모두 pivot-agnostic (bottom-center / mesh-center / 임의 좌표 어느 쪽이든 동일).
    //     entry.PlacementAnchor 는 두 경로 모두에서 사용 안 함.
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
            // localPosition=zero 으로 reset (아래 bounds-translation 으로 재정렬).
            // localRotation: preserveNative 모드에서 entry.PlacementRotation 적용 (예: vintage_table
            // (0, 90, 0) 으로 Y 축 90도). false 모드에선 zero (auto-fit 계산 단순화).
            // localScale 은 아래 분기에서 결정.
            // entry.PlacementAnchor 는 두 모드 모두에서 사용 안 함.
            var go = Object.Instantiate(entry.Prefab, deskTransform);
            go.name = DeskVisualSkin.SkinChildName;
            go.transform.localPosition = Vector3.zero;

            bool preserveNative = entry.PreserveSkinNativeScale;
            go.transform.localEulerAngles = preserveNative ? entry.PlacementRotation : Vector3.zero;

            DisableAllColliders(go);

            // scale 결정 — preserveSkinNativeScale 분기.
            //   (a) false (default): functional bounds 비등방 auto-fit × SkinScaleMultiplier.
            //       parent lossyScale 은 functional bounds 자체에 이미 반영되어 있어 추가 보정 불필요.
            //   (b) true: GLB 의 world 크기 (= prefab 을 scene root 에 떨궜을 때 보이는 크기) 를
            //       base 로, entry.PlacementScale (override multiplier) 를 곱한 값으로 만든다.
            //       PlacementScale 이 (1,1,1) 또는 zero 면 native 그대로, 아니면 디자이너가
            //       Inspector 에서 잡은 비율로 미세 조정 (예: vintage_table (0.5, 0.5, 0.4)).
            //
            //       parent (Desk transform) 의 lossyScale 이 1 이 아니면 그냥 localScale=multiplier
            //       로 set 하면 world scale = parent.lossyScale × multiplier 로 왜곡되므로, prefab
            //       root 의 authored localScale × multiplier 를 parent.lossyScale 로 역보정 set:
            //       worldScale = parent.lossyScale × ((prefab.localScale × multiplier) / parent.lossyScale)
            //                   = prefab.localScale × multiplier (= 의도된 world 크기).
            var skinBoundsBefore = ComputeRendererBoundsSafe(go);
            Vector3 scaleFactor = Vector3.one;
            Vector3 multiplier = SanitizeMultiplier(entry.SkinScaleMultiplier);
            Vector3 placementScaleOverride = SanitizeMultiplier(entry.PlacementScale);
            bool multiplierApplied = false;
            if (preserveNative)
            {
                // preserveNative target worldScale = prefab.localScale × placementScaleOverride × multiplier.
                // 이전 버그: multiplier (SkinScaleMultiplier) 가 분기에서 누락 → DeepSea (multiplier=3,3,3 +
                // placementScaleOverride=1,1,1) 에서 multiplier 가 무시되어 raw size 그대로 나옴. 두 필드 모두
                // 곱하면 placementScale 만 쓰는 vintage_table (multiplier=1,1,1) 도 회귀 없음.
                var prefabRootScale = entry.Prefab.transform.localScale;
                var parentLossy = deskTransform.lossyScale;
                scaleFactor = new Vector3(
                    SafeInvScale(prefabRootScale.x * placementScaleOverride.x * multiplier.x, parentLossy.x),
                    SafeInvScale(prefabRootScale.y * placementScaleOverride.y * multiplier.y, parentLossy.y),
                    SafeInvScale(prefabRootScale.z * placementScaleOverride.z * multiplier.z, parentLossy.z));
                go.transform.localScale = scaleFactor;
                multiplierApplied = true;
            }
            else if (skinBoundsBefore.HasValue &&
                     skinBoundsBefore.Value.size.x > 1e-4f &&
                     skinBoundsBefore.Value.size.y > 1e-4f &&
                     skinBoundsBefore.Value.size.z > 1e-4f)
            {
                scaleFactor = new Vector3(
                    Mathf.Clamp((functionalBounds.Value.size.x / skinBoundsBefore.Value.size.x) * multiplier.x, 0.01f, 100f),
                    Mathf.Clamp((functionalBounds.Value.size.y / skinBoundsBefore.Value.size.y) * multiplier.y, 0.01f, 100f),
                    Mathf.Clamp((functionalBounds.Value.size.z / skinBoundsBefore.Value.size.z) * multiplier.z, 0.01f, 100f));
                go.transform.localScale = scaleFactor;
                multiplierApplied = true;
            }
            else
            {
                go.transform.localScale = Vector3.one;
            }

            // 위치 정렬 — 우선순위:
            //   (1) preserveNative=true && entry.SkinLocalPositionOverride != zero → 디자이너 직접
            //       지정 localPosition (Desk-local) 그대로 set. bounds-based 정렬 skip.
            //   (2) preserveNative=true (override 없음) → XZ 중심 functional.center.xz + Y bottom 을
            //       functional.min.y 에 맞춤 (floor 자연 안착).
            //   (3) preserveNative=false → bounds-center 풀 정렬 (auto-fit 결과를 functional center 에 맞춤).
            var skinBoundsAfterScale = ComputeRendererBoundsSafe(go);
            Vector3 worldOffset = Vector3.zero;
            Vector3 localPositionOverride = entry.SkinLocalPositionOverride;
            bool usedLocalPositionOverride = preserveNative && localPositionOverride.sqrMagnitude > 1e-10f;
            if (usedLocalPositionOverride)
            {
                go.transform.localPosition = localPositionOverride;
            }
            else if (skinBoundsAfterScale.HasValue && skinBoundsAfterScale.Value.size.sqrMagnitude > 1e-8f)
            {
                Vector3 desiredCenter;
                if (preserveNative)
                {
                    desiredCenter = new Vector3(
                        functionalBounds.Value.center.x,
                        functionalBounds.Value.min.y + skinBoundsAfterScale.Value.size.y * 0.5f,
                        functionalBounds.Value.center.z);
                }
                else
                {
                    desiredCenter = functionalBounds.Value.center;
                }
                worldOffset = desiredCenter - skinBoundsAfterScale.Value.center;
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

            // monitor 모드 — entry.HideLaptopRenderers=true 면 Notebook hierarchy 의 모든 Renderer 를
            // 추가로 disable. lockNotebookStationToMonitor=true 와 짝으로 사용 — NotebookStation 이
            // monitor 위로 이동한 상태에서 laptop body / 화면 mesh 가 monitor 와 시각 충돌하는 걸 해소.
            // Canvas UI 는 Renderer 가 아니라 CanvasRenderer 라 hide 대상에서 자동 제외 → ScreenSaverCanvas
            // / DashboardCanvas (그 안의 dashboard UI) 만 monitor 화면 위치에 남는다.
            // marker.HiddenLaptopRenderers 에 캡처 → ClearSkinUnder 에서 enable 원복.
            int laptopRenderersHidden = 0;
            if (entry.HideLaptopRenderers && notebookTransform != null)
            {
                laptopRenderersHidden = HideLaptopHierarchyRenderers(notebookTransform, marker);
            }

            // monitor 화면 fit — entry 의 notebookScreen* override 로 Notebook/Screen 의 transform 을
            // 디자이너 지정 값으로 set. ScreenSaverCanvas / DashboardCanvas 는 Screen 자식이라 같이
            // 따라감. native (procedural 노트북의 (0, 0.114, -0.154) / Euler (-99.5, 0, 0)) 는 marker
            // 에 캡처 후 ClearSkinUnder 에서 복원.
            Transform screenTransform = null;
            Vector3 finalScreenLocalPos = Vector3.zero;
            Vector3 finalScreenLocalEuler = Vector3.zero;
            Vector3 finalScreenLocalScale = Vector3.one;
            bool screenOverrideApplied = false;
            if (notebookTransform != null)
            {
                for (int i = 0; i < notebookTransform.childCount; i++)
                {
                    var c = notebookTransform.GetChild(i);
                    if (c.gameObject.name == "Screen") { screenTransform = c; break; }
                }
            }
            if (screenTransform != null)
            {
                // native 캡처 — 첫 진입에만 의미. 같은 marker 가 재사용되면 이미 has=true 라 skip.
                marker.NativeScreenLocalPosition = screenTransform.localPosition;
                marker.NativeScreenLocalRotation = screenTransform.localRotation;
                marker.NativeScreenLocalScale = screenTransform.localScale;
                marker.HasNativeScreenCapture = true;

                var screenScaleMult = SanitizeMultiplier(entry.NotebookScreenScaleMultiplier);
                bool hasAnyOverride =
                    entry.NotebookScreenLocalPosition.sqrMagnitude > 1e-10f ||
                    entry.NotebookScreenLocalRotation.sqrMagnitude > 1e-10f ||
                    (screenScaleMult - Vector3.one).sqrMagnitude > 1e-10f;
                if (hasAnyOverride)
                {
                    if (entry.NotebookScreenLocalPosition.sqrMagnitude > 1e-10f)
                        screenTransform.localPosition = entry.NotebookScreenLocalPosition;
                    if (entry.NotebookScreenLocalRotation.sqrMagnitude > 1e-10f)
                        screenTransform.localEulerAngles = entry.NotebookScreenLocalRotation;
                    screenTransform.localScale = new Vector3(
                        marker.NativeScreenLocalScale.x * screenScaleMult.x,
                        marker.NativeScreenLocalScale.y * screenScaleMult.y,
                        marker.NativeScreenLocalScale.z * screenScaleMult.z);
                    screenOverrideApplied = true;
                }
                finalScreenLocalPos = screenTransform.localPosition;
                finalScreenLocalEuler = screenTransform.localEulerAngles;
                finalScreenLocalScale = screenTransform.localScale;
            }

            // laptop Y 재정렬 — skin world top 을 Desk-local 로 변환 후 LaptopBottomOffset 더함.
            // X / Z 는 native 그대로 보존 (놓이는 평면 anchor 의 designer 의도 유지).
            //
            // LockNotebookStationToMonitor=true 인 entry (예: DeepSea) 는 NotebookStation 자체가
            // MeshSwap_DeskMonitor 위로 이동하므로 (RoomEditModeController.ApplyNotebookStationPlacement
            // 가 monitor anchor 기준 SetPositionAndRotation) laptop 의 Desk-local Y 를 desk skin top
            // 으로 옮기면 NotebookStation 이동 후 laptop 이 monitor 화면에서 어긋남. 이 경우엔
            // laptop native localPosition 그대로 두고 NotebookStation 의 entry.NotebookStation* 값으로
            // 모니터 화면 align 을 위임.
            float deskTopLocalY = 0f;
            bool laptopYAdjusted = false;
            if (!entry.LockNotebookStationToMonitor)
            {
                if (skinBoundsAfter.HasValue)
                {
                    var deskTopWorld = new Vector3(notebookTransform.position.x, skinBoundsAfter.Value.max.y, notebookTransform.position.z);
                    deskTopLocalY = deskTransform.InverseTransformPoint(deskTopWorld).y;
                }
                else
                {
                    var deskTopWorld = new Vector3(notebookTransform.position.x, functionalBounds.Value.max.y, notebookTransform.position.z);
                    deskTopLocalY = deskTransform.InverseTransformPoint(deskTopWorld).y;
                }
                float newLaptopLocalY = deskTopLocalY + LaptopBottomOffset;
                notebookTransform.localPosition = new Vector3(nativeLaptopLocal.x, newLaptopLocalY, nativeLaptopLocal.z);
                laptopYAdjusted = true;
            }

            var laptopAfterWorld = notebookTransform.position;
            var laptopDelta = laptopAfterWorld - laptopBeforeWorld;

            float tableTopWorldY = skinBoundsAfter.HasValue ? skinBoundsAfter.Value.max.y : functionalBounds.Value.max.y;

            var finalLocalScale = go.transform.localScale;
            var finalWorldScale = go.transform.lossyScale;

            // monitor 화면 fit 진단용 metric — Canvas 가 결국 어디에 어떤 크기로 떠 있는지 직접 측정.
            // ScreenSaverCanvas / DashboardCanvas 는 Screen 자식 (런타임 생성) 이라 Screen.position +
            // CanvasSize × CanvasScale × Screen.lossyScale 로 world 면적이 결정됨.
            Vector3 finalScreenCanvasPosition = Vector3.zero;
            Vector3 finalScreenCanvasRotation = Vector3.zero;
            Vector3 finalScreenCanvasScale = Vector3.zero;
            Vector3 cameraFocusForward = Vector3.zero;
            if (screenTransform != null)
            {
                finalScreenCanvasPosition = screenTransform.position;
                finalScreenCanvasRotation = screenTransform.eulerAngles;
                finalScreenCanvasScale = screenTransform.lossyScale;
                cameraFocusForward = screenTransform.forward;
            }

            Debug.Log($"[DeskSkin] entryAssetId={entry.AssetId} prefab={entry.Prefab.name} deskRoot={deskTransform.name} notebook={notebookTransform.name} " +
                      $"preserveSkinNativeScale={preserveNative} placementRotation={entry.PlacementRotation} placementScaleOverride={placementScaleOverride} " +
                      $"skinLocalPositionOverride={localPositionOverride} usedLocalPositionOverride={usedLocalPositionOverride} " +
                      $"lockNotebookStationToMonitor={entry.LockNotebookStationToMonitor} laptopYAdjusted={laptopYAdjusted} " +
                      $"hideLaptopRenderers={entry.HideLaptopRenderers} laptopRenderersHidden={laptopRenderersHidden} " +
                      $"screenOverrideApplied={screenOverrideApplied} " +
                      $"finalScreenLocal=(pos={finalScreenLocalPos}, eul={finalScreenLocalEuler}, scale={finalScreenLocalScale}) " +
                      $"finalScreenCanvasPosition={finalScreenCanvasPosition} finalScreenCanvasRotation={finalScreenCanvasRotation} finalScreenCanvasScale={finalScreenCanvasScale} cameraFocusForward={cameraFocusForward} " +
                      $"prefabRootLocalScale={entry.Prefab.transform.localScale} deskLossyScale={deskTransform.lossyScale} " +
                      $"multiplier=({multiplier.x:F3},{multiplier.y:F3},{multiplier.z:F3}) multiplierApplied={multiplierApplied} " +
                      $"scaleFactor=({scaleFactor.x:F3},{scaleFactor.y:F3},{scaleFactor.z:F3}) " +
                      $"finalLocalScale=({finalLocalScale.x:F3},{finalLocalScale.y:F3},{finalLocalScale.z:F3}) " +
                      $"finalWorldScale=({finalWorldScale.x:F3},{finalWorldScale.y:F3},{finalWorldScale.z:F3}) " +
                      $"skinWorldScale={finalWorldScale} " +
                      $"functionalDeskBounds=(center={functionalBounds.Value.center}, size={functionalBounds.Value.size}) " +
                      $"skinBoundsBefore={(skinBoundsBefore.HasValue ? skinBoundsBefore.Value.size.ToString() : "null")} " +
                      $"finalSkinBounds=(center={(skinBoundsAfter.HasValue ? skinBoundsAfter.Value.center.ToString() : "null")}, size={(skinBoundsAfter.HasValue ? skinBoundsAfter.Value.size.ToString() : "null")}) " +
                      $"worldOffset={worldOffset} " +
                      $"tableTopWorldY={tableTopWorldY:F4} deskTopLocalY={deskTopLocalY:F4} laptopBottomOffset={LaptopBottomOffset:F4} " +
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
                // hidden native desk renderer 복원
                var hidden = marker.HiddenFunctionalRenderers;
                for (int i = 0; i < hidden.Count; i++)
                {
                    var r = hidden[i];
                    if (r != null) r.enabled = true;
                }
                hidden.Clear();

                // hidden laptop hierarchy renderer 복원 (monitor 모드용)
                var hiddenLaptop = marker.HiddenLaptopRenderers;
                for (int i = 0; i < hiddenLaptop.Count; i++)
                {
                    var r = hiddenLaptop[i];
                    if (r != null) r.enabled = true;
                }
                hiddenLaptop.Clear();

                // laptop native localPosition 복원 — attach 시 캡처한 값으로 정확히 되돌림.
                if (marker.HasNativeLaptopCapture && notebookTransform != null)
                {
                    notebookTransform.localPosition = marker.NativeLaptopLocalPosition;
                }

                // Notebook/Screen native transform 복원 — monitor 모드에서 override 했을 경우.
                if (marker.HasNativeScreenCapture && notebookTransform != null)
                {
                    Transform screen = null;
                    for (int i = 0; i < notebookTransform.childCount; i++)
                    {
                        var c = notebookTransform.GetChild(i);
                        if (c.gameObject.name == "Screen") { screen = c; break; }
                    }
                    if (screen != null)
                    {
                        screen.localPosition = marker.NativeScreenLocalPosition;
                        screen.localRotation = marker.NativeScreenLocalRotation;
                        screen.localScale = marker.NativeScreenLocalScale;
                    }
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

        // monitor 모드 — Notebook hierarchy 전체의 mesh Renderer 를 disable. Particle/Trail/Line 은
        // 제외 (효과용). Canvas UI 는 CanvasRenderer 라 Renderer 검색 결과에 안 들어옴 → 자동 보존.
        // 반환: hide 한 renderer 수 (로그 / 진단용).
        private static int HideLaptopHierarchyRenderers(Transform notebookTransform, DeskVisualSkin marker)
        {
            var renderers = notebookTransform.GetComponentsInChildren<Renderer>(true);
            int hidden = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.enabled = false;
                marker.HiddenLaptopRenderers.Add(r);
                hidden++;
            }
            return hidden;
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

        // preserveSkinNativeScale 분기에서 parent.lossyScale 을 역보정해 world scale = prefabRoot
        // 을 만드는 helper. parent.lossy 0 / 매우 작은 값 / 음수 모두 안전 처리 (분모 폭주/부호
        // 반전 방지). 일반 desk hierarchy 에선 1 근방이라 그대로 통과.
        private static float SafeInvScale(float prefabRoot, float parentLossy)
        {
            float denom = Mathf.Abs(parentLossy) < 1e-6f ? 1f : parentLossy;
            return prefabRoot / denom;
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
