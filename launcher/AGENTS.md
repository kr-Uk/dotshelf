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

prod 정적 호스팅:
- 호스트 디렉터리: `/opt/dotshelf/launcher/windows/` (prod EC2 `3.38.240.110`)
- 컨테이너 마운트: `/var/www/dotshelf-launcher/`
- nginx location: `/launcher/` → `default.conf` 의 `dotshelf.digital` server block 참고

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

## Unity 빌드 zip 규칙

Unity Windows 빌드를 zip으로 묶고, manifest의 `packageUrl`, `sha256`, `entrypoint`를 맞춥니다.

Unity Product Name이 `Dotshelf`로 설정되어 있어 빌드 산출물은 `Dotshelf.exe`입니다 (manifest의 `entrypoint`와 일치).

manifest 예시:

```json
{
  "latestVersion": "0.1.0",
  "mandatory": false,
  "packageUrl": "http://127.0.0.1:8080/Dotshelf-0.1.0.zip",
  "sha256": "UNITY_BUILD_ZIP_SHA256",
  "entrypoint": "Dotshelf.exe",
  "minLauncherVersion": "0.1.0",
  "releaseNotes": [
    "Initial launcher-compatible Unity build."
  ]
}
```

SHA256 계산:

```powershell
(Get-FileHash -Algorithm SHA256 -Path .\Dotshelf-0.1.0.zip).Hash
```

zip을 다시 만들면 SHA256도 반드시 다시 계산합니다.

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
