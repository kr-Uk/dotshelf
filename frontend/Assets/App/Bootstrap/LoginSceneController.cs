using UnityEngine;

namespace App.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class LoginSceneController : MonoBehaviour
    {
        private void Awake()
        {
            var onboardingScreenController = GetComponent<OnboardingScreenController>();
            if (onboardingScreenController == null)
            {
                onboardingScreenController = gameObject.AddComponent<OnboardingScreenController>();
            }

            onboardingScreenController.InitializeIfNeeded();

            var loginScreenController = GetComponent<LoginScreenController>();
            if (loginScreenController == null)
            {
                loginScreenController = gameObject.AddComponent<LoginScreenController>();
            }

            loginScreenController.InitializeIfNeeded();

            if (TryGetComponent<LoginUiStyler>(out var legacyStyler))
            {
                legacyStyler.enabled = false;
            }
        }
    }
}
