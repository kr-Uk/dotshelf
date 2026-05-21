namespace App.Bootstrap.Cursors
{
    public static class MouseCursorService
    {
        public static void SetOverride(object owner, MouseCursorKind cursor, int priority = 0)
        {
            GlobalMouseCursorController.SetOverride(owner, cursor, priority);
        }

        public static void ClearOverride(object owner)
        {
            GlobalMouseCursorController.ClearOverride(owner);
        }

        public static void SetLoading(object owner, bool isLoading)
        {
            if (isLoading)
            {
                SetOverride(owner, MouseCursorKind.Loading, 100);
                return;
            }

            ClearOverride(owner);
        }
    }
}
