using System;
using System.Collections.Generic;
using System.IO;
using App.Application.Decoration;
using App.Domain.Decoration;
using UnityEngine;

namespace App.Infrastructure.Decoration
{
    // 백엔드 데코레이션 API 가 준비되기 전까지 사용하는 로컬 JSON 영속화.
    // 파일 위치: UnityEngine.Application.persistentDataPath / decorations_{userId}.json
    //   - Editor: Library/.../persistentDataPath
    //   - Android: /storage/emulated/0/Android/data/{bundle}/files/decorations_{userId}.json
    //   - Standalone: %appdata%/.../decorations_{userId}.json 등
    //
    // userId 가 양수면 파일명에 포함해 user/space scope 를 분리한다 — MR #120 skh1205 리뷰 #2 반영.
    // 같은 기기에 다른 계정으로 로그인해도 layout 이 섞이지 않는다. userId 가 0(미로그인) 인 경우는
    // 계정 식별이 없으므로 anon 파일명("decorations_anon.json")로 fallback — 정상 운영 흐름에서는
    // 진입 자체가 막혀야 하지만, dev 환경 / unit test 등에서 호출돼도 NRE 안 나도록 방어.
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

        // userId 0 이하 = 미로그인. 호출자가 AuthSession.UserId 를 그대로 넘겨도 되도록 long 타입.
        public LocalFileDecorationRepository(long userId)
        {
            // `using App.Application.Decoration` 가 있어서 `Application` 단독 식별자가 그 네임스페이스로
            // 해석되어 CS0234 발생 → UnityEngine.Application 으로 fully-qualified.
            var fileName = userId > 0 ? $"decorations_{userId}.json" : "decorations_anon.json";
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
                App.Core.AppLog.Decoration($"[LocalFileDecorationRepo] saved {payload.items.Count} items → {filePath}");
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
                    App.Core.AppLog.Decoration($"[LocalFileDecorationRepo] no saved file at {filePath} — empty list.");
                    onSuccess?.Invoke(new List<DecorationLayoutItem>());
                    return;
                }
                var json = File.ReadAllText(filePath);
                var payload = JsonUtility.FromJson<SerializableLayout>(json);
                var items = payload?.items ?? new List<DecorationLayoutItem>();
                App.Core.AppLog.Decoration($"[LocalFileDecorationRepo] loaded {items.Count} items ← {filePath}");
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
