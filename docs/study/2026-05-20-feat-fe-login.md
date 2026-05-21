# 로그인 진행 다이얼로그 UX 보강

- **브랜치**: `feat/fe/login`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-05-20 ~ 2026-05-20
- **관련 커밋**: `b5f300ea`

## TL;DR

Google, Kakao, Apple 로그인 버튼을 누르면 로그인 진행 다이얼로그가 뜨도록 개선했다.
다이얼로그에는 진행 메시지, 로딩 스피너, 취소 버튼이 있고, 로그인 성공 시 자동으로 닫힌 뒤 기존 온보딩/메인 이동 흐름을 그대로 따른다.
로그인 실패 시에는 실패 상태를 보여주고, 사용자가 취소 버튼을 눌러 닫은 뒤 다른 로그인 방식을 선택할 수 있게 했다.

## 무엇을 했나

### 1. 로그인 진행 다이얼로그 추가

- 사용자 관점 효과: 소셜 로그인 버튼 클릭 후 앱이 멈춘 것처럼 보이지 않고, 현재 로그인 중임을 명확히 알 수 있다.
- 시스템 관점 효과: 로그인 진행 상태를 `LoginScreenController` 내부 상태로 통합 관리한다.
- 주요 파일: `frontend/Assets/App/Bootstrap/LoginScreenController.cs`

### 2. 로딩 스피너와 취소 버튼 구현

- 사용자 관점 효과: 외부 브라우저나 네이티브 로그인 응답을 기다리는 동안 진행 중 표시를 볼 수 있다.
- 시스템 관점 효과: `Update()`에서 스피너 회전만 처리하고, 취소 버튼은 현재 로그인 시도를 무효화한다.

### 3. 로그인 시도 ID 기반 stale callback 방어

- 사용자 관점 효과: 이전 로그인 요청을 취소한 뒤 늦게 도착한 콜백이 현재 화면 상태를 꼬이게 만들지 않는다.
- 시스템 관점 효과: `activeLoginAttemptId`와 `loginAttemptSequence`로 현재 시도와 콜백이 같은 요청인지 확인한다.

### 4. 성공/실패 UX 분리

- 성공 시: 다이얼로그 자동 닫힘 → 기존 `ApplyLoginResult` 흐름대로 온보딩 또는 메인 화면 이동
- 실패 시: 다이얼로그가 `로그인 실패` 상태로 남음 → 사용자가 취소 후 다른 로그인 방식 선택 가능

## 왜 했나

기존 로그인 화면은 Google/Kakao/Apple 버튼을 누른 뒤 외부 브라우저 또는 Apple native 콜백을 기다리는 동안 사용자가 진행 상태를 알기 어려웠다.
특히 데스크톱 OAuth는 브라우저 창과 Unity 앱이 분리되어 있어, 사용자는 "로그인이 진행 중인지", "실패했는지", "다른 방식을 눌러도 되는지"를 명확히 알기 어렵다.

이번 변경은 로그인 진행 상태를 UI로 드러내고, 실패/취소 후 다른 로그인 방식을 선택할 수 있는 명확한 복구 경로를 제공하기 위한 작업이다.

## 어떻게 구현했나

### 로그인 시도 상태 관리

로그인 시도마다 ID를 부여하고, 콜백 처리 전에 현재 시도와 일치하는지 확인한다.

```csharp
private int StartLoginAttempt(SocialProvider provider, string message)
{
    if (activeLoginAttemptId != 0 || isLoginInFlight)
    {
        return 0;
    }

    activeLoginProvider = provider;
    activeLoginAttemptId = ++loginAttemptSequence;
    loginProgressFailureVisible = false;
    isLoginInFlight = true;
    SetInteractable(false);
    ShowLoginProgressDialog("로그인 중", message, true);
    SetMessage(loginMessageLabel, message);
    return activeLoginAttemptId;
}
```

핵심 포인트:
- 중복 로그인 시도를 막는다.
- 취소 후 늦게 도착한 콜백은 `activeLoginAttemptId`가 0이므로 무시된다.
- browser OAuth와 native Apple, token submit 흐름이 같은 상태 모델을 공유한다.

### 취소 버튼 처리

취소 버튼은 외부 브라우저나 네이티브 로그인 창 자체를 강제로 닫는 기능이 아니라, Unity 앱 내부의 로그인 대기 상태를 종료한다.

```csharp
private void OnLoginProgressCancelClicked()
{
    activeLoginAttemptId = 0;
    loginProgressFailureVisible = false;
    isLoginInFlight = false;
    CancelInFlightBrowserOAuth();
    HideLoginProgressDialog();
    SetInteractable(true);
    SetMessage(loginMessageLabel, "로그인이 취소되었습니다.");
}
```

핵심 포인트:
- 데스크톱 OAuth는 `CancellationTokenSource`를 취소해 loopback listener 대기를 끊는다.
- 이미 외부에서 진행된 인증 결과가 나중에 돌아와도 attempt ID가 맞지 않으면 처리하지 않는다.

### 실패 상태 유지

실패 시에는 다이얼로그를 바로 닫지 않고, 스피너를 숨긴 실패 상태로 바꾼다.

```csharp
private void FailLoginAttempt(int attemptId, string message)
{
    if (!IsCurrentLoginAttempt(attemptId))
    {
        return;
    }

    loginProgressFailureVisible = true;
    isLoginInFlight = false;
    SetInteractable(true);
    SetMessage(loginMessageLabel, message);
    ShowLoginProgressDialog("로그인 실패", message, false);
}
```

핵심 포인트:
- 실패를 사용자가 확인할 수 있다.
- 취소 버튼으로 닫은 뒤 다른 로그인 방식을 다시 선택할 수 있다.

## 배운 것

- **취소의 의미 분리**: OAuth 취소는 외부 브라우저/네이티브 UI를 강제로 닫는 것과 앱 내부 대기 상태를 취소하는 것이 다르다. Unity 앱에서는 후자를 안전하게 구현해야 한다.
- **stale callback 방어**: 비동기 로그인 플로우에서는 이전 요청의 결과가 늦게 도착할 수 있으므로, request/attempt ID로 현재 요청인지 검증해야 한다.
- **실패 UX**: 실패 즉시 다이얼로그를 닫으면 사용자가 원인을 놓친다. 실패 상태를 유지하고 명시적 취소로 복구하게 하는 편이 흐름이 명확하다.
- **프로그래매틱 UI 구성**: 기존 로그인 화면은 prefab보다 코드 기반 생성이 중심이라, 다이얼로그도 같은 헬퍼(`CreateImage`, `CreateText`, `CreateButton`) 패턴으로 맞추는 것이 일관적이다.

## 더 공부할 것

- [ ] Unity에서 외부 브라우저 OAuth 창을 사용자가 닫았는지 더 명확히 감지하는 방법
- [ ] OAuth login attempt ID를 state 값과 더 강하게 연결하는 방식
- [ ] 로그인 실패 코드별로 사용자 메시지를 다르게 보여주는 UX 기준
- [ ] `TMP_Text.enableWordWrapping` deprecation 대응과 `textWrappingMode` 전환 범위

## 참고

- 관련 커밋: `b5f300ea`
- 검증:
  - `git diff --check`
  - `dotnet build frontend\Assembly-CSharp.csproj`
  - `dotnet build frontend\Assembly-CSharp-Editor.csproj`
  - `dotnet test frontend\App.Tests.EditMode.csproj --no-restore --verbosity normal`
