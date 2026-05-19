using System.Collections.Generic;
using BookshelfPorting.Runtime;
using UnityEngine;

namespace App.Bootstrap
{
    // 테마의 ThemeSlot.Shelf spec 으로 functional bookshelf 위에 attach 된 visual skin
    // GameObject 에 부착되는 marker. RoomEditModeController 가 직접 reference 하지 않고
    // BookshelfVisualSkinApplier 가 attach/clear 양 경로에서 lookup 키로 사용.
    //
    // 보관 항목:
    //   themeId   — spawn 한 테마 assetId. 디버깅 + 같은 테마 재적용 시 식별.
    //   assetKey  — spec.AssetKey. clear 후 다른 자산으로 재부착할 때 동일성 확인 용.
    //   hiddenFunctionalRenderers — attach 시점에 disable 한 BookshelfRoot 자식 Renderer 들.
    //       clear 시 원복해 functional shell mesh 가 다시 보이게 한다. BooksRoot 자식 +
    //       skin 본인 자식은 hide 대상에서 제외 (책/skin 은 그대로 보여야 함).
    //       runtime-only (SerializeField 미지정) — scene save / Inspector 직렬화 시 destroyed
    //       Renderer 의 fake-null reference 가 따라가 stale 직렬화 회귀 방지 (MR #174 beshu #3).
    //
    // BookshelfShellRebuilt 자동 재hide:
    //   BookshelfGenerator.RebuildActiveBookshelfVariant (회전 swap 끝나면 호출) / BuildBookshelf
    //   가 BookshelfRoot 자식 functional shell 을 새로 만든다. ClearChildren 은 skin 이름
    //   ("ThemeBookshelfVisual") 을 except 처리해 skin 자체는 살아남지만, 새로 생성된 functional
    //   shell renderer 들은 enabled=true 라 skin 위로 덮인다 (또는 z-fight). 이 marker 는
    //   OnEnable 에서 BookshelfGenerator.BookshelfShellRebuilt event 를 subscribe, 발생 시
    //   자기 형제 (BookshelfRoot 의 다른 자식) renderer 들을 다시 hide.
    //
    // Generator lookup cache (MR #174 beshu #1):
    //   매 OnEnable 마다 FindFirstObjectByType<BookshelfGenerator> 를 호출하면 활성 marker N 개
    //   × scene_size 만큼 비용이 누적된다 (책장 swap 시점 OnDisable+OnEnable 양쪽 fire). static
    //   cache 한 reference 를 우선 사용하고 Unity fake-null 시점에만 재탐색 — 단일 generator
    //   가정 (BookshelfSceneInstaller 가 scene 당 1개만 생성) 하에 N→1 비용 감소.
    [DisallowMultipleComponent]
    public sealed class BookshelfVisualSkin : MonoBehaviour
    {
        public const string SkinChildName = "ThemeBookshelfVisual";

        [SerializeField] private long themeId;
        [SerializeField] private string assetKey;
        // [SerializeField] 미지정 — runtime-only list. attach/clear 1 cycle 동안만 의미가 있어
        // disk persist 불필요. 직렬화하면 destroyed Renderer 의 fake-null 이 따라가 stale.
        private readonly List<Renderer> hiddenFunctionalRenderers = new List<Renderer>();

        private BookshelfGenerator subscribedGenerator;
        // 단일 generator 가정 — scene 당 1개 BookshelfGenerator 만 생성됨. 다른 scene 으로
        // 전환되면 Unity fake-null 처리로 자동 재탐색.
        private static BookshelfGenerator s_generatorCache;

        public long ThemeId
        {
            get => themeId;
            set => themeId = value;
        }

        public string AssetKey
        {
            get => assetKey;
            set => assetKey = value;
        }

        public List<Renderer> HiddenFunctionalRenderers => hiddenFunctionalRenderers;

        private void OnEnable()
        {
            // 이미 subscribe 한 상태에서 다시 OnEnable 진입하면 += 가 중복 호출돼 같은 event 가
            // 두 번 처리됨. 방어용 가드.
            if (subscribedGenerator != null) return;

            var gen = ResolveGeneratorCached();
            if (gen == null) return;
            subscribedGenerator = gen;
            gen.BookshelfShellRebuilt += OnBookshelfShellRebuilt;
            gen.TransitionShellPrepared += OnTransitionShellPrepared;
        }

        private void OnDisable()
        {
            UnsubscribeFromGenerator();
        }

        // OnTransitionShellPrepared 에서 clone marker 를 Destroy 하기 *전* 호출. Destroy 는 next
        // frame OnDisable 까지 지연되므로 그 사이에 도착하는 event 가 clone handler 까지 호출
        // (double-hide / 로그 noise) 하는 회귀 회피. (MR #174 beshu #2 반영.)
        internal void ForceUnsubscribe()
        {
            UnsubscribeFromGenerator();
        }

