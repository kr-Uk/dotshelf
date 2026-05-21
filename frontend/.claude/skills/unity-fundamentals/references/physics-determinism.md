# Physics & Determinism

> **한 줄 요약**: Unity의 물리는 PhysX 기반이고 **Local Determinism**만 보장한다 — 같은 게임 세션 + 같은 timestep + 같은 입력이면 같은 결과. 다른 머신·다른 CPU·다른 프레임 분포에서는 보장 없음. dotshelf가 책 배치를 *슬롯 기반*으로 못 박은 결정은 이 한계의 직접 귀결이다.

## 어떤 결정이 이 글로 풀리는가

- **"왜 책 배치를 물리(Rigidbody+Collider)로 안 하나? 그게 더 자연스러운데?"** → §결정성 한계 + §프로젝트 연결
- **"Update와 FixedUpdate의 진짜 차이가 뭔가?"** → §메커니즘
- **"같은 코드인데 빌드마다 책 위치가 살짝 다름"** → §결정성 한계
- **"어떤 입력에서 EditMode 테스트가 통과했는데 PlayMode에서 깨짐"** → §함정 카탈로그

## 메커니즘

### 1. Update / FixedUpdate / LateUpdate

| 콜백 | 호출 빈도 | 시간 스텝 | 용도 |
|---|---|---|---|
| `Update` | 매 프레임 | `Time.deltaTime` (가변, 보통 16.6ms@60Hz, 모바일 33ms@30Hz) | 입력, 비-물리 시각 갱신, UI |
| `FixedUpdate` | 고정 간격 | `Time.fixedDeltaTime` (기본 0.02s = 50Hz) | 물리 시뮬레이션, Rigidbody 적용 |
| `LateUpdate` | 매 프레임 (Update 후) | `Time.deltaTime` | 카메라 추적 등 다른 객체 이동 후 처리 |

매뉴얼 인용:
> *"Unity's physics system updates at a fixed time interval, which is important for the accuracy and consistency of physics simulations."*

### 2. FixedUpdate가 한 프레임에 0~N번 호출되는 이유

매뉴얼 인용:
> *"A backlog of fixed updates accumulates during some frames and so each frame has one or more fixed updates to allow the physics simulation to catch up."*

프레임이 짧으면 (예: 144Hz, ~7ms) 한 프레임에 FixedUpdate가 0번 호출될 수 있고, 프레임이 길면 (예: 모바일에서 50ms 끊김) 2~3번 누적되어 한꺼번에 호출된다.

`Maximum Allowed Timestep` (Project Settings → Time): *"There is a maximum timestep period beyond which Unity will not attempt to catch up with the simulation."* 즉 너무 큰 끊김(예: 1초)이 들어와도 1초어치 시뮬레이션을 한 번에 따라잡지 않고 무시 — 무한 catch-up으로 영원히 못 따라잡는 *spiral of death* 방지.

### 3. 물리 시뮬레이션은 FixedUpdate **이후** 실행

순서 (한 fixed step 안):
1. 모든 MonoBehaviour의 `FixedUpdate`
2. 물리 시뮬레이션 step (Solver, 충돌 감지/해결, Rigidbody 위치 갱신)
3. `OnTriggerXxx`, `OnCollisionXxx` 콜백
4. (다음 fixed step으로)

→ FixedUpdate에서 `transform.position = ...`로 직접 옮기면 물리 시뮬레이션이 그 위치에서 시작. `Rigidbody.MovePosition(...)`이면 물리 인식 이동 (보간·충돌 정합).

### 4. Time.deltaTime 사용 규칙

매뉴얼 인용:
> *"When applying movement calculations inside FixedUpdate, you do not need to multiply your values by Time.deltaTime because FixedUpdate is called on a reliable timer, independent of the frame rate."*

→ FixedUpdate 안에선 `Time.deltaTime`도 **자동으로 `fixedDeltaTime`** 을 반환한다. 즉 FixedUpdate에 있는 한 `* Time.deltaTime`을 곱해도 결과는 같음. 그러나 의도를 명확히 하려면 `Time.fixedDeltaTime`을 명시.

### 5. PhysX 결정성 — 무엇이 보장되고 무엇이 안 되는가

Unity 공식 표현 (Discussions 모음 + 매뉴얼):

**보장되는 것 (Local Determinism)**:
- *"if a game executes the exact same set of physics API calls using the exact same frame timing, the engine will be perfectly deterministic globally"*
- 같은 세션·같은 머신·같은 timestep·같은 호출 순서 → 같은 결과.

**보장 안 되는 것**:
- 머신 간 — *"Desyncs can occur when replays captured on one CPU vendor (Intel) are run on a different one (AMD)"* (부동소수 NaN/denormal 처리, SIMD 차이)
- 세션 간 — Unity 자체가 시작될 때마다 약간의 비결정성 가능. *"There are no guarantees for determinism without recreating the physics world or restarting the game session."*
- `Rigidbody.constraints`로 Freeze Rotation/Position 사용 — *"break local determinism"*
- Cloth, soft body — *"PhysX does not guarantee determinism for any scene with non-rigid bodies, such as cloth or soft bodies."*
- 프레임 분포가 다르면 입력 처리 시점이 fixed step 사이 어디에 떨어지는가가 달라져 결과가 갈림

→ **결론**: 멀티플레이 락스텝, 리플레이, 서버-클라 동기화 같은 결정성 의존 기능은 PhysX로 못 만든다. 굳이 한다면 Unity Physics (DOTS, Havok 또는 Unity Physics 패키지)를 써야 하지만 우리 프로젝트 범위 밖.

