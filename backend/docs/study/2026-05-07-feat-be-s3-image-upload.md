# 2026-05-07 feat/be/s3-image-upload

- **브랜치**: `feat/be/s3-image-upload`
- **범위**: `origin/dev..HEAD` (working tree, push 전)
- **기간**: 2026-05-07 (1일)

## TL;DR

DotShelf 의 이미지/에셋을 S3 에 올리는 인프라를 만들었다. AWS SDK v2 의 `S3Client`/`S3Presigner` 를 묶은 `S3StorageService` 를 도메인 서비스가 공유해 쓰는 구조이고, 위에 4개 도메인 엔드포인트를 얹었다 — Unity 자동 캡처 방 썸네일(`POST /v1/spaces/me/thumbnail`, multipart 1-step), 사용자 프로필 이미지(presigned PUT 2-step), 관리자용 에셋 본체와 프리뷰 이미지(presigned PUT 2-step). 자격증명이 비면 `DefaultCredentialsProvider` 로 떨어지게 해 키 없는 환경에서도 부팅은 통과하도록 했다.

## 왜 했나

기능 요구는 세 가지였다. (1) 사용자 프로필 이미지 변경, (2) 관리자가 등록하는 꾸미기 에셋의 본체(Unity AssetBundle 등)와 상점 노출용 프리뷰 이미지, (3) Unity 가 방 배치 변경을 감지할 때마다 자동 캡처해서 보내는 방 썸네일.

세 가지 모두 결국 "외부 스토리지에 바이너리 파일을 올리고, 그 URL 을 DB 에 박는다" 라는 같은 추상이지만, 클라이언트와 트래픽 특성이 달라서 업로드 방식 선택이 갈렸다. Unity 자동 캡처는 트리거가 자주 발생하고 파일이 작아서(수백 KB) BE 가 한 번에 받는 게 클라 단순함에 맞고, 프로필/에셋은 사용자가 직접 누르는 흐름이라 파일이 클 수 있고 BE 부하를 안 거치고 가는 게 자연스러웠다.

또 한 가지 — 작업 시작 시점에 AWS 계정 셋업/IAM 키 발급이 아직 안 된 상태였다. 그래서 키 없이도 부팅이 통과하도록 설계해 두고, 키는 GitLab Variables 로 나중에 주입할 수 있게 분리했다. MR 머지와 인프라 셋업이 병렬로 진행될 수 있도록.

## 무엇을 했나

### 1. S3 인프라 (Phase 1)

- AWS SDK v2 의존성 한 줄(`software.amazon.awssdk:s3:2.29.20`) 추가. `S3Presigner` 가 같은 모듈에 들어 있어 별도 `s3-presigner` 가 **없다**는 걸 처음 알았다(아래 "배운 것" 참고).
- `application.yml` 에 `app.storage.s3.*` 프로퍼티 묶음 추가 — bucket, region, access-key, secret-key, public-base-url, presign-ttl-seconds, presign-bundle-ttl-seconds.
- `global/storage/S3StorageProperties` (record + `@ConfigurationProperties`) 와 `global/config/S3Config` 의 `S3Client`/`S3Presigner` 빈.
- `global/storage/S3StorageService` — `putObject`, `presignPut`, `objectExists`, `publicUrl` 4개 메서드로 도메인-agnostic 추상화.
- `ErrorCode` 에 `ST` prefix 신규(STORAGE_INVALID_CONTENT_TYPE / FILE_TOO_LARGE / OBJECT_NOT_UPLOADED / INTERNAL_ERROR / INVALID_KEY) + `patterns.md` 의 prefix 목록 갱신.

### 2. 방 썸네일 — multipart 1-step (Phase 2A)

- 마이그레이션 V18: `spaces.thumbnail_url VARCHAR(500)` nullable 컬럼.
- `Space` 엔티티에 `thumbnailUrl` 필드 + `updateThumbnail(...)` 메서드.
- `POST /v1/spaces/me/thumbnail` (multipart). 검증 → S3 putObject(`spaces/{spaceId}/thumbnail-{layoutVersion}.jpg`) → DB 갱신 + `nextLayoutVersion()` 으로 버전 bump.
- `application.yml` 에 `spring.servlet.multipart.max-file-size=4MB`. 기본 1MB 가 2MB 한도(`MAX_THUMBNAIL_SIZE_BYTES`)보다 작아 막혔다.

