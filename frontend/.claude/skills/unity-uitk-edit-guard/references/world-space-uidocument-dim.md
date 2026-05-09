# World-space UIDocument 가 어둡게 그려지는 문제 (미해결)

## 한 줄 요약

**Unity 6.3 LTS + URP + World-space UIDocument** 조합에서 UI 가 의도된 sRGB 톤보다 dim 하게 그려진다. **Unity 의 알려진 한계**에 가깝고, 2026-05 시점에 PanelSettings 손잡이만으로는 풀 수 없다.

## 증상

- `MainRoomScene` 의 GuestbookBoard 화이트보드 위에 dashboard 가 표시되는데, **순백색이 cream 톤으로 dim 하게 보임**.
- 같은 dashboard 컨텐츠 (`DashboardScreenPanel.prefab` + `DashboardView.uxml/uss`) 가 **`DashboardTestScene` 에서는 순백색으로 선명하게 표시됨** — 사용자가 비교 기준으로 삼은 정상 톤.
- panel 컨텐츠 (UXML/USS) 자체는 멀쩡. 어두움은 **렌더링 path 단계** 에서 발생.

## 두 씬의 결정적 차이

| 항목 | MainRoomScene (dim) | DashboardTestScene (선명) |
|---|---|---|
| dashboard UIDocument 의 PanelSettings | `DashboardWorldPanelSettings` (m_RenderMode=1, World) | `NotebookPanelSettings` (m_RenderMode=0, Screen Space Overlay) |
| Camera `m_RenderPostProcessing` | **1 (ON)** | **0 (OFF)** |
| 룸 라이팅 컨텍스트 | 있음 (벽/책장/노트북 lit) | 없음 (테스트 씬) |

## 시도한 fix (모두 무효)

다음 fix 들을 **세션 한 번에 순차/조합으로** 시도했으나 dashboard 톤이 의미있게 밝아지지 않았다.

### A. PanelSettings 의 색·gamma 손잡이

| 시도 | 결과 |
|---|---|
| `forceGammaRendering: 0 → 1` | 무효 (World-space 모드에서) |
| `m_ClearColor: 0 → 1`, `m_ColorClearValue: {0,0,0,0} → off-white` | 무효 — World-space 모드에서 ClearColor 무시됨이 거의 확실 |
| `m_ColorClearValue: red (1,0,0,1)` 진단 임시 변경 | 무효 — 빨강 안 깔림. World-space + ClearColor 무시 가설 강력 시사 |

### B. Surface mesh 의 material

`BookshelfGenerator.BuildGuestbookBoard()` 의 Surface 박스에 `MaterialFactory.GetWhiteboardSurfaceMaterial()` 을 입힘. 원래 URP Lit.

| 시도 | 결과 |
|---|---|
| Surface material 을 URP Unlit 으로 교체 (`CreateUnlitMaterial` 신규) | 무효 — surface 가 unlit 이어도 dashboard 가 올라가는 단계에서 dim |
| dashboard panel 의 ColorClearValue alpha=0 (검정·투명) 인 채 surface color 가 비치는 구조였다는 가설 | 절반만 사실 — surface 를 unlit 으로 만들어도 panel 자체가 World-space 합성에서 어두움 |

### C. UIDocument 의 PanelSettings 자체를 우회

| 시도 | 결과 |
|---|---|
| `DashboardScreenController.Configure()` 의 `if (overridePanelSettings != null)` 블록을 `if (false && ...)` 로 가드하여 prefab 기본 PanelSettings 로 떨어뜨리는 D 옵션 | 무효 — `UIDocument.m_PanelSettings` 자체가 prefab 직렬화 시점에 이미 `DashboardWorldPanelSettings` 로 박혀 있어 controller 가드가 무의미했음. 진짜 D 의 의미 (Screen Space Overlay) 는 PanelSettings asset 자체의 `m_RenderMode: 0` 변경일 때만 살아남 |
| `m_RenderMode: 1 → 0` (Overlay) + ColorClearValue=red 진단 변경 | **여기서만** 빨강이 dashboard 영역에 깔리는 것을 사용자가 확인. ClearColor 손잡이가 World 모드에서 무시되고 Overlay 에서만 동작한다는 가설 100% 확정. 다만 Overlay 는 룸 안에 dashboard 가 박히는 시각효과를 잃는다 — 진짜 fix 아님 |

