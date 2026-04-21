using System;
using UnityEngine;

namespace App.Features.LibraryRoom
{
    public enum RoomMode
    {
        MyRoom,
        VisitorMode
    }

    public enum UiMode
    {
        ViewMode,
        EditMode
    }

    public sealed class RoomStateController : MonoBehaviour
    {
        public RoomMode CurrentRoomMode { get; private set; } = RoomMode.MyRoom;
        public UiMode CurrentUiMode { get; private set; } = UiMode.ViewMode;

        public event Action<RoomMode> OnRoomModeChanged;
        public event Action<UiMode> OnUiModeChanged;

        public void SetRoomMode(RoomMode next)
        {
            if (CurrentRoomMode == next)
            {
                return;
            }

            CurrentRoomMode = next;
            OnRoomModeChanged?.Invoke(next);
        }

        public void SetUiMode(UiMode next)
        {
            if (CurrentUiMode == next)
            {
                return;
            }

            CurrentUiMode = next;
            OnUiModeChanged?.Invoke(next);
        }
    }
}

