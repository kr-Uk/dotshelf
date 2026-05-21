# Unity ↔ 백엔드 DTO 정합성 감사

> Unity 클라이언트 DTO와 Spring Boot 백엔드 DTO를 필드 단위로 비교한 감사 문서. [unity-backend-mapping.md](unity-backend-mapping.md)의 ⚠/✗ 행에서 이쪽 섹션으로 점프.
>
> - Unity DTO 한곳에 모인 파일: [Dtos/NotebookApiDtos.cs](../../../frontend/Assets/App/Infrastructure/Api/Dtos/NotebookApiDtos.cs)
> - 백엔드 DTO는 도메인별 `domain/{도메인}/dto/{도메인}Dtos.java`에 record로 정의.
> - 마지막 갱신: 2026-04-29

## 표기 규약

| 기호 | 의미 |
| --- | --- |
| ✓ | 양쪽에 모두 존재, 타입 호환 |
| ⚠ | 양쪽에 있으나 타입/이름이 미묘하게 다름 (Newtonsoft 자동 변환으로 무해할 수 있음) |
| ⊖ | Unity에만 있는 필드 — 백엔드 응답에 빠져 있어 Unity는 항상 null 받음 |
| ⊕ | 백엔드에만 있는 필드 — Unity가 무시 (Newtonsoft가 미사용 필드를 그냥 버림) |
| 🚫 | 양쪽 의미가 다른 필드 (호환 안 됨) |

타입 비교에서 Java `Long` ↔ C# `long`, Java `Integer` ↔ C# `int/long`, Java `BigDecimal` ↔ C# `decimal`은 모두 호환으로 본다.

## 공통 응답 래퍼

### ApiEnvelope (응답 envelope)

- Unity: [Dtos/ApiEnvelopeDto.cs](../../../frontend/Assets/App/Infrastructure/Api/Dtos/ApiEnvelopeDto.cs)
- Backend: [ApiResponse.java](../../src/main/java/com/dotshelf/global/common/ApiResponse.java)

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| httpStatus | string | int | ⚠ | Newtonsoft가 int → string 자동 변환. 기능적 무해. |
| isSuccess | bool | boolean | ✓ | |
| success | bool (fallback) | (없음) | ⊖ | Unity가 `Succeeded = IsSuccess \|\| Success`로 폴백을 두지만 백엔드가 `success` 필드를 안 보내므로 사실상 사문. 제거 가능. |
| message | string | String | ✓ | |
| code | string | String | ✓ | 성공 시 `"SUCCESS"`, 실패 시 [ErrorCode](../../src/main/java/com/dotshelf/global/error/ErrorCode.java) 값. |
| result | T | T | ✓ | `@JsonInclude(NON_NULL)` 글로벌 정책으로 null이면 키 자체가 빠짐. Unity는 `default`로 받음. |

총평: ⚠ — `httpStatus` 타입 불일치는 무해, `success` 필드는 미사용.

### ApiError (실패 응답)

- Unity: [Dtos/ApiErrorResponseDto.cs](../../../frontend/Assets/App/Infrastructure/Api/Dtos/ApiErrorResponseDto.cs)

Unity에는 ApiErrorResponseDto가 별도로 있지만, 백엔드는 실패 시에도 동일한 [ApiResponse](../../src/main/java/com/dotshelf/global/common/ApiResponse.java)로 응답함 (`isSuccess=false`, `result=null`). Unity의 `ApiErrorResponseDto.statusCode/error/body` 필드는 **HTTP 통신 실패 시 클라이언트에서 채워 넣는 필드**이고, 백엔드에서 보내는 게 아님. 호환 OK.

### ToggleResponse / IdResponse / Void

- Backend: [ToggleResponse.java](../../src/main/java/com/dotshelf/global/common/ToggleResponse.java), [IdResponse.java](../../src/main/java/com/dotshelf/global/common/IdResponse.java)

| DTO | Unity 필드 | Backend 필드 | 상태 |
| --- | --- | --- | --- |
| ToggleResponse | active, count | active (boolean), count (long) | ✓ |
| IdResponse | id | id (Long) | ✓ |
| VoidResponseDto | (없음) | (응답 본문 자체가 빈 객체) | ✓ — Unity는 `result`를 무시. |

