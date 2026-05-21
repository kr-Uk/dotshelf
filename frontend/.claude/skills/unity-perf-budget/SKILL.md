---
name: unity-perf-budget
description: Use this skill whenever the user is making a change in the dotshelf Unity project that has performance implications on mobile (Android primary, iOS secondary) — adding new meshes, materials, lights, post-processing effects, real-time shadows, particles, UI Toolkit panels, or anything that affects draw calls, fill rate, GC, or texture memory. Trigger this proactively when the user adds 3D content (Meshy import, new Prefab in scenes), creates a new shader/material, adjusts URP Asset settings, changes Quality Settings, or asks "is this fast enough", "will this run on low-end Android", "the editor is fine but my phone stutters". The skill enforces a per-tier (low/mid/high) frame budget — draw calls, SetPass, vertices, real-time lights, shadow distance, MSAA, post-processing — and reports budget consumption alongside any rendering-affecting change. Don't treat it as a one-time audit; apply it on every PR that touches rendering, scenes, prefabs, or the URP/Quality assets.
---

# unity-perf-budget

dotshelf의 모바일 빌드(Android primary)가 의도한 디바이스 등급에서 frame budget을 지키게 강제하는 스킬. 새 자산·머티리얼·라이트·UI 변경마다 **예산 차감**을 추적하고, 등급별 권장 URP/Quality 설정에서 벗어나는 변경을 사전에 잡는다. 깊은 메커니즘은 `unity-fundamentals/references/` (특히 `asset-pipeline.md`, `build-il2cpp.md`)에 두고, 이 스킬은 **예산 + 권장 설정표 + 측정 절차**에 집중한다.

## 왜 이 스킬이 필요한가

- 모바일은 GPU 메모리 대역폭, 발열, 배터리가 한 묶음으로 묶여 있어 PC 기준 "괜찮음"이 모바일 기준 "끊김"으로 흔히 바뀐다.
- 에디터 Profiler는 PC 머신 기준이라 **모바일 끊김을 가린다**. "에디터 60fps → 실기 25fps"는 거의 항상 발견이 늦다.
- 예산 없이 자산을 더하면 한 PR 단위로는 보이지 않다가 누적되어 **3개월 뒤 갑자기 끊긴다**. 변경마다 예산 차감을 명시하면 누적이 보인다.
- 이미 잡힌 설계 결정(슬롯 기반 배치, UI Toolkit, URP)은 모바일 친화적이지만, **자산이 늘어날 때마다** 그 이득이 깎인다.

## 언제 발동하나

자동 적용 — 다음 중 하나라도 해당하면 변경 보고에 §예산 블록을 포함한다.

- `Assets/Arts/`, `Assets/QA_Books/`, `Assets/App/Features/**/Resources/` 등에 새 메시·텍스처·머티리얼·prefab 추가
- 새 셰이더/머티리얼 도입 (특히 SRP Batcher 비호환 가능성 있는 셰이더)
- `Assets/Settings/` 의 **URP Asset** (Renderer Asset 포함) 또는 **QualitySettings** 변경
- 씬에 라이트(특히 Realtime, Mixed) 추가, 그림자 캐스터 토글
- Post-processing volume 도입/변경
- UI Toolkit panel 추가 (UI는 별도 draw call 묶음을 만듦)
- 사용자가 "왜 끊김?", "이거 모바일에서 괜찮을까?", "프레임 떨어진다" 같은 신호

## 디바이스 등급 정의

dotshelf 타깃 디바이스를 3개 등급으로 못 박는다 (사용자가 다른 타깃을 명시하면 그 정의로 갱신):

| 등급 | 대표 기기 | 칩셋 예 | 권장 fps | Frame budget |
|---|---|---|---|---|
| **low** | 2020년 보급형 Android | Snapdragon 6xx, Helio G80, Exynos 9611 | **30 fps** | **33.3 ms / frame** |
| **mid** | 2022년 중급 Android, iPhone SE 2 | Snapdragon 7xx, Dimensity 900, Apple A13 | **60 fps** | **16.6 ms / frame** |
| **high** | 2024년 플래그십 | Snapdragon 8 Gen 2/3, Apple A16/17 Pro | **60 fps (90 옵션)** | **16.6 ms (11.1 ms)** |

각 frame budget은 **CPU + GPU + Render thread** 합산이 아닌 **각 thread별 budget**. 모바일은 보통 GPU bound이지만, draw call이 많으면 CPU bound로 빠르게 넘어간다. **Low 등급이 조용히 돌 수 있게** 다른 등급의 예산은 자동으로 여유.

## 예산 항목별 권장값 (low 등급 기준)

low를 기준선으로 잡고, mid/high는 **자동 여유** 또는 명시적 quality preset으로 분기. 새 변경이 low 예산을 깎는다면 alert.

