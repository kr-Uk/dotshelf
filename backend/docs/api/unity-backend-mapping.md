# Unity ↔ 백엔드 API 매핑표

> Unity 클라이언트(`frontend/`)가 실제로 호출하는 모든 엔드포인트와 Spring Boot 백엔드(`backend/`)의 노출 엔드포인트를 1:1 매핑한 표. 백엔드 PR 리뷰 시 “Unity가 이걸 부르고 있다”의 1차 레퍼런스.
>
> - **Unity 호출 카운트**: 87
> - **백엔드 전용 (Unity 미호출)**: 9
> - **총 행**: 96
> - 마지막 갱신: 2026-04-29
> - 짝꿍 문서: [unity-backend-dto-audit.md](unity-backend-dto-audit.md) — DTO 필드 갭 상세 비교

## 정합성 기호

| 기호 | 의미 |
| --- | --- |
| ✓ | 경로/메서드/요청·응답 DTO 필드가 모두 매칭 |
| ⚠ | 경로/메서드는 매칭, DTO 필드에 갭 있음 (audit 문서에서 자세히) |
| ✗ | 경로 자체가 백엔드에 없음 또는 이름 불일치 |
| ➕ | 백엔드만 노출, Unity 미호출 (정보용) |

## 공통 컨벤션 (모든 행에 공통 적용)

- **응답 래퍼**: 모든 응답은 [ApiResponse.java](../../src/main/java/com/dotshelf/global/common/ApiResponse.java)로 래핑. Unity는 [ApiEnvelopeDto](../../../frontend/Assets/App/Infrastructure/Api/Dtos/ApiEnvelopeDto.cs)로 역직렬화. 표의 “응답” 컬럼은 envelope 안의 `result` 타입만 표기.
- **인증 헤더**: Unity는 `Authorization: Bearer {token}` + 커스텀 `access: {token}` 두 개를 전송. 백엔드는 [JwtAuthenticationFilter.java](../../src/main/java/com/dotshelf/global/security/JwtAuthenticationFilter.java)에서 `Authorization` 만 읽고 `access`는 무시.
- **JWT 정책**: HS256, access 30분(1800s), refresh 30일. permitAll 경로는 [SecurityConfig.java](../../src/main/java/com/dotshelf/global/config/SecurityConfig.java) 참고.
- **에러 코드 카탈로그**: [ErrorCode.java](../../src/main/java/com/dotshelf/global/error/ErrorCode.java).

---

## Health (1행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | GET | `/health` | [APIManager.cs:61](../../../frontend/Assets/App/Infrastructure/Api/APIManager.cs#L61) | `HealthController.health()` | ✓ | 인증 불필요. Unity는 `AppApiConfig.HealthCheckPath`로 경로 커스터마이즈 가능. |

요약: ✓ 1.

---

