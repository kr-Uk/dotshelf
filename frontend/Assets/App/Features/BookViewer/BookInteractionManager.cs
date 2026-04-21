using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace App.Features.BookViewer
{
    public sealed class BookInteractionManager : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private float tapMoveThresholdPixels = 8f;

        private BookView pressedBook;
        private Vector2 pressPosition;

        public BookView CurrentHovered { get; private set; }
        public BookView CurrentFocused { get; private set; }

        public event Action<BookView> OnBookTapped;

        private void Awake()
        {
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }
        }

        public void PointerDown(Vector2 screenPosition)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                pressedBook = null;
                return;
            }

            pressedBook = RaycastBook(screenPosition);
            pressPosition = screenPosition;
            pressedBook?.SetState(BookState.Pressed);
        }

        public void PointerUp(Vector2 screenPosition)
        {
            if (pressedBook == null)
            {
                return;
            }

            var releasedBook = pressedBook;
            pressedBook = null;
            if (Vector2.Distance(pressPosition, screenPosition) <= tapMoveThresholdPixels)
            {
                RequestFocus(releasedBook);
                OnBookTapped?.Invoke(releasedBook);
                return;
            }

            releasedBook.SetState(BookState.Idle);
        }

        public void RequestFocus(BookView book)
        {
            if (book == null)
            {
                return;
            }

            if (CurrentFocused != null && CurrentFocused != book)
            {
                CurrentFocused.SetState(BookState.Idle);
            }

            CurrentFocused = book;
            CurrentFocused.SetState(BookState.Focused);
        }

        public void ReleaseFocus()
        {
            if (CurrentFocused == null)
            {
                return;
            }

            CurrentFocused.SetState(BookState.Idle);
            CurrentFocused = null;
        }

        private BookView RaycastBook(Vector2 screenPosition)
        {
            if (sceneCamera == null)
            {
                return null;
            }

            var ray = sceneCamera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out var hit, 100f)
                ? hit.collider.GetComponentInParent<BookView>()
                : null;
        }
    }
}

