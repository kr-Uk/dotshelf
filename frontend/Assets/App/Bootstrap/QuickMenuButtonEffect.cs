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
        private bool isAnimating;

        private const float ScaleSnapThreshold = 0.001f;
        private const float ColorSnapThreshold = 0.004f;

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

            if (targetImage != null)
            {
                targetImage.color = normalColor;
            }
            transform.localScale = Vector3.one;
            isAnimating = false;
        }

        private void Update()
        {
            if (!isAnimating)
            {
                return;
            }

            var targetColor = isPressed ? pressedColor : (isHovered ? hoverColor : normalColor);
            var targetScale = isPressed ? pressedScale : (isHovered ? hoverScale : 1f);
            var t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / duration);

            var colorSettled = true;
            if (targetImage != null)
            {
                targetImage.color = Color.Lerp(targetImage.color, targetColor, t);
                colorSettled = IsClose(targetImage.color, targetColor);
                if (colorSettled)
                {
                    targetImage.color = targetColor;
                }
            }

            var scale = transform.localScale.x;
            var nextScale = Mathf.Lerp(scale, targetScale, t);
            var scaleSettled = Mathf.Abs(nextScale - targetScale) <= ScaleSnapThreshold;
            if (scaleSettled)
            {
                nextScale = targetScale;
            }

            transform.localScale = new Vector3(nextScale, nextScale, 1f);
            isAnimating = !(colorSettled && scaleSettled);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            isAnimating = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            isAnimating = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            isAnimating = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            isAnimating = true;
        }

        private static bool IsClose(Color current, Color target)
        {
            return Mathf.Abs(current.r - target.r) <= ColorSnapThreshold &&
                   Mathf.Abs(current.g - target.g) <= ColorSnapThreshold &&
                   Mathf.Abs(current.b - target.b) <= ColorSnapThreshold &&
                   Mathf.Abs(current.a - target.a) <= ColorSnapThreshold;
        }
    }
}
