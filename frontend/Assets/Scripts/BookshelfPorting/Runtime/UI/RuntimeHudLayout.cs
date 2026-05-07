using UnityEngine;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    internal static class RuntimeHudLayout
    {
        public static void SetObjectRect(GameObject target, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (target == null)
            {
                return;
            }

            SetRect(target.GetComponent<RectTransform>(), anchor, pivot, anchoredPosition, size);
        }

        public static void SetComponentRect(Component target, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (target == null)
            {
                return;
            }

            SetRect(target.GetComponent<RectTransform>(), anchor, pivot, anchoredPosition, size);
        }

        public static void SetChildRect(GameObject parent, string childName, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return;
            }

            var child = parent.transform.Find(childName);
            if (child == null)
            {
                return;
            }

            SetRect(child.GetComponent<RectTransform>(), anchor, pivot, anchoredPosition, size);
        }

        public static void SetChildActive(GameObject parent, string childName, bool isActive)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
            {
                return;
            }

            var child = parent.transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(isActive);
            }
        }

        public static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        public static void StretchTextToParent(Text text)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        public static void InsetTextToParent(Text text, float horizontalPadding, float verticalPadding)
        {
            if (text == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        public static float GetScrollViewportHeight(ScrollRect scrollRect, float fallbackHeight)
        {
            if (scrollRect != null && scrollRect.viewport != null && scrollRect.viewport.rect.height > 1f)
            {
                return scrollRect.viewport.rect.height;
            }

            return fallbackHeight;
        }

        public static void ApplyScrollState(
            ScrollRect scrollRect,
            Scrollbar scrollbar,
            RectTransform content,
            bool canScroll,
            bool resetToTop,
            float scrollSensitivity = 24f)
        {
            if (scrollbar != null)
            {
                scrollbar.gameObject.SetActive(canScroll);
                scrollbar.interactable = canScroll;
                if (!canScroll)
                {
                    scrollbar.size = 1f;
                    scrollbar.value = 1f;
                }
            }

            if (scrollRect == null)
            {
                return;
            }

            scrollRect.enabled = canScroll;
            scrollRect.scrollSensitivity = scrollSensitivity;
            Canvas.ForceUpdateCanvases();
            if (!canScroll || resetToTop)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                if (content != null)
                {
                    content.anchoredPosition = Vector2.zero;
                }

                return;
            }

            ClampScrollContentToViewport(scrollRect, content);
        }

        public static void ClampScrollContentToViewport(ScrollRect scrollRect, RectTransform content)
        {
            if (scrollRect == null || content == null || scrollRect.viewport == null)
            {
                return;
            }

            var maxY = Mathf.Max(0f, content.rect.height - scrollRect.viewport.rect.height);
            var position = content.anchoredPosition;
            position.x = 0f;
            position.y = Mathf.Clamp(position.y, 0f, maxY);
            content.anchoredPosition = position;
            if (maxY <= 0.01f)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }
}
