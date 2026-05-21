# S3 기반 Unity 에셋 배포 API 확장

- **브랜치**: `feat/be/assetsdown`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-05-11 ~ 2026-05-11
- **관련 커밋**: `8b00d684 feat: add asset delivery APIs`

## TL;DR

Unity에서 사용할 꾸미기 에셋을 S3에 저장하고, BE는 메타데이터와 다운로드 manifest를 관리하는 구조로 확장했다.
admin-web은 에셋 메타데이터를 먼저 생성하고, presigned PUT으로 본체와 썸네일을 올린 뒤 confirm/publish 흐름으로 사용자 카탈로그에 공개한다.
사용자 API는 카테고리 외에 컬렉션과 타입 필터를 제공하고, 구매한 에셋만 다운로드 manifest를 받을 수 있게 막았다.
전체 테마는 여러 child asset을 묶을 수 있도록 `theme_asset_items` 조인 테이블을 추가했다.

## 무엇을 했나

### 1. 에셋 전달 메타데이터 확장

- 사용자 관점 효과: 다운로드할 파일의 URL, 크기, 해시, 버전을 manifest에서 받을 수 있다.
- 시스템 관점 효과: S3 객체 자체는 외부 스토리지에 두고, DB에는 검색/검증/버전 관리를 위한 메타데이터만 저장한다.
- 주요 파일: `src/main/java/com/dotshelf/domain/asset/entity/Asset.java`, `src/main/resources/db/migration/V22__expand_asset_delivery_metadata.sql`

추가된 주요 컬럼:

- `collection_code`: 깊은바다 같은 테마/컬렉션 필터
- `content_type`: S3 confirm 시 확인한 실제 MIME 타입
- `file_size`: S3 객체 크기
- `file_hash`: 클라이언트 캐시/무결성 검증용 해시
- `version`: 같은 asset code의 파일 교체 버전

### 2. admin-web용 에셋 생성/수정/공개 API

- 사용자 관점 효과: 운영자가 admin-web에서 에셋과 썸네일을 등록하고 공개 여부를 통제할 수 있다.
- 시스템 관점 효과: 메타데이터 생성과 바이너리 업로드를 분리해 대용량 파일이 BE를 거치지 않는다.
- 주요 파일: `AdminAssetController.java`, `AdminAssetService.java`, `AssetDtos.java`

추가된 흐름:

1. `POST /v1/admin/assets`로 draft 에셋 메타데이터 생성
2. `POST /v1/admin/assets/{assetId}/bundle/upload-url`로 S3 PUT URL 발급
3. 클라이언트가 S3로 직접 PUT
4. `PATCH /v1/admin/assets/{assetId}/bundle`로 S3 객체 존재와 content metadata 확인
5. `PUT /v1/admin/assets/{assetId}/publish`로 사용자 카탈로그 공개

### 3. 사용자 카탈로그 필터와 다운로드 manifest

- 사용자 관점 효과: `collection`, `type`, `q`, `owned` 조건으로 에셋을 찾을 수 있다.
- 시스템 관점 효과: 구매하지 않은 에셋의 다운로드 URL은 manifest API에서 차단한다.
- 주요 파일: `AssetController.java`, `AssetService.java`, `AssetRepository.java`

추가된 API:

- `GET /v1/assets?category=&collection=&type=&q=&owned=`
- `GET /v1/assets/filters`
- `GET /v1/assets/{assetId}/download-manifest`

### 4. 전체 테마 child asset 매핑

- 사용자 관점 효과: 전체 테마 하나를 적용할 때 배경, 바닥, 책상, 조명 등 필요한 파일 묶음을 한 번에 알 수 있다.
- 시스템 관점 효과: 기존 `compatibleTargets` JSON 기반 단순 매핑을 유지하면서, 신규 구조는 `theme_asset_items`로 정규화했다.
- 주요 파일: `ThemeAssetItem.java`, `ThemeAssetItemRepository.java`

## 왜 했나

Unity 에셋은 DB에 직접 저장하기에는 크고, BE를 통해 다운로드시키면 서버 대역폭과 응답 시간이 불필요하게 커진다.
그래서 원본 파일과 썸네일은 S3 또는 CloudFront에 두고, BE는 "누가 어떤 파일을 받을 권리가 있는가", "어떤 파일들이 한 테마를 구성하는가", "프론트가 어떤 필터로 목록을 볼 수 있는가"만 책임지는 구조가 맞다.

또한 운영자 등록 흐름에서는 파일 업로드와 공개를 분리해야 한다.
에셋 메타데이터만 만든 상태, 본체만 올라간 상태, 썸네일만 올라간 상태가 모두 생길 수 있으므로, 사용자에게 보이는 시점은 `publish`에서 명시적으로 제어하도록 했다.

## 어떻게 구현했나

### Flyway로 스키마를 먼저 고정

`assets` 테이블에 다운로드 메타데이터를 추가하고, 전체 테마의 child asset 관계는 별도 테이블로 분리했다.

