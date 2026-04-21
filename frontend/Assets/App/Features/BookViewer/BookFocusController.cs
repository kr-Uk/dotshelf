using App.Features.LibraryRoom;
using UnityEngine;

namespace App.Features.BookViewer
{
    public sealed class BookFocusController : MonoBehaviour
    {
        [SerializeField] private BookInteractionManager interactionManager;
        [SerializeField] private CameraStateController cameraStateController;
        [SerializeField] private GameObject bookDetailPanel;

        private void OnEnable()
        {
            if (interactionManager != null)
            {
                interactionManager.OnBookTapped += HandleBookTapped;
            }
        }

        private void OnDisable()
        {
            if (interactionManager != null)
            {
                interactionManager.OnBookTapped -= HandleBookTapped;
            }
        }

        public void ReleaseFocus()
        {
            interactionManager?.ReleaseFocus();

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(false);
            }

            cameraStateController?.SwitchTo(CameraState.ShelfView);
        }

        private void HandleBookTapped(BookView book)
        {
            if (book == null)
            {
                return;
            }

            cameraStateController?.SwitchTo(CameraState.BookFocusView);

            if (bookDetailPanel != null)
            {
                bookDetailPanel.SetActive(true);
            }
        }
    }
}

