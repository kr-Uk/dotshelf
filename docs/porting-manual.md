# DotShelf 로컬 포팅 메뉴얼

작성 기준: 2026-05-20

이 문서는 새 개발자가 이 저장소를 받은 뒤 DotShelf 백엔드, PostgreSQL, Redis, Unity 클라이언트, 웹 모듈, Windows 런처를 로컬에서 실행하기 위한 절차를 정리한다. 운영 서버 배포 절차가 아니라 로컬 개발/검증 환경 재현을 목표로 한다.

## 1. 전체 구성

| 경로 | 역할 | 주 실행 환경 |
| --- | --- | --- |
| `backend/` | Spring Boot API 서버, Flyway DB migration, PostgreSQL/Redis local compose | JDK 21, Docker |
| `frontend/` | Unity 6 기반 DotShelf 클라이언트 | Unity Editor 6000.3.13f1 |
| `web/admin-web/` | 운영자 백오피스 Next.js 앱 | Node.js 22 권장 |
| `web/service-web/` | 공개 랜딩/다운로드 Next.js 앱 | Node.js 22 권장 |
| `launcher/` | Windows Unity 앱 설치/업데이트 런처 | .NET 8 SDK, Windows |
| `apple/` | RealityKit/Metal 프로토타입 Xcode 프로젝트 | macOS, Xcode |
| `fantasy/`, `space/`, `spooky/` | 테마 GLB/PNG 원본 자산 | Unity import/reference asset |
| `admin-web/`, `creator-web/` | 루트 placeholder 디렉터리. 현재 실제 웹 앱은 `web/*` 아래에 있음 | 포팅 필수 아님 |
| `json/` | OAuth client JSON 등 민감 설정 후보 | 비밀 파일 취급 |
| `tmp/`, `.codex-pet-runs/` | 작업 중 생성된 임시 산출물 | 포팅 필수 아님 |

현재 로컬 전체 실행의 기본 순서는 다음이다.

1. Docker Desktop 실행
2. `backend/docker-compose.yml`로 PostgreSQL/Redis 실행
3. `backend/.env.example`을 복사해 `backend/.env.local` 작성
4. Spring Boot 백엔드 실행
5. `/health`, Swagger 확인
6. 필요한 경우 seed SQL 적용
7. Unity 프로젝트를 열고 API URL을 로컬 백엔드로 맞춘 뒤 Play
8. 필요하면 `admin-web`, `service-web`, `launcher`를 별도로 실행

## 2. 사전 설치

| 도구 | 권장 버전 | 확인 명령 |
| --- | --- | --- |
| Git | 최신 안정 버전 | `git --version` |
| Docker Desktop | Compose v2 포함 | `docker version`, `docker compose version` |
| JDK | 21 | `java -version` |
| Unity Hub | 최신 | GUI 확인 |
| Unity Editor | `6000.3.13f1` | `frontend/ProjectSettings/ProjectVersion.txt` |
| Node.js | 22 LTS 권장 | `node --version` |
| npm | Node와 함께 설치 | `npm --version` |
| .NET SDK | 8.x | `dotnet --version` |
| PowerShell | Windows 기본 또는 7.x | `$PSVersionTable.PSVersion` |

주의:

- Windows 기준 경로는 `C:\SSAFY\S14P31F206`을 사용한다.
- Unity는 정확히 `6000.3.13f1`로 여는 것을 권장한다. 버전이 다르면 패키지 복원, 씬 serialization, URP 설정이 달라질 수 있다.
- `backend/.env.local`, OAuth client secret, S3 key, Inicis sign key는 절대 커밋하지 않는다.

## 3. Git과 제외 파일

루트와 하위 프로젝트의 `.gitignore`가 Unity/Gradle/IDE 산출물을 제외한다. 로컬 포팅 중 생성되는 아래 파일은 커밋 대상이 아니다.

```text
frontend/Library/
frontend/Temp/
frontend/Obj/
frontend/Build/
frontend/Builds/
frontend/Logs/
frontend/UserSettings/
frontend/*.csproj
frontend/*.slnx
backend/.env
backend/.env.local
backend/build/
web/**/.next/
web/**/node_modules/
launcher/bin/
launcher/obj/
launcher/dist/
tmp/
.codex-pet-runs/
```

현재 저장소에는 미추적 자산/문서/임시 폴더가 있을 수 있다. 포팅 메뉴얼 작업이나 로컬 실행 중 이 파일들을 삭제하거나 되돌리지 않는다.

## 4. 백엔드와 DB/Redis

### 4-1. 백엔드 스택

| 항목 | 값 |
| --- | --- |
| Framework | Spring Boot `4.0.5` |
| Language | Java 21 |
| Build | Gradle Kotlin DSL |
| DB | PostgreSQL 17 |
| Cache | Redis 7.4 |
| Migration | Flyway, `backend/src/main/resources/db/migration` |
| API port | `8080` |
| Local profile | `local` |
| Health | `GET http://localhost:8080/health` |
| Swagger | `http://localhost:8080/swagger-ui.html` |
| OpenAPI JSON | `http://localhost:8080/v3/api-docs` |

### 4-2. DB/Redis 실행