---

## A. 갭 있음 (⚠/✗) — 백엔드 작업 후보

### AuthTokenResponse

POST `/v1/auth/social-login`, POST `/v1/auth/token/refresh` 응답.

- Unity: [Dtos/NotebookApiDtos.cs](../../../frontend/Assets/App/Infrastructure/Api/Dtos/NotebookApiDtos.cs) `AuthTokenResponseDto`
- Backend: [domain/auth/dto/AuthDtos.java](../../src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java) `AuthTokenResponse`

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| userId | long | Long | ✓ | |
| accessToken | string | String | ✓ | |
| refreshToken | string | String | ✓ | |
| tokenType | string | (없음) | ⊖ | Unity가 받지만 백엔드가 안 보냄. `@JsonInclude(NON_NULL)`으로 키 자체 누락. **액션 후보**: 백엔드에 `"Bearer"` 상수 응답 추가. |
| expiresIn | long | (없음) | ⊖ | Unity가 토큰 만료 카운트다운에 쓸 수 있음. **액션 후보**: 백엔드 JWT 설정의 `accessTtlSeconds`(현재 1800)를 그대로 응답. |
| newUser | bool | boolean | ✓ | |
| onboarded | bool | boolean | ✓ | |

총평: ⚠ — `tokenType`/`expiresIn` 두 필드 추가가 가장 작은 비용으로 가장 큰 효용. record 필드 + 매핑 한 줄.

### GuestSessionResponse

POST `/v1/auth/guest-session` 응답.

- Unity: `GuestSessionResponseDto`
- Backend: `GuestSessionResponse` ([AuthDtos.java](../../src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java))

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| guestToken | string | String | ✓ | |
| expiresInSeconds | long | long | ✓ | |
| guestSessionId | string | (없음) | ⊖ | Unity는 이 ID를 받아 `GuestMigrationRequest`에 다시 보내려는 의도가 명확. 백엔드에 누락. |

총평: ⚠ — 아래 `GuestMigrationRequest`와 짝지어 같이 처리.

### GuestMigrationRequest

POST `/v1/auth/guest-migration` 요청.

- Unity: `GuestMigrationRequestDto`
- Backend: `GuestMigrationRequest` ([AuthDtos.java](../../src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java))

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| guestSessionId | string | (없음) | ⊖ | Unity가 보내지만 백엔드는 무시. 게스트 세션 식별이 토큰만으로 충분한지, 아니면 세션 ID도 필요한지 결정 필요. |
| payload | string | String | ✓ | |

총평: ⚠ — `GuestSessionResponse.guestSessionId` 추가와 함께 처리. 추가 안 할 거면 Unity가 보낸 필드는 백엔드에서 그냥 버려지므로 기능적 무해.

### SocialLoginRequest

POST `/v1/auth/social-login` 요청.

- Unity: `SocialLoginRequestDto`
- Backend: `SocialLoginRequest` ([AuthDtos.java](../../src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java))

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| provider | string | SocialProvider (enum) | ⚠ | Jackson이 `"GOOGLE"`/`"APPLE"` 등 문자열을 enum으로 역직렬화. Unity가 정확한 enum 값을 보내야 함. |
| tokenType | string | SocialTokenType (enum, nullable) | ⚠ | 위와 동일. |
| token | string | String | ✓ | |
| idToken | string | (없음) | ⊖ | Unity가 보내는데 백엔드가 무시. Unity의 `NormalizeSocialLoginRequest` ([NotebookApiClient.cs:537-549](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L537-L549))가 `idToken/accessToken` 중 하나를 `token`에 합쳐서 보내므로 페이로드에는 이 필드가 빈 값으로 동봉될 가능성. 백엔드가 record면 미선언 필드는 그냥 버리니 기능 무해. |
| accessToken | string | (없음) | ⊖ | 위와 동일. |
| nonce | (없음) | String (nullable) | ⊕ | 백엔드가 받지만 Unity는 안 보냄. `nonce` 검증이 필수면 Unity 로그인이 실패. **액션**: 현재 nonce 검증이 optional인지 백엔드 SocialLoginService에서 확인. |