## 이 프로젝트와의 연결

### `frontend/CLAUDE.md` 규칙 3 인용

> *"드래그/배치는 슬롯 기반이다. 물리(Rigidbody/Collider 충돌 응답) 기반 배치를 도입하지 않는다."*

이 규칙의 진짜 근거:

1. **결정성 부재** — 책을 떨어뜨리는 물리 기반 배치는 같은 입력에 같은 결과를 주지 못한다. 사용자가 "방금처럼 다시 놓아주세요"가 안 됨. 슬롯 기반은 입력 → 출력이 1:1.
2. **테스트 가능성** — EditMode 테스트로 배치 결과를 검증하려면 결정적이어야 함. 슬롯은 "어느 슬롯에 들어갔다"라는 이산 상태로 검증 가능. 물리는 부동소수 위치라 검증이 epsilon 기반으로 흐물흐물해짐.
3. **모바일 성능 안정성** — PhysX는 프레임 끊김 시 catch-up step이 누적되어 성능 변동을 키운다. 슬롯 기반은 0 물리 비용.
4. **Preview/Commit 분리** (`CLAUDE.md` 규칙 4와 결합) — 미리보기에서 책이 떨어지는 게 보이는 순간 그 위치는 도메인 상태가 된다. 슬롯이면 "어느 슬롯에 스냅될지"만 미리 보여주고 commit 시점에 한 번 반영.

### 어디까지 물리를 써도 되나

물리를 **완전히 금지**하는 건 아님. 다음은 OK:
- 카메라 셰이크/이펙트 (PhysX와 무관, transform 직접 조작)
- 파티클 시스템 (자체 물리, PhysX 외)
- 비-배치 시각 효과 (예: 책 펼칠 때 페이지가 살짝 흔들리는 정도)

NOT OK:
- 책의 최종 위치를 물리가 결정 (떨어뜨려 슬롯에 자리잡음)
- 슬롯 점유 판정에 `OnTriggerEnter`/`OnCollisionEnter` 의존
- Rigidbody로 책의 입력 응답 처리

### Update에서 책 위치 갱신 OK인 이유

슬롯 기반이라 Rigidbody가 없고 → 물리 시뮬레이션 영향 없음 → Update에서 `transform.position = ...` 직접 갱신해도 충돌·보간 깨짐이 없음. FixedUpdate를 강요하는 건 Rigidbody가 끼어 있을 때다.

## 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| FixedUpdate에서 입력 받았더니 한 프레임 늦거나 놓침 | 입력은 Update 빈도, FixedUpdate는 다른 빈도 | 입력은 Update에서 받아 *플래그* 또는 *intent*에 저장, FixedUpdate가 그 intent를 반영 |
| Rigidbody 위치를 transform.position으로 직접 옮겼더니 충돌 무시 | 물리 시뮬은 Rigidbody의 내부 상태 기준 | `Rigidbody.MovePosition` / `MoveRotation` 사용 |
| 같은 입력에 다른 결과 (드물게) | PhysX local determinism 한계 + 프레임 분포 | 결정성 의존하지 않게 설계. 우리는 슬롯 기반이라 무관 |
| 모바일에서 끊김 후 객체가 벽을 통과 (tunneling) | 큰 timestep + 빠른 객체 + 얇은 콜라이더 | Rigidbody.collisionDetectionMode = Continuous, 또는 fixed timestep을 0.02 → 0.01로 낮춤 (모바일 비용 ↑) |
| catch-up step이 누적되어 성능 더 떨어짐 | spiral of death | Maximum Allowed Timestep을 보수적으로 설정 (기본 0.333s 적정), 프레임 끊김 원인 별도 추적 |
| EditMode 테스트는 통과, PlayMode 빌드는 깨짐 | EditMode는 물리 step을 안 굴림 | 물리 동작 검증은 PlayMode 테스트로. 우리는 슬롯 기반이라 EditMode로 충분 |
| Freeze Rotation 켰더니 결정성 깨짐 | 매뉴얼 명시된 한계 | 결정성 필요한 코드는 constraints 대신 외부 로직으로 회전 제거 |

## Quick reference: 우리 결정의 한 줄 요약

- **슬롯 기반** = 결정적, 테스트 가능, 모바일 안정적. 물리는 시각적 보조에만.
- **입력은 Update, 슬롯 스냅 계산은 Update 한 번 / 사용자 확정 시점 한 번** — FixedUpdate 불필요.
- **Preview는 시각적 임시**, 슬롯 도메인 상태는 commit 시점에만 변경.

## 출처

- Unity Manual — [Fixed updates](https://docs.unity3d.com/6000.2/Documentation/Manual/fixed-updates.html)
- Unity Manual — [Order of execution for event functions](https://docs.unity3d.com/Manual/ExecutionOrder.html)
- Unity Scripting API — [MonoBehaviour.FixedUpdate](https://docs.unity3d.com/ScriptReference/MonoBehaviour.FixedUpdate.html)
- Unity Discussions — [Determinism (PhysX)](https://discussions.unity.com/t/determinism-physx/866457)
- Unity Discussions — [Why Unity Physics Is Not Deterministic](https://discussions.unity.com/t/why-unity-physics-is-not-deterministic/1667389)
- Unity Support — [Determinism with 2D Physics](https://support.unity.com/hc/en-us/articles/360015178512-Determinism-with-2D-Physics)
- Unity — [Physics solutions for game development](https://unity.com/solutions/programming-physics)