```powershell
cd C:\SSAFY\S14P31F206\backend
docker compose up -d
docker compose ps
```

`backend/docker-compose.yml`은 다음 컨테이너를 띄운다.

| 컨테이너 | 이미지 | 포트 | 계정/DB |
| --- | --- | --- | --- |
| `dotshelf-postgres` | `postgres:17` | `5432:5432` | DB `dotshelf`, user `dotshelf`, password `dotshelf` |
| `dotshelf-redis` | `redis:7.4` | `6379:6379` | password 없음 |

컨테이너 로그 확인:

```powershell
cd C:\SSAFY\S14P31F206\backend
docker compose logs -f postgres
docker compose logs -f redis
```

DB 접속 확인:

```powershell
docker exec -it dotshelf-postgres psql -U dotshelf -d dotshelf
```

Redis 확인:

```powershell
docker exec -it dotshelf-redis redis-cli ping
```

정상 응답은 `PONG`이다.

DB를 완전히 초기화해야 할 때만 아래 명령을 사용한다. 모든 로컬 DB 데이터가 삭제된다.

```powershell
cd C:\SSAFY\S14P31F206\backend
docker compose down -v
docker compose up -d
```

### 4-3. 백엔드 환경변수

로컬 백엔드는 `application.yml`에서 아래 파일을 자동으로 읽는다.

```text
backend/.env
backend/.env.local
```

기본 작성 절차:

```powershell
cd C:\SSAFY\S14P31F206\backend
if (-not (Test-Path .env.local)) {
  Copy-Item .env.example .env.local
}
```

`backend/.env.local`의 필수/권장 값은 아래처럼 나눈다.

| 변수 | 로컬 부팅 필수 | 설명 |
| --- | --- | --- |
| `JWT_SECRET` | 예 | 최소 32바이트 이상의 JWT 서명 문자열 |
| `JWT_ACCESS_TTL` | 아니오 | 기본 `1800`초 |
| `JWT_REFRESH_TTL` | 아니오 | 기본 `2592000`초 |
| `S3_BUCKET` | 예 | `S3StorageProperties`에서 `@NotBlank`; 실제 업로드를 안 해도 placeholder 필요 |
| `S3_REGION` | 예 | 기본 `ap-northeast-2` |
| `S3_ACCESS_KEY` | 기능별 | S3 업로드 테스트 시 필요 |
| `S3_SECRET_KEY` | 기능별 | S3 업로드 테스트 시 필요 |
| `S3_PUBLIC_BASE_URL` | 기능별 | 클라이언트가 GET으로 접근할 공개 URL |
| `GOOGLE_*` | 기능별 | Google 로그인 테스트 시 필요 |
| `KAKAO_*` | 기능별 | Kakao 로그인 테스트 시 필요 |
| `APPLE_*` | 기능별 | Apple 로그인 테스트 시 필요 |
| `INICIS_*` | 기능별 | 결제 테스트 시 필요 |
| `ADMIN_TEST_AUTH_ENABLED` | 선택 | 로컬 admin API 테스트용 header 인증 fallback |
| `ADMIN_TEST_AUTH_HEADER_NAME` | 선택 | 기본 `X-Test-Admin-User-Id` |

소셜 로그인, S3, 결제를 테스트하지 않고 서버 부팅과 일반 API 구조만 확인하려면 최소 예시는 다음과 같다.

```properties
JWT_SECRET=local-dev-secret-change-me-32bytes-minimum-length
JWT_ACCESS_TTL=1800
JWT_REFRESH_TTL=2592000

S3_BUCKET=local-placeholder-not-real
S3_REGION=ap-northeast-2
S3_ACCESS_KEY=
S3_SECRET_KEY=
S3_PUBLIC_BASE_URL=https://local-placeholder-not-real.s3.ap-northeast-2.amazonaws.com
S3_PRESIGN_TTL=300
S3_PRESIGN_BUNDLE_TTL=1800

ADMIN_TEST_AUTH_ENABLED=true
ADMIN_TEST_AUTH_HEADER_NAME=X-Test-Admin-User-Id
```

주의:

- `S3_BUCKET`은 빈 값이면 백엔드 부팅이 실패한다.
- S3 placeholder로 부팅은 가능하지만 실제 업로드/다운로드 API는 실패할 수 있다.
- `ADMIN_TEST_AUTH_ENABLED=true`는 로컬 테스트 편의용이다. 운영/배포 환경에는 쓰지 않는다.
- 실제 OAuth secret, Apple private key, Inicis sign key, AWS key를 문서나 MR 본문에 붙이지 않는다.

### 4-4. 백엔드 실행

```powershell
cd C:\SSAFY\S14P31F206\backend
.\gradlew.bat bootRun
```

Spring profile은 기본값이 `local`이다. `application-local.yml`은 다음 값을 사용한다.

```yaml
spring:
  datasource:
    url: jdbc:postgresql://localhost:5432/dotshelf
    username: dotshelf
    password: dotshelf
  flyway:
    enabled: true
  jpa:
    hibernate:
      ddl-auto: validate
  data:
    redis:
      host: localhost
      port: 6379
```

