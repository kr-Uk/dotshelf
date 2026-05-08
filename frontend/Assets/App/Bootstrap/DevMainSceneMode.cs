#if UNITY_EDITOR
namespace App.Bootstrap
{
    // Process-level flag set when LoginScreenController's dev shortcut button
    // bypasses login. Editor-only: the type and every call site are guarded
    // by UNITY_EDITOR so player builds neither contain nor reference it.
    //
    // The flag is a plain static field, NOT EditorPrefs, so it resets
    // automatically when the play session ends. That avoids stale local
    // toggles changing later real-login QA sessions.
    //
    // Runtime consumers may use this as an editor-only "dev shortcut" marker,
    // but RealApi bookshelf/book flows must still use the backend as source of
    // truth. Do not switch the bookshelf generator back to local catalog data
    // just because this flag is active.
    public static class DevMainSceneMode
    {
        public static bool IsActive { get; private set; }

        public static void Activate()
        {
            IsActive = true;
        }

        public static void Deactivate()
        {
            IsActive = false;
        }
    }
}
#endif