### 3. 프로필 이미지 — presigned PUT 2-step (Phase 2B)

- 마이그레이션 불필요(`users.profile_image_url` 이미 존재).
- `POST /v1/users/me/profile-image/upload-url` 로 presigned URL 발급, 클라가 S3 직업로드, `PATCH /v1/users/me/profile-image` 로 confirm.
- key 패턴 `users/{userId}/profile/{uuid}.{ext}`. confirm 단계에서 key prefix 가 `users/{currentUserId}/profile/` 로 시작하는지 검사 — 다른 사용자의 객체를 자기 프로필로 등록하는 흐름 차단.

### 4. 에셋 본체 / 프리뷰 — presigned PUT 2-step, 관리자 (Phase 2C, 2D)

- 마이그레이션 불필요(`assets.asset_url`, `assets.preview_image_url` 이미 존재).
- 새 컨트롤러/서비스 `AdminAssetController`, `AdminAssetService` (`/v1/admin/assets/...`).
- 본체: `application/octet-stream`, `application/zip` 만 허용, 200MB 한도, presign TTL 30분(에셋 번들이 큼).
- 프리뷰: image 화이트리스트, 5MB, presign TTL 5분.
- 관리자 권한은 `User.role == "ADMIN"` 검사. 기존 `AdminService.requireAdminUser` 와 같은 패턴을 새 서비스에 짧게 복제했다(공용 헬퍼화는 후속).

## 어떻게 구현했나

### S3StorageService 의 두 가지 업로드 모드

```java
public void putObject(String key, MultipartFile file, String contentType) { ... }

public PresignedPut presignPut(String key, String contentType, long byteLength, Duration ttl) {
    PutObjectRequest objectRequest = PutObjectRequest.builder()
            .bucket(properties.bucket())
            .key(key)
            .contentType(contentType)
            .contentLength(byteLength)
            .build();
    PutObjectPresignRequest presignRequest = PutObjectPresignRequest.builder()
            .signatureDuration(ttl)
            .putObjectRequest(objectRequest)
            .build();
    PresignedPutObjectRequest presigned = s3Presigner.presignPutObject(presignRequest);
    return new PresignedPut(presigned.url().toString(), key, ttl.getSeconds());
}
```

핵심 포인트:

- **presign 시점에 박은 contentType / contentLength 가 클라의 PUT 요청 헤더와 정확히 일치해야 S3 가 받는다**. 다르면 403 SignatureDoesNotMatch. 클라 가이드에 이걸 꼭 명시해야 함.
- `PresignedPut` 은 record 하나로 uploadUrl + key + expiresInSeconds 묶어서 반환 — 도메인 DTO 가 이걸 그대로 매핑한다.
- `putObject` 는 BE → S3 직접 업로드(서버 경유 1-step), `presignPut` 은 클라 → S3 직업로드(URL 발급 후 클라가 직접). 도메인이 둘 중 어울리는 걸 골라 쓴다.

### 자격증명 분기 — Static vs Default chain

```java
private AwsCredentialsProvider credentialsProvider(S3StorageProperties properties) {
    if (hasText(properties.accessKey()) && hasText(properties.secretKey())) {
        return StaticCredentialsProvider.create(
                AwsBasicCredentials.create(properties.accessKey(), properties.secretKey()));
    }
    return DefaultCredentialsProvider.create();
}
```

로컬은 `.env` 에 키를 박아 Static 으로 가고, 운영은 EC2 IAM Role 이 붙으면 `DefaultCredentialsProvider` 가 인스턴스 메타데이터에서 자동으로 임시 자격증명을 가져온다. 한 코드로 두 환경 모두 커버.

