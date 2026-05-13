using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
using UnityEngine;

namespace App.Bootstrap
{
    // ThemeSlot.Shelf spec 을 일반 floor placement (placedItemsRoot 자식) 가 아니라 functional
    // BookshelfRoot 위의 "visual skin" 자식으로 attach 하는 static helper.
    //
    // 정책 (특정 테마 (vintage 등) 하드코딩 없음 — ThemeSlot.Shelf 만 조건):
    //   - functional bookshelf root (BookshelfTurntableRoot/BookshelfRoot 또는 BookshelfRoot)
    //     자체는 절대 destroy/reparent 하지 않는다 — 책 슬롯/collider/focus/camera target/
    //     bounds 기준 interaction 모두 그대로 유지. BookshelfGenerator 의 ShelfSection 리스트
    //     /BooksRoot/책 anchor/row·column 계산도 손대지 않음 (read-only API 만 사용).
    //   - skin prefab 은 BookshelfRoot 자식으로 instantiate, 이름 = SkinChildName.
    //     turntable 회전을 자연스럽게 따라가기 위해 BookshelfTurntableRoot 가 존재하면 그 자식
    //     (BookshelfRoot) 아래에 들어간다.
    //   - BookshelfGenerator.CanonicalShellSize 를 reference 로 비등방 scale normalize +
    //     **bounds-center 정렬 translation** 을 적용한다. pivot-agnostic — GLB 가 bottom-center
    //     /mesh-center/임의 좌표 어느 쪽을 pivot 으로 export 되었든, scale 적용 후 renderer
    //     bounds.center 가 BookshelfRoot local (0, height/2, 0) 에 정확히 정렬된다. center
    //     정렬 누락 시 책장 visual 이 y 축으로 -height/2 만큼 빠져 책이 선반 위에 떠 보이는
    //     회귀가 발생했었음. spec.LocalPosition 은 placedItemsRoot frame 기준이라 무시.
    //   - skin 의 모든 Collider 는 disable — functional bookshelf 의 raycast/selection 을 가린다.
    //   - BookshelfRoot 자식의 기존 mesh Renderer 들은 attach 시점에 disable 하고 marker 에 저장,
    //     clear 시 enable 원복. BooksRoot 자식 (책) + skin 본인 자식은 hide 대상에서 제외.
    //   - 같은 위치에 이미 skin 이 있으면 destroy 후 새 skin 으로 교체 (idempotent re-apply).
    //   - 회귀 가드: attach 전/후 BooksRoot 첫 자식의 world position 을 캡처해 bookDelta 가
    //     0 이 아니면 warning. functional state 가 의도와 다르게 변형됐다는 신호.
    public static class BookshelfVisualSkinApplier
    {
        // 호출 책임: spec.ThemeSlot == ThemeSlot.Shelf 인 경우만 호출. 다른 슬롯에 대해서는
        // 호출자가 일반 floor/wall placement 경로를 타야 한다 (이 함수는 그런 경우 false 반환만 함).
        //
        // 반환: 부착 성공 여부. false 면 호출자가 fallback 처리 필요 (현재 RoomEditModeController
        // 는 false 일 때도 일반 floor placement 로 fallback 하지 않고 그냥 skip — Shelf 자산은
        // skin 으로만 의미가 있어 일반 placement 로 두면 위치가 어색하기 때문).
        public static bool TryAttach(RoomThemeDecoSpec spec, long themeId, IThemeAssetLoader loader, string source)
        {
            if (spec == null || spec.ThemeSlot != ThemeSlot.Shelf)
            {
                Debug.LogWarning($"[BookshelfSkin] TryAttach skip — spec null 또는 themeSlot != Shelf (themeSlot={(spec != null ? spec.ThemeSlot.ToString() : "spec=null")}) themeId={themeId} source={source}.");
                return false;
            }

            VisualSkinLog.Verbose($"[BookshelfSkin] TryAttach enter themeId={themeId} themeSlot={spec.ThemeSlot} assetKey={spec.AssetKey} prefabWired={(spec.Prefab != null ? spec.Prefab.name : "null")} source={source}");

            var prefab = loader != null ? loader.Resolve(spec) : spec.Prefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[BookshelfSkin] TryAttach FAIL reason=prefab_null — themeId={themeId} assetKey={spec.AssetKey} source={source}. " +
                                 "Inspector 에서 spec.prefab 슬롯 wiring 또는 fileID 확인. skin 미적용 (native shell 만 보임).");
                return false;
            }

