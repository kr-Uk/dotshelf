---
name: unity-package-security
description: Use this skill whenever the user is adding, updating, removing, or auditing a Unity package in dotshelf — `Packages/manifest.json` edit, Package Manager UI install, OpenUPM scoped registry add, Asset Store package import, or any third-party DLL drop into Assets/. Trigger when the user says "이 패키지 추가", "manifest 바꿔줘", "Asset Store에서 가져와", "scoped registry", "이 라이브러리 써도 돼?", or whenever a diff touches `Packages/manifest.json`, `Packages/packages-lock.json`, or files under `Packages/`. The skill enforces Unity 6.3+ package signature checks (Signed Official / Signed Limited / Invalid Signature / Unsigned), known-good source policy (UPM official > verified publishers > scoped registries > tarballs > raw DLL), supply-chain hygiene (lockfile review, version pinning, license check), and integrates with unity-perf-budget (build size impact) and unity-fundamentals/build-il2cpp.md (link.xml needs).
---

# unity-package-security

dotshelf에 외부 패키지·라이브러리를 들이는 모든 경로에서 *공급망 보안*과 *유지보수성*을 강제하는 스킬. Unity 6.3+의 **Package Signature** 시스템을 1차 게이트로, 그 위에 우리 정책(소스 우선순위, 라이선스, lockfile 리뷰, 빌드 영향)을 얹는다.

## 왜 필요한가

- 게임/앱 공급망 공격은 외부 패키지나 Asset Store 자산을 통한 침투가 자주 — 빌드 산출물에 임의 코드가 들어가면 인증/결제/사용자 데이터에 접근 가능.
- Unity 6.3부터 *패키지 서명* 검증이 정식 — 매뉴얼 인용: *"a cryptographic identifier that helps verify where a package came from and ensures it wasn't changed after the developer created it."*
- 그러나 **Unity의 서명 검증은 강제 차단이 아니라 경고 수준**. 우리 프로젝트 정책으로 더 강한 가드 필요.
- 외부 패키지는 **빌드 사이즈, IL2CPP 호환성, link.xml, GC alloc, license 의무**까지 영향을 준다 — 단순 "기능이 있다 → 도입"이 아니라 6가지 게이트를 통과해야 한다.

## 언제 발동하나

- `Packages/manifest.json` 변경 (의존 추가·삭제·버전 변경)
- `Packages/packages-lock.json` 변경
- Package Manager UI에서 새 패키지 install 시도
- Asset Store 패키지 import
- `.unitypackage` 직접 import
- 외부 `.dll` 을 `Assets/Plugins/` 에 드롭
- OpenUPM scoped registry 추가
- 사용자가 "이 라이브러리 써도 돼?", "이 패키지 안전해?", "Asset Store에서 봤는데" 같은 신호

## Package Signature 시스템 (Unity 6.3+, 매뉴얼 인용)

| 상태 | 의미 | 우리 정책 |
|---|---|---|
| **Signed (Official)** | Unity 또는 조직 서명. *"safe to use"* | ✅ 기본 허용 |
| **Signed (Limited)** | 공개 채널/낯선 조직. *"verification of source recommended"* | ⚠ 도입 전 source 확인 필수 |
| **Invalid Signature** | 서명되었지만 변조됨. *"might indicate that the package has been tampered with, is unsafe, or malicious"* | 🔴 **차단** — 도입 금지, 사용자에게 경고 |
| **Unsigned** | 서명 없음 | ⚠ 별도 검증 절차 필요 (§Unsigned 패키지 검증) |

매뉴얼 권고: unsigned 패키지는 *"ask the package owner to publish a version of the package with a signature"*. Unity가 install을 막지는 않으니, 우리 정책이 막아야 함.

→ 6.3 이전 버전이거나 패키지가 아직 서명 안 된 경우가 많음. **현재 단계에선 unsigned가 흔함**. 그래서 §소스 우선순위가 1차 가드.

## 소스 우선순위 (Trust Tier)

