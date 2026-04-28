using UnityEngine;

namespace App.Bootstrap
{
    // Marks a runtime-placed decoration as editable (selectable, rotatable, deletable)
    // by RoomEditModeController. Core scene objects (bookshelf / notebook / whiteboard /
    // hamster) intentionally do not carry this component, so they remain untouched
    // during edit mode interactions.
    [DisallowMultipleComponent]
    public sealed class DecorationItemMarker : MonoBehaviour
    {
    }
}