## Auth (6행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 2 | POST | `/v1/auth/guest-session` | [NotebookApiClient.cs:25](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L25) | `AuthController.startGuestSession()` | ⚠ | 응답 [GuestSessionResponse](unity-backend-dto-audit.md#guestsessionresponse)에 `guestSessionId` 누락. |
| 3 | POST | `/v1/auth/social-login` | [NotebookApiClient.cs:30](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L30) | `AuthController.socialLogin()` | ⚠ | 요청 [SocialLoginRequest](unity-backend-dto-audit.md#socialloginrequest) 필드 갭 (`idToken`/`accessToken` vs `nonce`), 응답 [AuthTokenResponse](unity-backend-dto-audit.md#authtokenresponse) `tokenType`/`expiresIn` 누락. |
| 4 | POST | `/v1/auth/token/refresh` | [NotebookApiClient.cs:35](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L35) | `AuthController.refresh()` | ⚠ | 응답 [AuthTokenResponse](unity-backend-dto-audit.md#authtokenresponse) `tokenType`/`expiresIn` 누락. |
| 5 | POST | `/v1/auth/logout` | [NotebookApiClient.cs:40](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L40) | `AuthController.logout()` | ✓ | 요청 `LogoutRequest{refreshToken}`. 응답 Void. |
| 6 | POST | `/v1/auth/guest-migration` | [NotebookApiClient.cs:45](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L45) | `AuthController.importGuestData()` | ⚠ | 요청 [GuestMigrationRequest](unity-backend-dto-audit.md#guestmigrationrequest)에 Unity는 `guestSessionId`도 보내지만 백엔드는 `payload`만 받음. |

요약: ✓ 1 / ⚠ 4 / ✗ 0.

컨트롤러: [AuthController.java](../../src/main/java/com/dotshelf/domain/auth/controller/AuthController.java)

---

## User (16행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 7 | GET | `/v1/users/me` | [NotebookApiClient.cs:242](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L242) | `UserController.getMe()` | ✓ | [UserProfileResponse](unity-backend-dto-audit.md#userprofileresponse). |
| 8 | PATCH | `/v1/users/me` | [NotebookApiClient.cs:247](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L247) | `UserController.updateMe()` | ✓ | 요청/응답 모두 매칭. |
| 9 | DELETE | `/v1/users/me` | [NotebookApiClient.cs:252](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L252) | `UserController.deleteMe()` | ✓ | Void 응답. |
| 10 | GET | `/v1/users/{userId}` | [NotebookApiClient.cs:257](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L257) | `UserController.getProfile()` | ✓ | |
| 11 | GET | `/v1/users/nickname/check` | [NotebookApiClient.cs:262](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L262) | `UserController.checkNickname()` | ✓ | 쿼리 `?nickname=...`. |
| 12 | POST | `/v1/users/{targetUserId}/follow` | [NotebookApiClient.cs:267](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L267) | `UserController.follow()` | ✓ | [ToggleResponse](unity-backend-dto-audit.md#toggleresponse). |
| 13 | DELETE | `/v1/users/{targetUserId}/follow` | [NotebookApiClient.cs:272](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L272) | `UserController.unfollow()` | ✓ | |
| 14 | GET | `/v1/users/me/followers` | [NotebookApiClient.cs:277](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L277) | `UserController.getFollowers()` | ✓ | [UserListResponse](unity-backend-dto-audit.md#userlistresponse). |
| 15 | GET | `/v1/users/me/followings` | [NotebookApiClient.cs:282](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L282) | `UserController.getFollowings()` | ✓ | |
| 16 | POST | `/v1/users/{targetUserId}/block` | [NotebookApiClient.cs:287](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L287) | `UserController.block()` | ✓ | |
| 17 | DELETE | `/v1/users/{targetUserId}/block` | [NotebookApiClient.cs:292](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L292) | `UserController.unblock()` | ✓ | |
| 18 | GET | `/v1/users/me/blocks` | [NotebookApiClient.cs:297](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L297) | `UserController.getBlocks()` | ✓ | |
| 19 | GET | `/v1/users/me/notification-settings` | [NotebookApiClient.cs:302](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L302) | `UserController.getNotificationSetting()` | ✓ | [NotificationSettingResponse](unity-backend-dto-audit.md#notificationsettingresponse). |
| 20 | PATCH | `/v1/users/me/notification-settings` | [NotebookApiClient.cs:307](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L307) | `UserController.updateNotificationSetting()` | ✓ | |
| 21 | GET | `/v1/users/me/feed-settings` | [NotebookApiClient.cs:312](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L312) | `UserController.getFeedSetting()` | ⚠ | [FeedSettingResponse](unity-backend-dto-audit.md#feedsettingresponse) 필드명 갭 — Unity 응답 DTO가 명시 안 돼 있어 맹목 역직렬화 의존. |
| 22 | PATCH | `/v1/users/me/feed-settings` | [NotebookApiClient.cs:317](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L317) | `UserController.updateFeedSetting()` | ⚠ | 위와 동일. |

요약: ✓ 14 / ⚠ 2 / ✗ 0.

컨트롤러: [UserController.java](../../src/main/java/com/dotshelf/domain/user/controller/UserController.java)

---

## Book (16행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 23 | GET | `/v1/books/search` | [NotebookApiClient.cs:61](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L61) | `BookController.search()` | ⚠ | 응답 [BookSearchListResponse](unity-backend-dto-audit.md#booksearchlistresponse) — 백엔드는 `page/size/isEnd` 추가, Unity는 무시. 호환됨. |
| 24 | GET | `/v1/books/{bookId}` | [NotebookApiClient.cs:66](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L66) | `BookController.getBook()` | ⚠ | 응답 [BookResponse](unity-backend-dto-audit.md#bookresponse) 큰 갭. Unity가 모르는 `genre/pricePages/reviewCount/viewCount/owned/userBookId/rank` 7필드, 백엔드가 없는 `source/sourceUrl/purchaseCount/likeCount` 4필드. |
| 25 | GET | `/v1/books?category=RANKING&limit={limit}` | [ApiBookCatalogRepository.cs:57](../../../frontend/Assets/App/Infrastructure/Api/ApiBookCatalogRepository.cs#L57) | **(없음)** | ✗ | **백엔드에 해당 경로 없음.** 백엔드는 `GET /v1/books/ranking?genre=&priceType=&sort=&cursor=&limit=`로 분리됨. Unity가 메인 카탈로그 로딩 시 매번 404 → Mock fallback 발생 가능. 액션: 백엔드에 호환 라우트 추가 또는 Unity가 `/v1/books/ranking`으로 마이그레이션. |
| 26 | POST | `/v1/bookshelves/{bookshelfId}/books` | [NotebookApiClient.cs:71](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L71) | `BookController.addToShelf()` | ✓ | 요청 [AddBookToShelfRequest](unity-backend-dto-audit.md#addbooktoshelfrequest), 응답 [UserBookResponse](unity-backend-dto-audit.md#userbookresponse). |
| 27 | POST | `/v1/bookshelves/{bookshelfId}/books/kakao` | [NotebookApiClient.cs:76](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L76) | `BookController.addKakaoBookToShelf()` | ✓ | [AddKakaoBookToShelfRequest](unity-backend-dto-audit.md#addkakaobooktoshelfrequest). |
| 28 | PATCH | `/v1/user-books/{userBookId}` | [NotebookApiClient.cs:81](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L81) | `BookController.updateUserBook()` | ✓ | [UpdateUserBookRequest](unity-backend-dto-audit.md#updateuserbookrequest). |
| 29 | POST | `/v1/user-books/{userBookId}/move` | [NotebookApiClient.cs:86](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L86) | `BookController.move()` | ✓ | [MoveUserBookRequest](unity-backend-dto-audit.md#moveuserbookrequest). |
| 30 | POST | `/v1/user-books/{userBookId}/archive` | [NotebookApiClient.cs:91](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L91) | `BookController.archive()` | ✓ | |
| 31 | DELETE | `/v1/user-books/{userBookId}` | [NotebookApiClient.cs:96](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L96) | `BookController.delete()` | ✓ | Void. |
| 32 | GET | `/v1/user-books/archived` | [NotebookApiClient.cs:101](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L101) | `BookController.getArchived()` | ✓ | [UserBookListResponse](unity-backend-dto-audit.md#userbooklistresponse). |
| 33 | POST | `/v1/user-books/{userBookId}/like` | [NotebookApiClient.cs:106](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L106) | `BookController.like()` | ✓ | |
| 34 | DELETE | `/v1/user-books/{userBookId}/like` | [NotebookApiClient.cs:111](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L111) | `BookController.unlike()` | ✓ | |
| - | GET | `/v1/books/recommended` | (미호출) | `BookController.recommended()` | ➕ | 백엔드만 노출. 응답 [BookListResponse](unity-backend-dto-audit.md#booklistresponse). |
| - | GET | `/v1/books/ranking` | (미호출) | `BookController.ranking()` | ➕ | 백엔드만 노출. **#25 ✗ 행과 짝**. Unity 마이그레이션 후보. |
| - | GET | `/v1/books/{bookId}/reviews` | (미호출) | `BookController.getReviews()` | ➕ | [BookReviewListResponse](unity-backend-dto-audit.md#bookreviewlistresponse). |
| - | POST | `/v1/books/{bookId}/reviews` | (미호출) | `BookController.createReview()` | ➕ | [BookReviewResponse](unity-backend-dto-audit.md#bookreviewresponse). |

요약: ✓ 9 / ⚠ 2 / ✗ 1 / ➕ 4.

컨트롤러: [BookController.java](../../src/main/java/com/dotshelf/domain/book/controller/BookController.java)

---

## Bookshelf (10행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 35 | GET | `/v1/spaces/{spaceId}/bookshelves` | [NotebookApiClient.cs:427](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L427) | `BookshelfController.getBookshelves()` | ✓ | [BookshelfListResponse](unity-backend-dto-audit.md#bookshelflistresponse). |
| 36 | POST | `/v1/bookshelves` | [NotebookApiClient.cs:432](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L432) | `BookshelfController.create()` | ✓ | [CreateBookshelfRequest](unity-backend-dto-audit.md#createbookshelfrequest). |
| 37 | PATCH | `/v1/bookshelves/{bookshelfId}` | [NotebookApiClient.cs:437](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L437) | `BookshelfController.update()` | ✓ | [UpdateBookshelfRequest](unity-backend-dto-audit.md#updatebookshelfrequest). |
| 38 | DELETE | `/v1/bookshelves/{bookshelfId}` | [NotebookApiClient.cs:442](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L442) | `BookshelfController.delete()` | ✓ | Void. |
| 39 | PUT | `/v1/bookshelves/order` | [NotebookApiClient.cs:447](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L447) | `BookshelfController.reorder()` | ✓ | [ReorderBookshelvesRequest](unity-backend-dto-audit.md#reorderbookshelvesrequest). |
| 40 | PATCH | `/v1/bookshelves/{bookshelfId}/sections/{sectionId}/alignment` | [NotebookApiClient.cs:452](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L452) | `BookshelfController.updateSectionAlignment()` | ✓ | [SectionResponse](unity-backend-dto-audit.md#sectionresponse). |
| 41 | POST | `/v1/bookshelves/{bookshelfId}/interior-objects` | [NotebookApiClient.cs:457](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L457) | `BookshelfController.addInteriorObject()` | ✓ | [BookshelfItemResponse](unity-backend-dto-audit.md#bookshelfitemresponse). |
| 42 | PATCH | `/v1/bookshelf-items/{itemId}` | [NotebookApiClient.cs:462](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L462) | `BookshelfController.moveItem()` | ✓ | |
| - | GET | `/v1/bookshelves/{bookshelfId}/items` | (미호출) | `BookshelfController.getItems()` | ➕ | **중요**: 책장 렌더 데이터([BookshelfRenderResponse](unity-backend-dto-audit.md#bookshelfrenderresponse) — 섹션/아이템/책 정보 통합). Unity가 “내 책장의 어떤 슬롯에 어떤 책이 꽂혔나”를 가져오는 호출이 현재 없음 — 추후 합류 필요. |
| - | GET | `/v1/user-books` | (미호출) | `BookController.getUserBooks()` | ➕ | 위와 묶음. 필터/페이지네이션 지원 ([UserBookPlacementListResponse](unity-backend-dto-audit.md#userbookplacementlistresponse)). |

요약: ✓ 8 / ⚠ 0 / ✗ 0 / ➕ 2. 단 ➕ 두 개가 “Unity가 책장 데이터를 어디서 가져오는가”라는 큰 누락을 시사.

컨트롤러: [BookshelfController.java](../../src/main/java/com/dotshelf/domain/bookshelf/controller/BookshelfController.java) (`/v1/user-books` GET은 [BookController.java](../../src/main/java/com/dotshelf/domain/book/controller/BookController.java))

---

## Space (12행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 43 | GET | `/v1/spaces/me` | [NotebookApiClient.cs:152](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L152) | `SpaceController.getMySpace()` | ✓ | [SpaceResponse](unity-backend-dto-audit.md#spaceresponse). |
| 44 | GET | `/v1/spaces/{spaceId}` | [NotebookApiClient.cs:157](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L157) | `SpaceController.getSpace()` | ✓ | |
| 45 | GET | `/v1/spaces/{spaceId}/dashboard` | [NotebookApiClient.cs:162](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L162) | `SpaceController.getDashboard()` | ✓ | 쿼리 `?period=daily`. [DashboardResponse](unity-backend-dto-audit.md#dashboardresponse). |
| 46 | POST | `/v1/spaces/{spaceId}/visits` | [NotebookApiClient.cs:167](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L167) | `SpaceController.recordVisit()` | ✓ | |
| 47 | POST | `/v1/spaces/{spaceId}/like` | [NotebookApiClient.cs:172](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L172) | `SpaceController.like()` | ✓ | |
| 48 | DELETE | `/v1/spaces/{spaceId}/like` | [NotebookApiClient.cs:177](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L177) | `SpaceController.unlike()` | ✓ | |
| 49 | PUT | `/v1/spaces/me/layout` | [NotebookApiClient.cs:182](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L182) | `SpaceController.saveLayout()` | ✓ | [LayoutSaveRequest](unity-backend-dto-audit.md#layoutsaverequest)/[LayoutSaveResponse](unity-backend-dto-audit.md#layoutsaveresponse). |
| 50 | PATCH | `/v1/spaces/me/theme` | [NotebookApiClient.cs:187](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L187) | `SpaceController.applyTheme()` | ✓ | [ThemeApplyRequest](unity-backend-dto-audit.md#themeapplyrequest). |
| 51 | PATCH | `/v1/spaces/me/visibility` | [NotebookApiClient.cs:192](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L192) | `SpaceController.updateVisibility()` | ✓ | [VisibilityRequest](unity-backend-dto-audit.md#visibilityrequest). |
| 52 | PATCH | `/v1/spaces/me/guestbook-visibility` | [NotebookApiClient.cs:197](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L197) | `SpaceController.updateGuestbookVisibility()` | ✓ | |
| 53 | GET | `/v1/spaces/templates` | [NotebookApiClient.cs:202](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L202) | `SpaceController.getTemplates()` | ✓ | [SpaceTemplateListResponse](unity-backend-dto-audit.md#spacetemplatelistresponse). |
| - | GET | `/v1/spaces` | (미호출) | `SpaceController.getSpaces()` | ➕ | 검색/리스트. [SpaceListResponse](unity-backend-dto-audit.md#spacelistresponse) — 다른 사용자 공간 탐색 UI에서 활용 가능. |

요약: ✓ 11 / ➕ 1.

컨트롤러: [SpaceController.java](../../src/main/java/com/dotshelf/domain/space/controller/SpaceController.java)

---

## Feed (4행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 54 | GET | `/v1/feed/following` | [NotebookApiClient.cs:207](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L207) | `FeedController.getFollowingFeed()` | ✓ | [FeedEventListResponse](unity-backend-dto-audit.md#feedeventlistresponse). |
| 55 | GET | `/v1/feed/recommended-records` | [NotebookApiClient.cs:212](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L212) | `FeedController.getRecommendedRecords()` | ✓ | |
| 56 | GET | `/v1/feed/recommended-books` | [NotebookApiClient.cs:217](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L217) | `FeedController.getRecommendedBooks()` | ✓ | |
| 57 | GET | `/v1/feed/book-promotions` | [NotebookApiClient.cs:222](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L222) | `FeedController.getBookPromotions()` | ✓ | [BookPromotionListResponse](unity-backend-dto-audit.md#bookpromotionlistresponse). |

요약: ✓ 4.

컨트롤러: [FeedController.java](../../src/main/java/com/dotshelf/domain/feed/controller/FeedController.java)

---

## Notification (3행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 58 | GET | `/v1/notifications` | [NotebookApiClient.cs:342](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L342) | `NotificationController.getNotifications()` | ✓ | [NotificationListResponse](unity-backend-dto-audit.md#notificationlistresponse). |
| 59 | PATCH | `/v1/notifications/{notificationId}/read` | [NotebookApiClient.cs:347](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L347) | `NotificationController.read()` | ✓ | |
| 60 | PATCH | `/v1/notifications/read-all` | [NotebookApiClient.cs:352](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L352) | `NotificationController.readAll()` | ✓ | Void. |

요약: ✓ 3.

컨트롤러: [NotificationController.java](../../src/main/java/com/dotshelf/domain/notification/controller/NotificationController.java)

---

## Payment (4행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 61 | POST | `/v1/payments/page-charge` | [NotebookApiClient.cs:227](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L227) | `PaymentController.charge()` | ⚠ | 응답 [WalletResponse](unity-backend-dto-audit.md#walletresponse) — 백엔드는 `updatedAt` 추가, Unity는 무시 (호환). |
| 62 | GET | `/v1/payments/purchases` | [NotebookApiClient.cs:232](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L232) | `PaymentController.getPurchases()` | ✓ | [PurchaseHistoryListResponse](unity-backend-dto-audit.md#purchasehistorylistresponse). |
| 63 | GET | `/v1/payments/methods` | [NotebookApiClient.cs:237](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L237) | `PaymentController.getMethods()` | ✓ | |
| - | GET | `/v1/payments/wallet` | (미호출) | `PaymentController.getWallet()` | ➕ | 잔액 단독 조회. 잔액 표시 UI가 page-charge 응답 외에 따로 필요해지면 호출 추가. |

요약: ✓ 2 / ⚠ 1 / ➕ 1.

컨트롤러: [PaymentController.java](../../src/main/java/com/dotshelf/domain/payment/controller/PaymentController.java)

---

## Asset (7행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 64 | GET | `/v1/assets` | [NotebookApiClient.cs:122](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L122) | `AssetController.getAssets()` | ✓ | 쿼리 `?category=&sort=&limit=`. [AssetListResponse](unity-backend-dto-audit.md#assetlistresponse). |
| 65 | GET | `/v1/assets/{assetId}` | [NotebookApiClient.cs:127](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L127) | `AssetController.getAsset()` | ✓ | [AssetResponse](unity-backend-dto-audit.md#assetresponse). |
| 66 | GET | `/v1/assets/me/inventory` | [NotebookApiClient.cs:132](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L132) | `AssetController.getInventory()` | ✓ | [InventoryResponse](unity-backend-dto-audit.md#inventoryresponse). |
| 67 | POST | `/v1/assets/{assetId}/wishlist` | [NotebookApiClient.cs:137](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L137) | `AssetController.wish()` | ✓ | |
| 68 | DELETE | `/v1/assets/{assetId}/wishlist` | [NotebookApiClient.cs:142](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L142) | `AssetController.unwish()` | ✓ | |
| 69 | POST | `/v1/assets/{assetId}/purchase` | [NotebookApiClient.cs:147](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L147) | `AssetController.purchase()` | ✓ | [AssetPurchaseResponse](unity-backend-dto-audit.md#assetpurchaseresponse). |
| - | GET | `/v1/assets/categories` | (미호출) | `AssetController.getCategories()` | ➕ | [AssetCategoryListResponse](unity-backend-dto-audit.md#assetcategorylistresponse). 인증 불필요. 카테고리 필터 UI에서 호출 추가 후보. |

요약: ✓ 6 / ➕ 1.

컨트롤러: [AssetController.java](../../src/main/java/com/dotshelf/domain/asset/controller/AssetController.java)

---

## Guestbook (4행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 70 | GET | `/v1/spaces/{spaceId}/guestbooks` | [NotebookApiClient.cs:322](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L322) | `GuestbookController.getGuestbooks()` | ✓ | [GuestbookListResponse](unity-backend-dto-audit.md#guestbooklistresponse). |
| 71 | POST | `/v1/spaces/{spaceId}/guestbooks` | [NotebookApiClient.cs:327](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L327) | `GuestbookController.create()` | ✓ | [GuestbookRequest](unity-backend-dto-audit.md#guestbookrequest). |
| 72 | DELETE | `/v1/guestbooks/{guestbookId}` | [NotebookApiClient.cs:332](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L332) | `GuestbookController.delete()` | ✓ | |
| 73 | POST | `/v1/guestbooks/{guestbookId}/replies` | [NotebookApiClient.cs:337](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L337) | `GuestbookController.reply()` | ✓ | [GuestbookReplyResponse](unity-backend-dto-audit.md#guestbookreplyresponse). |

요약: ✓ 4.

컨트롤러: [GuestbookController.java](../../src/main/java/com/dotshelf/domain/guestbook/controller/GuestbookController.java)

---

## ReadingRecord & Comment (12행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 74 | GET | `/v1/reading-records/{recordId}` | [NotebookApiClient.cs:367](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L367) | `ReadingRecordController.getRecord()` | ✓ | [ReadingRecordResponse](unity-backend-dto-audit.md#readingrecordresponse). |
| 75 | GET | `/v1/users/{userId}/reading-records` | [NotebookApiClient.cs:372](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L372) | `ReadingRecordController.getUserRecords()` | ✓ | |
| 76 | POST | `/v1/reading-records` | [NotebookApiClient.cs:377](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L377) | `ReadingRecordController.create()` | ✓ | |
| 77 | PATCH | `/v1/reading-records/{recordId}` | [NotebookApiClient.cs:382](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L382) | `ReadingRecordController.update()` | ✓ | |
| 78 | DELETE | `/v1/reading-records/{recordId}` | [NotebookApiClient.cs:387](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L387) | `ReadingRecordController.delete()` | ✓ | |
| 79 | POST | `/v1/reading-records/{recordId}/like` | [NotebookApiClient.cs:392](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L392) | `ReadingRecordController.like()` | ✓ | |
| 80 | DELETE | `/v1/reading-records/{recordId}/like` | [NotebookApiClient.cs:397](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L397) | `ReadingRecordController.unlike()` | ✓ | |
| 81 | GET | `/v1/reading-records/{recordId}/comments` | [NotebookApiClient.cs:402](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L402) | `ReadingRecordController.getComments()` | ✓ | [CommentListResponse](unity-backend-dto-audit.md#commentlistresponse). |
| 82 | POST | `/v1/reading-records/{recordId}/comments` | [NotebookApiClient.cs:407](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L407) | `ReadingRecordController.createComment()` | ✓ | |
| 83 | DELETE | `/v1/comments/{commentId}` | [NotebookApiClient.cs:412](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L412) | `ReadingRecordController.deleteComment()` | ✓ | |
| 84 | POST | `/v1/comments/{commentId}/like` | [NotebookApiClient.cs:417](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L417) | `ReadingRecordController.likeComment()` | ✓ | |
| 85 | DELETE | `/v1/comments/{commentId}/like` | [NotebookApiClient.cs:422](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L422) | `ReadingRecordController.unlikeComment()` | ✓ | |

요약: ✓ 12.

컨트롤러: [ReadingRecordController.java](../../src/main/java/com/dotshelf/domain/readingrecord/controller/ReadingRecordController.java)

---

## Report (2행)

| # | Method | Path | Unity 호출 | Backend 핸들러 | 정합성 | 비고 |
| --- | --- | --- | --- | --- | --- | --- |
| 86 | POST | `/v1/reports` | [NotebookApiClient.cs:357](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L357) | `ReportController.report()` | ✓ | [ReportRequest](unity-backend-dto-audit.md#reportrequest)/[ReportResponse](unity-backend-dto-audit.md#reportresponse). |
| 87 | GET | `/v1/reports/me` | [NotebookApiClient.cs:362](../../../frontend/Assets/App/Infrastructure/Api/NotebookApiClient.cs#L362) | `ReportController.getMyReports()` | ✓ | [ReportListResponse](unity-backend-dto-audit.md#reportlistresponse). |

요약: ✓ 2.

컨트롤러: [ReportController.java](../../src/main/java/com/dotshelf/domain/report/controller/ReportController.java)

---

## 전체 통계

| 카테고리 | 행 수 |
| --- | --- |
| ✓ 완전 매칭 | 77 |
| ⚠ 필드 갭 (호환되거나 일부 손실) | 9 |
| ✗ 경로 미스매치 (백엔드 미구현) | 1 |
| ➕ 백엔드 전용 (Unity 미호출) | 9 |
| **합계** | **96** |

> Unity 87 호출 + 백엔드 전용 9 = 96행. 셀프 카운트 확인 ✓.

## 우선순위 액션 (백엔드 변경, Unity = source of truth)

> 모든 액션은 **백엔드를 Unity 호출 형태에 맞추는 방향**. Unity 코드/DTO/경로는 변경하지 않음.
> 상세 변경 가이드는 [unity-backend-dto-audit.md](unity-backend-dto-audit.md#백엔드-변경-액션-unity--source-of-truth) 참고.

| 우선순위 | 항목 | 매핑 행 | 변경 위치 |
| --- | --- | --- | --- |
| **P0** | `GET /v1/books?category=RANKING&limit=N` 라우트 신설 (Unity 카탈로그 로딩이 현재 404 → Mock fallback). **비인증 호출**이라 `permitAll` 등록 필수 | #25 ✗ | [BookController.java](../../src/main/java/com/dotshelf/domain/book/controller/BookController.java) + [SecurityConfig.java](../../src/main/java/com/dotshelf/global/config/SecurityConfig.java) |
| **P0** | `SocialLoginRequest.nonce` 검증 optional/스킵 (Unity 미송신) | #3 ⚠ | `domain/auth/` SocialLoginService |
| **P1** | `AuthTokenResponse`에 `tokenType="Bearer"`, `expiresIn` 필드 추가 | #3, #4 ⚠ | [AuthDtos.java](../../src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java) record 확장 + 매핑 |
| **P1** | `GuestSessionResponse.guestSessionId` + `GuestMigrationRequest.guestSessionId` 추가 | #2, #6 ⚠ | [AuthDtos.java](../../src/main/java/com/dotshelf/domain/auth/dto/AuthDtos.java) record 확장 + 게스트 세션 식별 정책 결정 |
| **P2** | `BookResponse.source / sourceUrl` 추가 (KAKAO/LOCAL 분기에 Unity 사용 중) | #24 ⚠ | [BookDtos.java](../../src/main/java/com/dotshelf/domain/book/dto/BookDtos.java) record 확장 + 매핑 |
| **P3** | `BookResponse`의 ⊖ `purchaseCount, likeCount` 추가 (선택) | #24 ⚠ | 동일 |
| 보류 | `GET /v1/bookshelves/{bookshelfId}/items` Unity 미호출 — Unity 트랙에서 결정 | ➕ | 백엔드 변경 없음 |
| 보류 | `WalletResponse.updatedAt`, `BookSearchListResponse.page/size/isEnd`, `ApiResponse.httpStatus` 타입, `access` 헤더 | 다수 ⚠ | Unity가 무시/Newtonsoft 자동 처리, 변경 불필요 |
