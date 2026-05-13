using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // 테마 spec → 실제 spawn 가능한 GameObject 변환. 현재 MVP 는 spec.Prefab 그대로 반환
    // (LocalThemeAssetLoader). 추후 S3/CDN 도입 시 RemoteThemeAssetLoader 가 같은 인터페이스
    // 로 합류 — assetKey 로 캐시 hit 확인 후 modelUrl 다운로드 + GLTFast 등으로 런타임 로드.
    //
    // 호출부 (RoomEditModeController.SpawnTheme*DecoFromSpec) 는 이 인터페이스만 의존 →
    // 로컬/원격 전환 시 spawn 코드 변경 없음.
    public interface IThemeAssetLoader
    {
        // 동기 즉시 로드. 로컬 prefab 전용. 원격 도입 시 별도 async 메서드 (TryLoadAsync)
        // 가 같은 인터페이스에 추가될 수 있음 — 그 시점에 spawn 경로 async 화.
        GameObject Resolve(RoomThemeDecoSpec spec);
    }

    public sealed class LocalThemeAssetLoader : IThemeAssetLoader
    {
        public GameObject Resolve(RoomThemeDecoSpec spec)
        {
            if (spec == null) return null;
            // spec.Prefab 우선 — Inspector 에서 직접 wire 된 reference. assetKey 는 식별/저장
            // 용도일 뿐 로컬 로드에는 영향 없음. 미래 RemoteThemeAssetLoader 는 assetKey 가
            // 1차 lookup 키가 된다.
            return spec.Prefab;
        }
    }
}