| 항목 | low 권장 상한 | 측정 도구 |
|---|---|---|
| **Draw calls** (씬당) | ≤ 100 | Frame Debugger, Stats |
| **SetPass calls** (씬당) | ≤ 50 | Stats |
| **Vertices** (씬당, 가시) | ≤ 100K | Stats |
| **Triangles** | ≤ 50K | Stats |
| **Real-time lights** | 1 directional + 0 point/spot | Light Inspector |
| **Real-time shadow casters** | ≤ 5 | Profiler GPU |
| **Texture memory** | ≤ 150 MB | Memory Profiler |
| **GC alloc / frame** | **0 in steady state** | Profiler CPU (Allocations) |
| **Build size (.aab)** | ≤ 100 MB (Play 무료 한도) | Build Report |
| **Cold start time** | ≤ 4 s | Logcat startup time |

→ 큰 자산을 한 번에 넣지 말고 자산 1개 추가 시 위 항목 어느 것이 얼마나 깎이는지 보고한다.

## 등급별 URP/Quality 권장 설정

`Assets/Settings/` 안의 URP Asset과 Quality Settings를 등급별 3개 preset으로 분기. 런타임에 `SystemInfo`로 등급 판정 후 `QualitySettings.SetQualityLevel(...)`.

매뉴얼 인용 (URP "Configure for better performance"):

| 설정 | low | mid | high |
|---|---|---|---|
| **SRP Batcher** | ON | ON | ON |
| **Dynamic Batching** | ON (UI/작은 메시) | ON | ON |
| **GPU Instancing** (머티리얼 단위) | 책 prefab 같은 반복 객체에 ON | ON | ON |
| **Anti-aliasing (MSAA)** | OFF | 2x | 4x |
| **HDR** | OFF | OFF | ON (필요시) |
| **Main Light Shadow Resolution** | 1024 | 2048 | 2048 |
| **Additional Lights Shadows** | OFF (or Per Vertex) | Per Pixel, OFF cast | Per Pixel |
| **Shadow Distance** | 15 | 30 | 50 |
| **Shadow Cascades** | 1 | 2 | 4 |
| **Soft Shadows** | OFF | ON (low quality) | ON |
| **Depth Texture** | OFF (안 쓸 때) | 필요시 | 필요시 |
| **Opaque Texture** | OFF | 필요시 | 필요시 |
| **Post-processing** | None or 단일 Bloom | Bloom + Vignette | 풀 스택 (제한적) |
| **LOD Cross Fade** | OFF | ON | ON |
| **Terrain Holes** | OFF | OFF | OFF (룸 환경엔 무관) |

매뉴얼 권장:
- *"Reduce or disable"* MSAA — 메모리 대역폭 직접 절감
- *"Disable"* HDR if unnecessary
- *"Set Additional Lights to Disabled or Per Vertex"* — 모바일 추가 라이트는 거의 항상 비용 대비 가치 낮음

→ low 등급에서 책장 룸 게임은 **directional 라이트 1개 + 베이크된 lightmap + 가벼운 ambient** 만으로 충분. additional realtime light는 추가 정당화 필요.

## 자산 도입 시 예산 차감 체크리스트

새 메시·텍스처·머티리얼이 들어올 때 다음을 보고:

```
## Perf budget impact (low tier)

### 자산 추가
- 추가: `Assets/Arts/Books/Book_Common.fbx` (verts: 1,200, tris: 800)
- 머티리얼: `Mat_BookCover_PBR.mat` (셰이더: URP/Lit, SRP Batcher 호환 ✅)
- 텍스처: `BookCover_Albedo.png` (2048², ASTC 6x6, ~1MB)

### 예산 차감
- Vertices: +1,200 × {예상 인스턴스 수, 예: 50} = +60K → 누적 80K (예산 100K, 80% 사용)
- Draw calls: GPU Instancing ON이면 +1, OFF면 +50 → ⚠️ 머티리얼에 GPU Instancing 켜기 권고
- Texture memory: +1MB → 누적 75MB

### 권고
- 같은 책 모델을 50개 띄울 거면 머티리얼 GPU Instancing 필수
- BookCover 텍스처는 책마다 다르므로 atlas로 묶거나 streaming 검토
```

수치는 추측 가능하면 추측, 정확한 수치가 필요하면 사용자에게 빌드/Profiler 측정을 요청.

## 측정 절차 (PR 머지 전)

다음 중 적어도 한 번은 돈다:

1. **Stats 패널** (Game view) — 씬에 들어가서 Draw Calls / SetPass / Tris / Verts 즉시 확인. 가장 가벼운 1차 게이트.
2. **Frame Debugger** (Window > Analysis > Frame Debugger) — 어떤 객체가 어떤 셰이더로 그려지는지 step-by-step. SRP Batcher가 안 묶이는 원인 추적의 1순위 도구.
3. **Profiler** (Window > Analysis > Profiler) — Editor가 아니라 **실기 연결** 필수. CPU/GPU/Render/Memory 탭. **GC alloc은 CPU > Profile > Allocations에서 확인** (정상은 steady state에서 0).
4. **Memory Profiler 패키지** — 전체 메모리 스냅샷, 텍스처/메시 메모리 절대값.
5. **Android GPU Inspector (AGI)** (Google 공식) 또는 **Snapdragon Profiler** — 실기 GPU bound 분석. 등급별 회귀 검증.
6. **빌드 후 실기 60초 테스트** — 룸 진입 → 책장 보기 → 책 들기 → 슬롯 배치 → 대시보드 오픈. 이 흐름에서 GC spike / draw call 폭증 / fps drop 검출.

