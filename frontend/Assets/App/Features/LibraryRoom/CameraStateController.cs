using System;
using UnityEngine;

namespace App.Features.LibraryRoom
{
    public enum CameraState
    {
        QuarterView,
        ShelfView,
        BookFocusView,
        DashboardView
    }

    public sealed class CameraStateController : MonoBehaviour
    {
        [SerializeField] private CameraState currentState = CameraState.QuarterView;

        public CameraState CurrentState => currentState;
        public event Action<CameraState> OnCameraStateChanged;

        public void SwitchTo(CameraState next)
        {
            if (currentState == next)
            {
                return;
            }

            currentState = next;
            OnCameraStateChanged?.Invoke(next);
        }
    }
}

