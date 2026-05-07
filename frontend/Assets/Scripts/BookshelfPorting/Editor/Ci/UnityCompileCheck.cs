using UnityEditor;
using UnityEngine;

namespace BookshelfPorting.Editor.Ci
{
    public static class UnityCompileCheck
    {
        public static void Run()
        {
            Debug.Log("Unity compile check passed.");
            EditorApplication.Exit(0);
        }
    }
}