            if (!TryFindFunctionalRoot(out var functionalRoot, out var referenceSize, out var rootFailReason))
            {
                Debug.LogWarning($"[BookshelfSkin] TryAttach FAIL reason={rootFailReason} — themeId={themeId} assetKey={spec.AssetKey} source={source}. " +
                                 "BookshelfGenerator/BookshelfRoot 가 ready 되면 deferred 재시도 (RenormalizeVintagePlacementsWhenBookshelfReady → TryReapplyShelfSkinFromCurrentTheme) 필요.");
                return false;
            }

            ClearSkinUnder(functionalRoot);

            // attach 전 첫 책 world position snapshot — "TryAttach 는 functional state 를
            // 건드리지 않는다" invariance 의 회귀 가드. attach 직후 같은 책의 world position 과
            // 비교해 diff 가 0 이 아니면 BooksRoot/책 transform 또는 BookshelfRoot transform 이
            // 영향받았다는 신호 (현재 로직상 0 이어야 정상).
            var firstBookBefore = TryGetFirstBookWorldPosition(functionalRoot);

            var go = Object.Instantiate(prefab, functionalRoot);
            go.name = BookshelfVisualSkin.SkinChildName;
            // localPosition: spec.LocalPosition 은 placedItemsRoot (world origin) frame 기준
            // 좌표라 그대로 박을 수 없다. 일단 zero 로 reset 한 뒤, scale 적용 후 renderer bounds
            // 측정해서 functional shell 의 기대 center (BookshelfRoot local (0, height/2, 0))
            // 와 정확히 일치하도록 bounds-center 정렬 translation 을 따로 적용한다 (아래).
            // pivot 이 bottom-center / mesh-center / 임의 좌표 어느 쪽이든 동일하게 정렬됨.
            //
            // localRotation: spec.LocalRotation 은 그대로 적용. GLB 의 forward (정면 mesh
            // 방향) 가 functional BookshelfRoot 의 forward 와 다른 경우 (예: vintage_shelf 는
            // 180 회전 필요) 를 디자이너가 spec 에서 보정한다. 다른 shelf GLB 라도 forward 가
            // 맞으면 spec.LocalRotation=zero 박으면 됨 — 일반화 유지.
            //
            // localScale: 일단 Vector3.one 으로 reset 후 아래에서 비등방 normalize 가 3축
            // 각각 CanonicalShellSize 에 정확히 맞추는 factor 로 덮어쓴다. spec.LocalScale 은
            // placedItemsRoot 시절 GLB raw 크기 보정용으로 박힌 값이라 skin 적용 시점엔 의미가
            // 다르며, 사용자 요구 "기존 책장 크기와 똑같이" 를 정확히 충족하려면 무시하는 게 맞다.
            go.transform.localPosition = Vector3.zero;
            go.transform.localEulerAngles = spec.LocalRotation;
            go.transform.localScale = Vector3.one;

            DisableAllColliders(go);

            // 비등방 normalize — width/height/depth 각각 CanonicalShellSize 에 fit. height 만
            // 맞추던 uniform 방식은 vintage_shelf 처럼 GLB 종횡비가 다른 자산에서 width/depth 가
            // 어긋났다 (사용자 회귀: ref=(1.60,1.60,0.25) vs after=(1.44,1.60,0.46)).
            var visualBoundsBefore = ComputeRendererBoundsSafe(go);
            Vector3 scaleFactor = Vector3.one;
            if (visualBoundsBefore.HasValue &&
                visualBoundsBefore.Value.size.x > 1e-4f && referenceSize.x > 0f &&
                visualBoundsBefore.Value.size.y > 1e-4f && referenceSize.y > 0f &&
                visualBoundsBefore.Value.size.z > 1e-4f && referenceSize.z > 0f)
            {
                scaleFactor = new Vector3(
                    Mathf.Clamp(referenceSize.x / visualBoundsBefore.Value.size.x, 0.01f, 100f),
                    Mathf.Clamp(referenceSize.y / visualBoundsBefore.Value.size.y, 0.01f, 100f),
                    Mathf.Clamp(referenceSize.z / visualBoundsBefore.Value.size.z, 0.01f, 100f));
                go.transform.localScale = scaleFactor;
            }

