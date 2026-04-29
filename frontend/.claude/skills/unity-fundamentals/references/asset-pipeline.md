# Asset Pipeline

> **한 줄 요약**: Unity의 자산 로딩은 직접 참조(인스펙터), Resources 폴더, AssetBundles, Addressables의 4가지가 있고 권장은 **직접 참조 + Addressables**다. Resources는 초기 로딩과 빌드 사이즈에서 강한 페널티가 있다. 모바일(Android) 텍스처는 **ASTC**가 표준이고, AssetPostprocessor로 임포트 설정을 강제하지 않으면 사람이 매번 다른 설정으로 들고 들어온다.

## 어떤 결정이 이 글로 풀리는가

- **"Resources.Load 써도 되나? 간편하던데"** → §Resources의 진짜 비용
- **"Addressables 도입할 시점이 언제인가? 지금은 오버킬?"** → §언제 Addressables를 도입하나
- **"Meshy로 들어온 glb 자산을 매번 같은 설정으로 임포트하려면?"** → §AssetPostprocessor
- **"빌드 사이즈가 갑자기 커졌다"** → §함정 카탈로그 (Resources 누적, 텍스처 미압축)
- **"Android 텍스처 압축은 뭘 골라야 하나?"** → §모바일 텍스처 압축

## 메커니즘

### 1. 자산 참조의 4가지 방식

| 방식 | 빌드 포함 시점 | 메모리 로드 시점 | 권장 |
|---|---|---|---|
| **직접 참조** (인스펙터에서 GameObject/Prefab/SO에 끌어다 놓음) | 참조한 씬·prefab이 빌드에 포함될 때 | 그 씬·prefab이 로드될 때 | ✅ 가장 단순한 케이스의 기본 |
| **Resources 폴더** (`Resources.Load("path")`) | `Resources/` 안의 모든 파일이 무조건 빌드 포함 | `Load` 호출 시점 (단, 시스템 초기화는 startup 시 전체) | ❌ 프로토타입 외 비추 |
| **AssetBundles** (수동 번들링·로드) | 별도 번들 파일로 빌드 산출 | `AssetBundle.LoadFromFile`/CDN 로드 | ⚠ 직접 다루지 말고 Addressables 위에서 사용 |
| **Addressables** (`AddressableAssets` 패키지) | Addressables Group 설정에 따라 (로컬/원격) | 비동기 `LoadAssetAsync<T>` | ✅ 라이브 콘텐츠/대규모/원격 |

### 2. Resources의 진짜 비용

매뉴얼 인용:
- *"Unity combines all objects referenced from assets in the Resources folder into a single serialized file"* — Resources 폴더의 *모든* 자산이 하나의 거대한 인덱스로 묶여 빌드에 들어간다.
- *"makes memory management more difficult"*, *"slows down application startup and the length of builds"*
- *"initializing a Resources system containing 10 thousand assets takes several seconds on low-end mobile devices"*

→ Resources 폴더에 자산이 늘면:
1. **빌드 사이즈가 누적적으로 커짐** — 한 번도 호출되지 않는 자산도 무조건 들어감
2. **앱 시작 시간이 길어짐** — startup에서 전체 인덱스를 만듦
3. **메모리 관리가 어려움** — 한 번 로드한 자산을 명시 unload하지 않으면 누적

권장: 프로토타입에선 OK, **프로덕션 진입 전에 모두 직접 참조 또는 Addressables로 마이그레이션**.

### 3. Addressables의 의미

Unity 공식 권장은 *"convert all Scene data and Resource folders to the Addressables build system"*. 핵심 이득:

- **on-demand 로드** — 비동기로 필요할 때만, 메모리 점유 시간 최소화
- **원격 콘텐츠 업데이트** — CDN에 자산을 두고 앱 패치 없이 갱신
- **위치 추상화** — 코드는 "어떤 키"만 알면 됨, "그 자산이 어디에 있는지"는 그룹 설정이 결정
- **참조 카운팅 자동** — `Release()` 짝만 맞추면 메모리 reclaim