## SRP Batcher 호환성 가드

매뉴얼 인용:
> *"reduces render-state changes between draw calls by combining a sequence of bind and draw GPU commands"*

호환 조건:
- URP/Lit, URP/Unlit, URP/Particles 같은 SRP-Batcher 호환 셰이더 사용
- 머티리얼 프로퍼티가 **CBUFFER로 묶여** 있을 것 (커스텀 셰이더 작성 시)
- 같은 셰이더 variant + 같은 keyword set이면 묶임

깨지는 패턴:
- 인스턴스마다 다른 텍스처 (다른 머티리얼 인스턴스)
- `MaterialPropertyBlock` 사용 → SRP Batcher와 GPU Instancing 사이에서 트레이드오프 (둘 다 불가능한 경우 있음)
- 표준 Unity Shader (Built-in pipeline용) — URP에서 fallback rendering 발생, SRP Batcher 안 묶임

→ 새 셰이더 도입 시 Frame Debugger로 묶임 여부 확인.

## GC alloc 가드 (steady state 0)

매 프레임 호출되는 코드에서 GC alloc이 0이어야 함:

❌ 흔한 누수:
- `string` 합성: `"score: " + value` — boxing + string alloc
- `foreach` over `List<T>` (대부분 케이스 OK이지만 Mono에서 enumerator alloc 발생 가능; IL2CPP는 OK)
- `Camera.main` — 매 호출마다 GameObject.Find 비용 (alloc은 안 하지만 비싼 lookup)
- LINQ — 거의 모든 LINQ 메서드는 alloc
- `new Vector3(...)` 자체는 struct alloc이라 OK, 단 `.ToString()`은 string alloc

✅ 패턴:
- string은 `StringBuilder` 또는 캐시
- `Camera.main` 결과를 멤버 필드에 캐시
- LINQ 대신 `for`/`foreach` 직접
- Profiler "GC.Alloc" 컬럼이 매 프레임 0인지 검증

## 출력 형식 (변경 보고 안에 항상 한 블록)

깨끗하면 한 줄:
```
✅ Perf budget (low tier): no impact (UI text change only)
```

영향이 있으면 표 한 개:
```
## Perf budget impact (low tier)
| 항목 | 변경 전 | 변경 후 | 예산 | 비고 |
|---|---|---|---|---|
| Draw calls (Dashboard 씬) | 38 | 42 | 100 | UI Toolkit 새 panel +4 |
| Vertices | 65K | 65K | 100K | 변동 없음 |
| Texture memory | 78 MB | 79 MB | 150 MB | 새 아이콘 1MB |
| GC alloc / frame | 0 | 0 | 0 | OK |

권고: 안전 범위. 단 Dashboard 씬 draw call 누적 추세 관찰 — 5개월간 +12 → 50 도달 시 panel 통합 검토.
```

## 연결 — 다른 스킬·reference

- 셰이더/렌더링 깊이: `unity-fundamentals/references/` 의 `rendering-urp.md` *(예정)*
- 자산 임포트 정책: `unity-fundamentals/references/asset-pipeline.md`
- IL2CPP 빌드 사이즈/속도: `unity-fundamentals/references/build-il2cpp.md`
- UI Toolkit 폴리시·트랜지션: `unity-uitk-polish` 스킬

## 출처

- Unity Manual — [Configure for better performance in URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/configure-for-better-performance.html)
- Unity Manual — [Scriptable Render Pipeline Batcher in URP](https://docs.unity3d.com/6000.3/Documentation/Manual/SRPBatcher.html)
- Unity Manual — [Quality settings tab reference](https://docs.unity3d.com/Manual/class-QualitySettings.html)
- Unity — [Art optimization tips for mobile game developers part 2](https://unity.com/how-to/mobile-game-optimization-tips-part-2)
- Unity Learn — [Demo: Draw Calls and SRP Batcher](https://learn.unity.com/course/3d-art-optimization-for-mobile-gaming-5474/unit/shaders-and-materials-6567/tutorial/demo-draw-calls-and-srp-batcher)
- Unity 무료 e-book — *Optimize your mobile game performance* (`resources.unity.com/games`)
- Unity 무료 e-book — *Universal Render Pipeline for advanced Unity creators*
- Android Developers — [Optimize rendering using URP Asset settings](https://developer.android.com/develop/xr/unity/performance/urp-asset-settings)
- ARM Developer — [Unity and ASTC](https://developer.arm.com/documentation/102162/latest/Unity-and-ASTC)
