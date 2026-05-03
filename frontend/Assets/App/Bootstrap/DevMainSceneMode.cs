#if UNITY_EDITOR
namespace App.Bootstrap
{
    // Process-level flag set when LoginScreenController's dev shortcut button
    // bypasses login. Editor-only — the type and every call site are guarded
    // by UNITY_EDITOR so player builds neither contain nor reference it.
    //
    // The flag is a plain static field, NOT EditorPrefs, so it resets
    // automatically when the play session ends. That avoids the trap where a
    // forgotten toggle bypasses real-login QA in a later play session — the
    // same trap that took out the previous EditorPrefs-based bypass.
    //
    // Two consumers in the runtime layer (also UNITY_EDITOR-gated where they
    // read this flag):
    //   - BookshelfApiSyncController.RefreshFromServer: on /v1/spaces/me
    //     401 / 403, switches the generator to local catalog mode and
    //     rebuilds, instead of leaving an empty bookshelf.
    //   - (future) any other authenticated call that wants a dev-only soft
    //     fallback while BE auth is being wired up.
    //
    // Real-user flow safety: even if this flag stays set across LoginScene
    // round-trips (guest entry → login → main), guest mode short-circuits
    // RefreshFromServer before reaching the fallback check, and a successful
    // social login ships a Bearer token so /v1/spaces/me does not 401 in
    // the first place. The flag only changes behaviour when the API actually
    // returns an auth failure, which is the only case that would have
    // bricked the main scene anyway.
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