```sql
ALTER TABLE assets
    ADD COLUMN IF NOT EXISTS collection_code varchar(80),
    ADD COLUMN IF NOT EXISTS content_type varchar(120),
    ADD COLUMN IF NOT EXISTS file_size bigint,
    ADD COLUMN IF NOT EXISTS file_hash varchar(160),
    ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 1;
```

핵심 포인트:

- `collection_code`와 `asset_type`에 인덱스를 둬 카탈로그 필터 조회를 빠르게 만든다.
- `theme_asset_items`는 `theme_asset_id`, `child_asset_id`, `role` 조합을 unique로 묶어 같은 역할이 중복 연결되는 것을 막는다.
- 기존 데이터가 있어도 마이그레이션이 지나가도록 새 컬럼에는 nullable 또는 default를 사용했다.

### S3 confirm에서 실제 객체 메타데이터를 저장

업로드 URL 발급 시 클라이언트가 신고한 `contentType`, `byteLength`만 믿지 않고, confirm 단계에서 S3 `headObject` 결과를 다시 확인한다.

```java
ObjectMetadata metadata = verifyConfirmedObject(
        request.key(),
        "assets/" + asset.getCode() + "/bundle/",
        ALLOWED_BUNDLE_TYPES::contains,
        MAX_BUNDLE_SIZE_BYTES);

asset.updateBundle(after, metadata.contentType(), metadata.contentLength(), request.fileHash(), request.version());
```

핵심 포인트:

- key prefix를 확인해 다른 asset 경로를 confirm하지 못하게 막는다.
- S3가 실제로 받은 content type과 크기를 다시 검증한다.
- version이 명시되지 않으면 기존 version을 1 증가시켜 교체 업로드를 표현한다.

### 다운로드 manifest는 권한 확인 후 생성

다운로드 URL은 목록 응답이 아니라 manifest API에서 내려준다.
사용자가 해당 asset을 보유하지 않으면 `ASSET_NOT_OWNED`로 차단한다.

```java
if (!inventoryRepository.existsByUserIdAndAssetId(currentUserId, assetId)) {
    throw new CustomException(ErrorCode.ASSET_NOT_OWNED);
}
```

전체 테마인 경우에는 `theme_asset_items`의 child asset들을 펼쳐서 파일 목록으로 반환한다.
아직 child mapping이 없는 legacy theme은 theme 자체를 하나의 다운로드 파일로 반환하도록 폴백을 남겼다.

### 테스트 범위

API 변경이므로 Controller, Service, Repository 단위 테스트를 추가했다.

- admin asset 생성/수정/publish 라우팅 테스트
- GLB 업로드 URL 확장 테스트
- confirm bundle metadata 저장 테스트
- asset 필터 JPQL 테스트
- download manifest 권한 테스트

## 배운 것

- **Presigned PUT은 업로드 권한만 위임한다**: 파일을 BE가 직접 받지 않으므로, confirm 단계에서 S3 `headObject`로 객체 존재와 metadata를 다시 확인해야 한다.
- **다운로드 URL은 권한 경계가 된다**: 목록 API에 URL을 무조건 노출하면 구매 전 접근이 쉬워진다. manifest API처럼 보유 여부 확인 뒤 내려주는 구조가 더 안전하다.
- **테마는 단일 asset이 아니라 asset bundle graph에 가깝다**: "깊은바다 테마"는 하나의 파일일 수도 있지만, 실제 적용 관점에서는 벽지/바닥/가구/조명 역할별 child asset 묶음이다.
- **Flyway default 전략이 중요하다**: 운영 DB에 이미 row가 있을 때 `NOT NULL` 컬럼을 추가하려면 default 또는 nullable 전략을 먼저 잡아야 배포가 안전하다.
- **필터 API는 화면 요구와 데이터 모델 사이의 계약이다**: DB에는 `collection_code`와 `asset_type`만 있어도, 프론트에는 `code/name` 형태의 필터 옵션으로 내려주는 편이 이후 라벨 확장에 유리하다.

## 더 공부할 것

- [ ] S3 객체 공개 방식 선택: public bucket, CloudFront signed URL, presigned GET 중 어떤 모델이 유료 에셋 다운로드에 가장 맞는가?
- [ ] Unity Addressables 또는 AssetBundle versioning을 BE의 `version/fileHash`와 어떻게 맞출 것인가?
- [ ] 테마 child asset의 `transform_json` 스키마를 JSON Schema로 검증할지, Unity 전용 DTO로 분해할지 결정하기
- [ ] S3 업로드 후 orphan object 정리 전략: admin이 draft를 삭제하거나 confirm 전에 이탈하면 S3 객체를 어떻게 청소할 것인가?
- [ ] `collection_code`를 문자열 코드로 둘지 별도 `asset_collections` 테이블로 승격할지 판단 기준 정리

## 참고

- 검증 명령:
  - `./gradlew.bat test --tests "com.dotshelf.domain.asset.*"`
  - `./gradlew.bat test --tests "com.dotshelf.integration.AssetIntegrationTest"`
  - `./gradlew.bat test`
- 관련 커밋: `8b00d684`
