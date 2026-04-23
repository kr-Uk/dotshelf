using System;
using System.Collections;
using UnityEngine;

namespace App.Features.ShelfSwitch
{
    public sealed class ShelfTurntableController : MonoBehaviour
    {
        [SerializeField] private Transform turntablePivot;
        [SerializeField] private float transitionDuration = 0.7f;

        private Coroutine transitionRoutine;

        public bool IsTransitioning => transitionRoutine != null;
        public event Action OnTransitionCompleted;

        public void RotateToIndex(int index, int shelfCount)
        {
            if (turntablePivot == null || shelfCount <= 0)
            {
                return;
            }

            var angle = -360f / shelfCount * index;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(RotateTo(Quaternion.Euler(0f, angle, 0f)));
        }

        private IEnumerator RotateTo(Quaternion targetRotation)
        {
            var elapsed = 0f;
            var startRotation = turntablePivot.localRotation;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
                turntablePivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            turntablePivot.localRotation = targetRotation;
            transitionRoutine = null;
            OnTransitionCompleted?.Invoke();
        }
    }
}

