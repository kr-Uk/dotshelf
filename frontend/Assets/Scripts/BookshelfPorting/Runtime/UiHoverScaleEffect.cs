using UnityEngine;
using UnityEngine.EventSystems;

namespace BookshelfPorting.Runtime
{
    public sealed class UiHoverScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float hoverScale = 1.02f;
        [SerializeField] private float animationSpeed = 14f;

        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;
        private bool isAnimating;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            targetScale = baseScale;
            isAnimating = false;
            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale;
            }
        }

        private void OnDisable()
        {
            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale;
            }
        }

        private void Update()
        {
            if (rectTransform == null)
            {
                return;
            }

            if (!isAnimating)
            {
                return;
            }

            var nextScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
            if ((nextScale - targetScale).sqrMagnitude < 0.0001f)
            {
                rectTransform.localScale = targetScale;
                isAnimating = false;
                return;
            }

            rectTransform.localScale = nextScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = baseScale * hoverScale;
            isAnimating = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = baseScale;
            isAnimating = true;
        }

        public void SetBaseScale(float scale)
        {
            SetBaseScale(Vector3.one * scale);
        }

        public void SetBaseScale(Vector3 scale)
        {
            baseScale = scale;
            targetScale = scale;
            isAnimating = false;

            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = scale;
            }
        }
    }
}