부팅 시 Flyway가 `backend/src/main/resources/db/migration`의 migration을 적용한다. 현재 migration은 `V1__init_schema.sql`부터 `V31__add_space_notebook_inventory_column.sql`까지 있다.

### 4-5. 백엔드 확인

다른 PowerShell 창에서 확인한다.

```powershell
Invoke-RestMethod http://localhost:8080/health
```

정상 상태:

- HTTP 200
- 응답 `data.status`가 `UP`
- DB/Redis health component가 `UP`

Swagger UI:

```text
http://localhost:8080/swagger-ui.html
```

OpenAPI JSON:

```text
http://localhost:8080/v3/api-docs
```

게스트 세션 API smoke test:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:8080/v1/auth/guest-session
```

### 4-6. Mock seed 데이터 적용

빈 DB에서 Unity/API 연동 테스트용 기본 데이터를 넣고 싶으면 `backend/scripts/seed-mock-data.sql`을 적용한다.

```powershell
cd C:\SSAFY\S14P31F206\backend
Get-Content .\scripts\seed-mock-data.sql | docker exec -i dotshelf-postgres psql -U dotshelf -d dotshelf
```

seed 데이터 개요:

- 사용자 4명: `1001`, `1002`, `1003`, `1004`
- `1004 dotshelf.admin`: admin-web 검증용 계정
- 도서 6권, 사용자 책 9건, 리뷰 9건
- 공간 4개, 책장 4개, 책장 아이템 9건
- 에셋 카테고리, 인벤토리, 구매 이력, 신고/알림/피드 이벤트 포함

admin-web을 실제 백엔드에 붙여 테스트할 때는 `backend/.env.local`에 아래 값을 넣고 seed를 적용한 뒤 `1004`를 테스트 관리자 ID로 쓰는 흐름이 편하다.

```properties
ADMIN_TEST_AUTH_ENABLED=true
ADMIN_TEST_AUTH_HEADER_NAME=X-Test-Admin-User-Id
```

### 4-7. 백엔드 테스트/빌드

```powershell
cd C:\SSAFY\S14P31F206\backend
.\gradlew.bat test
.\gradlew.bat build
```

참고:

- `check`는 `jacocoTestCoverageVerification`에 의존한다.
- coverage 기준은 line covered ratio `0.85`다.
- 일부 PostgreSQL/Testcontainers 테스트는 시스템 속성 `runPostgresTests`에 따라 분기될 수 있다.

## 5. Unity 클라이언트

### 5-1. Unity 프로젝트 정보

| 항목 | 값 |
| --- | --- |
| 경로 | `frontend/` |
| Unity Editor | `6000.3.13f1` |
| Render Pipeline | URP `17.3.0` |
| Input | Input System `1.19.0` |
| 주요 패키지 | Cinemachine, Addressables, UniTask, Newtonsoft Json, glTFast, FBX Exporter |
| API config | `frontend/Assets/App/Config/AppApiConfig.asset` |
| API 기본값 | `https://dev-api.dotshelf.digital` |

Unity Hub에서 열 경로:

```text
C:\SSAFY\S14P31F206\frontend
```

처음 열 때는 패키지 복원과 스크립트 컴파일이 끝날 때까지 기다린다.

### 5-2. 현재 Build Settings 씬

`frontend/ProjectSettings/EditorBuildSettings.asset` 기준 등록 씬은 다음이다.

| 순서 | 씬 |
| ---: | --- |
| 0 | `Assets/Scenes/StartScene.unity` |
| 1 | `Assets/Scenes/LoginScene.unity` |
| 2 | `Assets/Scenes/BootstrapScene.unity` |
| 3 | `Assets/Scenes/MainRoomScene.unity` |
| 4 | `Assets/Scenes/NotebookUIScene.unity` |

기존 `frontend/README.md`에는 `MainScene` 언급이 남아 있지만, 현재 프로젝트 파일 기준으로는 위 씬 목록을 우선한다.

권장 로컬 실행:

1. Unity Hub에서 `frontend/` 열기
2. Console compile error가 0개인지 확인
3. `Assets/Scenes/StartScene.unity` 또는 `Assets/Scenes/MainRoomScene.unity` 열기
4. Play 실행
5. 공간/책장 콘텐츠가 비어 있으면 `Tools > Project Setup > Build MainRoomScene Content` 실행

유용한 Editor 메뉴:

| 메뉴 | 목적 |
| --- | --- |
| `Tools > Project Setup > Create Folder Structure` | 기본 폴더 구조 생성 |
| `Tools > Project Setup > Run Phase 0-1 Setup` | 초기 scene/bootstrap 구조 재생성 |
| `Tools > Project Setup > Build MainRoomScene Content` | MainRoomScene 콘텐츠 생성 |
| `Dotshelf > Refactor > Validate Scene Split` | scene split 검증 |
| `Dotshelf > Notebook > Validate Dashboard Layouts` | 노트북 대시보드 레이아웃 검증 |
| `Tools > DotShelf > Build & Publish Launcher Package` | Unity Windows 빌드/런처 패키징 자동화 |

### 5-3. Unity 로컬 백엔드 연결

현재 Unity API 기본 URL은 배포 개발 서버다.