        private void UnsubscribeFromGenerator()
        {
            if (subscribedGenerator == null) return;
            subscribedGenerator.BookshelfShellRebuilt -= OnBookshelfShellRebuilt;
            subscribedGenerator.TransitionShellPrepared -= OnTransitionShellPrepared;
            subscribedGenerator = null;
        }

        private static BookshelfGenerator ResolveGeneratorCached()
        {
            // Unity fake-null 도 `== null` 로 잡힘 — destroyed 또는 scene 전환 시 자동 재탐색.
            if (s_generatorCache == null)
            {
                s_generatorCache = FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            }
            return s_generatorCache;
        }

        // BookshelfRoot 자식이 새로 (재)생성된 시점 — 기존 hidden 리스트는 이미 destroy 된
        // renderer reference 라 stale. 새 자식들을 walk 해서 다시 hide 한다 (BooksRoot 와 skin
        // 본인 자식은 제외).
        private void OnBookshelfShellRebuilt()
        {
            hiddenFunctionalRenderers.Clear();
            var parent = transform.parent;
            if (parent == null) return;

            var renderers = parent.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (IsUnderNamedAncestor(r.transform, SkinChildName)) continue;
                if (IsUnderNamedAncestor(r.transform, "BooksRoot")) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.enabled = false;
                hiddenFunctionalRenderers.Add(r);
            }

            VisualSkinLog.Verbose($"[BookshelfSkinRestore] themeId={themeId} assetKey={assetKey} event=BookshelfShellRebuilt re-hidFunctionalRenderers={hiddenFunctionalRenderers.Count} functionalRootPreserved=true");
        }

        // 회전 swap 중간 — transitionBookshelfRoot 가 새 functional shell 로 채워진 상태.
        // 회전 animation 중에 transition 이 카메라에 들어오면 skin 없이 raw 책장이 잠깐 보이는
        // 회귀를 막기 위해, 자신 (active skin) 의 hierarchy 를 transition root 자식으로 clone
        // 하고 transition root 의 functional renderer 들을 hide. transition root 가 swap 후
        // BookshelfGenerator 에 의해 destroy 될 때 clone 도 자동 정리됨 (자식이므로).
        private void OnTransitionShellPrepared(Transform transitionRoot)
        {
            if (transitionRoot == null) return;

            var clone = Instantiate(gameObject, transitionRoot, false);
            // clone 의 marker 는 Instantiate 가 OnEnable 을 호출하면서 이미 generator event 에
            // subscribe 한 상태. Destroy 는 next frame OnDisable 까지 지연되므로 그 사이에
            // 들어오는 BookshelfShellRebuilt / TransitionShellPrepared 가 clone handler 까지 호출
            // 하는 race 회피 — Destroy 전에 ForceUnsubscribe() 로 동일 frame 내 즉시 해제.
            var cloneMarker = clone.GetComponent<BookshelfVisualSkin>();
            if (cloneMarker != null)
            {
                cloneMarker.ForceUnsubscribe();
                Destroy(cloneMarker);
            }
            clone.name = SkinChildName;
            clone.transform.localPosition = transform.localPosition;
            clone.transform.localRotation = transform.localRotation;
            clone.transform.localScale = transform.localScale;

            var renderers = transitionRoot.GetComponentsInChildren<Renderer>(true);
            int hidden = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (IsUnderNamedAncestor(r.transform, SkinChildName)) continue;
                if (IsUnderNamedAncestor(r.transform, "BooksRoot")) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.enabled = false;
                hidden++;
            }

            VisualSkinLog.Verbose($"[BookshelfSkinRestore] themeId={themeId} assetKey={assetKey} event=TransitionShellPrepared transitionRoot={transitionRoot.name} cloneAttached={clone.name} hidTransitionRenderers={hidden}");
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
    }

    // verbose 로그 가드 — production 빌드에서 IL 자체 제거 (zero overhead). UNITY_EDITOR 가
    // default 활성, 빌드에서도 보고 싶으면 DOTSHELF_LOG_VISUAL_SKIN scripting define 을 Player
    // Settings 에 추가. Debug.LogWarning / Debug.LogError 는 호출 site 에서 직접 사용 — production
    // 에서도 의미 있는 진단이라 보존. (MR #174 beshu #8 / 다른 리뷰어 #2 반영.)
    internal static class VisualSkinLog
    {
        [System.Diagnostics.Conditional("DOTSHELF_LOG_ALL")]
        [System.Diagnostics.Conditional("DOTSHELF_LOG_VISUAL_SKIN")]
        internal static void Verbose(string message)
        {
            UnityEngine.Debug.Log(message);
        }
    }

    // Application.isPlaying ? Destroy : DestroyImmediate 패턴이 Applier 4 곳 + Override 1 곳에서
    // 중복되던 것을 single helper 로 통합. App.Application namespace 와 충돌 회피 위해 FQN 사용.
    // (MR #174 다른 리뷰어 #10 반영.)
    internal static class UnityObjectSafeDestroy
    {
        public static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
