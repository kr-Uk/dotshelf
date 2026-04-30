---
name: unity-layer-check
description: Use this skill whenever the user is editing or reviewing C# files under frontend/Assets/App/, especially in Domain/, Application/, Infrastructure/, or Features/. It verifies that the dotshelf clean-architecture rules from frontend/CLAUDE.md and AGENTS.md are not being violated — UnityEngine leaks into Domain, Features bypassing UseCases to call Infrastructure directly, MonoBehaviours appearing outside Features/UI, asmdef references that break the dependency direction, and Mock/Real API code paths diverging by anything other than base URL. Trigger this proactively when the user asks to "add a feature", "wire up an API", "create a use case", "review this code", "merge this PR", or whenever a diff touches multiple layers in App/. Don't wait for an explicit "check layers" — apply silently as part of any code-touching task in this directory.
---

# unity-layer-check

dotshelf Unity 프로젝트의 레이어 경계를 지키게 하는 스킬. AGENTS.md와 frontend/CLAUDE.md의 불변 규칙이 깨지지 않게 코드 변경을 사전 검증하고, 위반을 찾으면 **수정안과 함께** 보고한다.

## 왜 이 규칙이 중요한가

- **Domain의 순수성** — Domain이 `UnityEngine`에 묶이면 EditMode 테스트가 Unity 런타임 없이 못 돌고, 백엔드/툴/CLI에서 도메인 로직 재사용이 막힌다. Domain은 "C#만 있으면 컴파일되는" 상태를 유지해야 한다.
- **UseCase 경계** — Features에서 Infrastructure를 직접 호출하면, API 변경이나 Mock 전환이 Feature 레이어 곳곳을 건드린다. UseCase가 사이에 있어야 변경이 한 곳에 격리된다.
- **슬롯 기반 배치** — 물리 기반 응답을 한 번 도입하면 결정론적 테스트가 깨지고 모바일 성능 변동이 커진다. 책 배치는 사용자 확정 시점에만 슬롯에 스냅하는 모델을 유지한다.
- **Preview/Commit 분리** — 미리보기에서 Domain을 변경하면 취소(Cancel) 시 상태 복구가 어렵고, 동기화 버그가 누적된다. Preview는 시각적 임시 상태로만 둔다.
- **Mock/Real API 동일 경로** — `if (mock) ... else ...` 분기가 들어가는 순간 Mock에서 통과한 흐름이 실제 API에서 다르게 동작한다. base URL만 다르고 같은 코드 경로여야 한다.

## 언제 발동하나

이 스킬은 다음 상황에서 **자동으로 적용**된다 — 사용자가 명시적으로 호출하지 않아도 다음 중 하나라도 해당하면 룰 체크를 같이 돈다.

- `Assets/App/Domain/` 아래 파일을 만들거나 수정할 때
- `Assets/App/Features/` 에서 `Infrastructure`/`Api`/`Repository`라는 이름이 들어간 타입을 새로 참조할 때
- `*.asmdef` 파일을 수정할 때
- `MonoBehaviour`, `ScriptableObject`, `Vector3`, `Transform` 등 Unity 타입을 새 파일에 도입할 때
- `Rigidbody`/`Collider` 충돌 응답 코드, 또는 `OnCollision*`/`OnTrigger*` 콜백을 추가할 때
- "Mock", "Fake", "Stub" API 관련 코드를 만들거나 환경 분기를 추가할 때
- PR 리뷰 / 커밋 직전 / "이 변경 괜찮아?" 같은 검증 요청

## 체크 항목 (순서대로)

### 1. Domain 순수성

`Assets/App/Domain/` 아래의 모든 `.cs` 파일에 대해:

```bash
grep -rn "using UnityEngine\|: MonoBehaviour\|: ScriptableObject\|UnityEngine\." frontend/Assets/App/Domain/ --include="*.cs"
```

또한 `App.Domain.asmdef`의 `references` 배열이 비어 있고 다른 어셈블리에 의존하지 않는지 본다. `noEngineReferences`는 이상적으로 `true`지만 현재 `false` 상태 — 이걸 단독으로 바꾸지는 말고 위반이 발견되면 사용자에게 "asmdef도 같이 잠글까요?" 라고 묻는다.

**위반 발견 시 수정안:**
- `Vector3`, `Quaternion` 같은 수학 타입은 Domain에 직접 정의한 POCO(예: `Domain.Math.Vec3`)나 `System.Numerics.Vector3`로 대체
- `MonoBehaviour` 상속이 Domain에 들어왔다면 그 타입은 `Features/`로 옮기고, 도메인 로직만 추출해 별도 POCO 클래스로 분리
- 이벤트 발신은 `UnityEvent` 대신 `event Action<T>` / `IObservable<T>` 또는 도메인 이벤트 인터페이스 사용

### 2. Feature → Infrastructure 직접 참조

```bash
grep -rn "using App\.Infrastructure\|new .*Repository(\|new .*Api(" frontend/Assets/App/Features/ --include="*.cs"
```