```text
https://dev-api.dotshelf.digital
```

로컬 백엔드(`http://localhost:8080`)에 붙이려면 Unity Inspector에서 아래 asset을 수정한다.

```text
frontend/Assets/App/Config/AppApiConfig.asset
```

권장 값:

```yaml
apiMode: RealApi
devBaseUrl: http://localhost:8080
prodBaseUrl: https://dev-api.dotshelf.digital
healthCheckPath: /health
requestTimeoutSeconds: 10
enableVerboseLog: true
```

주의:

- `AppApiConfig.asset`은 추적 대상 asset이다. 로컬 테스트 후 commit에 포함하지 않도록 확인한다.
- 일부 레거시 코드가 `AppApiConfig`가 아니라 `ApiEndpointDefaults.DefaultBaseUrl`을 직접 참조한다. 로그에 계속 `https://dev-api.dotshelf.digital`이 보이면 `frontend/Assets/App/Domain/ApiEndpointDefaults.cs`의 기본값을 임시로 `http://localhost:8080`으로 바꾸는 로컬 패치가 필요할 수 있다. 이 임시 패치는 커밋하지 않는다.
- 보호 API를 완전히 테스트하려면 Unity 로그인으로 발급된 DotShelf JWT가 필요하다. `DOTSHELF_ACCESS_TOKEN` 환경변수를 쓰면 `AppApiConfig.ResolveAccessToken()`이 해당 토큰을 우선 사용한다.

PowerShell에서 Unity Editor를 띄우기 전에 임시 access token을 넣는 예:

```powershell
$env:DOTSHELF_ACCESS_TOKEN = "eyJ..."
```

### 5-4. Unity 소셜 로그인 설정

공개 설정 문서:

```text
frontend/SOCIAL_LOGIN_SETUP.md
frontend/ANDROID_DEBUG_KEYSTORE.md
```

private 문서:

```text
frontend/SOCIAL_LOGIN_SETUP_PRIVATE.md
```

private 문서는 `.gitignore`에 포함되어 있으며, 실제 secret이 들어갈 수 있으므로 외부 공유/커밋 금지다.

Android 개발 keystore:

| 항목 | 값 |
| --- | --- |
| 경로 | `frontend/keystores/dotshelf-debug.keystore` |
| Alias | `dotshelfdebug` |
| Keystore password | `android` |
| Key password | `android` |
| SHA-1 | `40:BB:69:92:B3:77:19:1F:99:6F:37:08:0B:E5:F4:92:4C:FC:D1:7E` |
| Kakao key hash | `QLtpkrN3GR+ZbzcIC+X0kkz80X4=` |

Unity 설정 위치:

```text
Edit > Project Settings > Player > Android > Publishing Settings
```

설정:

- `Custom Keystore` 체크
- Keystore: `frontend/keystores/dotshelf-debug.keystore`
- Alias: `dotshelfdebug`
- Password: `android`

Desktop OAuth callback:

| Provider | 로컬 callback |
| --- | --- |
| Google | `http://127.0.0.1:{동적 포트}/dotshelf/oauth/callback/` |
| Kakao | `http://127.0.0.1:18080/dotshelf/oauth/callback/` 또는 코드에서 선택된 loopback URI |
| Apple Desktop relay | 백엔드 `/v1/auth/apple/desktop-callback` → Unity loopback redirect |

백엔드 `.env.local`의 OAuth client id/secret과 Unity 쪽 client id가 일치해야 한다.

### 5-5. Unity 패키지와 구조

주요 패키지는 `frontend/Packages/manifest.json` 기준이다.

| Package | Version | 목적 |
| --- | --- | --- |
| `com.unity.render-pipelines.universal` | `17.3.0` | URP |
| `com.unity.cinemachine` | `2.10.7` | 카메라 |
| `com.unity.inputsystem` | `1.19.0` | 입력 |
| `com.unity.addressables` | `2.10.1` | 에셋 로딩 경계 |
| `com.cysharp.unitask` | `2.5.10` | async |
| `com.unity.nuget.newtonsoft-json` | `3.2.2` | JSON |
| `com.unity.formats.fbx` | `5.1.5` | FBX |
| `com.unity.cloud.gltfast` | `6.18.0` | glTF/GLB |
| `com.unity.test-framework` | `1.6.0` | Unity Test |

코드 구조:

```text
frontend/Assets/App/
  Bootstrap/
  Config/
  Core/
  Domain/
  Application/
  Infrastructure/
  Features/
  Platform/
  UI/
  Editor/
  Tests/

frontend/Assets/Scripts/BookshelfPorting/Runtime/
  BookshelfGenerator.cs
  BookshelfExperienceManager.cs
  BookshelfApiSyncController.cs
  BookInteractionController.cs
  CameraController.cs
  NotebookScreenController.cs
  ...
```

개발 규칙:

- 새 기능은 가능한 한 `Assets/App` 구조에 둔다.
- `Assets/App/Domain`에는 `UnityEngine`, `MonoBehaviour`, `GameObject`, `Vector3` 의존을 넣지 않는다.
- API 호출은 `Infrastructure` 또는 Application/UseCase 경계를 통해 연결한다.
- 책 배치/드래그는 물리 기반이 아니라 slot 기반을 유지한다.