### D. RenderTexture 우회 path

가장 큰 변경:

1. `Assets/App/Features/Dashboard/Presentation/DashboardRT.renderTexture` 신규 생성 (1200×800, R8G8B8A8_SRGB, sRGB=true, no depth).
2. `DashboardWorldPanelSettings.asset` 의 `m_TargetTexture` 에 RT 할당, `m_RenderMode: 0` (Overlay; UI Toolkit 6.x 에서 m_TargetTexture 우선).
3. `MaterialFactory.GetWhiteboardSurfaceMaterial()` 가 자동으로 RT 를 `_BaseMap` 에 바인딩. `_BaseColor=Color.white` 로 modulate 안 되게.

**1단계 검증** (RT 자체) — RT 의 Inspector preview 에서 dashboard 가 sRGB 그대로 정상 캡처됨을 확인. Linear/sRGB 라운드트립이 panel→RT 단계에서는 정상.

**2단계 검증** (Surface 위 표시) — RT 가 Surface 의 unlit material 에 깔리지만 **여전히 dim**. preview 와 surface 위 톤이 명확히 다름.

진단 로그 (`[DIAG dashboard-tone]` prefix) 로 확인한 사실:
- shader: `Universal Render Pipeline/Unlit` (정상 Unlit)
- mainTexture: DashboardRT (정상 바인딩)
- baseColor: RGBA(1, 1, 1, 1)
- rtSRGB: True
- colorSpace: Linear

→ **shader/RT/color 모두 정상인데 surface 위에서 dim**.

가설: URP Linear color space 에서 World-space rendering path 가 sRGB 톤을 미묘하게 dim 시키는 알려진 동작. 우리 손잡이로는 풀 수 없음.

### E. Camera 후처리 토글 / Volume profile

| 시도 | 결과 |
|---|---|
| `MainRoomScene.unity` 의 Camera 의 `m_RenderPostProcessing: 1 → 0` (DashboardTestScene 과 같은 OFF 환경으로 맞춤) | 무효 — 사용자가 Unity Inspector 에서 직접 토글 OFF 후 확인. dashboard 톤 변화 없음 |
| `Assets/Settings/DefaultVolumeProfile.asset` 의 후처리 컴포넌트 28개 모두 `active: 1 → 0` (Volume 효과 컴포넌트 단위 비활성) | 무효 — dashboard 톤 변화 없음. Volume / 카메라 후처리는 dim 의 직접 원인이 아님 |

→ Camera/Volume 후처리 layer 는 dim 원인이 아닌 것으로 확정. 진짜 원인은 **URP World-space UIDocument 자체의 합성 path** 에 있음.

## Web 참고 자료

이 문제는 community 에서도 동일하게 보고되고 있다 — **우리만의 버그가 아니다**.