부팅 시 자격증명이 아예 없어도(키도 없고 Role 도 없는 로컬에서 `.env` 비어 있을 때) 빈 생성은 통과한다 — `DefaultCredentialsProvider` 는 lazy resolve 라 호출 시점에만 실패한다. 그래서 S3 호출 안 하는 페이지는 정상 동작.

### multipart 1-step vs presigned 2-step 의 선택 기준

| | multipart 1-step | presigned 2-step |
|---|---|---|
| 클라 단순함 | 높음 (요청 1번) | 낮음 (3번 round-trip) |
| BE 부하 | 파일 본체가 BE 거쳐감 | 클라 → S3 직접, BE 는 URL 발급/confirm 만 |
| 큰 파일 | 메모리/디스크 사용 + multipart 한도 | 적합 (S3 가 직접 받음) |
| 자동 트리거 빈도 | 매 호출이 BE round-trip 1번 | round-trip 3번 |

방 썸네일은 작고 자주 자동 발생 → multipart. 프로필/에셋은 사용자가 직접 트리거 + 큼 → presigned.

### key 패턴과 layoutVersion 캐시 무효화

방 썸네일 키는 `spaces/{spaceId}/thumbnail-{layoutVersion}.{ext}` 다. 매 업데이트마다 `space.nextLayoutVersion()` 으로 버전을 올려서 키 자체가 바뀐다. 그래서 CDN(나중에 CloudFront 붙이면) 캐시 무효화 호출 없이도 항상 최신 이미지가 공개된다. 대신 이전 버전이 누적되니 S3 라이프사이클로 7일 뒤 자동 삭제 정책을 거는 게 운영 권장.

프로필/에셋 키는 `{prefix}/{uuid}.{ext}` 라 매번 새 객체 생성 — 마찬가지로 캐시 충돌 없음. 단 confirm 실패한 객체가 고아로 남을 수 있어 라이프사이클 24h 정리 정책이 같이 필요.

### confirm 단계의 두 가지 검증

```java
String prefix = "users/" + currentUserId + "/profile/";
if (!request.key().startsWith(prefix)) {
    throw new CustomException(ErrorCode.STORAGE_INVALID_KEY);
}
if (!s3StorageService.objectExists(request.key())) {
    throw new CustomException(ErrorCode.STORAGE_OBJECT_NOT_UPLOADED);
}
```

- **Prefix 검사**: presigned URL 자체는 BE 가 발급해서 안전하지만, 만약 두 사용자가 동시에 발급받았다면 다른 사용자의 key 를 confirm 으로 보낼 수 있다. prefix 검사로 그걸 차단.
- **headObject 검사**: presigned PUT 만 발급되고 클라가 실제로 안 올렸을 수 있다. headObject 한 번으로 실존 확인.

## 배운 것

