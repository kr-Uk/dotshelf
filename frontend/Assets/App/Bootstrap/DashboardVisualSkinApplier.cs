using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // ThemeSlot.Dashboard spec 을 일반 placement (placedItemsRoot 자식) 가 아니라 functional
    // dashboard root (GuestbookBoard / DashboardScreenPanel) 위 "visual skin" 자식으로 attach
    // 하는 static helper. BookshelfVisualSkinApplier 와 동일 정책의 dashboard 버전.
    //
    // 정책 (특정 테마 (vintage 등) 하드코딩 없음 — ThemeSlot.Dashboard 만 조건):
    //   - functional dashboard root (GuestbookBoard 우선, 없으면 DashboardScreenPanel) 자체는
    //     절대 destroy/reparent 하지 않는다 — UIDocument/Collider/raycast/focus/NotebookStation
    //     등 dashboard interaction 모두 그대로 유지.
    //   - skin prefab 은 functional root 자식으로 instantiate, 이름 = SkinChildName.
    //   - referenceBounds 는 functional root 자체의 Renderer bounds (DashboardScreenPanel/
    //     NotebookStation/skin 본인 자식은 제외) 를 측정해 산출. 비등방 normalize 로
    //     vintage_dashboard 의 raw GLB 크기를 native board mesh 크기에 맞춤.
    //   - skin 의 모든 Collider 는 disable — functional dashboard 의 raycast/UI panel
    //     interaction 을 가리지 않도록 보호.
    //   - functional root 자식의 기존 mesh Renderer 들은 attach 시점에 disable 하고 marker 에
    //     저장, clear 시 enable 원복. 예외: DashboardScreenPanel (UIDocument World-Space
    //     panel target), NotebookStation (책상 노트북 — Dashboard mesh swap 에서도 보존),
    //     skin 본인 자식은 hide 대상에서 제외.
    //   - z-fight + UI 가림 방지: skin 의 localPosition 에 SkinDepthOffset 만큼 후방 offset.
    //     vintage_dashboard 의 face 가 UIDocument World-Space panel 보다 약간 뒤에 위치 →
    //     UI 가 vintage_dashboard 앞에 정상 표시.
    //   - 같은 위치에 이미 skin 이 있으면 destroy 후 새 skin 으로 교체 (idempotent re-apply).
    public static class DashboardVisualSkinApplier
    {
        // 아래 4개 const 는 dashboard skin 의 z-offset / scale clamp / panel overscan 의 baseline.
        // MR #174 beshu #6 권고: 디자이너 fine-tune 가능하도록 [SerializeField] 노출 권장.
        // 본 클래스는 static 이라 SerializeField 직접 불가 — `DashboardSkinTuning` ScriptableObject
        // 분리 + 호출 site (RoomEditModeController) 에서 inject 하는 follow-up PR 로 분리.
        // 그 전까지는 이 const 값을 바꾸려면 코드 수정 + 컴파일 필요. 변경 시 z-fight / panel
        // 잘림 회귀가 일어날 수 있어 측정/캡처 후 commit 권고.

        // UIDocument World-Space panel 의 face 평면보다 약간 뒤에 vintage_dashboard 가
        // 오도록 하는 local-Z offset. 너무 작으면 z-fight, 너무 크면 보드/UI 사이 깊이감 ↑.
        // 디자이너가 추후 spec.LocalPosition 으로 미세 보정 가능 — 이 값은 baseline.
        private const float SkinDepthOffset = -0.005f;

        // Board-plane uniform scale clamp. 비등방 fit 시 두께 축에 큰 ratio 가 곱해져 mesh 가
        // 길게 늘어나는 회귀를 방지. width/height 평면 기준 uniform scale 만 사용해도 GLB 별
        // raw 크기 차이가 다소 다르므로 [0.05, 5] 범위로 안전하게 clamp.
        private const float ScaleMinClamp = 0.05f;
        private const float ScaleMaxClamp = 5f;

        // Panel overscan — skin 의 평면이 panel/board mesh 를 "감싸는 느낌" 으로 약간 더 크게
        // fit. 1.0 = board mesh 와 동일 크기, 1.15 = 15% 더 큼 (panel 꼭짓점이 skin 밖으로
        // 튀어나오지 않도록 여유). 디자이너가 fine-tune 필요시 이 값 조절.
        private const float PanelOverscanMultiplier = 1.15f;

        // 호출 책임: spec.ThemeSlot == ThemeSlot.Dashboard 인 경우만 호출. 다른 슬롯에 대해서는
        // false 만 반환 — 호출자가 일반 placement 경로 또는 별도 분기를 타야 한다.
        public static bool TryAttach(RoomThemeDecoSpec spec, long themeId, IThemeAssetLoader loader, string source)
        {
            if (spec == null || spec.ThemeSlot != ThemeSlot.Dashboard)
            {
                Debug.LogWarning($"[DashboardSkin] TryAttach skip — spec null 또는 themeSlot != Dashboard (themeSlot={(spec != null ? spec.ThemeSlot.ToString() : "spec=null")}) themeId={themeId} source={source}.");
                return false;
            }

            VisualSkinLog.Verbose($"[DashboardSkin] TryAttach enter themeId={themeId} themeSlot={spec.ThemeSlot} assetKey={spec.AssetKey} prefabWired={(spec.Prefab != null ? spec.Prefab.name : "null")} source={source}");

            var prefab = loader != null ? loader.Resolve(spec) : spec.Prefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[DashboardSkin] TryAttach FAIL reason=prefab_null — themeId={themeId} assetKey={spec.AssetKey} source={source}. " +
                                 "Inspector 에서 spec.prefab 슬롯 wiring 또는 fileID/guid 확인. (예: vintage_dashboard 는 fileID 7983643115131895607.)");
                return false;
            }

            if (!TryFindFunctionalRoot(out var functionalRoot, out var rootFailReason))
            {
                Debug.LogWarning($"[DashboardSkin] TryAttach FAIL reason={rootFailReason} — themeId={themeId} assetKey={spec.AssetKey} source={source}. " +
                                 "scene 에 GuestbookBoard/DashboardScreenPanel 이 ready 되면 deferred 재시도 필요 (TryReapplyDashboardSkinFromCurrentTheme).");
                return false;
            }

            // referenceBounds — functional root 의 native board mesh world-space bounds. skin
            // 부착 *전에* 측정 (아직 hide 안 한 상태라 native renderer 가 valid; skin 자식 들어가면
            // 그것까지 포함돼 측정 오염됨). DashboardScreenPanel/NotebookStation/skin 자식 제외 →
            // 보드면 자체의 크기/위치만 잡는다. size 뿐 아니라 center 도 보존 — 아래 pivot
            // alignment 에서 사용.
            var referenceBoundsNullable = ComputeFunctionalReferenceBoundsSafe(functionalRoot);
            if (!referenceBoundsNullable.HasValue)
            {
                Debug.LogWarning($"[DashboardSkin] TryAttach FAIL reason=reference_bounds_null — themeId={themeId} assetKey={spec.AssetKey} functionalRoot={functionalRoot.name} source={source}. " +
                                 "functional dashboard 의 native board mesh renderer 가 아직 ready 안 됐거나 모두 hide 됨. " +
                                 "GuestbookBoard 자식 (AlcoveLeft 등) 의 renderer.enabled 확인.");
                return false;
            }
            Bounds referenceBounds = referenceBoundsNullable.Value;

            ClearSkinUnder(functionalRoot);

            var go = Object.Instantiate(prefab, functionalRoot);
            go.name = DashboardVisualSkin.SkinChildName;
            // localPosition: zero 로 reset 후 아래에서 world-space pivot alignment 로 보정.
            //   - SkinDepthOffset (UI panel 가림 회피 z 후방 미세 offset) 도 functional root
            //     forward 방향으로 world-space 에서 적용 (rotation 무관 일관).
            //   - spec.LocalPosition 은 placedItemsRoot frame 기준 좌표라 functional root 자식
            //     frame 에선 의미가 달라 무시 (BookshelfVisualSkinApplier 와 동일 정책).
            // localRotation: spec.LocalRotation 그대로 — GLB 의 forward 가 functional root 의
            //   forward 와 다르면 디자이너가 spec 에서 보정 (예: vintage_dashboard 가 180 회전
            //   필요시 spec.LocalRotation=(0,180,0)).
            // localScale: Vector3.one 으로 reset → 아래에서 비등방 normalize 가 referenceBounds.size
            //   에 맞춰 덮어씀.
            go.transform.localPosition = Vector3.zero;
            go.transform.localEulerAngles = spec.LocalRotation;
            go.transform.localScale = Vector3.one;

            DisableAllColliders(go);

            // boardNormal 검출 — 회귀 fix: 이전엔 functionalRoot.forward 를 board normal 로
            // 가정했는데, GuestbookBoard 의 setup 이 forward 가 board plane 방향 (벽 위쪽)
            // 인 케이스가 있어 잘못된 axis 정렬 → mesh 가 벽에 직각으로 꽂힘. 신뢰성 있는
            // 검출 방식: referenceBounds (world-AABB) 의 가장 작은 axis 가 board 의 thickness
            // 축 = normal 의 world direction. 부호는 functionalRoot 의 local axes (right/up/
            // forward) 중 그 axis 와 가장 align 된 것의 부호 (board outward 가 functional 의
            // local +axis 방향이라 가정). align 정보 없으면 +axis default.
            Vector3 boardNormal;
            int boardNormalAxisIdx = -1;
            if (IsAllPositive(referenceBounds.size))
            {
                var baseOrderForNormal = OrderAxesDescending(referenceBounds.size);
                boardNormalAxisIdx = baseOrderForNormal[2];
                Vector3 axisVec = WorldAxisVector(boardNormalAxisIdx);
                // 부호 결정 — functionalRoot 의 local axes 중 axisVec 과 가장 align 된 것의 부호.
                Vector3[] localAxes = { functionalRoot.right, functionalRoot.up, functionalRoot.forward };
                float bestAbsDot = 0f;
                int bestSign = 1;
                for (int i = 0; i < 3; i++)
                {
                    float d = Vector3.Dot(localAxes[i], axisVec);
                    if (Mathf.Abs(d) > bestAbsDot)
                    {
                        bestAbsDot = Mathf.Abs(d);
                        bestSign = (d >= 0) ? 1 : -1;
                    }
                }
                boardNormal = axisVec * bestSign;
            }
            else
            {
                boardNormal = functionalRoot.forward; // fallback — referenceBounds 가 valid 하지 않을 때만
            }

            // 1) Rotation auto-correction — skin 의 thickness 축 (smallest world-AABB axis) 을
            // 위에서 검출한 boardNormal 에 align. Quaternion.FromToRotation 으로 최소 회전 산출.
            //   - spec.LocalRotation 은 이 시점에 이미 적용됨 (designer explicit intent).
            //   - 그 위에 auto-correction 을 world rotation 으로 multiply.
            //   - skin 이 이미 정렬돼 있으면 correction == identity → no-op.
            //   - +board normal / -board normal 중 dot 부호로 가까운 쪽 선택 → 최소 회전.
            // Manual override: designer 가 자동 보정을 원치 않으면 spec.LocalRotation 으로 미리
            // thickness 축을 board normal 에 일치시키면 됨 (correction 이 identity 가 됨).
            var visualBoundsPreCorrection = ComputeRendererBoundsSafe(go);
            Quaternion rotationCorrection = Quaternion.identity;
            Vector3 detectedThicknessDirInitial = Vector3.zero;
            int thicknessAxisInitial = -1;
            if (visualBoundsPreCorrection.HasValue && IsAllPositive(visualBoundsPreCorrection.Value.size))
            {
                var orderInitial = OrderAxesDescending(visualBoundsPreCorrection.Value.size);
                thicknessAxisInitial = orderInitial[2];
                detectedThicknessDirInitial = WorldAxisVector(thicknessAxisInitial);
                Vector3 targetNormal = (Vector3.Dot(detectedThicknessDirInitial, boardNormal) >= 0)
                    ? boardNormal
                    : -boardNormal;
                rotationCorrection = Quaternion.FromToRotation(detectedThicknessDirInitial, targetNormal);
                go.transform.rotation = rotationCorrection * go.transform.rotation;
            }

            // 2) board-plane uniform scale — dashboard 는 얇은 판형 mesh 라 비등방 fit (X/Y/Z
            // 각각 ratio) 적용하면 두께 축 (skin 의 가장 작은 axis) 에 큰 ratio 가 곱해져
            // mesh 가 길게 찌그러진다 (이전 회귀: Z=11x). 정책:
            //   - skin 의 bounds.size 에서 가장 작은 축 = thickness, 나머지 두 축 = board plane.
            //   - board plane 두 축에 대해 widthRatio / heightRatio 산출.
            //   - uniformScale = max(widthRatio, heightRatio) * PanelOverscanMultiplier
            //     → skin 평면이 board mesh 의 큰 차원에 fit + 15% overscan 으로 panel 을 감쌈.
            //     사용자 요구: "대시보드 오브젝트는 화면보다 커야 감싸는 느낌이 들거 같음".
            //     이전 정책 (min) 은 board mesh 안에 들어가는 보수적 fit 이라 panel 꼭짓점이
            //     mesh 밖으로 튀어나오는 회귀가 났음.
            //   - thickness 축도 같은 uniform factor 로 곱해져 GLB 원본 종횡비 보존.
            //   - clamp [ScaleMinClamp, ScaleMaxClamp] 로 비정상 비율 방지.
            // 입력: rotation correction 후의 bounds (visualBoundsAfterRotation). 회전 보정 안 한
            // 상태의 bounds 를 쓰면 skin 의 가장 작은 축이 dashboard 의 가장 작은 축과 매칭되도록
            // 라벨 정렬되지만, 실제 정렬 방향이 어긋남.
            var visualBoundsAfterRotation = ComputeRendererBoundsSafe(go);
            Vector3 scaleFactor = Vector3.one;
            int skinThicknessAxis = -1;
            int basePlaneLargestAxis = -1, basePlaneMiddleAxis = -1;
            float widthRatio = 1f, heightRatio = 1f, chosenUniform = 1f;
            if (visualBoundsAfterRotation.HasValue && IsAllPositive(visualBoundsAfterRotation.Value.size) && IsAllPositive(referenceBounds.size))
            {
                var skinOrder = OrderAxesDescending(visualBoundsAfterRotation.Value.size);
                var baseOrder = OrderAxesDescending(referenceBounds.size);
                skinThicknessAxis = skinOrder[2];
                basePlaneLargestAxis = baseOrder[0];
                basePlaneMiddleAxis = baseOrder[1];

                float skinLargest = visualBoundsAfterRotation.Value.size[skinOrder[0]];
                float skinMiddle  = visualBoundsAfterRotation.Value.size[skinOrder[1]];
                float baseLargest = referenceBounds.size[baseOrder[0]];
                float baseMiddle  = referenceBounds.size[baseOrder[1]];

                widthRatio  = baseLargest / skinLargest;
                heightRatio = baseMiddle  / skinMiddle;

                chosenUniform = Mathf.Clamp(Mathf.Max(widthRatio, heightRatio) * PanelOverscanMultiplier, ScaleMinClamp, ScaleMaxClamp);
                scaleFactor = Vector3.one * chosenUniform;
                go.transform.localScale = scaleFactor;
            }
            var visualBoundsAfterScale = ComputeRendererBoundsSafe(go);

            // 3) pivot alignment — Meshy GLB 의 root pivot 이 visible geometry 중심과 어긋난
            // 경우 (자주 일어남) localPosition=zero 만 박으면 mesh 가 functional dashboard 자리
            // 에서 떨어진 위치 (벽/바닥/허공) 로 spawn 된다. world-space 에서 referenceBounds.center
            // 와 visual bounds.center 가 일치하도록 transform.position 보정. scale 적용 *후* bounds
            // 를 다시 측정해야 정확.
            Vector3 centerOffset = Vector3.zero;
            if (visualBoundsAfterScale.HasValue)
            {
                centerOffset = referenceBounds.center - visualBoundsAfterScale.Value.center;
                go.transform.position += centerOffset;
            }
            else
            {
                Debug.LogWarning($"[DashboardSkin] visualBoundsAfterScale null — pivot alignment skip. skin renderer 가 측정 가능한 상태인지 확인 (mesh import / activeInHierarchy).");
            }

            // 4) Surface offset — functional board normal 방향으로 baseline 미세 offset. SkinDepthOffset
            // (-5mm) × boardNormal = board 뒤쪽 baseline. center alignment 이후 적용해야 alignment 가
            // 깨지지 않음. 두께가 얇은 GLB 면 이것으로 충분. 두꺼운 GLB 는 step 5 가드가 추가 push.
            go.transform.position += boardNormal * SkinDepthOffset;
            var visualBoundsAfterSurfaceOffset = ComputeRendererBoundsSafe(go);

            // Panel position 은 직접 조작하지 않는다. 이유 — BookshelfExperienceManager.MountExternalDashboardPanel
            // 이 매 프레임 panel.position/rotation/scale 을 GuestbookBoardHitArea 기준으로 강제 reset.
            // 여기서 panel.position 을 set 해도 다음 프레임에 덮어써져서 회귀. panel 위치 보정은
            // BookshelfExperienceManager.ExternalDashboardPanelSurfaceOffset 값 조정으로 처리한다.

            var visualBoundsFinal = ComputeRendererBoundsSafe(go);

            var marker = go.AddComponent<DashboardVisualSkin>();
            marker.ThemeId = themeId;
            marker.AssetKey = spec.AssetKey;

            HideFunctionalBoardRenderers(functionalRoot, marker);

            VisualSkinLog.Verbose($"[DashboardSkin] TryAttach SUCCESS themeId={themeId} category=DashboardSkin assetKey={spec.AssetKey} " +
                      $"functionalRoot={functionalRoot.name} functionalWorldPos={functionalRoot.position} " +
                      $"boardNormalAxisIdx={AxisName(boardNormalAxisIdx)} boardNormal={boardNormal} (detected from referenceBounds smallest axis) " +
                      $"referenceBounds.center={referenceBounds.center} referenceBounds.size={referenceBounds.size} " +
                      $"visualAttached={go.name} visualWorldPos={go.transform.position} " +
                      $"visualBoundsPreCorrection.size={(visualBoundsPreCorrection?.size.ToString() ?? "null")} " +
                      $"thicknessAxisInitial={AxisName(thicknessAxisInitial)} detectedThicknessDirInitial={detectedThicknessDirInitial} " +
                      $"rotationCorrection.euler={rotationCorrection.eulerAngles} finalLocalRotation={go.transform.localEulerAngles} " +
                      $"visualBoundsAfterRotation.size={(visualBoundsAfterRotation?.size.ToString() ?? "null")} " +
                      $"skinThicknessAxis={AxisName(skinThicknessAxis)} basePlaneAxes=[{AxisName(basePlaneLargestAxis)},{AxisName(basePlaneMiddleAxis)}] " +
                      $"widthRatio={widthRatio:F3} heightRatio={heightRatio:F3} chosenUniform={chosenUniform:F3} (max*overscan{PanelOverscanMultiplier:F2}, clamp[{ScaleMinClamp:F2},{ScaleMaxClamp:F2}]) " +
                      $"visualBoundsAfterScale.center={(visualBoundsAfterScale?.center.ToString() ?? "null")} " +
                      $"visualBoundsAfterScale.size={(visualBoundsAfterScale?.size.ToString() ?? "null")} " +
                      $"scaleFactor=({scaleFactor.x:F3},{scaleFactor.y:F3},{scaleFactor.z:F3}) " +
                      $"centerOffset={centerOffset} surfaceOffset(boardNormal*SkinDepthOffset)={boardNormal * SkinDepthOffset} " +
                      $"visualBoundsFinal.center={(visualBoundsFinal?.center.ToString() ?? "null")} " +
                      $"visualBoundsFinal.size={(visualBoundsFinal?.size.ToString() ?? "null")} " +
                      $"finalLocalScale={go.transform.localScale} source={source}");
            return true;
        }

        // bounds.size axis 를 내림차순 인덱스로 정렬. 결과: indices[0]=가장 큰 축, indices[1]=중간,
        // indices[2]=가장 작은 축 (board mesh 의 thickness 후보).
        private static int[] OrderAxesDescending(Vector3 size)
        {
            int[] indices = { 0, 1, 2 };
            System.Array.Sort(indices, (a, b) => size[b].CompareTo(size[a]));
            return indices;
        }

        private static bool IsAllPositive(Vector3 v)
        {
            return v.x > 1e-4f && v.y > 1e-4f && v.z > 1e-4f;
        }

        private static string AxisName(int axis)
        {
            if (axis == 0) return "X";
            if (axis == 1) return "Y";
            if (axis == 2) return "Z";
            return "?";
        }

        private static Vector3 WorldAxisVector(int axis)
        {
            if (axis == 0) return Vector3.right;   // world +X
            if (axis == 1) return Vector3.up;      // world +Y
            if (axis == 2) return Vector3.forward; // world +Z
            return Vector3.zero;
        }

        // DashboardScreenPanel transform 검색. 검색 우선순위:
        // 외부 (테마 전환 시) 명시 호출. functional dashboard root 못 찾으면 no-op.
        public static void ClearSkin(string source = null)
        {
            if (!TryFindFunctionalRoot(out var functionalRoot, out _))
            {
                return;
            }
            var existing = functionalRoot.Find(DashboardVisualSkin.SkinChildName);
            bool exists = existing != null;
            ClearSkinUnder(functionalRoot);
            if (exists)
            {
                VisualSkinLog.Verbose($"[DashboardSkinRestore] themeId=- visualExists={exists} normalizationApplied=false " +
                          $"functionalRootPreserved=true source={source ?? "ClearSkin"}");
            }
        }

        private static void ClearSkinUnder(Transform functionalRoot)
        {
            if (functionalRoot == null) return;
            var existing = functionalRoot.Find(DashboardVisualSkin.SkinChildName);
            if (existing == null) return;
            var marker = existing.GetComponent<DashboardVisualSkin>();
            if (marker != null)
            {
                var hidden = marker.HiddenFunctionalRenderers;
                for (int i = 0; i < hidden.Count; i++)
                {
                    var r = hidden[i];
                    if (r != null) r.enabled = true;
                }
                hidden.Clear();
            }
            UnityObjectSafeDestroy.Destroy(existing.gameObject);
        }

        // functional dashboard root 검색. 우선순위:
        //   1) GuestbookBoard — 컨테이너. AlcoveLeft (보드 mesh) + GuestbookHitArea (collider) +
        //      DashboardScreenPanel (UIDocument) + NotebookStation 의 부모.
        //   2) DashboardScreenPanel — UIDocument 호스트 자체. GuestbookBoard 가 없는 분리된
        //      configuration (testbed 등) 에서 fallback.
        // 둘 다 못 찾으면 false + failReason 으로 호출자가 사용자에게 정확히 보고 가능.
        // RoomGameObjectPatterns.Dashboard 의 1차/2차 이름과 일치.
        private static bool TryFindFunctionalRoot(out Transform root, out string failReason)
        {
            root = null;
            failReason = null;
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // 1st pass: GuestbookBoard
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i] != null && allTransforms[i].gameObject.name == "GuestbookBoard")
                {
                    root = allTransforms[i];
                    return true;
                }
            }
            // 2nd pass: DashboardScreenPanel fallback
            for (int i = 0; i < allTransforms.Length; i++)
            {
                if (allTransforms[i] != null && allTransforms[i].gameObject.name == "DashboardScreenPanel")
                {
                    root = allTransforms[i];
                    return true;
                }
            }
            failReason = "dashboard_root_not_found (GuestbookBoard 와 DashboardScreenPanel 둘 다 scene 에 없음)";
            return false;
        }

        // skin 부착 시 functional root 자식의 visible mesh 들을 hide. 예외:
        //   - DashboardScreenPanel 자식 (UIDocument World-Space panel target — UI 렌더 보존)
        //   - NotebookStation 자식 (책상 노트북 — dashboard skin 과 무관, visual 보존)
        //   - GuestbookHitArea (BoxCollider only, renderer 없음 — but 자식 검사로 안전)
        //   - skin 본인 자식
        //   - ParticleSystem/Trail/Line renderer
        //   - 비활성 renderer
        // marker.HiddenFunctionalRenderers 에 저장해 clear 시 원복.
        private static void HideFunctionalBoardRenderers(Transform functionalRoot, DashboardVisualSkin marker)
        {
            var renderers = functionalRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (IsUnderNamedAncestor(r.transform, DashboardVisualSkin.SkinChildName)) continue;
                if (IsUnderNamedAncestor(r.transform, "DashboardScreenPanel")) continue;
                if (IsUnderNamedAncestor(r.transform, "NotebookStation")) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.enabled = false;
                marker.HiddenFunctionalRenderers.Add(r);
            }
        }

        // functional root 자체의 reference bounds — DashboardScreenPanel/NotebookStation/skin
        // 자식은 제외. native board mesh (AlcoveLeft 등) bounds 만 잡아 vintage_dashboard
        // 크기 normalize 의 target 으로 사용.
        private static Bounds? ComputeFunctionalReferenceBoundsSafe(Transform functionalRoot)
        {
            if (functionalRoot == null) return null;
            var renderers = functionalRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return null;
            Bounds? acc = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (!r.gameObject.activeInHierarchy) continue;
                if (IsUnderNamedAncestor(r.transform, DashboardVisualSkin.SkinChildName)) continue;
                if (IsUnderNamedAncestor(r.transform, "DashboardScreenPanel")) continue;
                if (IsUnderNamedAncestor(r.transform, "NotebookStation")) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-8f) continue;
                if (acc == null) acc = b;
                else { var aa = acc.Value; aa.Encapsulate(b); acc = aa; }
            }
            return acc;
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

        // skin 전용 간이 bounds — RoomEditModeController.ComputeRendererBounds /
        // BookshelfVisualSkinApplier.ComputeRendererBoundsSafe 와 동일 정책.
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