            // bounds-center 정렬 — scale 적용 직후 측정한 skin 의 world-space renderer bounds
            // center 를 functional shell 의 기대 world center 와 일치시키는 translation.
            //
            // 왜 이게 필요한가:
            //   BookshelfRoot 의 pivot 은 shell bottom-center (BookshelfGenerator.BuildBookshelfShell
            //   이 BottomBoard 를 y=boardThickness*0.5, TopBoard 를 y=height-boardThickness*0.5
            //   에 배치 — local y ∈ [0, height] 점유). 책 anchor 도 동일 frame 의 양수 y 에
            //   분포 (bottomY = boardThickness + row * (sectionHeight + boardThickness)).
            //
            //   GLB skin 의 pivot 이 mesh geometric center 인 경우 (Blender/Meshy 기본 export):
            //   scale fit 후 bounds center 는 BookshelfRoot local origin (0,0,0) 근방, bounds 는
            //   y ∈ [-height/2, +height/2] 점유 → skin top 이 y=+height/2 에서 끝, 그러나 책은
            //   y=+height 까지 분포 → "책이 선반 위로 뜬다" 회귀 발생.
            //
            //   bottom-pivot GLB 만 우연히 잘 맞는 상태였으므로, 일반화하려면 pivot 무관하게
            //   bounds.center 를 강제 정렬해야 한다.
            //
            // 구현: world-space 에서 desiredCenter - currentCenter 의 vector 를 transform.position
            // 에 더한다. parent 의 lossyScale/rotation 은 Unity 가 inverse transform 으로 자동
            // 반영하므로 별도 변환 불필요.
            var visualBoundsAfterScale = ComputeRendererBoundsSafe(go);
            Vector3 worldOffset = Vector3.zero;
            Vector3 desiredWorldCenter = functionalRoot.TransformPoint(new Vector3(0f, referenceSize.y * 0.5f, 0f));
            if (visualBoundsAfterScale.HasValue && visualBoundsAfterScale.Value.size.sqrMagnitude > 1e-8f)
            {
                worldOffset = desiredWorldCenter - visualBoundsAfterScale.Value.center;
                go.transform.position += worldOffset;
            }
            var visualBoundsAfter = ComputeRendererBoundsSafe(go);

            var marker = go.AddComponent<BookshelfVisualSkin>();
            marker.ThemeId = themeId;
            marker.AssetKey = spec.AssetKey;

            HideFunctionalShellRenderers(functionalRoot, marker);

            var firstBookAfter = TryGetFirstBookWorldPosition(functionalRoot);
            var bookDelta = (firstBookBefore.HasValue && firstBookAfter.HasValue)
                ? (firstBookAfter.Value - firstBookBefore.Value)
                : Vector3.zero;

            VisualSkinLog.Verbose($"[BookshelfSkin] themeId={themeId} themeSlot={spec.ThemeSlot} assetKey={spec.AssetKey} functionalRoot={functionalRoot.name} functionalWorldPos={functionalRoot.position} visualAttached={go.name} visualWorldPos={go.transform.position} referenceBounds={referenceSize} visualBoundsBefore={(visualBoundsBefore?.size.ToString() ?? "null")} visualBoundsAfter={(visualBoundsAfter?.size.ToString() ?? "null")} visualBoundsCenter={(visualBoundsAfter?.center.ToString() ?? "null")} desiredCenter={desiredWorldCenter} worldOffset={worldOffset} scaleFactor=({scaleFactor.x:F3},{scaleFactor.y:F3},{scaleFactor.z:F3}) firstBookBefore={(firstBookBefore?.ToString() ?? "null")} firstBookAfter={(firstBookAfter?.ToString() ?? "null")} bookDelta={bookDelta} source={source}");