### 5-6. Unity 검증

최소 검증:

- Console compile error 0개
- `StartScene` 또는 `MainRoomScene` Play 가능
- API 로그가 의도한 base URL을 가리킴
- `Tools > Project Setup > Build MainRoomScene Content` 실행 후 책장/방 콘텐츠 표시
- 로그인/백엔드 연동 테스트 시 백엔드 `/health`가 `UP`

Unity Test Runner를 쓰는 경우:

```text
Window > General > Test Runner
```

EditMode 테스트 assembly:

```text
frontend/App.Tests.EditMode.csproj
frontend/Assets/App/Tests
```

## 6. 웹 모듈

### 6-1. admin-web

경로:

```text
C:\SSAFY\S14P31F206\web\admin-web
```

기술:

- Next.js `16.2.4`
- React `19.2.4`
- Tailwind v4
- MSW mock 지원
- 운영 빌드 `output: "standalone"`

설치:

```powershell
cd C:\SSAFY\S14P31F206\web\admin-web
npm install
```

Mock 모드 실행:

```powershell
$env:NEXT_PUBLIC_USE_MOCKS = "true"
npm run dev
```

접속:

```text
http://localhost:3001
```

Mock 로그인 흐름:

```text
/login -> 사내 SSO 버튼 -> /mfa -> 6자리 입력 -> /dashboard
```

실제 로컬 백엔드에 연결하려면 `.env.local`을 만들고 다음 값을 둔다.

```properties
NEXT_PUBLIC_USE_MOCKS=false
NEXT_PUBLIC_ADMIN_API_BASE_URL=/api/admin
ADMIN_API_REWRITE_BASE_URL=http://localhost:8080/v1/admin
NEXT_PUBLIC_ADMIN_TEST_AUTH_HEADER_NAME=X-Test-Admin-User-Id
NEXT_PUBLIC_ADMIN_TEST_USER_ID=1004
```

백엔드에도 아래 값이 필요하다.

```properties
ADMIN_TEST_AUTH_ENABLED=true
ADMIN_TEST_AUTH_HEADER_NAME=X-Test-Admin-User-Id
```

그리고 `backend/scripts/seed-mock-data.sql`이 적용되어 있어야 `1004` 관리자 계정 기준 테스트가 쉽다.

검증:

```powershell
npm run lint
npm run build
```

### 6-2. service-web

경로:

```text
C:\SSAFY\S14P31F206\web\service-web
```

기술:

- Next.js `16.2.4`
- React `19.2.4`
- Tailwind v4
- 공개 랜딩/다운로드 페이지
- 운영 빌드 `output: "standalone"`

설치:

```powershell
cd C:\SSAFY\S14P31F206\web\service-web
npm install
```

환경변수 파일:

```powershell
if (-not (Test-Path .env.local)) {
  Copy-Item .env.example .env.local
}
```

주요 환경변수:

| 변수 | 설명 |
| --- | --- |
| `NEXT_PUBLIC_USE_MOCKS` | `true`면 MSW mock 활성화 |
| `NEXT_PUBLIC_MERCHANT_NAME` | 사업자명 |
| `NEXT_PUBLIC_MERCHANT_REGISTRATION_NUMBER` | 사업자등록번호 |
| `NEXT_PUBLIC_MERCHANT_REPRESENTATIVE` | 대표자 |
| `NEXT_PUBLIC_MERCHANT_ADDRESS` | 주소 |
| `NEXT_PUBLIC_MERCHANT_TEL` | 전화번호 |
| `NEXT_PUBLIC_MERCHANT_EMAIL` | 이메일 |
| `NEXT_PUBLIC_COMPLAINT_CONTACT_NAME` | 민원 담당자 |
| `NEXT_PUBLIC_COMPLAINT_CONTACT_TEL` | 민원 연락처 |
| `NEXT_PUBLIC_RESPONSIBILITY_NOTICE` | 환금성 책임 문구 |
| `NEXT_PUBLIC_SITE_HOST` | footer 표시용 host |

PowerShell에서 mock 모드로 실행:

```powershell
$env:NEXT_PUBLIC_USE_MOCKS = "true"
npx next dev
```

Git Bash/WSL에서는 package script를 그대로 써도 된다.

```bash
npm run dev
```

접속:

```text
http://localhost:3000
```

검증:

```powershell
npm run lint
npm run build
```

참고:

- `service-web/package.json`의 `dev` script는 `NEXT_PUBLIC_USE_MOCKS=true next dev` 형식이다.
- 이 형식은 Windows PowerShell/cmd에서 실패할 수 있으므로 PowerShell에서는 환경변수를 따로 설정한 뒤 `npx next dev`를 쓰는 편이 안전하다.

## 7. Windows 런처

### 7-1. 런처 정보

경로:

```text
C:\SSAFY\S14P31F206\launcher
```

구성:

```text
launcher/
  DotShelfLauncher.slnx
  src/DotShelf.Launcher/          # WPF UI
  src/DotShelf.Launcher.Core/     # manifest/download/install/update core
  manifests/dev/windows/latest.json
  manifests/dev/windows/events.json
  docs/versioning.md
  docs/update-protocol.md
  scripts/bump-version.ps1
  scripts/publish-unity.ps1
```

