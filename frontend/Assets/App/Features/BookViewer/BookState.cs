namespace App.Features.BookViewer
{
    public enum BookState
    {
        Idle,
        Hover,
        Focused,
        Pressed,
        Dragging,
        Preview,
        CrossShelfPending,
        CrossShelfTransition,
        Dropping,
        Returning
    }
}