총평: ⚠ — 가장 위험한 건 `nonce`. 검증 강제면 401/400 에러. 백엔드 코드 확인 필요.

### BookResponse

GET `/v1/books/{bookId}` 응답.

- Unity: `BookResponseDto` (extends `BookListItemDto`)
- Backend: `BookResponse` ([domain/book/dto/BookDtos.java](../../src/main/java/com/dotshelf/domain/book/dto/BookDtos.java))

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| id | long | Long | ✓ | |
| bookId | string | String | ✓ | KAKAO 외부 ID. |
| isbn | string | String | ✓ | |
| isbn13 | string | String | ✓ | |
| isbn10 | string | String | ✓ | |
| title | string | String | ✓ | |
| author | string | String | ✓ | |
| publisher | string | String | ✓ | |
| publishedDate | string | LocalDate | ⚠ | ISO-8601 문자열 호환. |
| description | string | String | ✓ | |
| coverImageUrl | string | String | ✓ | |
| avgRating | decimal | BigDecimal | ✓ | |
| placementCount | int | long | ⚠ | 백엔드 long → Unity int 캐스팅. 책장 1개당 수십~수백이면 무해, 이론적으로 overflow는 가능. |
| recordCount | int | long | ⚠ | 위와 동일. |
| hasPaidCover | bool | boolean | ✓ | |
| source | string | (없음) | ⊖ | KAKAO/LOCAL 구분. **추가 권장** — Unity가 KAKAO 책 분기에 사용. |
| sourceUrl | string | (없음) | ⊖ | KAKAO 원본 URL. |
| purchaseCount | int | (없음) | ⊖ | |
| likeCount | int | (없음) | ⊖ | |
| genre | (없음) | String | ⊕ | 백엔드만. |
| pricePages | (없음) | Integer | ⊕ | 페이지 단위 가격. |
| reviewCount | (없음) | long | ⊕ | |
| viewCount | (없음) | long | ⊕ | |
| owned | (없음) | boolean | ⊕ | 현재 사용자 소장 여부. |
| userBookId | (없음) | Long (nullable) | ⊕ | 소장 시 user_book id. |
| rank | (없음) | Integer (nullable) | ⊕ | 랭킹 응답 시. |

총평: ⚠ — 4개 ⊖, 7개 ⊕. Unity가 의존하는 `source/sourceUrl` 추가가 실질 우선순위. 나머지 ⊖는 신규 메트릭 표시 UI 도입 시 백엔드도 추가.

### BookSearchListResponse

GET `/v1/books/search` 응답.

- Unity: `BookListResponseDto` ([Dtos/BookListResponseDto.cs](../../../frontend/Assets/App/Infrastructure/Api/Dtos/BookListResponseDto.cs))
- Backend: `BookSearchListResponse` ([BookDtos.java](../../src/main/java/com/dotshelf/domain/book/dto/BookDtos.java))

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| books | List\<BookListItemDto\> | List\<BookSearchResponse\> | ⚠ | Unity는 `EffectiveItems` 헬퍼로 `items ?? books`. 어느 쪽으로 보내도 OK. |
| items | List\<BookListItemDto\> | List\<BookSearchResponse\> | ⚠ | 위 동일. |
| nextCursor | string | String (nullable) | ✓ | |
| page | (없음) | int | ⊕ | |
| size | (없음) | int | ⊕ | |
| isEnd | (없음) | boolean | ⊕ | |

총평: ⚠ — 호환됨, Unity는 `page/size/isEnd` 무시. 페이지네이션 UI를 cursor 외에 page-base로 만들 거면 Unity DTO에도 추가 필요.

### FeedSettingResponse / FeedSettingRequest

- Unity: `FeedSettingResponse` (이름 가정 — Unity DTO 카탈로그에 명시되지 않은 묶음)
- Backend: `FeedSettingResponse` ([domain/user/dto/UserDtos.java](../../src/main/java/com/dotshelf/domain/user/dto/UserDtos.java))

백엔드 필드: `showBookAdded, showReadingRecord, showSpaceVisit, showGuestbook, showReadingRecordComment` (모두 boolean).