요구 환경:

- Windows
- .NET 8 SDK

### 7-2. 실행/빌드

실행:

```powershell
cd C:\SSAFY\S14P31F206\launcher
dotnet run --project .\src\DotShelf.Launcher\DotShelf.Launcher.csproj
```

빌드:

```powershell
cd C:\SSAFY\S14P31F206\launcher
dotnet build .\src\DotShelf.Launcher\DotShelf.Launcher.csproj
```

배포용 단일 exe publish:

```powershell
cd C:\SSAFY\S14P31F206\launcher
dotnet publish .\src\DotShelf.Launcher\DotShelf.Launcher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\dist\win-x64
```

산출물:

```text
launcher/dist/win-x64/DotShelfLauncher.exe
```

### 7-3. 런처 업데이트 모델

런처는 아래 URL을 기본 manifest로 사용한다.

```text
https://dotshelf.digital/launcher/windows/latest.json
https://dotshelf.digital/launcher/windows/events.json
```

코드 위치:

```text
launcher/src/DotShelf.Launcher/MainWindow.xaml.cs
```

로컬 설치 상태:

```text
%LOCALAPPDATA%\DotShelfLauncher\settings.json
{InstallRoot}\install-state.json
{InstallRoot}\versions\{version}\...
```

manifest shape:

```json
{
  "latestVersion": "0.3.0",
  "mandatory": false,
  "packageUrl": "https://cdn.dotshelf.digital/windows/Dotshelf-0.3.0.zip",
  "sha256": "1105B6E55E07338A8D148825BA4FC8A734333D1FE70625C16F51B269B99FD67C",
  "entrypoint": "Dotshelf.exe",
  "minLauncherVersion": "0.1.0",
  "releaseNotes": [
    "..."
  ]
}
```

로컬 manifest 서버로 런처 업데이트를 테스트하려면 `latest.json`, `events.json`, `Dotshelf-{version}.zip`을 한 폴더에 두고 정적 서버를 띄운다.

```powershell
cd C:\Users\SSAFY\Desktop\launcher-test
python -m http.server 8080
```

그 뒤 `MainWindow.xaml.cs`의 기본 manifest URL을 임시로 `http://127.0.0.1:8080/latest.json`로 바꿔 테스트할 수 있다. 이 변경은 커밋하지 않는다.

## 8. Apple 프로토타입

`apple/` 아래 프로젝트는 현재 Unity 본체와 별개인 Apple 플랫폼 프로토타입이다.

| 경로 | 설명 | 실행 환경 |
| --- | --- | --- |
| `apple/DotshelfRealityKit/` | RealityKit 기반 iOS prototype | macOS, Xcode |
| `apple/DotshelfMetal/` | Metal 기반 macOS prototype | macOS, Xcode |
| `apple/Dotshelf_RCP/` | Reality Composer Pro package data | macOS, Reality Composer Pro |

RealityKit 프로젝트 설정:

- Bundle ID: `com.jihocompany.dotshelf`
- iOS deployment target: `18.0`
- Swift version: `5.0`
- Team ID는 project file에 들어 있으나, 로컬 빌드에서는 본인 Apple Developer Team으로 바꿔야 할 수 있다.

Metal 프로젝트 설정:

- Bundle ID: `com.simonjiho.DotshelfMetal`
- SDK: macOS
- Swift version: `5.0`

실행:

```text
Xcode에서 apple/DotshelfRealityKit/DotshelfRealityKit.xcodeproj 열기
Xcode에서 apple/DotshelfMetal/DotshelfMetal.xcodeproj 열기
```

주의:

- macOS/Xcode가 없는 Windows 환경에서는 로컬 실행 대상에서 제외한다.
- 이 폴더의 `initial-spec-prompt.md`, `metal-reference/`는 설계/학습 자료다.

## 9. 테마/원본 자산

아래 폴더는 Unity에 쓰이는 테마 자산 원본 또는 작업 산출물이다.

| 경로 | 주요 파일 |
| --- | --- |
| `fantasy/` | `fantasy_*.glb`, `fantasy_wallpaper*.png` |
| `space/` | `space_*.glb`, `space_wallpaper.png` |
| `spooky/` | `spooky_*.glb`, `spooky_*.png` |

포팅 시 처리 원칙:

- 원본 GLB/PNG는 Unity import 대상이거나 remote asset bundle 제작의 입력이다.
- 파일 크기가 크므로 불필요한 복제본을 만들지 않는다.
- Unity에서 자산을 다시 import하면 `.meta`와 Library 캐시가 갱신될 수 있다. `Library/`는 커밋하지 않는다.

## 10. 전체 로컬 실행 순서

### 10-1. 백엔드 + DB + Unity 최소 실행

PowerShell 창 1:

```powershell
cd C:\SSAFY\S14P31F206\backend
docker compose up -d
```

PowerShell 창 2:

```powershell
cd C:\SSAFY\S14P31F206\backend
if (-not (Test-Path .env.local)) {
  Copy-Item .env.example .env.local
}
notepad .env.local
```