높은 → 낮은 신뢰. 가능한 한 위 단계에서 해결.

### Tier 1 — Unity 공식 (`com.unity.*`)
- Package Manager 기본 등록
- Unity가 직접 서명·유지보수
- 예: `com.unity.inputsystem`, `com.unity.render-pipelines.universal`, `com.unity.ai.inference`
- **자동 신뢰**

### Tier 2 — Verified Publisher (Asset Store / Unity 인증)
- 매뉴얼: *"UPM publishers receive a unique namespace in the Unity Editor, validated in Package Manager, to indicate that packages are reviewed, signed and distributed by the Unity Asset Store."*
- 인증된 조직이 게시 — DUNS / 도메인 검증 거친 곳
- **추천. 라이선스만 확인 후 도입**

### Tier 3 — OpenUPM / 잘 알려진 community 패키지
- `com.cysharp.unitask`, `jp.hadashikick.vcontainer` 같은 정평 라이브러리
- GitHub 별 수, 활성 commit, 최근 release, 알려진 사용 사례
- 보통 unsigned (Limited) — **소스 코드 + 라이선스 + 메인테이너 reputation 확인 후**

### Tier 4 — GitHub URL 직접 의존
```json
"jp.example.lib": "https://github.com/example/lib.git#v1.2.0"
```
- **버전 태그 또는 commit hash 고정** 필수 (`#main` 같은 floating ref 금지)
- **fork → 우리 조직 mirror** 권장 (외부 저장소가 사라지거나 변조되어도 빌드 안전)
- 도입 검토 회의 + 사용자 명시 승인

### Tier 5 — `.unitypackage` / 직접 `.dll` 드롭
- **금지에 가깝게**. 마지막 수단.
- 설치 후 lockfile에 기록 안 됨 → 재현성 깨짐
- 수동 license 추적 필요
- 도입 시 (1) `.dll` 출처 + 버전 + 해시 기록, (2) 리뷰어 2명, (3) 별도 PR

## 도입 전 6게이트 체크리스트

새 패키지를 추가하기 전 다음을 모두 확인. 한 항목이라도 ❌면 도입 보류.

| 게이트 | 확인 |
|---|---|
| **1. 서명 / Tier** | Tier 1~3 우선. Tier 4~5는 명시 승인 |
| **2. 라이선스** | MIT / BSD / Apache-2.0 / Unity-compatible. GPL/AGPL/SSPL은 게임 배포 시 침습적 — 사용자 컨펌 |
| **3. 활성 / 최근 release** | 마지막 release가 12개월 이내. Issue tracker 활성. abandoned 라이브러리는 미래 부채 |
| **4. IL2CPP 호환** | `unity-fundamentals/build-il2cpp.md` 참조. README/issue에 "IL2CPP support" 또는 link.xml 동봉 여부. 모바일 빌드 필수 |
| **5. 빌드 사이즈 영향** | `unity-perf-budget` 차감. 본격 도입 전 빈 프로젝트에 add → AAB 사이즈 변동 측정 |
| **6. 의존성 그래프** | manifest의 `dependencies`가 또 다른 외부 패키지를 끌고 오는지. transitive 의존 모두 위 게이트 통과해야 |

추가 권장:
- **CoreCLR 호환성** (`unity-coreclr-prep` 참조) — `target framework`가 `netstandard2.1` / `net8.0`이면 안전
- **Privacy / 네트워크 호출** — 패키지가 외부 서버에 텔레메트리/광고 SDK 호출하는지 source 확인
- **GitHub release notes** 또는 changelog 마지막 12개월 review

## Unsigned 패키지 검증 절차

Tier 2/3에서 unsigned가 흔함. 다음 절차로 보강:

1. **Source 위치** 명시 (GitHub URL, commit/tag)
2. **GitHub stars / forks / contributors** — 최소 100+ stars 권장 (절대 기준은 아님)
3. **마지막 commit / release** 날짜 — 12개월 이내
4. **Issues tab** — 보안 이슈 또는 abandoned 신호 검색 ("malicious", "unmaintained", "deprecated")
5. **License** 파일 직접 확인 (LICENSE 또는 `package.json` license 필드)
6. **Changelog** 12개월치 — breaking change 빈도, 유지보수 톤
7. **Sample / minimal repro** — 빈 프로젝트에 install → 작동·예상 동작 확인 (자체 격리 테스트)
8. **결정 기록** — `docs/dependencies-audit.md` 같은 파일에 도입 사유·소스·검증 결과 명시

## 패키지 추가 워크플로 (정식)

새 패키지 도입은 다음 순서로:

```
1. 사용자 요구 명확화 — 어떤 문제 해결? 우리 자체 구현 vs 외부 도입 비교
2. 6게이트 체크
3. 사용자 컨펌 — Tier, 라이선스, 빌드 사이즈 영향
4. Packages/manifest.json 변경
5. Packages/packages-lock.json 자동 갱신 → diff 리뷰
6. (Tier 4) docs/dependencies-audit.md 항목 추가
7. IL2CPP 빌드 한 번 (모바일 표적) — link.xml 필요한지 즉시 확인
8. perf-budget 차감 보고
```

## lockfile 리뷰 패턴

`Packages/packages-lock.json` 변경 시:

```bash
git diff Packages/packages-lock.json
```

확인할 것:
- **새로 추가된 의존**이 명시적인 것만인지 (transitive 의존이 갑자기 늘어남 = 위험)
- **버전 변경**이 의도한 것인지 (minor 또는 patch만 — major bump는 별도 PR)
- **`hash`** 변경 (signed package의 경우 변경 = 변조 가능성)
- **`url`** 변경 (registry 변경, 보통 의도하지 않음)

