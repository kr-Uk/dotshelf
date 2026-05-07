using System;
using System.Collections.Generic;
using System.IO;
using App.Application.Decoration;
using App.Domain.Decoration;
using UnityEngine;

namespace App.Infrastructure.Decoration
{
    // 백엔드 데코레이션 API 가 준비되기 전까지 사용하는 로컬 JSON 영속화.
    // 파일 위치: UnityEngine.Application.persistentDataPath / decorations.json
    //   - Editor: Library/.../persistentDataPath
    //   - Android: /storage/emulated/0/Android/data/{bundle}/files/decorations.json
    //   - Standalone: %appdata%/.../decorations.json 등
    //
    // TODO(server-decoration-api): 백엔드에 GET/PUT /v1/spaces/me/decorations 가 추가되면
    // ApiDecorationRepository 를 별도 구현하고 컴포지션 루트에서 IDecorationRepository 주입을
    // LocalFile* → Api* 로 교체. 그 시점:
    //   - DecorationLayoutItem 그대로 DTO 로 사용 가능 ([Serializable] + public fields).
    //   - 또는 DecorationLayoutDto 를 별도 정의해서 DecorationDtoMapper 로 변환.
    //   - 사용자 단말 로컬 파일은 마이그레이션 도구로 1회 서버 동기화 후 폐기.
    public sealed class LocalFileDecorationRepository : IDecorationRepository
    {
        // JsonUtility 가 collection 을 root 로 직렬화 못 하므로 wrapper 사용.
        [Serializable]
        private sealed class SerializableLayout
        {
            public List<DecorationLayoutItem> items = new List<DecorationLayoutItem>();
        }

        private readonly string filePath;

        public LocalFileDecorationRepository(string fileName = "decorations.json")
        {
            // `using App.Application.Decoration` 가 있어서 `Application` 단독 식별자가 그 네임스페이스로
            // 해석되어 CS0234 발생 → UnityEngine.Application 으로 fully-qualified.
            filePath = Path.Combine(UnityEngine.Application.persistentDataPath, fileName);
        }

        public string FilePath => filePath;

        public void Save(IReadOnlyList<DecorationLayoutItem> items, Action onSuccess, Action<string> onError)
        {
            try
            {
                var payload = new SerializableLayout { items = new List<DecorationLayoutItem>(items ?? Array.Empty<DecorationLayoutItem>()) };
                var json = JsonUtility.ToJson(payload, prettyPrint: true);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, json);
                Debug.Log($"[LocalFileDecorationRepo] saved {payload.items.Count} items → {filePath}");
                onSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalFileDecorationRepo] save failed: {ex.GetType().Name} {ex.Message}");
                onError?.Invoke($"{ex.GetType().Name}: {ex.Message}");
            }
        }

        public void Load(Action<IReadOnlyList<DecorationLayoutItem>> onSuccess, Action<string> onError)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.Log($"[LocalFileDecorationRepo] no saved file at {filePath} — empty list.");
                    onSuccess?.Invoke(new List<DecorationLayoutItem>());
                    return;
                }
                var json = File.ReadAllText(filePath);
                var payload = JsonUtility.FromJson<SerializableLayout>(json);
                var items = payload?.items ?? new List<DecorationLayoutItem>();
                Debug.Log($"[LocalFileDecorationRepo] loaded {items.Count} items ← {filePath}");
                onSuccess?.Invoke(items);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalFileDecorationRepo] load failed: {ex.GetType().Name} {ex.Message}");
                onError?.Invoke($"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
