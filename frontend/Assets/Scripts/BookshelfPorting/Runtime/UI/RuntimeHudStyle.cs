using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BookshelfPorting.Runtime
{
    internal static class RuntimeHudStyle
    {
        private const float ButtonTextHorizontalPadding = 10f;
        private const float ButtonTextVerticalPadding = 3f;

        private static Sprite roundedUiSprite;
        private static Texture2D roundedUiTexture;

        public static void ApplyRoundedSprite(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GetRoundedUiSprite();
            image.type = Image.Type.Sliced;
        }

        public static void AddSoftShadow(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            Shadow shadow = null;
            var shadows = target.GetComponents<Shadow>();
            for (var i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    shadow = shadows[i];
                    break;
                }
            }

            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        public static void AddSoftOutline(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            var outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        public static void StylePanel(Image image, Color background)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = background;
            AddSoftShadow(image.gameObject, new Color(0f, 0f, 0f, 0.12f), new Vector2(0f, -5f));
        }

        public static void StyleBookDetailPanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 248, 236, 242);
            AddSoftShadow(image.gameObject, new Color(46f / 255f, 30f / 255f, 20f / 255f, 0.20f), new Vector2(0f, -9f));
            AddSoftOutline(image.gameObject, new Color32(255, 255, 255, 150), new Vector2(1f, -1f));
        }

        public static void StyleBookshelfIndexPanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            ApplyRoundedSprite(image);
            image.color = new Color32(255, 248, 236, 214);
            AddSoftShadow(image.gameObject, new Color(46f / 255f, 30f / 255f, 20f / 255f, 0.18f), new Vector2(0f, -7f));
            AddSoftOutline(image.gameObject, new Color32(255, 255, 255, 132), new Vector2(1f, -1f));
        }

        public static void PrepareBookshelfNavigationButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            var hoverEffect = button.GetComponent<UiHoverScaleEffect>();
            if (hoverEffect == null)
            {
                hoverEffect = button.gameObject.AddComponent<UiHoverScaleEffect>();
            }

            hoverEffect.SetBaseScale(Vector3.one);
            AddSoftOutline(button.gameObject, new Color32(255, 255, 255, 150), new Vector2(1f, -1f));
        }

        public static void StyleBookshelfNavigationButton(Button button, bool isInteractable)
        {
            if (button == null)
            {
                return;
            }

            var background = isInteractable
                ? new Color32(255, 248, 236, 222)
                : new Color32(238, 231, 221, 96);
            var highlighted = new Color32(255, 255, 255, 245);
            var disabled = new Color32(224, 216, 205, 92);
            var textColor = isInteractable
                ? new Color32(66, 49, 39, 255)
                : new Color32(124, 111, 101, 142);

            StyleButton(button, background, highlighted, disabled, 34, textColor, true);
            AddSoftOutline(button.gameObject, new Color32(255, 255, 255, 150), new Vector2(1f, -1f));

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            }
        }

        public static void StyleButton(Button button, Color background, Color highlightedBackground, Color disabledBackground, int fontSize, Color textColor, bool isBold = false)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                ApplyRoundedSprite(image);
                image.color = background;
            }

            AddSoftShadow(button.gameObject, new Color(0f, 0f, 0f, 0.06f), new Vector2(0f, -2f));

            var colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = highlightedBackground;
            colors.pressedColor = Color.Lerp(highlightedBackground, Color.black, 0.12f);
            colors.selectedColor = highlightedBackground;
            colors.disabledColor = disabledBackground;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var label = GetButtonLabelText(button);
            if (label != null)
            {
                label.fontSize = fontSize;
                label.color = textColor;
                label.fontStyle = isBold ? FontStyle.Bold : FontStyle.Normal;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.lineSpacing = 0.98f;
                RuntimeHudLayout.InsetTextToParent(label, ButtonTextHorizontalPadding, ButtonTextVerticalPadding);
            }
        }

        public static void StyleScrollbar(Scrollbar scrollbar, Color32 handleColor, Color32 trackColor)
        {
            if (scrollbar == null)
            {
                return;
            }

            var trackImage = scrollbar.GetComponent<Image>();
            if (trackImage != null)
            {
                ApplyRoundedSprite(trackImage);
                trackImage.color = trackColor;
            }

            if (scrollbar.handleRect == null)
            {
                return;
            }

            var handleImage = scrollbar.handleRect.GetComponent<Image>();
            if (handleImage != null)
            {
                ApplyRoundedSprite(handleImage);
                handleImage.color = handleColor;
            }
        }

        public static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = GetButtonLabelText(button);
            if (text != null)
            {
                text.text = label;
            }
        }

        public static Text GetButtonLabelText(Button button)
        {
            if (button == null)
            {
                return null;
            }

            var labelTransform = button.transform.Find("Label");
            if (labelTransform != null)
            {
                var labelText = labelTransform.GetComponent<Text>();
                if (labelText != null)
                {
                    return labelText;
                }
            }

            return button.GetComponentInChildren<Text>();
        }

        private static Sprite GetRoundedUiSprite()
        {
            if (roundedUiSprite == null)
            {
                const int textureSize = 32;
                const float cornerRadius = 11f;
                roundedUiTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
                {
                    name = "GeneratedUiSprite",
                    hideFlags = HideFlags.HideAndDontSave
                };

                roundedUiTexture.wrapMode = TextureWrapMode.Clamp;
                roundedUiTexture.filterMode = FilterMode.Bilinear;
                for (var y = 0; y < textureSize; y++)
                {
                    for (var x = 0; x < textureSize; x++)
                    {
                        var nearestX = Mathf.Clamp(x, cornerRadius, textureSize - 1f - cornerRadius);
                        var nearestY = Mathf.Clamp(y, cornerRadius, textureSize - 1f - cornerRadius);
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(nearestX, nearestY));
                        var alpha = Mathf.Clamp01(cornerRadius + 0.5f - distance);
                        roundedUiTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                roundedUiTexture.Apply();
                roundedUiSprite = Sprite.Create(
                    roundedUiTexture,
                    new Rect(0f, 0f, textureSize, textureSize),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(12f, 12f, 12f, 12f));
                roundedUiSprite.name = "GeneratedUiSprite";
                roundedUiSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return roundedUiSprite;
        }
    }

    internal static class LikeHeartIconProvider
    {
        private const string IconResourcePath = "App/UI/Icons/Reactions/icon_like_heart";
        private const string IconAssetPath = "Assets/Resources/App/UI/Icons/Reactions/icon_like_heart.png";

        private static readonly HashSet<string> LoggedRenderPoints = new();
        private static Sprite cachedSprite;
        private static Texture2D fallbackTexture;

        public static Sprite GetSprite()
        {
            if (cachedSprite != null)
            {
                return cachedSprite;
            }

            cachedSprite = Resources.Load<Sprite>(IconResourcePath);

#if UNITY_EDITOR
            if (cachedSprite == null)
            {
                cachedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(IconAssetPath);
            }
#endif

            if (cachedSprite == null)
            {
                Debug.LogWarning($"[LikeHeartIconProvider] icon_like_heart sprite not found at Resources path '{IconResourcePath}'; using generated fallback.");
                cachedSprite = CreateFallbackSprite();
            }

            return cachedSprite;
        }

        private static Sprite CreateFallbackSprite()
        {
            const int size = 64;
            fallbackTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "GeneratedLikeHeartIcon",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var px = (x + 0.5f) / size * 2f - 1f;
                    var py = (y + 0.5f) / size * 2f - 1f + 0.08f;
                    var heart = Mathf.Pow(px * px + py * py - 0.42f, 3f) - px * px * py * py * py;
                    var filledAlpha = Mathf.Clamp01((0.018f - heart) * 42f);
                    var innerAlpha = Mathf.Clamp01((-0.045f - heart) * 42f);
                    var alpha = Mathf.Clamp01(filledAlpha - innerAlpha);
                    fallbackTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            fallbackTexture.Apply(false, false);
            var sprite = Sprite.Create(
                fallbackTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "GeneratedLikeHeartIcon";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static void ApplyTo(Image image, string renderPoint)
        {
            ApplyTo(image, renderPoint, Color.white);
        }

        public static void ApplyTo(Image image, string renderPoint, Color tint)
        {
            if (image == null)
            {
                return;
            }

            var sprite = GetSprite();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.material = null;
            image.color = tint;
            image.raycastTarget = false;

            LogRenderPointOnce(image, sprite, renderPoint);
        }

        private static void LogRenderPointOnce(Image image, Sprite sprite, string renderPoint)
        {
            var key = string.IsNullOrWhiteSpace(renderPoint) ? image.name : renderPoint;
            if (!LoggedRenderPoints.Add(key))
            {
                return;
            }

            var texture = sprite != null ? sprite.texture : null;
            var textureInfo = texture != null
                ? $"{texture.name} {texture.width}x{texture.height} filter={texture.filterMode}"
                : "null";
            var materialName = image.material != null ? image.material.name : "null";
            Debug.Log(
                $"[LikeHeartIconProvider] {key}: sprite={(sprite != null ? sprite.name : "null")} " +
                $"texture={textureInfo} type={image.type} preserveAspect={image.preserveAspect} " +
                $"material={materialName} color={image.color}");
        }
    }
}