Feature 코드는 UseCase 또는 인터페이스(`IBookRepository`, `ILoadBookshelfUseCase` 등)에만 의존한다. 구체 타입을 직접 `new` 하거나 `using App.Infrastructure;` 가 들어가 있으면 위반.

**수정안:**
- 의존성을 인터페이스로 추상화하고 Application 레이어에 정의
- 구체 타입 주입은 `Bootstrap/`의 컴포지션 루트에서만

### 3. asmdef 의존성 방향

다음 방향만 허용:
```
Features  ──▶ Application ──▶ Domain
   │              │
   ▼              │
  UI ◀────────────┘
   │
Infrastructure ──▶ Application ──▶ Domain
                         ▲
Bootstrap ──▶ 모든 레이어 (조립 전용)
```

체크:
```bash
for f in frontend/Assets/App/*/App.*.asmdef; do
  echo "=== $f ==="
  cat "$f" | python3 -c "import sys,json; d=json.load(sys.stdin); print('refs:', d.get('references', []))"
done
```

`App.Domain.asmdef.references`는 항상 `[]`. `App.Application`은 `App.Domain`만. `App.Features`가 `App.Infrastructure`를 직접 참조하면 위반.

### 4. MonoBehaviour 위치

```bash
grep -rln ": MonoBehaviour" frontend/Assets/App/ --include="*.cs" | grep -v "/Features/" | grep -v "/UI/" | grep -v "/Bootstrap/" | grep -v "/Editor/" | grep -v "/Tests/"
```

위 명령에 무언가 잡히면 위반. MonoBehaviour는 Features/UI/Bootstrap/Editor/Tests 외에 존재할 수 없다.

### 5. 슬롯 vs 물리

새로 추가/수정된 코드에서:
```bash
grep -rn "Rigidbody\|OnCollision\|OnTrigger\|AddForce\|isKinematic = false" frontend/Assets/App/ --include="*.cs"
```

슬롯 배치 흐름 안에 물리 응답이 들어 있으면 의심. 카메라 셰이크/이펙트 같은 비배치 용도는 OK — 책/슬롯 위치 결정에 물리가 개입하면 NOT OK.

### 6. Preview / Commit 분리

미리보기 상태가 Repository나 Domain Aggregate를 직접 mutate하지 않는지 본다. 패턴:
- Preview는 별도 ViewModel/뷰 상태(필드/observable)로 보관
- "확정" 버튼 등 명시적 트리거에서만 UseCase 호출 → Repository 반영

`Preview`라는 이름이 들어간 클래스가 `Repository.Save`나 도메인 Aggregate `Apply`를 직접 부르면 위반.

### 7. Mock/Real API 분기

```bash
grep -rn "if.*Mock\|#if.*MOCK\|isMock\|UseMock" frontend/Assets/App/ --include="*.cs"
```

런타임 분기가 잡히면 위반. Mock은 같은 인터페이스 다른 구현으로, base URL만 환경 설정에서 주입한다.

## 출력 형식

레이어 체크 결과는 변경 보고 안에 **항상 한 블록**으로 포함한다 — 위반 0개여도 "✅ Layer check: clean" 한 줄은 남긴다. 사용자가 검증이 돌았는지 확인할 수 있어야 한다.

```
## Layer check
- ✅ Domain purity (no UnityEngine leaks)
- ⚠️  Feature → Infrastructure: BookListView.cs:42 references App.Infrastructure.BookApiClient directly
  → 수정안: IBookRepository 인터페이스를 통해 주입받기. Bootstrap/AppInstaller.cs에서 등록.
- ✅ asmdef direction
- ✅ MonoBehaviour placement
- ✅ Slot-based placement
- ✅ Preview/Commit separation
- ✅ Mock/Real API parity
```

## 위반 처리 원칙

1. **혼자 고치지 않는다** — 위반을 발견하면 보고하고, 수정안을 제시한 뒤 사용자 승인을 받는다. asmdef/Settings 변경은 특히.
2. **Domain 위반은 우선순위 최상** — Domain에 UnityEngine이 들어가면 다른 모든 규칙보다 먼저 알린다.
3. **기존 위반은 옐로 카드** — 이 스킬이 적용되기 전에 이미 들어와 있던 위반은, 사용자가 그 코드 영역을 건드릴 때만 알린다 (관련 없는 파일에서 사용자 흐름을 끊지 않는다).
4. **현재 자산의 알려진 부채** — `App.Domain.asmdef.noEngineReferences = false`. 새 위반을 막는 것이 우선이고, 이 부채를 잠그는 변경은 별도 작업으로 사용자에게 제안한다.

## 검증

레이어 체크 자체가 검증의 일부지만, 코드 변경이 같이 들어갔다면 추가로:
- `dotnet build frontend/App.Domain.csproj` (Domain만 별도로 빌드 — UnityEngine 누수가 있으면 실패)
- Unity 에디터 컴파일 확인
- EditMode 테스트가 있으면 실행