비용:
- 학습 곡선 (Group, Schema, Profile 개념)
- 빌드 단계가 한 단계 더 필요 (Build Player Content)
- 비동기 API라 코드가 약간 더 복잡 (`async`/`UniTask`/`coroutine`)

### 4. 언제 Addressables를 도입하나

| 상황 | 권장 |
|---|---|
| 프로토타입, 자산 < 100개, 단일 빌드 배포 | 직접 참조로 충분 |
| 자산 수백 개, 메모리/빌드 사이즈 압박 시작 | Addressables 도입 시작 |
| CDN 배포, 라이브 콘텐츠 갱신 필요 | Addressables 필수 |
| 책 표지/3D 모델/사용자 업로드 콘텐츠 | Addressables 또는 Cloud Content Delivery |

dotshelf의 경우 Meshy 모델·책 표지처럼 **콘텐츠가 늘어날 가능성이 큰 자산**은 Addressables 후보. 단 도입 시점은 자산 수가 100~200개 넘기 시작할 때가 적정.

### 5. AssetPostprocessor — 임포트 설정 강제

`AssetPostprocessor`는 자산이 임포트될 때 (또는 다시 임포트될 때) 자동으로 호출되는 Editor 스크립트다. 핵심 활용:

- **텍스처**: 모바일 ASTC 압축 자동 적용, max size 제한
- **메시**: glb 임포트 시 LOD/normals/scale 일관성
- **오디오**: 모바일 비트레이트, 스트리밍 vs 디코드 압축

```csharp
// Assets/App/Editor/AutoTextureImporter.cs (예시)
using UnityEditor;
using UnityEngine;

class AutoTextureImporter : AssetPostprocessor {
    void OnPreprocessTexture() {
        var importer = (TextureImporter)assetImporter;
        // 새 텍스처가 처음 들어올 때만 (사용자 변경 보존)
        if (importer.importSettingsMissing) {
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            // 안드로이드 ASTC 6x6
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(android);
        }
    }
}
```

**중요**: `importSettingsMissing` 으로 기존 자산의 사용자 설정을 덮어쓰지 않게. 강제로 모두 통일하고 싶을 때만 그 가드를 빼고, 그건 사용자 컨펌 후.

### 6. 모바일 텍스처 압축 — ASTC

ARM 공식 + Unity Discussions 종합:

- **ASTC (Adaptive Scalable Texture Compression)**: 블록 사이즈 (4x4 ~ 12x12) 로 비트레이트 조절
- iOS와 모던 Android(OpenGL ES 3.2 / Vulkan / OpenGL ES 3.0+ASTC 확장 지원 기기) 표준
- **블록 사이즈 가이드** (작을수록 고품질·큰 사이즈):
  - `4x4`: 8 bpp — 디테일 중요한 UI 아이콘
  - `6x6`: 3.56 bpp — 일반 텍스처 (균형점, **권장 기본값**)
  - `8x8`: 2 bpp — 큰 배경, 디테일 적은 텍스처
- **fallback**: 미지원 기기에서 Unity는 자동으로 ETC2(OpenGL ES 3.0)로 떨어뜨릴 수 있게 빌드 설정 가능. 또는 *Override For Android*로 명시.

함정: 매뉴얼·블로그가 강조한 것 — **임포트 시 압축 안 한 텍스처는 런타임에 압축 해제 사본을 추가로 만든다.** 즉 메모리 2배. 압축 형식이 기기에서 미지원이면 같은 일이 일어남. 항상 명시 압축을 강제해야 함.

### 7. 메시·모델 임포트

glb / fbx 임포트 시 주의:
- **Read/Write Enabled** 끄기 (런타임에 메시 데이터 안 만질 거면) — 메모리 2배 절감
- **Mesh Compression**: Off / Low / Medium / High. 모바일은 Medium 권장 (압축률 vs 정밀도 균형)
- **Normals**: Calculate / Import 명시. 자동 계산은 일관성 떨어질 수 있음
- **Scale Factor**: glb는 보통 1m=1unit. Meshy 모델은 임포트 후 검증 필요

### 8. 오디오

