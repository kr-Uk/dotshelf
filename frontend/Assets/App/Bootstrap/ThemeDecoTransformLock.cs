using UnityEngine;

namespace App.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class ThemeDecoTransformLock : MonoBehaviour
    {
        [SerializeField] private string assetKey = string.Empty;
        [SerializeField] private Vector3 lockedLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 lockedLocalEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 lockedLocalScale = Vector3.one;

        private bool isConfigured;
        private bool hasLoggedOverwrite;

        public void Configure(string sourceAssetKey, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            assetKey = sourceAssetKey ?? string.Empty;
            lockedLocalPosition = localPosition;
            lockedLocalEulerAngles = localEulerAngles;
            lockedLocalScale = localScale;
            isConfigured = true;
            hasLoggedOverwrite = false;
            ApplyLockedTransform();
        }

        private void Awake()
        {
            ApplyLockedTransform();
        }

        private void OnEnable()
        {
            ApplyLockedTransform();
        }

        private void LateUpdate()
        {
            if (!isConfigured || !HasTransformDrift())
            {
                return;
            }

            if (!hasLoggedOverwrite)
            {
                Debug.Log($"[ThemeDecoTransformLock] Restoring local transform for '{assetKey}' on '{name}'.");
                hasLoggedOverwrite = true;
            }

            ApplyLockedTransform();
        }

        private void ApplyLockedTransform()
        {
            if (!isConfigured)
            {
                return;
            }

            transform.localPosition = lockedLocalPosition;
            transform.localRotation = Quaternion.Euler(lockedLocalEulerAngles);
            transform.localScale = lockedLocalScale;
        }

        private bool HasTransformDrift()
        {
            return !Approximately(transform.localPosition, lockedLocalPosition) ||
                   !ApproximatelyEuler(transform.localEulerAngles, lockedLocalEulerAngles) ||
                   !Approximately(transform.localScale, lockedLocalScale);
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Abs(left.x - right.x) <= 0.0001f &&
                   Mathf.Abs(left.y - right.y) <= 0.0001f &&
                   Mathf.Abs(left.z - right.z) <= 0.0001f;
        }

        private static bool ApproximatelyEuler(Vector3 left, Vector3 right)
        {
            return Mathf.Abs(Mathf.DeltaAngle(left.x, right.x)) <= 0.01f &&
                   Mathf.Abs(Mathf.DeltaAngle(left.y, right.y)) <= 0.01f &&
                   Mathf.Abs(Mathf.DeltaAngle(left.z, right.z)) <= 0.01f;
        }
    }
}