## scoped registry 운영 (예: OpenUPM)

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.cysharp.unitask"   // 정확한 스코프만, "com.*" 같은 광역 금지
      ]
    }
  ]
}
```

규칙:
- **정확한 패키지 이름** 또는 **정확한 조직 prefix** 만 scope에 등록 (`com.cysharp` OK, `com` ❌)
- registry url 변경 시 PR description에 명시
- 새 registry 추가는 사용자 컨펌

## 이 프로젝트와의 연결

### 현재 의존 감사 (사전 진단)

`Packages/manifest.json` 의 모든 `com.unity.*` 외 패키지에 대해 6게이트 통과 여부 확인:
- 오래된 unsigned 패키지 → 가능하면 Tier 1/2 대체
- 의존 트리 깊이 → 일부 의존은 새 패키지가 또 다른 외부 의존 끌고 옴

### 권장 첫 액션

1. `frontend/Packages/manifest.json` 1차 감사 — 각 의존이 어느 Tier인지 표로 정리, `docs/dependencies-audit.md` 신규 작성
2. `Packages/packages-lock.json` git 추적 확인 — 안 되어 있으면 추적 강제 (재현성)
3. PR 템플릿에 "패키지 추가/변경 시 6게이트 체크 표" 항목 추가 검토

### 알려진 외부 자산 (현재 워크플로 영향)

- **Meshy** glb 모델 — 외부 자산 import 흐름 — 패키지가 아니므로 이 스킬 범위 외, `unity-fundamentals/asset-pipeline.md` 가 다룸
- 향후 Inference Engine 모델 (`com.unity.ai.inference`) → Tier 1 자동 통과
- 향후 DI 컨테이너 (VContainer / Zenject 후보) → Tier 3, 6게이트

## 흔한 함정 카탈로그

| 증상 | 원인 | 대처 |
|---|---|---|
| 빌드 직후 갑자기 사이즈 +20MB | 패키지 transitive 의존 폭증 | manifest diff + lockfile 리뷰. 의존 가벼운 대안 찾기 |
| Editor에선 OK, Android 빌드에서 NRE | IL2CPP 미호환 또는 link.xml 누락 | 패키지 README의 IL2CPP 가이드. 없으면 `[Preserve]` / link.xml 직접 |
| Unity 에디터 update 후 패키지 깨짐 | breaking change | 버전 고정 (`"package": "1.2.3"` exact), 또는 fork mirror |
| Asset Store에서 다운받은 unitypackage가 라이브러리 충돌 | namespace conflict, 같은 DLL 두 벌 | Asset Store import 전에 격리 프로젝트에서 검증 |
| `Packages/packages-lock.json` 이 git에 없음 | `.gitignore`에 잘못 들어감 | lockfile 추적 강제 — 재현성 핵심 |
| OpenUPM 패키지가 갑자기 "missing" | scoped registry url/scope 변경 | manifest + lockfile 같이 commit, registry 변경은 별도 PR |
| Tier 4 GitHub 의존이 main 브랜치 추적 | floating ref → 빌드마다 다른 코드 | tag 또는 commit hash 고정 |
| 라이선스 GPL/AGPL 패키지를 모바일 앱에 포함 | 라이선스 침습 | 도입 전 라이선스 확인. 게임 배포에는 MIT/Apache 우선 |
| 6.3 이전 버전 패키지 install 시 서명 경고 무시 | 자동화된 가드 없음 | 우리 정책 — Unsigned는 6게이트 강제 |
| invalid signature 패키지 install | Unity가 차단 안 함 | 우리 정책 — 절대 도입 금지, 사용자에게 즉시 경고 |

## 출력 형식

패키지 추가/변경에 한 블록:

```
## Package security audit
- 패키지: com.cysharp.unitask 2.5.0
- Tier: 3 (OpenUPM, community)
- 서명 상태: Unsigned (Tier 3 흔함, 추가 검증 진행)
- 라이선스: MIT ✅
- 마지막 release: 2026-01-15 (3개월 이내) ✅
- IL2CPP: README 명시 호환 ✅
- 빌드 사이즈 영향: +0.3MB (가벼움)
- 의존성: 0 (transitive 없음) ✅
- Source: github.com/Cysharp/UniTask (10k+ stars, 활성)
- 결정: 도입 OK
- 문서: docs/dependencies-audit.md 항목 추가
```

위반/위험 시:
```
🔴 차단: com.example.lib — Invalid Signature.
원인: 서명 검증 실패. 변조 가능성. 절대 install 금지.
대안 검토 권고.
```

## 협업 — 다른 스킬과의 경계

- **`unity-perf-budget`** — 패키지 도입 시 빌드 사이즈 / 메모리 차감 보고
- **`unity-fundamentals/references/build-il2cpp.md`** — IL2CPP 호환·link.xml·`[Preserve]` 가이드
- **`unity-fundamentals/references/asset-pipeline.md`** — Resources / Addressables 관련 패키지 결정
- **`unity-coreclr-prep`** — 패키지 CoreCLR 호환성 검증 (target framework, Mono 의존 API)
- **`unity-layer-check`** — 새 패키지가 도메인 레이어에 새지 않는지 (대부분 Infrastructure에서만 사용)

## 출처

- Unity Manual — [Package signatures (6.3+)](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-signature.html)
- Unity Manual — [Package manifest reference](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-manifestPkg.html)
- Unity Manual — [Package Manager troubleshooting](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-errors.html)
- Unity Discussions — [Package Manager Changes — Package Signing and Status Labels](https://discussions.unity.com/t/package-manager-changes-package-signing-and-status-labels/1688660)
- Unity Discussions — [Package signature signing feedback](https://discussions.unity.com/t/package-signature-signing-feedback/1683364)
- Unity Discussions — [Package manager invalid signatures issue](https://discussions.unity.com/t/package-manager-invalid-signatures-issue/1705385)
- Unity Asset Store — [UPM Publishing](https://assetstore.unity.com/publishing/upm-publishing)
- Digital Production — [Unity's 2026 Roadmap: Verified Packages](https://digitalproduction.com/2025/11/26/unitys-2026-roadmap-coreclr-verified-packages-fewer-surprises/)
- OpenUPM — [openupm/openupm (GitHub)](https://github.com/openupm/openupm)
