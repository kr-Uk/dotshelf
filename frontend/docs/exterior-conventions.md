# Exterior Asset 디자이너 Contract

외부 환경(창밖 풍경 + 자연광 + 분위기) 옵션을 만들 때 따라야 할 좌표계 / 거리 / 시야 / 금지사항 규약. `ExteriorAsset` SO 와 `scenePrefab` 의 디자인 contract.

관련 코드:
- [ExteriorAsset.cs](../Assets/App/Infrastructure/Catalogs/ExteriorAsset.cs) — SO 정의
- [ExteriorCatalogAsset.cs](../Assets/App/Infrastructure/Catalogs/ExteriorCatalogAsset.cs) — 카탈로그
- [ExteriorApplier.cs](../Assets/App/Bootstrap/Exterior/ExteriorApplier.cs) — 씬 적용 MonoBehaviour

---

## 1. 핵심 원칙

> **얇은 SO + 풍부한 prefab.** 동적 콘텐츠는 모두 `scenePrefab` 안에. SO 는 Far layer (skybox/ambient/fog/reflection) 와 Sun override 만.

- mesh / material / animator / Point·Spot Light / ParticleSystem / MonoBehaviour 동작 → **prefab 자유**
- DirectionalLight (태양) → **SO 의 Sun override 슬롯만 사용** (씬 sun 충돌 방지)
- skybox / ambient / fog / reflection → **SO 슬롯** (`RenderSettings` 로 set 해야 하므로)

---

## 2. 좌표계

```
ExteriorRoot GameObject (씬에 1개, MainRoomScene)
  Transform.position = World Origin (0, 0, 0)
              또는 방 정중앙 floor pivot
  Transform.rotation = Quaternion.identity (방향 가정 없음)
  Transform.scale    = (1, 1, 1)

scenePrefab 은 ExteriorRoot 자식으로 instantiate.
prefab root.localPosition = (0, 0, 0).
즉 디자이너 입장에서 prefab origin = 방 정중앙 floor.
```

### 축 정의 (Unity 표준)

- **+X** = world right
- **+Y** = up
- **+Z** = world forward

→ **방향 의미 부여 안 함.** 어느 벽에 창문이 있든 동일하게 보여야 함.

---

## 3. 방 dimensions (BookshelfGenerator 기반 추정)

> 정확한 수치는 [BookshelfGenerator.cs](../Assets/Scripts/BookshelfPorting/Runtime/BookshelfGenerator.cs) 에서 확정.

```
방 크기:        대략 6m × 6m × 3m (W × D × H)
방 바닥 (floor) Y = 0
방 천장 (ceiling) Y ≈ 3
창문 가능 높이  Y ≈ 1 ~ 2.5
방 외벽:        |X|, |Z| ≈ 3 (방 중앙에서 벽까지 ~3m)
```

→ **외부 자산은 |X| 또는 |Z| > 3 영역에서만 시작** (방 내부 침범 금지).

---

## 4. 거리 zone (XZ 반경 기준, 방 중앙 측정)

| Zone | 반경 | 용도 |
|---|---|---|
| **Forbidden** | 0 ~ 3 m | 방 자체. 자산 두면 벽에 박힘 |
| **Buffer** | 3 ~ 5 m | 비워두기 권장 (창문 frame 가림 회피) |
| **Near** | 5 ~ 15 m | quality 가장 살릴 곳 (창 옆 가지 / 잔디 / 꽃) |
| **Mid** | 20 ~ 80 m | silhouette (큰 나무 / 언덕 / 건물) |
| **Transition** | 80 ~ 150 m | skybox 와 자연 연결용 fade-out gap |
| **Far** | 150 m + | skybox 위임. mesh 두지 말 것 |

→ Near/Mid 사이 (15 ~ 20 m), Mid/Far 사이 (80 ~ 150 m) 갭은 일부러 비워서 **거리감 그라데이션** 만듦.

---

## 5. 높이 (Y) 가이드

| 자산 | 높이 |
|---|---|
| ground plane | -0.1 ~ 0.0 (방 바닥 또는 살짝 아래) |
| near 자산 | 0 ~ 5 (창문 시야 내) |
| mid 자산 | 0 ~ 30 (큰 나무 / 언덕) |
| 멀리 산 silhouette | 5 ~ 80 |
| 하늘 | Y 무관 (skybox) |

→ ground plane Y 는 살짝 아래 권장 — 창문에서 살짝 내려보이는 자연스러움.

---

## 6. 배치 패턴 (디자이너 컨셉 따라 선택)

