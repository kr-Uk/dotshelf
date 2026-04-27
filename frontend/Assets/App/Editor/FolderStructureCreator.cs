using System.IO;
using UnityEditor;
using UnityEngine;

namespace App.Editor
{
    public static class FolderStructureCreator
    {
        private static readonly string[] Folders =
        {
            "Assets/App/Bootstrap",
            "Assets/App/Core",
            "Assets/App/Domain",
            "Assets/App/Application",
            "Assets/App/Infrastructure",
            "Assets/App/Features",
            "Assets/App/Platform",
            "Assets/App/UI",
            "Assets/App/Config",
            "Assets/App/Tests",
            "Assets/App/Editor",
            "Assets/Art",
            "Assets/Scenes"
        };

        [MenuItem("Tools/Project Setup/Create Folder Structure")]
        public static void CreateFolderStructure()
        {
            foreach (var folder in Folders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                Directory.CreateDirectory(folder);
            }

            AssetDatabase.Refresh();
            Debug.Log("Project folder structure is ready.");
        }
    }
}

