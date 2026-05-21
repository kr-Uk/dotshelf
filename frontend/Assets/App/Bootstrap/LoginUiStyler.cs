using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Bootstrap
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class LoginUiStyler : MonoBehaviour
    {
        [Header("Background / Card")]
        [SerializeField] private Image background;
        [SerializeField] private Image card;
        [SerializeField] private Shadow cardShadow;

        [Header("Texts")]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text userIdLabel;
        [SerializeField] private TMP_Text passwordLabel;
        [SerializeField] private TMP_Text errorLabel;

        [Header("Inputs")]
        [SerializeField] private TMP_InputField userIdInput;
        [SerializeField] private Image userIdInputBg;
        [SerializeField] private Outline userIdInputOutline;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Image passwordInputBg;
        [SerializeField] private Outline passwordInputOutline;

        [Header("Button")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Image loginButtonBg;
        [SerializeField] private TMP_Text loginButtonLabel;
        [SerializeField] private Shadow loginButtonShadow;

        [Header("Copy")]
        [SerializeField] private string titleText    = "다시 오신 걸 환영해요";
        [SerializeField] private string subtitleText = "계정 정보를 입력하고 세션을 시작합니다.";

        private static readonly Color Bg        = Hex("#f7fff7");
        private static readonly Color CardBg   = Hex("#ffffff");
        private static readonly Color Navy     = Hex("#0f172a");
        private static readonly Color Blue     = Hex("#58b9ff");
        private static readonly Color BlueHover = Hex("#2ea3ff");
        private static readonly Color BluePress = Hex("#1c8fe6");
        private static readonly Color Danger   = Hex("#ef4444");

        private void Awake()
        {
            if (GetComponent<LoginScreenController>() != null)
            {
                enabled = false;
                return;
            }

            ApplyStyle();
            HookFocus();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (GetComponent<LoginScreenController>() != null)
            {
                return;
            }

            if (!UnityEngine.Application.isPlaying) ApplyStyle();
        }
#endif

        private void ApplyStyle()
        {
            if (background != null) background.color = Bg;

            if (card != null) card.color = CardBg;
            if (cardShadow != null)
            {
                cardShadow.effectColor = WithA(Navy, 0.5f);
                cardShadow.effectDistance = new Vector2(0f, -8f);
            }

            if (title != null)
            {
                title.text = titleText;
                title.fontSize = 32;
                title.fontStyle = FontStyles.Bold;
                title.color = Navy;
                title.characterSpacing = -2f;
            }
            if (subtitle != null)
            {
                subtitle.text = subtitleText;
                subtitle.fontSize = 14;
                subtitle.color = WithA(Navy, 0.6f);
                subtitle.fontStyle = FontStyles.Normal;
            }

            StyleFieldLabel(userIdLabel);
            StyleFieldLabel(passwordLabel);

            StyleInput(userIdInput,    userIdInputBg,    userIdInputOutline);
            StyleInput(passwordInput,  passwordInputBg,  passwordInputOutline);

            if (loginButtonBg != null) loginButtonBg.color = Blue;
            if (loginButtonLabel != null)
            {
                loginButtonLabel.fontSize = 16;
                loginButtonLabel.fontStyle = FontStyles.Bold;
                loginButtonLabel.color = CardBg;
            }
            if (loginButtonShadow != null)
            {
                loginButtonShadow.effectColor = Blue;
                loginButtonShadow.effectDistance = new Vector2(0f, -6f);
            }
            if (loginButton != null)
            {
                var c = loginButton.colors;
                c.normalColor      = Blue;
                c.highlightedColor = BlueHover;
                c.pressedColor     = BluePress;
                c.selectedColor    = BlueHover;
                c.disabledColor    = WithA(Blue, 0.5f);
                c.fadeDuration     = 0.1f;
                loginButton.colors = c;
            }

            if (errorLabel != null)
            {
                errorLabel.fontSize = 13;
                errorLabel.color = Danger;
                errorLabel.alignment = TextAlignmentOptions.Center;
            }
        }

        private static void StyleFieldLabel(TMP_Text label)
        {
            if (label == null) return;
            label.fontSize = 11;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.color = WithA(Navy, 0.6f);
            label.characterSpacing = 8f;
        }

        private static void StyleInput(TMP_InputField input, Image bg, Outline outline)
        {
            if (input == null) return;
            if (bg != null) bg.color = Bg;

            if (input.textComponent is TMP_Text tc)
            {
                tc.fontSize = 16;
                tc.color = Navy;
            }
            if (input.placeholder is TMP_Text ph)
            {
                ph.fontSize = 16;
                ph.color = WithA(Navy, 0.4f);
            }
            if (outline != null)
            {
                outline.effectColor = WithA(Navy, 0.15f);
                outline.effectDistance = new Vector2(1f, 1f);
            }
        }

        private void HookFocus()
        {
            if (userIdInput != null)
            {
                userIdInput.onSelect.AddListener(OnUserIdSelect);
                userIdInput.onDeselect.AddListener(OnUserIdDeselect);
            }
            if (passwordInput != null)
            {
                passwordInput.onSelect.AddListener(OnPasswordSelect);
                passwordInput.onDeselect.AddListener(OnPasswordDeselect);
            }
        }

        private void OnUserIdSelect(string _)   { if (userIdInputOutline != null)   userIdInputOutline.effectColor   = Blue; }
        private void OnUserIdDeselect(string _) { if (userIdInputOutline != null)   userIdInputOutline.effectColor   = WithA(Navy, 0.15f); }
        private void OnPasswordSelect(string _) { if (passwordInputOutline != null) passwordInputOutline.effectColor = Blue; }
        private void OnPasswordDeselect(string _){ if (passwordInputOutline != null) passwordInputOutline.effectColor = WithA(Navy, 0.15f); }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
        private static Color WithA(Color c, float a) { c.a = a; return c; }
    }
}
