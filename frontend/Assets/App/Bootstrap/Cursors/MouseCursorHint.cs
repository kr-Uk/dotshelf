using UnityEngine;
using UnityEngine.EventSystems;

namespace App.Bootstrap.Cursors
{
    [DisallowMultipleComponent]
    public sealed class MouseCursorHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private MouseCursorKind cursor = MouseCursorKind.Pointer;
        [SerializeField] private bool usePressedCursor;
        [SerializeField] private MouseCursorKind pressedCursor = MouseCursorKind.Grabbing;
        [SerializeField] private int priority = 10;

        private bool isHovering;
        private bool isPressed;

        public MouseCursorKind Cursor => cursor;
        public bool UsePressedCursor => usePressedCursor;
        public MouseCursorKind PressedCursor => pressedCursor;
        public int Priority => priority;

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            isPressed = false;
            MouseCursorService.ClearOverride(this);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!usePressedCursor || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            isPressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!usePressedCursor || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            isPressed = false;
            if (isHovering)
            {
                Apply();
                return;
            }

            MouseCursorService.ClearOverride(this);
        }

        private void OnDisable()
        {
            isHovering = false;
            isPressed = false;
            MouseCursorService.ClearOverride(this);
        }

        private void Apply()
        {
            MouseCursorService.SetOverride(this, isPressed ? pressedCursor : cursor, priority);
        }
    }
}
