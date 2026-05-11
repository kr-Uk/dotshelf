# DotShelf Launcher Agent Guide

이 디렉터리는 DotShelf Unity 앱을 설치, 업데이트, 실행하기 위한 Windows WPF 런처입니다.

## 기본 방향

- 런처는 DotShelf 모노레포(`S14P31F206`)의 `launcher/` 하위 모듈입니다. Unity(`frontend/`), 백엔드(`backend/`), 웹(`web/`), Apple(`apple/`)과 같은 레벨에서 독립적으로 빌드됩니다.
- 로그인, 프로필, 공지, 설정 화면은 현재 범위에 넣지 않습니다. 로그인·에셋 상점 등은 Unity 앱 내부에서 처리합니다.
- UI는 단일 앱용 PC 클라이언트 런처처럼 유지합니다.
  - 왼쪽: 라이브러리, DotShelf 앱 카드, 다운로드 대기 미니게임
  - 오른쪽: DotShelf 상세, 설치/업데이트/실행/삭제, 진행률, 작업 기록
- 빌드 산출물은 Git에 올리지 않습니다.
  - `dist/`
  - `bin/`
  - `obj/`

## 프로젝트 구조

- `DotShelfLauncher.slnx`: 솔루션 파일
- `src/DotShelf.Launcher`: WPF 데스크탑 UI
- `src/DotShelf.Launcher.Core`: manifest 조회, 다운로드, 검증, 압축 해제, 설치, 실행 로직
- `src/DotShelf.Launcher/Assets`: 앱 아이콘
- `manifests/dev/windows/latest.json`: 개발용 manifest 샘플
- `manifests/dev/windows/events.json`: 이벤트/업데이트 배너 샘플
- `docs/update-protocol.md`: 업데이트 manifest 규칙 설명
- `docs/versioning.md`: launcher / unity 두 버전 트랙 + bump 절차
- `scripts/bump-version.ps1`: 버전 bump 헬퍼 (`docs/versioning.md` 참고)

## 필수 환경

- Windows
- .NET 8 SDK

설치 확인:

```powershell
dotnet --version
```

## 로컬 개발 실행

모노레포 루트에서:

```powershell
cd launcher
dotnet run --project .\src\DotShelf.Launcher\DotShelf.Launcher.csproj
```

빌드만 확인:

```powershell
dotnet build .\DotShelfLauncher.slnx
```

## 배포용 exe 생성

```powershell
dotnet publish .\src\DotShelf.Launcher\DotShelf.Launcher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\dist\win-x64
```

실행 파일:

```text
.\dist\win-x64\DotShelfLauncher.exe
```

## 현재 업데이트 주소

운영 매니페스트 + 이벤트 배너 주소가 코드에 고정되어 있습니다.

```text
https://dotshelf.digital/launcher/windows/latest.json
https://dotshelf.digital/launcher/windows/events.json
```

위치는 다음 파일입니다.

```text
src/DotShelf.Launcher/MainWindow.xaml.cs
```

상수:

```csharp
private const string DefaultManifestUrl = "https://dotshelf.digital/launcher/windows/latest.json";
private const string DefaultEventsUrl   = "https://dotshelf.digital/launcher/windows/events.json";
```

prod 정적 호스팅 (2026-05-11 ~ Cloudflare R2 로 이전):
- 실제 파일: Cloudflare R2 버킷 `dotshelf-launcher`, 공개 URL `https://cdn.dotshelf.digital/windows/...`
- 업로드: `.gitlab-ci.yml` `launcher:publish` 잡에서 `aws s3 cp` (S3 호환 API)
- 기존 0.1.0 런처는 위 상수가 `dotshelf.digital` 로 하드코딩 → EC2 nginx 가 `/launcher/windows/*` 를 cdn.dotshelf.digital 로 `301 redirect`. 다음 런처 버전부터 위 상수를 `https://cdn.dotshelf.digital/windows/...` 로 갈아끼우고, 모든 사용자 교체 완료되면 EC2 redirect 블록 제거.

로컬에서 테스트하고 싶으면 잠깐 위 상수를 `http://127.0.0.1:8080/...` 으로 바꾸고 아래 "로컬 manifest 서버 실행" 절차를 따른다. 커밋 전 반드시 운영 URL 로 되돌릴 것.

## 로컬 manifest 서버 실행