Unity 쪽은 `NotebookApiDtos.cs`에 명시 DTO가 보이지 않음 → Newtonsoft가 동적으로 매핑할 가능성이 있음. **액션**: Unity DTO 정의를 [NotebookApiDtos.cs](../../../frontend/Assets/App/Infrastructure/Api/Dtos/NotebookApiDtos.cs)에서 검색해 명시 필드를 확인하고, 누락 시 Unity 측에 정의 요청. 본 audit 시점에는 ⚠로 두고 보류.

### WalletResponse

POST `/v1/payments/page-charge` 응답, GET `/v1/payments/wallet` 응답.

- Unity: `WalletResponseDto`
- Backend: `WalletResponse` ([domain/payment/dto/PaymentDtos.java](../../src/main/java/com/dotshelf/domain/payment/dto/PaymentDtos.java))

| 필드 | Unity 타입 | Backend 타입 | 상태 | 메모 |
| --- | --- | --- | --- | --- |
| userId | long | Long | ✓ | |
| balancePages | int | int | ✓ | |
| updatedAt | (없음) | Instant (nullable) | ⊕ | Unity 미수신. UI에 “마지막 충전 시각” 표시할 거면 Unity DTO에도 추가. 현 상태는 호환. |

총평: ⚠ (호환) — 액션 없음. Unity 쪽 UI 결정에 따라 후속.

### `/v1/books?category=RANKING&limit={limit}` (✗)

이 호출은 백엔드에 핸들러 자체가 없으므로 DTO 비교 대상이 아님. 가장 가까운 백엔드 엔드포인트는 `GET /v1/books/ranking`이며 응답은 `BookListResponse`. Unity가 그쪽으로 마이그레이션할 때 `BookResponse` 갭(위 항목 참조)이 동시에 적용된다.

---

## B. 매칭 (✓) — 추가 작업 불필요

각 DTO마다 양쪽 필드명이 동일하고 타입이 호환됨. 갭이 있으면 위 A 섹션에 따로 분리했음.

### Auth

| DTO | Unity ↔ Backend | 비고 |
| --- | --- | --- |
| TokenRefreshRequest | `refreshToken` | ✓ |
| LogoutRequest | `refreshToken` | ✓ |

### User

| DTO | 필드 | 비고 |
| --- | --- | --- |
| UserProfileResponse | id, email, nickname, profileImageUrl, introduction, onboarded | ✓ |
| UpdateProfileRequest | nickname, profileImageUrl, introduction | ✓ |
| NicknameAvailabilityResponse | available | ✓ |
| UserSummaryResponse | id, nickname, profileImageUrl | ✓ |
| UserListResponse | users[] | ✓ |
| NotificationSettingResponse | notifyNewGuestbook, notifyGuestbookReply, notifyReadingRecordReaction, notifyNewFollower, notifyBookAddedByOthers | ✓ |
| NotificationSettingRequest | 위와 동일 (모두 nullable) | ✓ |

### Book

| DTO | 필드 | 비고 |
| --- | --- | --- |
| AddBookToShelfRequest | bookId, sectionId, slotNo, displayStyle | ✓ |
| AddKakaoBookToShelfRequest | isbn13, isbn10, title, author, publisher, publishedDate, description, coverImageUrl, sectionId, slotNo, displayStyle | ✓ |
| UpdateUserBookRequest | readingStatus, ownerRating, selectedCoverProductId | ✓ |
| MoveUserBookRequest | bookshelfId, sectionId, slotNo, displayStyle | ✓ |
| UserBookResponse | id, userId, bookId, readingStatus, storageStatus, ownerRating, liked | ✓ |
| UserBookListResponse | books[] | ✓ |

### Bookshelf

