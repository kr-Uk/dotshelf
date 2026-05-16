using UnityEngine;

namespace App.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class FantasyDeskTransformProbe : MonoBehaviour
    {
        [SerializeField] private string probeName = string.Empty;
        [SerializeField] private bool useWorldTransform;
        [SerializeField] private Vector3 expectedPosition = Vector3.zero;
        [SerializeField] private Vector3 expectedEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 expectedScale = Vector3.one;
        [SerializeField] private string configuredSource = string.Empty;

        private bool hasLoggedDrift;

        public void Configure(
            string name,
            bool trackWorld,
            Vector3 position,
            Vector3 eulerAngles,
            Vector3 scale,
            string source)
        {
            probeName = string.IsNullOrWhiteSpace(name) ? gameObject.name : name;
            useWorldTransform = trackWorld;
            expectedPosition = position;
            expectedEulerAngles = eulerAngles;
            expectedScale = scale;
            configuredSource = source ?? string.Empty;
            hasLoggedDrift = false;
        }

        private void LateUpdate()
        {
            var actualPosition = useWorldTransform ? transform.position : transform.localPosition;
            var actualEulerAngles = useWorldTransform ? transform.eulerAngles : transform.localEulerAngles;
            var actualScale = useWorldTransform ? transform.lossyScale : transform.localScale;
            if (!HasDrift(actualPosition, actualEulerAngles, actualScale))
            {
                hasLoggedDrift = false;
                return;
            }

            if (hasLoggedDrift)
            {
                return;
            }

            hasLoggedDrift = true;
            Debug.LogWarning(
                $"[FantasyDesk] Transform overwritten by runtime probe='{probeName}' source='{configuredSource}' " +
                $"space={(useWorldTransform ? "world" : "local")} " +
                $"actualPos={actualPosition} actualRot={actualEulerAngles} actualScale={actualScale} " +
                $"expectedPos={expectedPosition} expectedRot={expectedEulerAngles} expectedScale={expectedScale}");
        }

        private bool HasDrift(Vector3 actualPosition, Vector3 actualEulerAngles, Vector3 actualScale)
        {
            return !Approximately(actualPosition, expectedPosition) ||
                   !ApproximatelyEuler(actualEulerAngles, expectedEulerAngles) ||
                   !Approximately(actualScale, expectedScale);
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