- **Compression Format**: 모바일 Vorbis 또는 ADPCM
- **Load Type**: Streaming (배경 음악, 큼), Compressed In Memory (효과음, 짧음), Decompress on Load (메모리 ↑ CPU ↓)
- 짧은 효과음을 Streaming으로 두면 디스크 I/O 트리거 → 끊김

## 이 프로젝트와의 연결

### 현재 상태

`frontend/Assets/`에 다음이 보임:
- `Meshy_AI_Hamham_0416082404_texture.glb` — Meshy로 만든 외부 모델 (직접 참조 패턴)
- `Resources/` 폴더 존재 — 안에 무엇이 있는지 감사 필요
- `QA_Books/`, `Arts/` — 자산 분류 폴더

### 권장 액션 (우선순위순)

1. **`Resources/` 감사** — 들어 있는 자산이 정말 startup 인덱싱이 필요한가? 단순 prefab/SO면 직접 참조로 옮기는 게 나음. 사용자 컨펌 후 마이그레이션.
2. **AssetPostprocessor 추가** — 새 텍스처/메시가 들어올 때 모바일 압축·max size를 자동 적용. 사람이 매번 다른 설정으로 들고 오는 회귀 방지.
3. **Meshy 자산 임포트 후처리** — Read/Write Enabled, Mesh Compression, Scale Factor를 자동 적용. 외부에서 업데이트되어 들어올 때마다 일관성 보장.
4. **Addressables 도입은 자산 100개 넘기 시작할 때** — 지금은 직접 참조로 충분. 단 책 표지 같은 가변 콘텐츠가 보일 때 미리 설계 고민.

## 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| 빌드 사이즈가 의도보다 큼 | Resources에 안 쓰는 자산까지 들어감 | Resources 감사 후 마이그레이션 |
| 앱 시작이 점점 느려짐 | Resources 인덱싱이 자산 수에 비례 | Resources에서 자산 제거. Addressables 또는 직접 참조 |
| 같은 텍스처가 메모리에 두 벌 | 압축 형식 미지원 → 런타임 decompressed 사본 생성 | 임포트 시 명시 압축 (ASTC), `SystemInfo.SupportsTextureFormat`로 fallback 제어 |
| 새로 들어온 텍스처마다 사람이 다른 max size로 임포트 | AssetPostprocessor 부재 | `OnPreprocessTexture`로 강제 |
| Resources.UnloadUnusedAssets() 호출 후에도 메모리 안 빠짐 | 어딘가 정적 참조 유지 중 | Memory Profiler로 root reference 추적. Addressables의 reference counting이 더 명확 |
| Mesh.vertices에 접근하니 NRE | Read/Write Enabled 꺼짐 | 런타임 접근이 진짜 필요한가 확인. 진짜면 Read/Write 켜고 메모리 비용 인정 |
| Meshy glb scale이 200m로 들어옴 | Meshy export 단위 mismatch | Importer Scale Factor 또는 AssetPostprocessor에서 자동 보정 |
| 빌드 결과 Android 기기에서 텍스처가 깨져 보임 | ASTC 미지원 기기 + fallback 미설정 | 빌드 설정에서 ETC2 fallback 활성화 |

## 출처

- Unity Manual — [Introduction to the Resources system](https://docs.unity3d.com/6000.2/Documentation/Manual/LoadingResourcesatRuntime.html)
- Unity Manual — [Assets, Resources and AssetBundles (Learn)](https://learn.unity.com/tutorial/assets-resources-and-assetbundles)
- Unity Blog — [Addressables: Planning and best practices](https://unity.com/blog/engine-platform/addressables-planning-and-best-practices)
- Unity Manual / Addressables — [Asset dependencies overview](https://docs.unity3d.com/Packages/com.unity.addressables@2.0/manual/AssetDependencies.html)
- ARM Developer — [Unity and ASTC](https://developer.arm.com/documentation/102162/latest/Unity-and-ASTC)
- Android Developers — [Target texture compression formats in Android App Bundles](https://developer.android.com/guide/playcore/asset-delivery/texture-compression)
- Unity Discussions — [ASTC fallback on Android devices](https://discussions.unity.com/t/astc-fallback-on-android-devices/943576)
