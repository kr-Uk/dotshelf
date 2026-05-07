using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace BookshelfPorting.Editor.Ci
{
    public static class UnityCompileCheck
    {
        public static void Run()
        {
            var errors = new List<CompilerMessage>();
            var assemblies = CompilationPipeline.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var messages = assemblies[i].compilerMessages;
                if (messages == null)
                {
                    continue;
                }

                for (var j = 0; j < messages.Length; j++)
                {
                    if (messages[j].type == CompilerMessageType.Error)
                    {
                        errors.Add(messages[j]);
                    }
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log("Unity compile check passed.");
                EditorApplication.Exit(0);
                return;
            }

            for (var i = 0; i < errors.Count; i++)
            {
                Debug.LogError($"{errors[i].file}:{errors[i].line} {errors[i].message}");
            }

            EditorApplication.Exit(1);
        }
    }
}