            // bookDelta 가 의미 있게 0 이 아니면 functional state invariance 가 깨졌다는 신호.
            // 현재 경로상 발생하지 않아야 하지만 회귀 가드로 warning 남김.
            if (bookDelta.sqrMagnitude > 1e-6f)
            {
                Debug.LogWarning($"[BookshelfSkin] BOOK INVARIANCE BROKEN — bookDelta={bookDelta} themeId={themeId} assetKey={spec.AssetKey}. TryAttach 가 BooksRoot/책/BookshelfRoot transform 에 영향을 줬다는 신호. 호출 site 점검 필요.");
            }
            return true;
        }

        // BooksRoot 자식 중 첫 GameObject 의 world position 을 반환. 자식이 없으면 null.
        // attach invariance (책 위치가 attach 로 영향받지 않는지) 회귀 가드 전용.
        private static Vector3? TryGetFirstBookWorldPosition(Transform functionalRoot)
        {
            if (functionalRoot == null) return null;
            var booksRoot = functionalRoot.Find("BooksRoot");
            if (booksRoot == null || booksRoot.childCount == 0) return null;
            return booksRoot.GetChild(0).position;
        }

        // 외부 (테마 전환 시) 명시 호출. functional bookshelf 못 찾으면 no-op.
        public static void ClearSkin(string source = null)
        {
            if (!TryFindFunctionalRoot(out var functionalRoot, out _, out _))
            {
                return;
            }
            var existing = functionalRoot.Find(BookshelfVisualSkin.SkinChildName);
            bool exists = existing != null;
            ClearSkinUnder(functionalRoot);
            if (exists)
            {
                VisualSkinLog.Verbose($"[BookshelfSkinRestore] themeId=- visualExists={exists} normalizationApplied=false functionalRootPreserved=true source={source ?? "ClearSkin"}");
            }
        }

        private static void ClearSkinUnder(Transform functionalRoot)
        {
            if (functionalRoot == null) return;
            var existing = functionalRoot.Find(BookshelfVisualSkin.SkinChildName);
            if (existing == null) return;
            var marker = existing.GetComponent<BookshelfVisualSkin>();
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

        // functional bookshelf 의 root transform 과 reference bounds size 산출.
        // 우선순위: BookshelfTurntableRoot/BookshelfRoot → BookshelfRoot. CanonicalShellSize
        // (Inspector serialized width/height/depth) 는 shell 이 아직 빌드 안 됐어도 deterministic.
        //
        // 호출자가 실패 원인을 사용자에게 정확히 보고할 수 있도록 failReason 을 out 으로 분리:
        //   "generator_not_found"   — scene 에 BookshelfGenerator 인스턴스가 아직 없음.
        //   "bookshelf_root_not_found" — gen 은 있는데 자식 BookshelfRoot/BookshelfTurntableRoot
        //                                transform 이 hierarchy 에 없음 (이름 변경 회귀 등).
        //   "canonical_shell_size_invalid" — Inspector width/height/depth 가 0 또는 미설정.
        private static bool TryFindFunctionalRoot(out Transform root, out Vector3 referenceSize, out string failReason)
        {
            root = null;
            referenceSize = Vector3.zero;
            failReason = null;
            var gen = Object.FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            if (gen == null)
            {
                failReason = "generator_not_found";
                return false;
            }
            root = gen.transform.Find("BookshelfTurntableRoot/BookshelfRoot")
                 ?? gen.transform.Find("BookshelfRoot");
            if (root == null)
            {
                failReason = $"bookshelf_root_not_found (gen={gen.name} childCount={gen.transform.childCount})";
                return false;
            }
            referenceSize = gen.CanonicalShellSize;
            if (referenceSize.x <= 0f || referenceSize.y <= 0f || referenceSize.z <= 0f)
            {
                failReason = $"canonical_shell_size_invalid (referenceSize={referenceSize}) — BookshelfGenerator Inspector 의 width/height/depth 확인 필요.";
                return false;
            }
            return true;
        }

        // skin 부착 시 BookshelfRoot 자식의 visible mesh 들을 hide. BooksRoot 자식 + skin 본인
        // 자식 + 비활성 Renderer 는 제외. marker.HiddenFunctionalRenderers 에 저장해 clear 시 원복.
        private static void HideFunctionalShellRenderers(Transform functionalRoot, BookshelfVisualSkin marker)
        {
            var renderers = functionalRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (IsUnderNamedAncestor(r.transform, BookshelfVisualSkin.SkinChildName)) continue;
                if (IsUnderNamedAncestor(r.transform, "BooksRoot")) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.enabled = false;
                marker.HiddenFunctionalRenderers.Add(r);
            }
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

        // skin 전용 간이 bounds — RoomEditModeController.ComputeRendererBounds 와 동일 정책
        // (ParticleSystem/Trail/Line + 비활성 Renderer 제외, 첫 valid renderer 를 base 로 encapsulate).
        // 비등방 scale 측정 후 normalize 계산용.
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
