using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace App.Core
{
    /// <summary>
    /// URL to Texture2D LRU cache with optional download coroutine.
    /// MonoBehaviour-free: callers run <see cref="Load"/> via StartCoroutine,
    /// or use <see cref="TryGet"/>/<see cref="Put"/> for cache-only access
    /// with their own download logic.
    /// </summary>
    public sealed class RemoteImageLoader : IDisposable
    {
        public const int DefaultMaxCacheSize = 64;
        public const int DefaultTimeoutSeconds = 8;

        private readonly int _maxCacheSize;
        private readonly int _timeoutSeconds;

        private readonly Dictionary<string, Texture2D> _cache = new();
        private readonly LinkedList<string> _lruOrder = new();
        private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new();

        public RemoteImageLoader(int maxCacheSize = DefaultMaxCacheSize, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            _maxCacheSize = Mathf.Max(1, maxCacheSize);
            _timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }

        /// <summary>Timeout in seconds used by <see cref="Load"/>.</summary>
        public int TimeoutSeconds => _timeoutSeconds;

        /// <summary>
        /// Try to get a cached texture. Touches LRU on hit.
        /// </summary>
        public bool TryGet(string url, out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!_cache.TryGetValue(url, out texture) || texture == null)
            {
                RemoveEntry(url);
                texture = null;
                return false;
            }

            TouchLru(url);
            return true;
        }

        /// <summary>
        /// Download a texture from <paramref name="url"/>, cache it, then invoke
        /// <paramref name="onLoaded"/>. Run with <c>StartCoroutine</c>.
        /// If the texture is already cached, the callback fires immediately.
        /// NOTE: callers in Assembly-CSharp should use their own coroutine
        /// with <see cref="TryGet"/>/<see cref="Put"/> if cross-asmdef
        /// IEnumerator issues arise.
        /// </summary>
        public IEnumerator Load(string url, Action<Texture2D> onLoaded)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                yield break;
            }

            if (TryGet(url, out var cached))
            {
                onLoaded?.Invoke(cached);
                yield break;
            }

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = _timeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    yield break;
                }

                var canonical = Put(url, texture);
                onLoaded?.Invoke(canonical);
            }
        }

        /// <summary>
        /// Insert a texture into the cache and return the canonical (cached) instance.
        /// If the same URL is already cached, the <paramref name="texture"/> passed in
        /// is destroyed and the existing cached texture is returned instead.
        /// This prevents race conditions where concurrent downloads for the same URL
        /// could destroy a texture still referenced by UI elements.
        /// </summary>
        /// <returns>The texture that remains in the cache — callers must use this reference.</returns>
        public Texture2D Put(string url, Texture2D texture)
        {
            if (string.IsNullOrWhiteSpace(url) || texture == null)
            {
                return texture;
            }

            // If a texture for this URL already exists, keep the existing one
            // and destroy the duplicate. This matches NotebookToolkitController's
            // CacheBookCoverTexture pattern and prevents destroying textures
            // that are still referenced by StyleBackground in UI elements.
            if (_cache.TryGetValue(url, out var existing) && existing != null)
            {
                if (existing != texture)
                {
                    UnityEngine.Object.Destroy(texture);
                }
                TouchLru(url);
                return existing;
            }

            _cache[url] = texture;

            if (_lruNodes.TryGetValue(url, out var node))
            {
                _lruOrder.Remove(node);
                _lruOrder.AddLast(node);
            }
            else
            {
                node = _lruOrder.AddLast(url);
                _lruNodes[url] = node;
            }

            Trim();
            return texture;
        }

        /// <summary>Remove a single entry and destroy its texture.</summary>
        public void Remove(string url)
        {
            RemoveEntry(url);
        }

        /// <summary>Number of currently cached textures.</summary>
        public int Count => _cache.Count;

        /// <summary>Clear all cached textures and destroy them.</summary>
        public void Dispose()
        {
            foreach (var texture in _cache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            _cache.Clear();
            _lruOrder.Clear();
            _lruNodes.Clear();
        }

        private void TouchLru(string url)
        {
            if (_lruNodes.TryGetValue(url, out var node))
            {
                _lruOrder.Remove(node);
                _lruOrder.AddLast(node);
            }
        }

        private void Trim()
        {
            while (_cache.Count > _maxCacheSize && _lruOrder.First != null)
            {
                RemoveEntry(_lruOrder.First.Value);
            }
        }

        private void RemoveEntry(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            if (_cache.TryGetValue(url, out var texture) && texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }

            _cache.Remove(url);

            if (_lruNodes.TryGetValue(url, out var node))
            {
                _lruOrder.Remove(node);
                _lruNodes.Remove(url);
            }
        }
    }
}
