using App.Auth;
using App.Config;
using UnityEngine;

namespace App.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        [SerializeField] private AppApiConfig apiConfig;

        public AppApiConfig ApiConfig => apiConfig;

        private void Awake()
        {
            // BootstrapScene owns global config/session services while MainRoomScene is loaded additively.
            CleanupDeprecatedGuestSession();
        }

        // One-time cleanup for users upgrading from the guest-flow era (MR #88
        // removed guest entry entirely). Two concerns:
        //   1. PlayerPrefs keys ("Dotshelf.Auth.GuestSessionId",
        //      "Dotshelf.Session.IsGuest", "Dotshelf.Session.GuestPostLoginIntent")
        //      stay on device after their constants were deleted — strip them
        //      so future key namespaces cannot surface stale values.
        //   2. A previously-issued guest token was saved with userId=0 +
        //      non-empty accessToken; that combination cannot pass BE
        //      userService.requireUserId(), so every authenticated request
        //      would 401. Clear the auth session so the user is routed to
        //      LoginScene as a fresh anonymous client.
        // Safe to remove this block 1-2 release cycles after MR #88 ships.
        private static void CleanupDeprecatedGuestSession()
        {
            PlayerPrefs.DeleteKey("Dotshelf.Auth.GuestSessionId");
            PlayerPrefs.DeleteKey("Dotshelf.Session.IsGuest");
            PlayerPrefs.DeleteKey("Dotshelf.Session.GuestPostLoginIntent");

            if (AuthSession.HasAccessToken && AuthSession.UserId == 0L)
            {
                AuthSession.Clear();
            }

            PlayerPrefs.Save();
        }
    }
}
