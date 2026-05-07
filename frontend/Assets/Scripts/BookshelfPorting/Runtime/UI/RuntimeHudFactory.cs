using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    internal static class RuntimeHudFactory
    {
        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityAction onClick)
        {
            return CreateButton(parent, name, label, anchoredPosition, onClick, new Vector2(170f, 40f), new Vector2(1f, 1f));
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityAction onClick, Vector2 size, Vector2 anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.11f, 0.12f, 0.14f, 0.86f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var labelText = CreateLabel(buttonObject.transform, "Label", Vector2.zero, size, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.fontSize = 18;
            return button;
        }

        public static GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            var image = panelObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.09f, 0.88f);
            image.raycastTarget = false;

            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return panelObject;
        }

        public static RectTransform CreateContainer(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var container = new GameObject(name);
            container.transform.SetParent(parent, false);

            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static GameObject CreateScrollViewport(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var viewportObject = new GameObject(name);
            viewportObject.transform.SetParent(parent, false);

            var image = viewportObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.002f);
            image.raycastTarget = true;
            viewportObject.AddComponent<RectMask2D>();

            var rect = viewportObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            var scrollRect = viewportObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 22f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return viewportObject;
        }

        public static RectTransform CreateScrollContent(Transform parent, string name, Vector2 size)
        {
            var contentObject = new GameObject(name);
            contentObject.transform.SetParent(parent, false);

            var rect = contentObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, size.y);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Scrollbar CreateVerticalScrollbar(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var scrollbarObject = new GameObject(name);
            scrollbarObject.transform.SetParent(parent, false);

            var trackImage = scrollbarObject.AddComponent<Image>();
            RuntimeHudStyle.ApplyRoundedSprite(trackImage);
            trackImage.color = new Color32(238, 231, 221, 150);
            trackImage.raycastTarget = true;

            var rect = scrollbarObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            var handleObject = new GameObject("Handle");
            handleObject.transform.SetParent(scrollbarObject.transform, false);
            var handleImage = handleObject.AddComponent<Image>();
            RuntimeHudStyle.ApplyRoundedSprite(handleImage);
            handleImage.color = new Color32(73, 117, 147, 210);
            handleImage.raycastTarget = true;

            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = new Vector2(1f, 1f);
            handleRect.offsetMax = new Vector2(-1f, -1f);
            handleRect.localScale = Vector3.one;

            var scrollbar = scrollbarObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            return scrollbar;
        }

        public static Image CreateImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Color color, bool isVisible = true)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            RuntimeHudStyle.ApplyRoundedSprite(image);
            image.color = color;
            image.raycastTarget = false;
            imageObject.SetActive(isVisible);

            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return image;
        }

        public static Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = RuntimeUiFont.Resolve();
            text.color = Color.white;
            text.alignment = alignment;
            text.text = string.Empty;
            text.raycastTarget = false;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : Vector2.one;
            rect.pivot = alignment == TextAnchor.UpperLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        public static GameObject CreateModalBackdrop(Transform parent, string name, Color color)
        {
            var backdropObject = new GameObject(name);
            backdropObject.transform.SetParent(parent, false);

            var image = backdropObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            var rect = backdropObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return backdropObject;
        }
    }
}
