using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace App.Bootstrap
{
    public sealed class QuickMenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Image targetImage;
        private Color normalColor;
        private Color hoverColor;
        private Color pressedColor;
        private float hoverScale = 1.03f;
        private float pressedScale = 0.96f;
        private float duration = 0.14f;
        private bool isHovered;
        private bool isPressed;

        public void Configure(
            Image image,
            Color normal,
            Color hoverColorValue,
            Color pressed,
            float hoverScaleValue,
            float pressedScaleValue,
            float transitionDuration)
        {
            targetImage = image;
            normalColor = normal;
            hoverColor = hoverColorValue;
            pressedColor = pressed;
            hoverScale = hoverScaleValue;
            pressedScale = pressedScaleValue;
            duration = Mathf.Max(0.01f, transitionDuration);
        }

        private void Update()
        {
            var targetColor = isPressed ? pressedColor : (isHovered ? hoverColor : normalColor);
            var targetScale = isPressed ? pressedScale : (isHovered ? hoverScale : 1f);
            var t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / duration);

            if (targetImage != null)
            {
                targetImage.color = Color.Lerp(targetImage.color, targetColor, t);
            }

            var scale = transform.localScale.x;
            var nextScale = Mathf.Lerp(scale, targetScale, t);
            transform.localScale = new Vector3(nextScale, nextScale, 1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }
    }
}