`S3_BUCKET`, `S3_REGION`, `JWT_SECRET`을 최소로 채운다.

PowerShell 창 2에서 백엔드 실행:

```powershell
.\gradlew.bat bootRun
```

PowerShell 창 3에서 확인:

```powershell
Invoke-RestMethod http://localhost:8080/health
```

Unity:

1. Unity Hub에서 `C:\SSAFY\S14P31F206\frontend` 열기
2. `Assets/App/Config/AppApiConfig.asset`의 `devBaseUrl`을 `http://localhost:8080`으로 임시 변경
3. `Assets/Scenes/StartScene.unity` 또는 `Assets/Scenes/MainRoomScene.unity` 열기
4. Play

### 10-2. seed 포함 전체 연동 확인

DB/Redis와 백엔드가 켜진 상태에서:

```powershell
cd C:\SSAFY\S14P31F206\backend
Get-Content .\scripts\seed-mock-data.sql | docker exec -i dotshelf-postgres psql -U dotshelf -d dotshelf
```

admin-web 실제 backend 모드:

```powershell
cd C:\SSAFY\S14P31F206\web\admin-web
npm install
$env:NEXT_PUBLIC_USE_MOCKS = "false"
$env:ADMIN_API_REWRITE_BASE_URL = "http://localhost:8080/v1/admin"
$env:NEXT_PUBLIC_ADMIN_API_BASE_URL = "/api/admin"
$env:NEXT_PUBLIC_ADMIN_TEST_USER_ID = "1004"
npm run dev
```

접속:

```text
http://localhost:3001
```

service-web:

```powershell
cd C:\SSAFY\S14P31F206\web\service-web
npm install
if (-not (Test-Path .env.local)) {
  Copy-Item .env.example .env.local
}
$env:NEXT_PUBLIC_USE_MOCKS = "true"
npx next dev
```

접속:

```text
http://localhost:3000
```

launcher:

```powershell
cd C:\SSAFY\S14P31F206\launcher
dotnet run --project .\src\DotShelf.Launcher\DotShelf.Launcher.csproj
```

## 11. 포트 목록

| 포트 | 프로세스 | 설명 |
| ---: | --- | --- |
| `5432` | PostgreSQL | `backend/docker-compose.yml` |
| `6379` | Redis | `backend/docker-compose.yml` |
| `8080` | Spring Boot API | local backend |
| `3000` | service-web | Next dev |
| `3001` | admin-web | Next dev |
| `18080` | Unity desktop OAuth loopback 후보 | Kakao/desktop OAuth |
| `8080` | launcher local manifest server 예시 | Python static server; 백엔드와 동시 사용 시 다른 포트 사용 |

포트 충돌 확인:

```powershell
netstat -ano | findstr :8080
netstat -ano | findstr :5432
netstat -ano | findstr :6379
```

## 12. 보안/비밀값 관리

비밀값으로 취급할 것:

- `backend/.env.local`
- `frontend/SOCIAL_LOGIN_SETUP_PRIVATE.md`
- `json/*.json` OAuth client secret 파일
- Google/Kakao/Apple client secret
- Apple private key
- AWS S3 access key/secret key
- Inicis sign key
- GitLab token
- Cloudflare/R2/S3 호환 API key

규칙:

- 비밀값을 Markdown 문서, MR 본문, 커밋 메시지, Slack 공개 채널에 붙이지 않는다.
- `.env.example`에는 변수명과 dummy 값만 둔다.
- 로컬에서 받은 secret은 본인 PC에만 두고, 필요 시 팀의 승인된 비밀 저장소에서 다시 받는다.
- Unity Inspector에 access token을 직접 저장하지 않는다. 필요하면 `DOTSHELF_ACCESS_TOKEN` 환경변수 사용을 우선한다.

## 13. 문제 해결

### 백엔드가 `app.storage.s3.bucket is required`로 실패

원인: `S3_BUCKET`이 비어 있다.

해결:

```properties
S3_BUCKET=local-placeholder-not-real
S3_REGION=ap-northeast-2
```

실제 S3 기능을 테스트하려면 팀에서 받은 bucket/key/public URL을 사용한다.

### 백엔드가 DB 연결 실패

확인:

```powershell
cd C:\SSAFY\S14P31F206\backend
docker compose ps
docker compose logs postgres
```

대부분의 원인:

- Docker Desktop 미실행
- `5432` 포트 충돌
- `dotshelf-postgres`가 아직 healthcheck 통과 전
- `application-local.yml`과 compose DB 계정 불일치

### Flyway validation 실패

원인:

- 이미 적용된 migration 파일을 수정했거나
- 로컬 DB가 이전 schema history를 갖고 있거나
- 새 migration 순서가 꼬였을 수 있다.

로컬 DB를 버려도 되는 경우:

```powershell
cd C:\SSAFY\S14P31F206\backend
docker compose down -v
docker compose up -d
.\gradlew.bat bootRun
```

팀 규칙:

- 기존 `V*.sql`은 수정하지 않는다.
- 새 변경은 다음 번호의 `Vn__description.sql`로 추가한다.

### Redis 연결 실패

확인:

```powershell
docker exec -it dotshelf-redis redis-cli ping
```

