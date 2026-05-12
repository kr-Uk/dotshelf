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
    //
    // BookshelfShellRebuilt 자동 재hide:
    //   BookshelfGenerator.RebuildActiveBookshelfVariant (회전 swap 끝나면 호출) / BuildBookshelf
    //   가 BookshelfRoot 자식 functional shell 을 새로 만든다. ClearChildren 은 skin 이름
    //   ("ThemeBookshelfVisual") 을 except 처리해 skin 자체는 살아남지만, 새로 생성된 functional
    //   shell renderer 들은 enabled=true 라 skin 위로 덮인다 (또는 z-fight). 이 marker 는
    //   OnEnable 에서 BookshelfGenerator.BookshelfShellRebuilt event 를 subscribe, 발생 시
    //   자기 형제 (BookshelfRoot 의 다른 자식) renderer 들을 다시 hide.
    [DisallowMultipleComponent]
    public sealed class BookshelfVisualSkin : MonoBehaviour
    {
        public const string SkinChildName = "ThemeBookshelfVisual";

        [SerializeField] private long themeId;
        [SerializeField] private string assetKey;
        [SerializeField] private List<Renderer> hiddenFunctionalRenderers = new List<Renderer>();

        private BookshelfGenerator subscribedGenerator;

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
            var gen = FindFirstObjectByType<BookshelfGenerator>(FindObjectsInactive.Include);
            if (gen == null) return;
            subscribedGenerator = gen;
            gen.BookshelfShellRebuilt += OnBookshelfShellRebuilt;
            gen.TransitionShellPrepared += OnTransitionShellPrepared;
        }

        private void OnDisable()
        {
            if (subscribedGenerator != null)
            {
                subscribedGenerator.BookshelfShellRebuilt -= OnBookshelfShellRebuilt;
                subscribedGenerator.TransitionShellPrepared -= OnTransitionShellPrepared;
                subscribedGenerator = null;
            }
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

            Debug.Log($"[BookshelfSkinRestore] themeId={themeId} assetKey={assetKey} event=BookshelfShellRebuilt re-hidFunctionalRenderers={hiddenFunctionalRenderers.Count} functionalRootPreserved=true");
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
            // clone 의 marker 컴포넌트는 transient — Instantiate 가 OnEnable 을 호출하면서 또
            // generator event 에 subscribe 했을 수 있다. 즉시 Destroy 호출 → next-frame OnDisable
            // 에서 unsubscribe 되어 self-replication / double-hide 회피.
            var cloneMarker = clone.GetComponent<BookshelfVisualSkin>();
            if (cloneMarker != null) Destroy(cloneMarker);
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

            Debug.Log($"[BookshelfSkinRestore] themeId={themeId} assetKey={assetKey} event=TransitionShellPrepared transitionRoot={transitionRoot.name} cloneAttached={clone.name} hidTransitionRenderers={hidden}");
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
}
