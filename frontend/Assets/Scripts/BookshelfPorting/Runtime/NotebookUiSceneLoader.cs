using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using App.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BookshelfPorting.Runtime
{
    public sealed class NotebookUiSceneLoader : MonoBehaviour
    {
        [SerializeField] private string sceneName = SceneKeys.NotebookUi;
        [SerializeField] private bool unloadOnFocusExit = false;
        [SerializeField] private bool logPerformance = true;

        private readonly List<Action<NotebookScreenController>> pendingCallbacks = new();
        private bool isLoading;
        private bool measuredCanvasActivation;
        private bool loggedRetentionPolicy;

        public bool IsLoading => isLoading;
        public bool IsLoaded
        {
            get
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public bool UnloadOnFocusExit => unloadOnFocusExit;
        public NotebookScreenController Controller { get; private set; }

        public void Configure(string notebookSceneName, bool unloadSceneOnFocusExit, bool enablePerformanceLog)
        {
            if (!string.IsNullOrWhiteSpace(notebookSceneName))
            {
                sceneName = notebookSceneName;
            }

            unloadOnFocusExit = unloadSceneOnFocusExit;
            logPerformance = enablePerformanceLog;
            Controller = FindLoadedController();
        }

        public NotebookScreenController RefreshController()
        {
            Controller = FindLoadedController();
            return Controller;
        }

        public bool OwnsController(NotebookScreenController controller)
        {
            return controller != null &&
                   IsLoaded &&
                   controller.gameObject.scene.IsValid() &&
                   controller.gameObject.scene.name == sceneName;
        }

        public void EnsureLoaded(Action<NotebookScreenController> onLoaded)
        {
            Controller = FindLoadedController();
            if (Controller != null)
            {
                onLoaded?.Invoke(Controller);
                return;
            }

            if (onLoaded != null)
            {
                pendingCallbacks.Add(onLoaded);
            }

            if (!isLoading)
            {
                StartCoroutine(LoadSceneRoutine());
            }
        }

        public void NotifyFocusExited()
        {
            if (!IsLoaded)
            {
                return;
            }

            if (!unloadOnFocusExit)
            {
                if (logPerformance && !loggedRetentionPolicy)
                {
                    loggedRetentionPolicy = true;
                    UnityEngine.Debug.Log($"Notebook UI scene retained after focus exit: {sceneName}. policy=keep-loaded");
                }

                return;
            }

            if (!isLoading)
            {
                StartCoroutine(UnloadSceneRoutine());
            }
        }

        public void MeasureCanvasActivationCost(NotebookScreenController controller)
        {
            if (!logPerformance || measuredCanvasActivation || controller == null)
            {
                return;
            }

            var canvases = controller.GetComponentsInChildren<Canvas>(true);
            if (canvases == null || canvases.Length == 0)
            {
                return;
            }

            measuredCanvasActivation = true;
            var enabledStates = new bool[canvases.Length];
            for (var i = 0; i < canvases.Length; i++)
            {
                enabledStates[i] = canvases[i] != null && canvases[i].enabled;
            }

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    canvases[i].enabled = false;
                }
            }

            for (var i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                {
                    canvases[i].enabled = enabledStates[i];
                }
            }

            stopwatch.Stop();
            UnityEngine.Debug.Log($"Notebook UI canvas enable toggle cost: {stopwatch.Elapsed.TotalMilliseconds:F2} ms ({canvases.Length} canvas).");
        }

        private IEnumerator LoadSceneRoutine()
        {
            isLoading = true;
            var stopwatch = Stopwatch.StartNew();
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    UnityEngine.Debug.LogError($"Failed to lazy-load notebook UI scene: {sceneName}");
                    isLoading = false;
                    FlushCallbacks(null);
                    yield break;
                }

                while (!operation.isDone)
                {
                    yield return null;
                }
            }

            stopwatch.Stop();
            Controller = FindLoadedController();
            if (logPerformance)
            {
                UnityEngine.Debug.Log($"Notebook UI scene lazy-loaded: {sceneName} in {stopwatch.Elapsed.TotalMilliseconds:F2} ms.");
            }

            if (Controller == null)
            {
                UnityEngine.Debug.LogError($"Notebook UI scene loaded but no NotebookScreenController was found: {sceneName}");
            }

            isLoading = false;
            FlushCallbacks(Controller);
        }

        private IEnumerator UnloadSceneRoutine()
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            isLoading = true;
            var stopwatch = Stopwatch.StartNew();
            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
                while (!operation.isDone)
                {
                    yield return null;
                }
            }

            stopwatch.Stop();
            Controller = null;
            measuredCanvasActivation = false;
            isLoading = false;
            if (logPerformance)
            {
                UnityEngine.Debug.Log($"Notebook UI scene unloaded: {sceneName} in {stopwatch.Elapsed.TotalMilliseconds:F2} ms.");
            }
        }

        private void FlushCallbacks(NotebookScreenController controller)
        {
            if (pendingCallbacks.Count == 0)
            {
                return;
            }

            var callbacks = pendingCallbacks.ToArray();
            pendingCallbacks.Clear();
            for (var i = 0; i < callbacks.Length; i++)
            {
                callbacks[i]?.Invoke(controller);
            }
        }

        private NotebookScreenController FindLoadedController()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<NotebookScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers == null)
            {
                return null;
            }

            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller != null &&
                    controller.gameObject.scene.IsValid() &&
                    controller.gameObject.scene.name == sceneName)
                {
                    return controller;
                }
            }

            return null;
        }
    }
}
