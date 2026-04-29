using UnityEngine;

namespace App.Auth
{
    // What the guest player asked for when they tapped one of the
    // post-login options on the guest data modal. Persisted alongside
    // SessionState.IsGuest so the post-login migration / discard flow can
    // branch on the player's intent even after the guest flag itself is
    // cleared by a successful login.
    public enum GuestPostLoginIntent
    {
        None = 0,
        // "기존 데이터 이어서 시작하기" — keep the local guest snapshot and
        // upload it to the freshly linked account.
        MigrateGuestData = 1,
        // "새 공간으로 시작하기" — discard the local guest snapshot, start
        // the linked account on a fresh empty room.
        DiscardAndStartFresh = 2,
    }

    // Tracks whether the current session was started as a guest or as a logged-in
    // user. State is mirrored to PlayerPrefs so a freshly launched app remembers
    // the previous mode and the main-scene chrome (e.g. the guest badge button)
    // can show without waiting on the login flow to set the flag again.
    //
    // The PlayerPrefs layer is a temporary local-only persistence; the design
    // keeps reads/writes funneled through this single class so the storage can
    // later be swapped for a server-issued session token without touching
    // call sites.
    public static class SessionState
    {
        private const string GuestPrefKey = "Dotshelf.Session.IsGuest";
        private const string PendingIntentPrefKey = "Dotshelf.Session.GuestPostLoginIntent";

        private static bool isInitialized;
        private static bool isGuest;
        private static GuestPostLoginIntent pendingIntent = GuestPostLoginIntent.None;

        public static bool IsGuest
        {
            get
            {
                EnsureLoaded();
                return isGuest;
            }
        }

        // The player's choice on the guest data modal, surfaced through the
        // login flow so the post-login data-migration handler can branch on
        // it. Cleared explicitly via ConsumePendingIntent once handled, so
        // a stale intent doesn't leak into a later session.
        public static GuestPostLoginIntent PendingIntent
        {
            get
            {
                EnsureLoaded();
                return pendingIntent;
            }
        }

        public static void MarkGuest()
        {
            EnsureLoaded();
            isGuest = true;
            PlayerPrefs.SetInt(GuestPrefKey, 1);
            PlayerPrefs.Save();
        }

        public static void MarkLoggedIn()
        {
            EnsureLoaded();
            isGuest = false;
            PlayerPrefs.SetInt(GuestPrefKey, 0);
            PlayerPrefs.Save();
        }

        // Record the guest's post-login intent (continue with existing data
        // vs start fresh). Stored separately from IsGuest so a subsequent
        // login that flips IsGuest off doesn't lose the choice — the
        // migration handler reads PendingIntent after MarkLoggedIn runs.
        public static void SetPendingIntent(GuestPostLoginIntent intent)
        {
            EnsureLoaded();
            pendingIntent = intent;
            PlayerPrefs.SetInt(PendingIntentPrefKey, (int)intent);
            PlayerPrefs.Save();
        }

        // Read-and-clear: returns the stored intent and resets it to None,
        // so each intent is acted on at most once per migration attempt.
        public static GuestPostLoginIntent ConsumePendingIntent()
        {
            EnsureLoaded();
            var current = pendingIntent;
            if (current != GuestPostLoginIntent.None)
            {
                pendingIntent = GuestPostLoginIntent.None;
                PlayerPrefs.SetInt(PendingIntentPrefKey, (int)GuestPostLoginIntent.None);
                PlayerPrefs.Save();
            }
            return current;
        }

        public static void Clear()
        {
            // Clearing the cache forces the next access to re-read PlayerPrefs.
            // (If something external changed PlayerPrefs we want the freshest
            // value, not the cached one.)
            isGuest = false;
            pendingIntent = GuestPostLoginIntent.None;
            isInitialized = false;
            PlayerPrefs.DeleteKey(GuestPrefKey);
            PlayerPrefs.DeleteKey(PendingIntentPrefKey);
            PlayerPrefs.Save();
        }

        private static void EnsureLoaded()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            isGuest = PlayerPrefs.GetInt(GuestPrefKey, 0) == 1;
            var rawIntent = PlayerPrefs.GetInt(PendingIntentPrefKey, (int)GuestPostLoginIntent.None);
            pendingIntent = rawIntent switch
            {
                (int)GuestPostLoginIntent.MigrateGuestData => GuestPostLoginIntent.MigrateGuestData,
                (int)GuestPostLoginIntent.DiscardAndStartFresh => GuestPostLoginIntent.DiscardAndStartFresh,
                _ => GuestPostLoginIntent.None,
            };
        }
    }
}