### (α) 360° 둘러쌈 — Default 권장

- ground plane: 방 주변 360° 둘러싸는 큰 평면 (원판 ⌀200m 또는 사각 200×200)
- mid/near 자산: 방 주변 4 ~ 8 방향에 분산 배치
- **장점:** 어느 벽 창문에서도 뭔가 보임
- **적합:** 일반 옵션 (초원, 도시 야경, 숲)

### (β) 주력 방향 — 시그니처 옵션

- 한 방향에 시그니처 자산 집중 (예: 거대한 우주 행성, 에펠탑)
- 나머지 방향은 ground + skybox 만
- **장점:** 임팩트 ↑
- **단점:** 그 방향 안 보는 창문에선 비어 보임 — 옵션 description 에 "정면 창에서 가장 잘 보입니다" 같은 안내 필수

→ **SO 에 `patternHint` 같은 필드 안 둠.** description 텍스트로 충분.

---

## 7. 시야각 / culling

```
방 안 카메라 → 창문 frustum 투과 시야각 추정:
  창문 중심에서 ±60° horizontal, ±30° vertical 가량
  (창문 크기와 카메라 거리에 따라 변함)
```

### Culling 설정

- scenePrefab 자식 자산: **Static flag ON** → static batching
- ExteriorRoot 에 작은 Bounding Volume → 창문 occlusion 으로 frustum 밖 자동 culling
- frustum 밖에서 강제 그리는 `Always Render` flag 는 **사용 X** (perf 낭비)

---

## 8. 금지 사항 (prefab 에 들어가면 안 되는 것)

| 금지 | 이유 | 대안 |
|---|---|---|
| `DirectionalLight` 컴포넌트 | 씬 sun 충돌 / RoomTheme conflict | ExteriorAsset 의 `Sun override` 슬롯 |
| `Realtime Reflection Probe` | 모바일 perf 폭발 | `customReflectionCubemap` 슬롯 |
| 방 내부 영역 (반경 < 3m, Y > 0 자산) | 벽 clipping | 외부 영역만 |
| 다른 RoomTheme 자산 reference | bundle 의존성 폭발 | scenePrefab 자체 완결 |
| AnimatorController 가 base APK 자산 reference | hot update 깨짐 | 외부 자산만 reference |
| `Camera` 컴포넌트 | 의도치 않은 view override | 절대 금지 |
| `AudioListener` | 씬에 1개만 가능 | `AudioSource` 만 OK |

---

## 9. 자산 품질 가이드 (zone 별)

| Zone | polycount/자산 | texture 해상도 | Meshy Refine | LOD |
|---|---|---|---|---|
| Near (5 ~ 15 m) | 15k ~ 30k tri | 2K PBR | ON | LOD0 만 충분 |
| Mid (20 ~ 80 m) | 5k ~ 15k tri | 1K PBR | OFF | LOD0 ~ LOD1 |
| Far (150 m +) | mesh 두지 마 — skybox | — | — | — |

---

## 10. Budget guardrails

`ExteriorAsset` SO 의 `Budget guardrails` 섹션에 옵션별 한도 명시:

| 항목 | 권장 한도 | 이유 |
|---|---|---|
| `maxAdditionalRealtimeLights` | 4 | URP 모바일 per-pixel additional light 한도 |
| `maxActiveParticleSystems` | 2 | fillrate overdraw 방지 |
| `targetBundleSizeMb` | 15 | 모바일 안전 한도 (Addressables 다운로드) |

→ Editor validator (Tier 1.5+ 도입 예정) 가 import 시 자동 체크.

---

## 11. 신규 옵션 만들기 (5 단계)

```
1. Project > Create > Material → "Skybox_<Name>"
   (Procedural Skybox 또는 Cubemap shader)

2. Project > Create > 빈 Prefab → "ExteriorScene_<Name>"
   - Plane / mesh 자산 배치 (위 컨벤션 따라)
   - Point/Spot Light 자유 배치 (DirectionalLight 금지)
   - 잎 흔들림 등 procedural shader / Animator 자유

3. Project > Create > Dotshelf > Exterior Asset → "Exterior_<Name>"
   - skyboxMaterial, scenePrefab 슬롯 채움
   - Sun 색/intensity/rotation override 값 설정
   - displayName, description, themeTag 등 메타 입력

4. ExteriorCatalog_Default.asset 의 entries 에 위 SO 추가
   (Tier 2 에서는 BE assets table row insert)

5. 디바이스 또는 Editor Play 모드에서 적용 확인
   (Tier 2 부터는 갤러리 UI 에서 선택)
```
