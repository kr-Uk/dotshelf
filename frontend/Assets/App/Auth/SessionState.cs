using UnityEngine;

namespace App.Auth
{
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

        private static bool isInitialized;
        private static bool isGuest;

        public static bool IsGuest
        {
            get
            {
                EnsureLoaded();
                return isGuest;
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

        public static void Clear()
        {
            isGuest = false;
            isInitialized = true;
            PlayerPrefs.DeleteKey(GuestPrefKey);
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
        }
    }
}
