using App.Infrastructure.Catalogs;
using BookshelfPorting.Runtime;
using System.Collections.Generic;
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
            // spec.LocalPosition / LocalRotation / LocalScale 3종 모두 single source of truth.
            // 이전 정책 (Vector3.zero/one reset + CanonicalShellSize 비등방 normalize + renderer
            // bounds 기반 bounds-center alignment) 이 매 spawn 마다 Inspector 튜닝값을 덮어써
            // play mode 진입 / 테마 변경 / 씬 재진입 시마다 값이 reset 되는 회귀가 있어 폐기.
            // 대가: pivot 이 mesh-center 인 GLB 가 들어오면 디자이너가 spec.LocalPosition 의 Y 를
            // 명시 보정해야 함. transition clone 도 transform 값 그대로 복사하므로 자동 합류.
            go.transform.localPosition = spec.LocalPosition;
            go.transform.localEulerAngles = spec.LocalRotation;
            if (spec.LocalScale.sqrMagnitude > 1e-6f)
            {
                go.transform.localScale = spec.LocalScale;
            }

            DisableAllColliders(go);

            // 진단용 bounds capture — transform 변경은 위에서 이미 끝났고 alignment 후속 처리도
            // 없어 결과 transform 은 spec 값과 일치. verbose 로그에서 일치 검증용.
            var visualBoundsAfter = ComputeRendererBoundsSafe(go);

            var marker = go.AddComponent<BookshelfVisualSkin>();
            marker.ThemeId = themeId;
            marker.AssetKey = spec.AssetKey;

            HideFunctionalShellRenderers(functionalRoot, marker);

            var firstBookAfter = TryGetFirstBookWorldPosition(functionalRoot);
            var bookDelta = (firstBookBefore.HasValue && firstBookAfter.HasValue)
                ? (firstBookAfter.Value - firstBookBefore.Value)
                : Vector3.zero;

            VisualSkinLog.Verbose($"[BookshelfSkin] themeId={themeId} themeSlot={spec.ThemeSlot} assetKey={spec.AssetKey} functionalRoot={functionalRoot.name} functionalWorldPos={functionalRoot.position} visualAttached={go.name} visualWorldPos={go.transform.position} visualLocalPos={go.transform.localPosition} visualLocalEuler={go.transform.localEulerAngles} visualLocalScale={go.transform.localScale} referenceBounds={referenceSize} visualBoundsAfter={(visualBoundsAfter?.size.ToString() ?? "null")} visualBoundsCenter={(visualBoundsAfter?.center.ToString() ?? "null")} specLocalPosition={spec.LocalPosition} specLocalRotation={spec.LocalRotation} specLocalScale={spec.LocalScale} firstBookBefore={(firstBookBefore?.ToString() ?? "null")} firstBookAfter={(firstBookAfter?.ToString() ?? "null")} bookDelta={bookDelta} source={source}");

            // bookDelta 가 의미 있게 0 이 아니면 functional state invariance 가 깨졌다는 신호.
            // 현재 경로상 발생하지 않아야 하지만 회귀 가드로 warning 남김.
            if (bookDelta.sqrMagnitude > 1e-6f)
            {
                Debug.LogWarning($"[BookshelfSkin] BOOK INVARIANCE BROKEN — bookDelta={bookDelta} themeId={themeId} assetKey={spec.AssetKey}. TryAttach 가 BooksRoot/책/BookshelfRoot transform 에 영향을 줬다는 신호. 호출 site 점검 필요.");
            }
            return true;
        }

        // Customization catalog path: same functional-root attach policy as
        // theme shelf specs, but the transform comes from CustomizationEntry.
        public static bool TryAttach(CustomizationEntry entry, long themeId, string source)
        {
            if (entry == null)
            {
                Debug.LogWarning($"[BookshelfSkin] TryAttach skip - entry null. themeId={themeId} source={source}.");
                return false;
            }

            if (entry.Prefab == null)
            {
                Debug.LogWarning($"[BookshelfSkin] TryAttach FAIL reason=prefab_null - entryAssetId={entry.AssetId} source={source}.");
                return false;
            }

            if (!TryFindFunctionalRoot(out var functionalRoot, out var referenceSize, out var rootFailReason))
            {
                Debug.LogWarning($"[BookshelfSkin] TryAttach FAIL reason={rootFailReason} - entryAssetId={entry.AssetId} source={source}.");
                return false;
            }

            ClearSkinUnder(functionalRoot);

            var firstBookBefore = TryGetFirstBookWorldPosition(functionalRoot);
            var go = Object.Instantiate(entry.Prefab, functionalRoot);
            go.name = BookshelfVisualSkin.SkinChildName;
            go.transform.localPosition = entry.PlacementAnchor;
            go.transform.localEulerAngles = entry.PlacementRotation;
            go.transform.localScale = entry.PlacementScale.sqrMagnitude > 1e-6f ? entry.PlacementScale : Vector3.one;

            DisableAllColliders(go);
            var visualBoundsAfter = ComputeRendererBoundsSafe(go);

            var marker = go.AddComponent<BookshelfVisualSkin>();
            marker.ThemeId = themeId;
            marker.AssetKey = !string.IsNullOrWhiteSpace(entry.ThemeTag)
                ? $"{entry.ThemeTag}:{entry.AssetId}"
                : entry.AssetId.ToString();

            HideFunctionalShellRenderers(functionalRoot, marker);

            var firstBookAfter = TryGetFirstBookWorldPosition(functionalRoot);
            var bookDelta = (firstBookBefore.HasValue && firstBookAfter.HasValue)
                ? (firstBookAfter.Value - firstBookBefore.Value)
                : Vector3.zero;

            VisualSkinLog.Verbose($"[BookshelfSkin] entryAssetId={entry.AssetId} themeTag={entry.ThemeTag} functionalRoot={functionalRoot.name} " +
                                  $"visualAttached={go.name} visualLocalPos={go.transform.localPosition} visualLocalEuler={go.transform.localEulerAngles} " +
                                  $"visualLocalScale={go.transform.localScale} referenceBounds={referenceSize} visualBoundsAfter={(visualBoundsAfter?.size.ToString() ?? "null")} " +
                                  $"firstBookBefore={(firstBookBefore?.ToString() ?? "null")} firstBookAfter={(firstBookAfter?.ToString() ?? "null")} bookDelta={bookDelta} source={source}");

            if (bookDelta.sqrMagnitude > 1e-6f)
            {
                Debug.LogWarning($"[BookshelfSkin] BOOK INVARIANCE BROKEN - bookDelta={bookDelta} entryAssetId={entry.AssetId}. Check skin attach path.");
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
            var targets = new List<Transform>();
            var skins = functionalRoot.GetComponentsInChildren<BookshelfVisualSkin>(true);
            if (skins != null)
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    if (skins[i] == null || skins[i].transform == null) continue;
                    if (!targets.Contains(skins[i].transform))
                    {
                        targets.Add(skins[i].transform);
                    }
                }
            }

            var children = functionalRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null || child == functionalRoot) continue;
                if (child.gameObject.name != BookshelfVisualSkin.SkinChildName) continue;
                if (!targets.Contains(child))
                {
                    targets.Add(child);
                }
            }

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                RestoreAndDestroySkin(targets[i]);
            }
        }

        private static void RestoreAndDestroySkin(Transform existing)
        {
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
            existing.gameObject.SetActive(false);
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
