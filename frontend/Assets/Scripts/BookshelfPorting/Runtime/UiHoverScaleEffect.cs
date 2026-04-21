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

            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = baseScale;
        }

        public void SetBaseScale(float scale)
        {
            SetBaseScale(Vector3.one * scale);
        }

        public void SetBaseScale(Vector3 scale)
        {
            baseScale = scale;
            targetScale = scale;

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