| DTO | 필드 | 비고 |
| --- | --- | --- |
| BookshelfResponse | id, spaceId, name, description, visibilityScope, displayOrder, bodyInventoryId, skinInventoryId | ✓ |
| BookshelfListResponse | bookshelves[] | ✓ |
| CreateBookshelfRequest | spaceId, bodyInventoryId, name, description, visibilityScope | ✓ |
| UpdateBookshelfRequest | name, description, visibilityScope, skinInventoryId | ✓ |
| ReorderBookshelvesRequest | bookshelfIds[] | ✓ |
| SectionResponse | id, rowNo, columnNo, maxItemCount, alignment | ✓ |
| SectionAlignmentRequest | alignment | ✓ |
| InteriorObjectRequest | assetInventoryId, sectionId, slotNo, displayStyle | ✓ |
| BookshelfItemRequest | bookshelfId, sectionId, slotNo, displayStyle | ✓ |
| BookshelfItemResponse | id, bookshelfId, sectionId, itemType, userBookId, assetInventoryId, slotNo, displayStyle | ✓ |

### Space

| DTO | 필드 | 비고 |
| --- | --- | --- |
| SpaceResponse | id, ownerUserId, spaceName, visibilityScope, guestbookVisibilityScope, visitCount, likeCount, layoutVersion, currentThemeInventoryId | ✓ |
| DashboardResponse | space, followerCount, followingCount, bookshelfCount, bookCount, readingRecordCount | ✓ |
| LayoutSaveRequest | layoutPayload, baseLayoutVersion | ✓ |
| LayoutSaveResponse | spaceId, layoutVersion | ✓ |
| VisibilityRequest | visibilityScope | ✓ |
| ThemeApplyRequest | inventoryId | ✓ |
| SpaceTemplateResponse | id, code, name, previewImageUrl, layoutPayload | ✓ |
| SpaceTemplateListResponse | templates[] | ✓ |

### Feed

| DTO | 필드 | 비고 |
| --- | --- | --- |
| FeedEventResponse | id, actorUserId, eventType, targetType, targetId, occurredAt | ⚠ → ✓ — `occurredAt`은 Unity string vs Backend LocalDateTime, ISO-8601 문자열로 호환. |
| FeedEventListResponse | events[] | ✓ |
| BookPromotionResponse | id, bookId, promotionType, title, description, imageUrl, landingUrl, sponsorName | ✓ |
| BookPromotionListResponse | promotions[] | ✓ |

### Notification

| DTO | 필드 | 비고 |
| --- | --- | --- |
| NotificationResponse | id, senderUserId, type, targetType, targetId, message, read | ✓ |
| NotificationListResponse | notifications[] | ✓ |

### Payment (`page-charge` 외)

| DTO | 필드 | 비고 |
| --- | --- | --- |
| PageChargeRequest | pagesAmount, amountKrw, platform | ✓ — 백엔드 record 정의 직접 확인 필요 시 [PaymentDtos.java](../../src/main/java/com/dotshelf/domain/payment/dto/PaymentDtos.java). |
| PurchaseHistoryResponse | id, assetId, pricePages, status | ✓ |
| PurchaseHistoryListResponse | purchases[] | ✓ |
| PaymentMethodListResponse | methods[] (List\<String\>) | ✓ |

### Asset

| DTO | 필드 | 비고 |
| --- | --- | --- |
| AssetResponse | id, categoryId, code, name, tier, assetType, previewImageUrl, pricePages, owned, wished | ✓ |
| AssetListResponse | assets[] | ✓ |
| InventoryResponse | assetIds[] | ✓ |
| AssetPurchaseResponse | purchaseId, assetId, inventoryId, pricePages | ✓ |

### Guestbook

| DTO | 필드 | 비고 |
| --- | --- | --- |
| GuestbookRequest | postItInventoryId, content, visibilityScope | ✓ |
| GuestbookResponse | id, spaceId, writerUserId, postItInventoryId, content, visibilityScope, replyCount | ✓ |
| GuestbookReplyRequest | content, visibilityScope | ✓ |
| GuestbookReplyResponse | id, guestbookId, writerUserId, content, visibilityScope | ✓ |

### ReadingRecord & Comment

| DTO | 필드 | 비고 |
| --- | --- | --- |
| ReadingRecordRequest | userBookId, title, content, visibilityScope | ✓ |
| ReadingRecordResponse | id, userBookId, title, content, visibilityScope, likeCount, commentCount | ✓ |
| ReadingRecordListResponse | records[] | ✓ |
| CommentRequest | parentCommentId, content | ✓ |
| CommentResponse | id, readingRecordId, userId, parentCommentId, depth, content, likeCount, deleted | ✓ |
| CommentListResponse | comments[] | ✓ |

