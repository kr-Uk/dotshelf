using App.Features.LibraryRoom;
using UnityEngine;
using UnityEngine.UI;

namespace App.Features.Debug
{
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private RoomStateController roomStateController;
        [SerializeField] private CameraStateController cameraStateController;
        [SerializeField] private Text statusText;

        private void Update()
        {
            if (statusText == null)
            {
                return;
            }

            var roomMode = roomStateController != null ? roomStateController.CurrentRoomMode.ToString() : "None";
            var uiMode = roomStateController != null ? roomStateController.CurrentUiMode.ToString() : "None";
            var cameraState = cameraStateController != null ? cameraStateController.CurrentState.ToString() : "None";
            statusText.text = $"Room: {roomMode}\nUI: {uiMode}\nCamera: {cameraState}";
        }
    }
}