- **AWS SDK Java v2 의 모듈 구조**: `S3Presigner` 가 별도 `s3-presigner` 아티팩트일 거라고 짐작했는데 Maven Central 에는 그런 모듈이 없었다. `software.amazon.awssdk:s3` 에 같이 들어있는 클래스다. 잘못된 의존성으로 빌드 실패가 나서 직접 `repo1.maven.org/maven2/software/amazon/awssdk/` 디렉터리를 listing 해보고 알아냈다. SDK 가 모듈을 어떻게 쪼개 두는지는 그룹별로 다르니 의존성 추가 전 한 번 확인하는 게 안전.
- **`io.spring.dependency-management` plugin 과 AWS BOM**: Spring Boot 4 의 dependency-management plugin 은 자체 BOM 관리 메커니즘이 있어서, `platform("software.amazon.awssdk:bom:2.29.20")` 으로 추가한 AWS BOM 의 버전 해석이 묻혔다. 해결은 명시 버전. 일반적으로 두 BOM 이 같은 그룹을 다루지 않으면 공존하지만, 우리 케이스는 BOM 자체가 무시됐다.
- **S3 Presigned URL 의 contentType 매칭**: presign 시점에 `PutObjectRequest.contentType()` 으로 박은 값이 클라의 PUT 요청 `Content-Type` 헤더와 **글자 단위로** 일치해야 한다. 헤더가 다르면 SignatureDoesNotMatch 가 떨어진다. 그래서 1단계 발급 요청에 클라가 contentType 을 신고하게 하고, 그 값을 서명에 박고, 2단계 PUT 에 그대로 보내게 강제하는 흐름이 됐다.
- **Spring multipart 기본 한도**: `spring.servlet.multipart.max-file-size` 기본은 1MB. 이게 도메인 한도(2MB)보다 작으면 클라가 보낸 요청이 컨트롤러까지 도달하기 전에 잘린다. 도메인 검증보다 먼저 막혀서 에러 메시지가 도메인 ErrorCode 가 아니라 `MaxUploadSizeExceededException` 으로 나온다 — 도메인 ErrorCode 로 통일되게 하려면 application.yml 에 충분히 큰 값을 박고 도메인 검증으로 거르거나, GlobalExceptionHandler 에 `MaxUploadSizeExceededException` → `STORAGE_FILE_TOO_LARGE` 매핑을 추가해야 한다(후자는 후속).
- **`DefaultCredentialsProvider` 의 lazy resolve**: 자격증명 체인은 빈 생성 시점이 아니라 첫 SDK 호출 시점에 resolve 된다. 덕분에 키 없는 로컬에서도 부팅이 통과한다. 단점은 S3 가 아닌 다른 페이지에선 잘 돌다가 첫 S3 호출에서 갑자기 401 이 떨어져 보일 수 있어서, 키 셋업이 끝났는지 추적하는 헬스체크가 별도로 필요할 수 있다.

## 더 공부할 것

- [ ] **CloudFront 를 끼얹는 시점과 비용 트레이드오프** — 지금은 S3 가상 호스트 URL 을 그대로 노출. CDN 캐싱이 의미 있어지려면 동시 사용자 / 트래픽이 어느 수준이어야 하나? CloudFront 의 origin shield, signed URL, OAC(Origin Access Control) 이 어떻게 묶이는지.
- [ ] **S3 multipart upload (대용량 분할 업로드)** — 200MB 를 단일 PUT 으로 받고 있는데, 실제 Unity AssetBundle 이 100MB 를 넘기면 단일 PUT 보다 multipart 가 안전. AWS SDK 의 `S3TransferManager` 와 presigned multipart upload 의 차이. 클라가 분할을 알아야 하는지, presigned 서명을 part 별로 발급해야 하는지.
- [ ] **CORS preflight 가 presigned PUT 에 미치는 영향** — 브라우저에서 PUT 을 보내면 OPTIONS preflight 가 먼저 간다. AllowedHeaders 의 와일드카드(`*`) 가 안 먹는 헤더가 있다는 얘기를 들은 적 있는데(특히 X-Amz-* 계열), 정확히 무엇이고 어떤 케이스에 명시가 필요한지.
- [ ] **IAM Role 의 임시 자격증명 갱신 주기** — EC2 Instance Profile 의 STS 토큰은 보통 1시간 만료라고 알고 있는데, AWS SDK v2 가 만료 직전 자동으로 갱신하는지, 아니면 갱신 실패가 어떻게 표면화되는지(특히 토큰 만료 직전에 들어온 long-running 요청).
- [ ] **고아 객체 정리 정책의 비용** — 라이프사이클 룰로 자동 삭제하는 게 정설인데, 24h 가 정말 충분한가? 클라가 confirm 을 못 보내고 재시도하는 케이스에서 객체가 사라지면 어떻게 되나. confirm 안 받은 객체는 별도 prefix(staging/)에 두고 confirm 시점에 prod prefix 로 copy 하는 패턴이 더 안전할까?

## 참고

- 변경 파일 13개 (수정) + 5개 신규(`S3Config.java`, `S3StorageProperties.java`, `S3StorageService.java`, `AdminAssetController.java`, `AdminAssetService.java`) + V18 마이그레이션
- patterns.md / checklist.md 따라 셀프리뷰 통과 (DTO `@Schema` 만 미준수 — 기존 코드 일관성 측면이라 후속 정리 PR 권장)