### Report

| DTO | 필드 | 비고 |
| --- | --- | --- |
| ReportRequest | reportedUserId, targetType, targetId, reasonType, reasonDetail | ✓ |
| ReportResponse | id, reporterUserId, reportedUserId, targetType, targetId, reasonType, status | ✓ |
| ReportListResponse | reports[] | ✓ |

---

## C. 백엔드 전용 DTO (➕) — Unity 미사용

다음 DTO들은 백엔드에만 정의돼 있고, 현 Unity 클라이언트는 해당 엔드포인트를 호출하지 않음. 향후 Unity가 호출을 추가하면 그대로 사용 가능.

| DTO | 백엔드 위치 | 사용 엔드포인트 | 메모 |
| --- | --- | --- | --- |
| BookListResponse | [BookDtos.java](../../src/main/java/com/dotshelf/domain/book/dto/BookDtos.java) | GET `/v1/books/recommended`, GET `/v1/books/ranking` | 필드 `items[]`, `nextCursor`. **Unity가 `/v1/books?category=RANKING`을 부르고 있는 상황 → 이쪽으로 마이그레이션 시 BookResponse 갭(위 A 섹션)도 동반 수반.** |
| BookReviewResponse / BookReviewListResponse | [BookDtos.java](../../src/main/java/com/dotshelf/domain/book/dto/BookDtos.java) | GET/POST `/v1/books/{bookId}/reviews` | 책 상세 페이지 리뷰 UI 도입 시. |
| UserBookPlacementResponse / List | [BookDtos.java](../../src/main/java/com/dotshelf/domain/book/dto/BookDtos.java) | GET `/v1/user-books` | 책장 슬롯 데이터. |
| BookshelfRenderResponse | [BookshelfDtos.java](../../src/main/java/com/dotshelf/domain/bookshelf/dto/BookshelfDtos.java) | GET `/v1/bookshelves/{bookshelfId}/items` | 책장 + 섹션 + 아이템 + 책/에셋 요약을 한방에 내려주는 통합 응답. **Unity가 책장을 그리려면 결국 이 응답이 필요할 가능성 높음.** |
| BookSummaryResponse / AssetSummaryResponse | [BookshelfDtos.java](../../src/main/java/com/dotshelf/domain/bookshelf/dto/BookshelfDtos.java) | BookshelfRenderResponse 내부 | |
| SpaceListItemResponse / SpaceListResponse | [SpaceDtos.java](../../src/main/java/com/dotshelf/domain/space/dto/SpaceDtos.java) | GET `/v1/spaces` | 다른 사용자 공간 검색/리스트. |
| AssetCategoryResponse / List | [AssetDtos.java](../../src/main/java/com/dotshelf/domain/asset/dto/AssetDtos.java) | GET `/v1/assets/categories` | 에셋 필터 UI에 활용. |

---

## 백엔드 변경 액션 (Unity = source of truth)

> **방향성**: 백엔드를 Unity 클라이언트에 맞춰 수정한다. Unity 측 코드/DTO/경로는 변경하지 않는다.
> ⊖(Unity 기대, 백엔드 누락) 항목은 모두 백엔드에서 메운다.
> ⊕(백엔드만 존재) 항목은 Unity가 무시하므로 무해. 임의 제거 금지 — 서버 내부/관리 도구가 의존할 수 있음. 제거가 필요하면 별도 PR로 별도 승인.

우선순위 순.

### P0 — Unity 호출이 현재 깨져 있을 가능성, 즉시 처리