- [World Space UI Toolkit is not rendered correctly in URP — Unity Discussions](https://discussions.unity.com/t/world-space-ui-toolkit-is-not-rendered-correctly-in-urp/1694895) — URP + World Space UI Toolkit 의 알려진 렌더링 이슈. 사용자들이 시도한 troubleshoot (post-processing 끄기, panel settings 변경, button animation 끄기) 이 우리 시도와 동일.
- [PanelRenderer: Evolution of the UIDocument Component in 6.5+](https://discussions.unity.com/t/panelrenderer-evolution-of-the-uidocument-component-in-6-5/1715909) — Unity 6.5+ 의 새 컴포넌트가 UIDocument 의 World-space 한계를 해결. **현재 우리는 6.3 LTS 라 못 씀**.
- [WorldSpaceUIDocument (Dephion)](https://github.com/Dephion/WorldSpaceUIDocument) — community 라이브러리. RT + 3D mesh + custom EventSystem 으로 정공 우회.
- [Custom Unity component to create a world-space UIToolkit panel](https://gist.github.com/katas94/7b220a591215efc36110860a0b1125eb) — 비슷한 우회 솔루션.
- [UIToolkit world space rendering with different shader](https://discussions.unity.com/t/uitoolkit-world-space-rendering-with-different-shader/1682682) — World-space rendering 을 다른 shader 로 바꾸려는 시도.
- [3D scene need Linear but UI need Gamma — Ming Wai Chan](https://cmwdexint.com/2019/05/30/3d-scene-need-linear-but-ui-need-gamma/) — Linear vs Gamma color space 충돌의 본질. UI 는 Gamma, 3D 는 Linear 가 자연스러운 분리.
- [URP raw image transparency problem (Linear vs gamma color space)](https://discussions.unity.com/t/urp-raw-image-transparency-problem-linear-vs-gamma-color-space/785078) — 비슷한 color space round-trip 케이스.

## 진짜 fix 후보 (큰 작업)

Camera/Volume 후처리 layer 가 dim 원인이 아니라는 확정 (E 시도 결과) 이후, **PP 격리 계열 fix 는 모두 무효** 라는 게 logical 결론:

- ❌ **URP Camera Stack (Base + Overlay) 패턴** — Overlay 의 PP OFF 도 결국 "dashboard 가 PP 단계 안 거침" 인데, 단일 카메라 m_RenderPostProcessing=0 에서 이미 같은 상태가 무효였음. **시도 가치 거의 없음** (확실해질 때까지만 한 번 검증해볼 수는 있음, 하지만 logical 로는 동일 결과 예상).

남은 후보는 모두 **World-space rendering path 자체** 를 우회하는 큰 작업:

1. **Dephion's WorldSpaceUIDocument 라이브러리 도입** — RT + 3D mesh + custom EventSystem 우회. UIDocument 의 internal shader 를 우회하고 우리가 제어하는 Unlit shader 로 RT 를 Surface 에 그림. 코드 변경 큼. dashboard 인터랙션 (클릭/스크롤) raycast 도 새로 wiring.
2. **Unity 6.5+ 로 업그레이드 후 PanelRenderer 마이그레이션** — Unity 가 native renderer 로 World-space rendering 을 다시 짠 정공 솔루션. PanelRenderer 가 UIDocument 의 internal shader 단계를 대체. 다만 6.3 LTS → 6.5 업그레이드는 별도 큰 마이그레이션 작업. **6.8 CoreCLR LTS 까지 기다려 한 번에 통합하는 옵션이 더 합리적** (LTS 안정성 + 마이그레이션 1회 통합).
3. **Dashboard 를 World-space 가 아닌 Screen Space Overlay 로 가되, 룸 안 박히는 시각효과는 빠진 채로 받아들임** — UX 타협. Q1 의 D 옵션이 정확히 이거.

**왜 PP 격리가 무효인가** — `m_RenderPostProcessing=0` 으로 단일 카메라가 PP 단계 자체 거치지 않았는데도 dashboard 가 dim 했음. 즉 dim 원인은 카메라/Volume layer 가 아니라 **UI Toolkit 의 World-space 합성 단계 (internal panel mesh shader, Linear↔sRGB 변환)** 에 있음. PanelSettings/카메라/Volume 손잡이 어느 것으로도 그 단계를 건드릴 수 없음.

## 다음 사람 메모

- 가설 기반 두드림 금지. 위 시도 다 똑같이 무효였음. 정공은 **두 씬 (MainRoomScene vs DashboardTestScene) 의 PanelSettings + Camera + Volume 차이를 끝까지 비교**하고 한 번에 격차 줄이기.
- `DashboardTestScene` 의 셋업이 정상 톤의 ground truth — 그쪽을 reference 로 삼고 차이를 하나씩 일치시키는 흐름이 가장 빠른 진단.
- panel 컨텐츠 (UXML/USS/themeUSS) 는 **건드릴 필요 없음** — 이미 정상이라는 사용자 증언 (test scene 에서 순백색).
- frontend/CLAUDE.md 의 "씬 자동 대량 수정 = 컨펌" 규칙 — `MainRoomScene.unity` 직접 수정 시 사용자 컨펌.