런처 설치/업데이트를 로컬에서 테스트하려면 Unity 빌드 zip과 `latest.json`을 같은 폴더에 둔 뒤 HTTP 서버를 띄워야 합니다.

권장 테스트 폴더:

```text
C:\Users\SSAFY\Desktop\launcher-test
```

필요 파일:

```text
latest.json
events.json
Dotshelf-0.1.0.zip
```

서버 주소:

```text
http://127.0.0.1:8080/latest.json
```

Python이 정상 설치되어 있다면:

```powershell
cd C:\Users\SSAFY\Desktop\launcher-test
python -m http.server 8080
```

Windows Store Python alias만 있는 PC에서는 Python 서버가 바로 종료될 수 있습니다. 그 경우 PowerShell/.NET 기반 정적 파일 서버를 따로 띄우거나, Python을 설치합니다.

## Unity 빌드 + publish 자동화 (1-click)

운영용 Unity Windows 빌드 + 배포는 Unity Editor 메뉴 한 번 눌러 끝낸다.

1. Unity 프로젝트(`frontend/`)를 Unity Editor 로 연다.
2. 메뉴: **Tools → DotShelf → Build & Publish Launcher Package**
3. 윈도우에서 새 버전(기본값: 현재 manifest patch +1, 수정 가능)과 release notes(한 줄당 bullet 하나)를 입력하고 **Build & Publish** 클릭.

Editor 가 다음을 자동으로 수행:

- `BuildPipeline.BuildPlayer (StandaloneWindows64)` 로 `frontend/Build/Windows/Dotshelf.exe` 빌드
- `launcher/scripts/publish-unity.ps1` 호출 →
  - `bump-version.ps1 -Target unity -NewVersion <ver>` 로
    `frontend/ProjectSettings/ProjectSettings.asset` (bundleVersion),
    `launcher/manifests/dev/windows/latest.json` (latestVersion / packageUrl),
    `web/service-web/app/(marketing)/download/page.tsx` (version / date) 동기 갱신
  - `Build/Windows/*` 를 `launcher/dist/Dotshelf-<ver>.zip` 으로 압축 + SHA256 계산
  - `latest.json` 의 `sha256`, `releaseNotes` 필드 patch
  - GitLab Generic Packages (`dotshelf-unity/<ver>/`) 로 PUT 업로드 → HEAD 검증
  - `feat/launcher/unity-<ver>` 신규 브랜치 생성 → 변경 커밋 → push → GitLab MR 생성 (target = `dev`)

머지되면 CI 의 `launcher:publish` 잡이 Generic Packages 에서 zip 받아 sha256 검증 + prod 정적 호스트 (`/launcher/windows/`) 로 rsync + nginx reload 까지 자동.

manifest 예시 (편집 대상은 `latestVersion`, `mandatory`, `packageUrl`, `sha256`, `entrypoint`, `minLauncherVersion`, `releaseNotes`):

```json
{
  "latestVersion": "0.1.0",
  "mandatory": false,
  "packageUrl": "https://dotshelf.digital/launcher/windows/Dotshelf-0.1.0.zip",
  "sha256": "UNITY_BUILD_ZIP_SHA256",
  "entrypoint": "Dotshelf.exe",
  "minLauncherVersion": "0.1.0",
  "releaseNotes": [
    "Initial launcher-compatible Unity build."
  ]
}
```

Unity Product Name 이 `Dotshelf` 로 설정되어 있어 빌드 산출물은 `Dotshelf.exe` 다 (manifest 의 `entrypoint` 와 일치).

### 사전 조건