1. **`GET /v1/books?category=RANKING&limit={limit}` 라우트 추가** ([매핑 #25](unity-backend-mapping.md#book-16행))
   - 호출 지점: [ApiBookCatalogRepository.cs:57](../../../frontend/Assets/App/Infrastructure/Api/ApiBookCatalogRepository.cs#L57). 메인 책 카탈로그 로딩이 매번 404 → Mock fallback으로 떨어지는 중.
   - **호출 형태 (코드 확인)**: `GET {baseUrl}/v1/books?category=RANKING&limit=48` (limit은 4×4×3 고정, 추가 쿼리 없음).
   - **🔴 인증 정책 — 중요**: 이 호출은 표준 `APIManager`가 아니라 [ApiBookCatalogRepository.cs:54](../../../frontend/Assets/App/Infrastructure/Api/ApiBookCatalogRepository.cs#L54)에서 **순수 `HttpClient`를 직접 인스턴스화**해서 쓰고, `Authorization` 헤더를 **붙이지 않음**. 즉 이 호출은 **익명/비인증으로 백엔드에 도달**한다.
     - 따라서 새 라우트는 [SecurityConfig.java](../../src/main/java/com/dotshelf/global/config/SecurityConfig.java)의 `permitAll()` 목록에 추가해야 함.
     - 기존 `/v1/books/ranking`은 현재 `authenticated`이므로 거기로 위임만 시키면 안 되고, 새 라우트의 시큐리티 분기 자체를 별도로 풀어야 함.
   - **백엔드 변경안**:
     - `BookController`에 `@GetMapping("/v1/books")` 추가 (`/v1/books/ranking`과는 분리).
     - `@RequestParam(required = false) String category`, `@RequestParam(defaultValue = "48") int limit` 받기.
     - 내부에서 `BookService.findRanking(...)` 호출 (기존 `ranking()` 핸들러의 비즈니스 로직 재사용). 단 호출 컨텍스트가 비인증이므로 `@CurrentUser`를 요구하지 않는 시그니처가 필요. 만약 `findRanking`이 현재 사용자 의존(예: `owned` 필드 채움)이면, 비인증 호출용 오버로드 추가.
     - SecurityConfig의 `requestMatchers(...).permitAll()`에 `"/v1/books"` 추가 (이미 `/v1/auth/**` 등 노출 중).
     - 응답은 기존 `BookListResponse` 그대로 — 단 [BookResponse 항목 5번](#5-bookresponsesource--sourceurl-추가)도 동시 처리.
   - **시그니처 차이**: 기존 `/v1/books/ranking`은 더 풍부한 쿼리(genre/priceType/sort/cursor) 받음 → 새 라우트는 모든 쿼리를 optional로 받고 미지정 시 기본값 사용.
   - 컨트롤러 파일: [BookController.java](../../src/main/java/com/dotshelf/domain/book/controller/BookController.java)

2. **`SocialLoginRequest.nonce` 검증 강제 여부 확인 + optional 처리**
   - Unity가 [NormalizeSocialLoginRequest](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L537-L549)에서 `nonce`를 절대 보내지 않음.
   - **백엔드 변경안**:
     - `SocialLoginService` (또는 Provider별 검증 클래스)에서 `nonce` 검증을 optional/best-effort로 다운그레이드. Unity가 미송신 시 검증 스킵, 송신 시 검증 수행.
     - `SocialLoginRequest` record는 `nonce`를 nullable로 유지 (이미 그렇게 정의돼 있을 가능성 높음 — 코드 확인 후 결정).
   - 검증 결과 “현재 nonce가 강제 아님”이면 액션 없음. 강제면 위처럼 변경.
   - 컨트롤러/서비스: `domain/auth/` 하위.

### P1 — 작은 record 변경으로 Unity가 즉시 활용

3. **`AuthTokenResponse.tokenType / expiresIn` 필드 추가**
   - 적용 엔드포인트: POST `/v1/auth/social-login`, POST `/v1/auth/token/refresh`.
   - **백엔드 변경안**:
     - [AuthDtos.java](../../src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java) `AuthTokenResponse` record에 `String tokenType, long expiresIn` 추가.
     - 응답 매핑(서비스 또는 컨트롤러)에서 `tokenType = "Bearer"`, `expiresIn = JwtProperties.accessTtlSeconds`(현재 1800).
   - Unity DTO에 이미 두 필드 정의돼 있어, 백엔드만 추가하면 끝.

4. **`GuestSessionResponse.guestSessionId` + `GuestMigrationRequest.guestSessionId` 추가**
   - **백엔드 변경안**:
     - `GuestSessionResponse` record에 `String guestSessionId` 추가. 발급 시 UUID 생성하거나 게스트 토큰 클레임에서 추출. 저장 위치는 `guest_sessions` 테이블이 있다면 그쪽 PK.
     - `GuestMigrationRequest` record에 `String guestSessionId` 추가. 마이그레이션 핸들러에서 세션 ID로 게스트 데이터 조회 후 회원에 합치도록 서비스 로직 보강 필요.
   - 만약 백엔드가 “세션 ID 추적 안 한다”는 정책이면, 두 필드를 그냥 빈 문자열/`null`로 응답해도 Unity는 받을 수 있음 (호환). 단 Unity가 마이그레이션에 그 ID를 의미 있게 쓰려면 백엔드도 매칭되는 식별자를 발급/검증해야 함.

### P2 — Unity가 표시·분기에 의존하는 필드

5. **`BookResponse.source / sourceUrl` 추가**
   - Unity가 KAKAO/LOCAL 분기 + KAKAO 원본 URL 표시에 사용.
   - **백엔드 변경안**:
     - [BookDtos.java](../../src/main/java/com/dotshelf/domain/book/dto/BookDtos.java) `BookResponse` record에 `String source, String sourceUrl` 추가.
     - DB의 `books.source`(또는 책 origin 컬럼) + KAKAO 원본 URL 필드를 채워서 매핑.
     - 동일 갭이 [BookSearchResponse](#booksearchlistresponse)에는 이미 반영돼 있으므로 (Unity 호환), `BookResponse`만 보강하면 양쪽 일관됨.
   - 추가로 Unity가 받지 못해도 무해한 ⊖ 4개 중 `purchaseCount, likeCount`는 Unity가 표시하면 좋음 (소셜 메트릭) — 시간 여유 있을 때 같이 메우기 권장.

6. **`/v1/bookshelves/{bookshelfId}/items` 호출 부재 — 정책 결정 필요**
   - Unity가 책장 슬롯 데이터를 백엔드에서 안 가져오는 게 의도된 건지 확인.
   - 백엔드 변경 자체는 필요 없음 (이미 핸들러 존재). 다만 매핑 표에 “Unity가 향후 호출 예정인지 미정”으로 표기.
   - Unity 측 누락이라면 Unity 트랙에 별도 이슈 등록 — 백엔드 작업 범위는 아님.

### P3 — 호환됨, 변경 권장 X (정보용)

| # | 항목 | 백엔드 액션 |
| --- | --- | --- |
| 7 | `WalletResponse.updatedAt` | 그대로 둠. Unity 무시 OK. |
| 8 | `BookSearchListResponse.page / size / isEnd` | 그대로 둠. Unity는 cursor 기반만 사용. |
| 9 | `ApiResponse.httpStatus` (int ↔ string) | 그대로 둠. Newtonsoft 자동 변환. |
| 10 | Unity 송신 `access` 헤더 | 그대로 둠. 백엔드 `JwtAuthenticationFilter`는 `Authorization`만 읽음. Unity 클라이언트가 보내는 부수 헤더라 무시되어도 무해. **백엔드에서 `access` 헤더를 추가 인식하도록 만들지는 필요 없음** — 이미 정상 헤더로 인증 통과 중. |
| 11 | `BookResponse`의 ⊕ 7필드(`genre/pricePages/reviewCount/viewCount/owned/userBookId/rank`) | 그대로 둠. Unity가 무시하지만 서버 다른 컨슈머/관리/통계가 의존할 수 있음. |

---

## 검증/추적

이 문서는 코드 진실(source-of-truth)이 아니라 **현재 시점 스냅샷**이다. 다음 케이스에서 갱신 필요:

- 백엔드 DTO 추가/수정 (record 필드 변경)
- Unity 다음 빌드에서 신규 엔드포인트 호출 추가
- Unity DTO 변경 (`Assets/App/Infrastructure/Api/Dtos/` 하위 파일 변경)

PR 리뷰 시 위 변경이 보이면 본 문서와 [unity-backend-mapping.md](unity-backend-mapping.md) 갱신 의무.
