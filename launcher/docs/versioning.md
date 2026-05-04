# DotShelf 버전 체계

런처 + Unity 본체 + 매니페스트 + 다운로드 페이지가 같은 버전을 공유하는 게 아니라, **두 개의 독립된 버전 트랙**을 가진다.

## 두 개의 트랙

| 트랙 | 의미 | 무엇이 바뀌면 bump? |
|---|---|---|
| **launcher** | `DotShelfLauncher.exe` 자체 (.NET WPF) | 런처 코드 (`launcher/src/**`) 변경 |
| **unity** | 책장 본체 빌드 (`Dotshelf.exe` + Unity assets) | Unity 프로젝트 (`frontend/**`) 변경 |

런처는 자주 안 바뀌고 Unity 본체는 매 sprint 바뀌므로 같이 묶지 않는다.

## Single source of truth

| 트랙 | source of truth (1곳) | 흘러가는 곳 |
|---|---|---|
| launcher | `launcher/src/DotShelf.Launcher/DotShelf.Launcher.csproj` 의 `<Version>` | `.exe` AssemblyVersion (자동) · 다운로드 URL · `latest.json.minLauncherVersion` (필요 시) |
| unity | `frontend/ProjectSettings/ProjectSettings.asset` 의 `bundleVersion` | Unity 빌드 메타 (자동) · zip 파일명 · `latest.json.latestVersion` · service-web `RELEASE.version` |

**원칙: 다른 곳에 손으로 적힌 버전 문자열은 다 source of truth 의 미러일 뿐이다.** 이걸 강제하려고 bump 헬퍼 스크립트가 있다 (아래).

## 파일명 규칙 (versioned filename)

Cloudflare CDN 캐시(4h edge cache)를 우회하려고 **변하는 산출물에는 버전을 박는다**.

```
https://dotshelf.digital/launcher/windows/
├── DotShelfLauncher-{launcherVersion}.exe   ← 버전 박힘
├── Dotshelf-{unityVersion}.zip              ← 버전 박힘
├── latest.json                              ← 그대로 (항상 "최신"을 의미)
└── events.json                              ← 그대로 (이벤트 배너)
```

`latest.json` / `events.json` 은 origin 캐시 60s + Cloudflare 짧은 TTL 이라 갱신 즉시 반영된다.

새 런처 / 새 본체 = 새 파일명 = CDN 입장에서 새 객체 = 첫 요청부터 origin fetch.

## 버전 bump 절차

### 런처 0.1.0 → 0.1.1 (런처 코드만 변경)

```powershell
.\launcher\scripts\bump-version.ps1 -Target launcher -NewVersion 0.1.1
```

자동으로:
- `DotShelf.Launcher.csproj` 의 `<Version>`, `<AssemblyVersion>`, `<FileVersion>` 갱신
- `web/service-web/.../page.tsx` 의 `LAUNCHER_WIN_URL` 을 새 파일명으로

후속 작업 (수동):
- `dotnet publish` 재빌드
- 새 파일명으로 SCP 업로드 (옛 파일은 살려두면 옛 다운로드 링크 누른 사람이 안전하게 옛 버전 받음 — 한 minor 정도 후 정리)

### Unity 본체 0.1.0 → 0.2.0 (Unity 프로젝트 변경)

```powershell
.\launcher\scripts\bump-version.ps1 -Target unity -NewVersion 0.2.0
```

자동으로:
- `frontend/ProjectSettings/ProjectSettings.asset` 의 `bundleVersion` 갱신
- `launcher/manifests/dev/windows/latest.json` 의 `latestVersion`, `packageUrl` (zip 파일명 versioned) 갱신
- `web/service-web/.../page.tsx` 의 `RELEASE.version`, `RELEASE.date` 갱신

후속 작업 (수동):
- Unity Editor 에서 빌드
- zip 압축 → SHA-256 계산
- `latest.json` 의 `sha256` 갱신
- zip + 새 latest.json SCP 업로드

## minLauncherVersion 게이트

`latest.json.minLauncherVersion` 은 "이 본체를 쓰려면 런처가 최소 이 버전 이상이어야 함" 의미. 호환성 깨질 때만 bump 한다 (예: launcher Core 의 `EventsManifest` 스키마 v2 도입). 평소엔 안 건드린다.

현재 코드는 이 값을 표시만 하고 강제 차단 로직은 없다 (TODO).

## Git tag 컨벤션 (제안 — 미시행)

릴리스 시 태그:
- `launcher-v0.1.1` — 런처 publish 시점
- `unity-v0.2.0` — Unity 본체 zip 업로드 시점

태그가 가리키는 commit 은 해당 산출물이 만들어진 코드 상태다.

## 절대 하지 말 것

- 한 PR 에서 launcher + unity 동시 bump (두 트랙 분리 의도가 무너짐)
- bump 헬퍼 거치지 않고 한 곳만 손으로 바꾸기 (다른 곳 미러가 어긋남)
- bare `DotShelfLauncher.exe` (버전 없는) 다운로드 링크 외부 노출 (캐시 충돌)