응답이 `PONG`이 아니면 Redis 컨테이너 로그를 본다.

```powershell
cd C:\SSAFY\S14P31F206\backend
docker compose logs redis
```

### Unity가 계속 배포 API를 호출

확인:

- Console 로그의 `baseUrl`
- `frontend/Assets/App/Config/AppApiConfig.asset`
- `frontend/Assets/App/Domain/ApiEndpointDefaults.cs`

해결:

1. `AppApiConfig.asset`의 `devBaseUrl`을 `http://localhost:8080`으로 변경
2. 그래도 일부 호출이 배포 API로 나가면 `ApiEndpointDefaults.cs`를 임시 로컬 패치
3. 테스트 후 해당 변경이 commit에 포함되지 않게 되돌림

### Unity 로그인 실패

확인:

- 백엔드 `.env.local`의 provider client id/secret
- Unity의 provider client id
- Google/Kakao/Apple developer console에 등록된 redirect URI
- Kakao desktop loopback port/path
- Apple desktop relay URI: `/v1/auth/apple/desktop-callback`

관련 문서:

```text
frontend/SOCIAL_LOGIN_SETUP.md
frontend/ANDROID_DEBUG_KEYSTORE.md
```

### admin-web mock이 안 켜짐

확인:

```powershell
$env:NEXT_PUBLIC_USE_MOCKS
```

실행 전에 설정:

```powershell
$env:NEXT_PUBLIC_USE_MOCKS = "true"
npm run dev
```

### service-web `npm run dev`가 Windows에서 실패

원인: package script의 `NEXT_PUBLIC_USE_MOCKS=true next dev`는 Unix shell 방식이다.

PowerShell에서는 다음처럼 실행한다.

```powershell
$env:NEXT_PUBLIC_USE_MOCKS = "true"
npx next dev
```

### launcher가 Unity zip 다운로드에 실패

확인:

- `latest.json`의 `packageUrl`
- zip의 SHA-256과 `latest.json.sha256`
- zip 안에 `Dotshelf.exe`, `Dotshelf_Data/`, `UnityPlayer.dll`이 있는지
- 네트워크에서 URL 접근 가능한지

로컬 manifest 서버를 쓸 때 백엔드 `8080`과 충돌하면 다른 포트를 사용한다.

```powershell
python -m http.server 8082
```

## 14. 최종 검증 체크리스트

백엔드:

- [ ] `docker compose ps`에서 PostgreSQL/Redis가 running 또는 healthy
- [ ] `.\gradlew.bat bootRun` 성공
- [ ] `http://localhost:8080/health`가 HTTP 200
- [ ] Swagger UI 접속 가능
- [ ] seed가 필요한 경우 `seed-mock-data.sql` 적용 완료

Unity:

- [ ] Unity Editor `6000.3.13f1`로 열림
- [ ] Console compile error 0개
- [ ] Build Settings에 필수 scene 등록
- [ ] `AppApiConfig.asset`의 local API URL 확인
- [ ] Play 진입 가능
- [ ] API 연동 시 backend 로그에 요청 도착

admin-web:

- [ ] `npm install` 완료
- [ ] mock 모드 또는 backend 모드 중 하나로 실행
- [ ] `http://localhost:3001` 접속 가능
- [ ] backend 모드면 `ADMIN_TEST_AUTH_ENABLED=true`와 seed user `1004` 확인

service-web:

- [ ] `.env.local` 작성
- [ ] `http://localhost:3000` 접속 가능
- [ ] `npm run build` 성공

launcher:

- [ ] `.NET 8 SDK` 설치
- [ ] `dotnet build .\src\DotShelf.Launcher\DotShelf.Launcher.csproj` 성공
- [ ] 런처 실행 가능
- [ ] manifest URL 또는 로컬 manifest 서버 접근 가능

## 15. 참고 문서

| 문서 | 용도 |
| --- | --- |
| `backend/docs/README.md` | 백엔드 문서 index |
| `backend/docs/architecture/patterns.md` | 백엔드 코드/아키텍처 규칙 |
| `backend/docs/architecture/flyway-migration-rules.md` | Flyway migration 규칙 |
| `backend/docs/infra/README.md` | 인프라 문서 index |
| `backend/docs/database/2026-04-29-mock-data-seed.md` | mock seed 데이터 설명 |
| `backend/docs/api/unity-backend-mapping.md` | Unity-Backend API mapping |
| `frontend/README.md` | Unity 프로젝트 개요 |
| `frontend/SOCIAL_LOGIN_SETUP.md` | Unity 소셜 로그인 설정 |
| `frontend/ANDROID_DEBUG_KEYSTORE.md` | Android debug keystore |
| `frontend/Assets/Docs/UnityBookshelfPortingGuide.md` | Unity 책장 포팅 설계 |
| `web/admin-web/README.md` | admin-web 실행/기능 설명 |
| `web/service-web/README.md` | service-web 실행/배포 설명 |
| `launcher/README.md` | 런처 개요 |
| `launcher/docs/versioning.md` | 런처/Unity 버전 체계 |
| `launcher/docs/update-protocol.md` | 런처 update manifest 규칙 |
