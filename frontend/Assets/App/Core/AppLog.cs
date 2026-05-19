using System.Diagnostics;

namespace App.Core
{
    public static class AppLog
    {
        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_API")]
        public static void Api(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_AUTH")]
        public static void Auth(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_BOOKSHELF")]
        public static void Bookshelf(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_BOOKSHELF_SYNC")]
        public static void BookshelfSync(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_DASHBOARD")]
        public static void Dashboard(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_DECORATION")]
        public static void Decoration(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_EXPERIENCE")]
        public static void Experience(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_NOTEBOOK")]
        public static void Notebook(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_ONBOARDING")]
        public static void Onboarding(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_ROOM_EDIT")]
        public static void RoomEdit(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_ROOM_THEME")]
        public static void RoomTheme(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_SCENE_INSTALL")]
        public static void SceneInstall(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_SETTINGS")]
        public static void Settings(string message) => UnityEngine.Debug.Log(message);

        [Conditional("DOTSHELF_LOG_ALL")]
        [Conditional("DOTSHELF_LOG_VISUAL_SKIN")]
        public static void VisualSkin(string message) => UnityEngine.Debug.Log(message);
    }
}