- Windows + .NET 8 SDK (`launcher` 빌드용) + Unity Editor (`frontend` 프로젝트)
- 환경변수 `GITLAB_TOKEN` (lab.ssafy.com PAT, scope=`api`, project Developer 이상) — `publish-unity.ps1` 가 GitLab API 호출에 사용
- **dev 브랜치 위 + clean working tree** — `publish-unity.ps1` 가 자동화 실행 전 강제 검증
  - master 브랜치 위에서 실행하면 정책상 거절 ("master 절대 건드리지 말 것")
  - feat/* 브랜치 위에서 실행하면 거절 (release commit 에 unrelated 변경 섞임 방지) — 작업 브랜치는 먼저 dev 로 머지 후 dev 위에서 메뉴 사용

### 실패 시 recovery

- **0~3 단계** (token/build/git pre-flight, bump-version, zip, manifest patch) 에서 실패: 외부 영향 없음. dirty file 만 git 으로 정리:
  ```powershell
  git checkout -- frontend/ProjectSettings/ProjectSettings.asset `
                  launcher/manifests/dev/windows/latest.json `
                  "web/service-web/app/(marketing)/download/page.tsx"
  ```
- **4 단계 (PUT) 이후 실패**: GitLab Generic Packages 에 zip 이 이미 올라간 상태. PUT 은 idempotent (같은 버전 덮어쓰기) 라 그대로 재시도 OK.
- **6 단계 (commit/push/MR) 중 실패**: 로컬에 `feat/launcher/unity-<ver>` 브랜치가 남음. 정리 후 재시도:
  ```powershell
  git checkout dev
  git branch -D feat/launcher/unity-<ver>
  # 원격에 push 까지 갔으면 GitLab 에서 해당 브랜치/MR 도 같이 정리
  ```

### 수동 fallback

자동화가 실패하면 다음 순서로:

```powershell
# 1) 버전/링크 동기 갱신
.\launcher\scripts\bump-version.ps1 -Target unity -NewVersion 0.1.1

# 2) Unity Editor → File > Build Settings > Build (Windows64) — frontend\Build\Windows\

# 3) zip + sha256
Compress-Archive -Path .\frontend\Build\Windows\* -DestinationPath .\launcher\dist\Dotshelf-0.1.1.zip
(Get-FileHash -Algorithm SHA256 -Path .\launcher\dist\Dotshelf-0.1.1.zip).Hash

# 4) launcher/manifests/dev/windows/latest.json 의 sha256, releaseNotes 손편집

# 5) Generic Packages 업로드
curl --request PUT `
  --header "PRIVATE-TOKEN: $env:GITLAB_TOKEN" `
  --upload-file .\launcher\dist\Dotshelf-0.1.1.zip `
  "https://lab.ssafy.com/api/v4/projects/1304436/packages/generic/dotshelf-unity/0.1.1/Dotshelf-0.1.1.zip"

# 6) feat/launcher/unity-0.1.1 브랜치로 변경 커밋 + dev 타깃 MR
```

zip 을 다시 만들면 SHA256 도 반드시 다시 계산한다.

## 설치 동작

런처는 manifest와 로컬 설치 상태를 비교해 버튼을 보여줍니다.

- 로컬 실행 파일 없음: `설치하기`
- 로컬 버전과 manifest 버전 다름: `업데이트`
- 로컬 버전과 manifest 버전 같음: `시작하기`, `삭제하기`

설치 기준 위치는 사용자가 `위치 선택`으로 고를 수 있습니다.

런처 설정 저장 위치:

```text
%LOCALAPPDATA%\DotShelfLauncher\settings.json
```

설치 상태 파일은 선택한 설치 기준 위치 아래에 저장됩니다.

```text
{InstallRoot}\install-state.json
{InstallRoot}\versions\{version}\...
```

삭제 기능은 `versions` 디렉터리 내부만 삭제하도록 제한되어 있습니다.

## UI 작업 주의사항

- 메인 화면은 앱 실행/설치가 최우선입니다.
- 미니게임은 보조 콘텐츠입니다. 앱 설치/실행 UI보다 시각적으로 강하면 안 됩니다.
- 작업 기록은 설치 중 실시간으로 아래에 쌓이고 항상 맨 아래로 스크롤되어야 합니다.
- 설치 진행률은 실제 전체 작업 기준입니다.
  - 다운로드
  - 무결성 검사
  - 압축 해제
  - 파일 복사
  - 설치 정보 저장
- 버튼 텍스트가 잘리지 않게 폭과 padding을 확인합니다.

## Git 규칙

- `main` 브랜치를 사용합니다.
- 기능 작업은 필요하면 별도 브랜치를 만들고 PR로 합칩니다.
- `dist/`, `bin/`, `obj/`는 커밋하지 않습니다.
- 로컬 테스트용 Unity zip도 커밋하지 않습니다.
- 변경 후 최소 검증:

```powershell
dotnet build .\DotShelfLauncher.slnx
```

배포 exe까지 확인해야 하면 publish 명령도 실행합니다.
