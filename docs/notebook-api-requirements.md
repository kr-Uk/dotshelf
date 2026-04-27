# 노트북 화면 API 요구사항

이 문서는 현재 Unity 노트북 화면 상태에 맞춘 백엔드 API 요구사항입니다.

현재 노트북 헤더 탭은 `책 시장`, `다른 공간`, `꾸미기 시장` 3개입니다. `피드` 탭은 제거된 상태이므로 이 문서에서도 피드 화면용 API는 제외합니다.

## 현재 구현 상태

| 화면/기능 | 현재 Unity 상태 | API 필요도 |
|---|---|---|
| 책 검색 | 검색 버튼 클릭 시 `GET /v1/books/search?keyword=` 호출 | 이미 wrapper 있음, 응답 보강 필요 |
| 추천 검색어 | chip 클릭은 입력값만 변경, 검색 버튼을 눌러야 API 호출 | 검색 API 재사용 |
| 오늘의 추천 책 | 현재 mock 목록 렌더링 | 추천 책 API 필요 |
| 지금 인기 있는 책 | 현재 mock 목록 렌더링 | 랭킹 API 필요 |
| 검색 결과 화면 | 검색 API 응답 책만 그리드로 표시 | 검색 API 필요 |
| 책 상세 모달 | 책 클릭 시 모달 표시, 리뷰 패널/별점/리뷰 입력 있음 | 상세/리뷰 API 필요 |
| 내 책장 추가/구매 | 현재 로컬 상태로 페이지 차감/책장 추가 | 책장 추가/지갑 API 필요 |
| 다른 공간 | 현재 mock 방 카드 표시 | 공간 목록 API 필요 |
| 꾸미기 시장 | 현재 mock 오브젝트 표시 | asset 목록/카테고리/구매 API 필요 |
| 보유 페이지 | 현재 로컬 값 표시 | 지갑 조회 API 필요 |

## 공통 규칙

- Base URL: `https://api.dotshelf.digital`
- 인증: 로그인 이후 `Authorization: Bearer {accessToken}` 사용
- 모든 응답은 기존 envelope을 유지합니다.

```json
{
  "code": "SUCCESS",
  "httpStatus": 200,
  "message": "요청이 성공했습니다.",
  "result": {},
  "success": true
}
```

실패 응답 예시:

```json
{
  "code": "A001",
  "httpStatus": 401,
  "message": "인증이 필요합니다.",
  "success": false
}
```

## 1. 서버 상태 확인

Unity에서 API 연결 여부를 빠르게 확인하기 위한 API입니다.

```http
GET /health
```

### Response

```json
{
  "status": "UP",
  "service": "dotshelf-api",
  "timestamp": "2026-04-27T09:00:00Z"
}
```

## 2. 책 검색

검색창에 입력만 해서는 API를 호출하지 않습니다. 사용자가 `검색` 버튼을 누를 때만 전체 책 검색 API를 호출하고, 응답된 책만 검색 결과 그리드에 표시합니다.

현재 Unity wrapper:

```csharp
NotebookApiClient.SearchBooks(keyword)
```

현재 호출 경로:

```http
GET /v1/books/search?keyword=완벽
Authorization: Bearer {accessToken}
```

### Query

| 필드 | 필수 | 타입 | 설명 |
|---|---:|---|---|
| `keyword` | 예 | string | 제목, 저자, 출판사, 설명 검색어 |

### Response

Unity DTO는 `items` 또는 `books` 둘 다 받을 수 있습니다. 백엔드는 하위 호환을 위해 `books`와 `items`를 함께 내려줍니다.

검색 결과의 `source`가 `LOCAL`이면 DB에 저장된 책이므로 `id/bookId`로 바로 상세 조회와 책장 추가가 가능합니다. `source`가 `KAKAO`이면 아직 DB에 없는 외부 검색 결과일 수 있으므로 `id/bookId`가 `null`일 수 있고, 사용자가 선택해 책장에 추가하는 시점에 `POST /v1/bookshelves/{bookshelfId}/books/kakao`로 저장/연결합니다.

```json
{
  "items": [
    {
      "id": 101,
      "bookId": "101",
      "isbn": "9790000000001",
      "isbn13": "9790000000001",
      "title": "완벽하지 않은 여름",
      "author": "서은재",
      "publisher": "백지출판",
      "publishedDate": "2026-03-12",
      "description": "완벽하지 않은 계절 속에서 관계를 회복하는 이야기입니다.",
      "coverImageUrl": "https://cdn.dotshelf.digital/books/101.png",
      "avgRating": 4.9,
      "placementCount": 4112,
      "purchaseCount": 2180,
      "likeCount": 18420,
      "recordCount": 9231,
      "hasPaidCover": true
    }
  ],
  "nextCursor": null
}
```

### Unity 사용 필드

| 응답 필드 | Unity 사용처 |
|---|---|
| `id`, `bookId` | 상세/책장 추가용 식별자 |
| `title` | 카드/상세 제목 |
| `author` | 카드/상세 저자 |
| `publisher` | 카드/상세 출판사 |
| `description` | 상세 줄거리 |
| `coverImageUrl` | 추후 실제 표지 이미지 |
| `avgRating` | 평점 |
| `likeCount`, `recordCount`, `placementCount` | 리뷰/조회/배치 수 표시 |
| `hasPaidCover` | 유료 표지 여부 |

## 3. 오늘의 추천 책

`책 시장 > 전체 탐색`, `추천 책` 탭에 쓰입니다. 현재는 mock 데이터입니다.

```http
GET /v1/books/recommended?limit=12
Authorization: Bearer {accessToken}
```

### Query

| 필드 | 필수 | 타입 | 설명 |
|---|---:|---|---|
| `limit` | 아니오 | int | 기본 12 |

### Response

```json
{
  "items": [
    {
      "id": 201,
      "bookId": "201",
      "title": "저녁의 문장들",
      "author": "한수정",
      "publisher": "밀림출판",
      "description": "하루의 마지막에 읽기 좋은 짧은 글들",
      "genre": "ESSAY",
      "coverImageUrl": "https://cdn.dotshelf.digital/books/201.png",
      "pricePages": 0,
      "avgRating": 4.8,
      "reviewCount": 612,
      "viewCount": 13980,
      "recordCount": 3410,
      "placementCount": 2201,
      "hasPaidCover": false,
      "owned": false,
      "rank": null
    }
  ]
}
```

## 4. 지금 인기 있는 책

`책 시장 > 전체 탐색`, `책 랭킹` 탭에 쓰입니다.

```http
GET /v1/books/ranking?genre=ALL&priceType=ALL&sort=POPULAR&cursor=&limit=20
Authorization: Bearer {accessToken}
```

### Query

| 필드 | 필수 | 타입 | 예시 | 설명 |
|---|---:|---|---|---|
| `genre` | 아니오 | string | `ALL`, `NOVEL`, `ESSAY`, `HUMANITIES`, `PSYCHOLOGY`, `BUSINESS`, `SF_FANTASY` | 장르 필터 |
| `priceType` | 아니오 | string | `ALL`, `FREE`, `PAID` | 전체/무료/유료 |
| `sort` | 아니오 | string | `POPULAR`, `RATING`, `REVIEW`, `VIEW`, `RECORD`, `RECENT` | 랭킹 정렬 |
| `cursor` | 아니오 | string | `book_101` | 다음 페이지 |
| `limit` | 아니오 | int | `20` | 조회 개수 |

### Response

```json
{
  "items": [
    {
      "rank": 1,
      "id": 101,
      "bookId": "101",
      "title": "완벽하지 않은 여름",
      "author": "서은재",
      "publisher": "백지출판",
      "description": "완벽하지 않은 계절 속에서 관계를 회복하는 이야기입니다.",
      "genre": "NOVEL",
      "coverImageUrl": "https://cdn.dotshelf.digital/books/101.png",
      "pricePages": 1400,
      "avgRating": 4.9,
      "reviewCount": 923,
      "viewCount": 18420,
      "recordCount": 9231,
      "placementCount": 4112,
      "hasPaidCover": true,
      "owned": false,
      "rank": 1
    }
  ],
  "nextCursor": "book_101"
}
```

## 5. 책 상세

책 카드 또는 검색 결과 카드를 클릭하면 페이지 이동이 아니라 모달이 뜹니다. 모달에는 제목, 저자, 출판사, 줄거리, 가격, 평점, 리뷰 일부, 책장 추가 버튼이 필요합니다. `favorite` 기능은 이번 범위에서 제외합니다.

현재 wrapper:

```csharp
NotebookApiClient.GetBook(bookId)
```

```http
GET /v1/books/{bookId}
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "id": 101,
  "bookId": "101",
  "isbn13": "9790000000001",
  "title": "완벽하지 않은 여름",
  "author": "서은재",
  "publisher": "백지출판",
  "publishedDate": "2026-03-12",
  "description": "완벽하지 않은 계절 속에서 관계를 회복하는 이야기입니다.",
  "genre": "NOVEL",
  "coverImageUrl": "https://cdn.dotshelf.digital/books/101.png",
  "pricePages": 1400,
  "avgRating": 4.9,
  "reviewCount": 923,
  "viewCount": 18420,
  "recordCount": 9231,
  "placementCount": 4112,
  "hasPaidCover": true,
  "owned": false,
  "userBookId": null,
  "rank": null
}
```

## 6. 책 리뷰 조회

책 상세 리뷰는 `book_reviews` 테이블에 저장합니다. `reading_records`는 사용자가 보유한 책(`user_books`)에 남기는 독서 기록/독후감이고, 이 API의 별점 리뷰와는 분리합니다.

상세 모달 하단에는 인기 리뷰 1~3개만 먼저 보이고, `리뷰 보기`를 누르면 오른쪽 패널에 전체 리뷰가 표시됩니다.

```http
GET /v1/books/{bookId}/reviews?sort=POPULAR&cursor=&limit=20
Authorization: Bearer {accessToken}
```

### Query

| 필드 | 필수 | 타입 | 설명 |
|---|---:|---|---|
| `sort` | 아니오 | string | `POPULAR`, `RECENT`, `RATING_DESC`, `RATING_ASC` |
| `cursor` | 아니오 | string | 다음 페이지 |
| `limit` | 아니오 | int | 기본 20 |

### Response

```json
{
  "reviews": [
    {
      "reviewId": 7001,
      "bookId": 101,
      "userId": 77,
      "nickname": "한수정",
      "profileImageUrl": "https://cdn.dotshelf.digital/users/77.png",
      "rating": 4.5,
      "content": "짧지만 오래 남는 문장이 많았습니다.",
      "likeCount": 12,
      "liked": false,
      "createdAt": "2026-04-27T09:00:00Z",
      "updatedAt": "2026-04-27T09:00:00Z"
    }
  ],
  "nextCursor": "review_7001"
}
```

## 7. 책 리뷰 작성

현재 UI는 별점 버튼으로 평점을 선택하고, 입력 박스에 리뷰 내용을 작성한 뒤 등록합니다.

```http
POST /v1/books/{bookId}/reviews
Authorization: Bearer {accessToken}
Content-Type: application/json
```

### Request

```json
{
  "rating": 5,
  "content": "계절감과 문장 흐름이 좋아서 다시 읽고 싶습니다."
}
```

### Response

```json
{
  "reviewId": 7002,
  "bookId": 101,
  "userId": 77,
  "nickname": "나",
  "rating": 5,
  "content": "계절감과 문장 흐름이 좋아서 다시 읽고 싶습니다.",
  "likeCount": 0,
  "liked": false,
  "createdAt": "2026-04-27T09:05:00Z",
  "updatedAt": "2026-04-27T09:05:00Z"
}
```

## 8. 책 즐겨찾기

이번 구현 범위에서는 사용하지 않습니다. 상세/추천/랭킹 응답에도 `favorite` 필드는 포함하지 않고, 사용자가 가진 책의 좋아요는 기존 `POST /v1/user-books/{userBookId}/like`와 `DELETE /v1/user-books/{userBookId}/like`를 유지합니다.

## 9. 내 책장 추가

책 상세 모달 또는 추천/랭킹 카드에서 `내 책장에 추가`, `구매하고 추가`를 누를 때 사용합니다.

현재 wrapper:

```csharp
NotebookApiClient.AddBookToShelf(bookshelfId, request)
```

```http
POST /v1/bookshelves/{bookshelfId}/books
Authorization: Bearer {accessToken}
Content-Type: application/json
```

### Request

```json
{
  "bookId": 101,
  "sectionId": 11,
  "slotNo": 2,
  "displayStyle": "FRONT"
}
```

### Response

```json
{
  "id": 9001,
  "userId": 77,
  "bookId": 101,
  "readingStatus": "UNREAD",
  "storageStatus": "ON_SHELF",
  "ownerRating": null,
  "liked": false
}
```

Kakao 검색 결과처럼 아직 DB `bookId`가 없는 책은 아래 API를 사용합니다. 서버는 `isbn13`, `isbn10` 순서로 기존 `books`를 찾고, 없으면 새로 저장한 뒤 현재 사용자의 `user_books`와 책장 아이템을 연결합니다.

```http
POST /v1/bookshelves/{bookshelfId}/books/kakao
Authorization: Bearer {accessToken}
Content-Type: application/json
```

### Kakao Request

```json
{
  "isbn13": "9788983920671",
  "isbn10": "898392067X",
  "title": "해리 포터와 마법사의 돌 1",
  "author": "J.K. 롤링",
  "publisher": "문학수첩",
  "publishedDate": "2014-12-18",
  "description": "카카오 도서 검색에서 받은 설명",
  "coverImageUrl": "https://...",
  "sectionId": 11,
  "slotNo": 2,
  "displayStyle": "FRONT"
}
```

## 10. 내 책 목록 조회

책 카드의 `책장에 있음` 상태, 상세 모달의 `owned`, 책장 배치 상태를 맞추기 위해 필요합니다.

현재 `GET /v1/user-books/archived`는 있으나 일반 목록 조회 API가 필요합니다.

```http
GET /v1/user-books?storageStatus=ON_SHELF&readingStatus=&bookshelfId=&sectionId=&cursor=&limit=50
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "books": [
    {
      "id": 9001,
      "userId": 77,
      "bookId": 101,
      "readingStatus": "UNREAD",
      "storageStatus": "ON_SHELF",
      "ownerRating": null,
      "liked": false,
      "bookshelfId": 1,
      "sectionId": 11,
      "slotNo": 2,
      "displayStyle": "FRONT"
    }
  ],
  "nextCursor": null
}
```

## 11. 지갑 잔액 조회

상단 `보유 페이지`와 구매 전 잔액 검증에 필요합니다.

현재 충전 API는 있으나 read-only 지갑 조회 API가 필요합니다.

```http
GET /v1/payments/wallet
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "userId": 77,
  "balancePages": 4820,
  "updatedAt": "2026-04-27T09:00:00Z"
}
```

## 12. 페이지 충전

현재 wrapper:

```csharp
NotebookApiClient.ChargePages(request)
```

```http
POST /v1/payments/page-charge
Authorization: Bearer {accessToken}
Content-Type: application/json
```

### Request

```json
{
  "amountPages": 1000,
  "paymentMethodId": 301
}
```

### Response

```json
{
  "walletId": 1,
  "userId": 77,
  "balancePages": 5820,
  "chargedPages": 1000,
  "transactionId": 50001
}
```

## 13. 다른 공간 목록

`다른 공간` 탭에서 다른 사람의 방을 사진형 카드로 보여주기 위한 API입니다.

현재 단건 `GET /v1/spaces/{spaceId}`는 있으나 목록 API가 필요합니다.

```http
GET /v1/spaces?keyword=&sort=POPULAR&cursor=&limit=20
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "spaces": [
    {
      "spaceId": 10,
      "ownerUserId": 77,
      "ownerNickname": "한수정",
      "ownerProfileImageUrl": "https://cdn.dotshelf.digital/users/77.png",
      "spaceName": "밤의 문장 작업실",
      "mood": "에세이 · 차분한 서재",
      "description": "책상 위 노트와 작은 조명으로 만든 조용한 기록 공간입니다.",
      "thumbnailUrl": "https://cdn.dotshelf.digital/spaces/10-thumb.png",
      "visibilityScope": "PUBLIC",
      "visitCount": 9230,
      "likeCount": 734,
      "followerCount": 1842,
      "liked": false,
      "following": false
    }
  ],
  "nextCursor": "space_10"
}
```

### 관련 액션 API

이미 wrapper가 있는 API입니다.

```http
POST   /v1/spaces/{spaceId}/visits
POST   /v1/spaces/{spaceId}/like
DELETE /v1/spaces/{spaceId}/like
POST   /v1/users/{targetUserId}/follow
DELETE /v1/users/{targetUserId}/follow
```

## 14. 꾸미기 오브젝트 목록

`꾸미기 시장` 탭에서 오브젝트 카드 목록을 보여줍니다.

현재 wrapper:

```csharp
NotebookApiClient.GetAssets(category, sort, limit)
```

```http
GET /v1/assets?category=DECOR_OBJECT&sort=popular&limit=30
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "assets": [
    {
      "id": 201,
      "categoryId": 2,
      "code": "DESK_LAMP_BLUE",
      "name": "푸른 조명",
      "tier": "COMMON",
      "assetType": "INTERIOR_OBJECT",
      "previewImageUrl": "https://cdn.dotshelf.digital/assets/201.png",
      "pricePages": 700,
      "owned": false,
      "wished": false
    }
  ]
}
```

## 15. 꾸미기 오브젝트 카테고리

현재 UI 카테고리는 `전체`, `필수 배치 오브젝트`, `기타 배치 오브젝트`, `전체 테마`입니다. 서버 데이터와 동기화하려면 카테고리 API가 필요합니다.

```http
GET /v1/assets/categories
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "categories": [
    {
      "categoryId": 1,
      "code": "REQUIRED_OBJECT",
      "name": "필수 배치 오브젝트",
      "displayOrder": 1
    },
    {
      "categoryId": 2,
      "code": "DECOR_OBJECT",
      "name": "기타 배치 오브젝트",
      "displayOrder": 2
    },
    {
      "categoryId": 3,
      "code": "ROOM_THEME",
      "name": "전체 테마",
      "displayOrder": 3
    }
  ]
}
```

## 16. 오브젝트 위시/구매/인벤토리

이미 wrapper가 있는 API입니다.

```http
GET    /v1/assets/me/inventory
POST   /v1/assets/{assetId}/wishlist
DELETE /v1/assets/{assetId}/wishlist
POST   /v1/assets/{assetId}/purchase
```

### Purchase Response

```json
{
  "purchaseId": 9101,
  "assetId": 201,
  "inventoryId": 7001,
  "pricePages": 700
}
```

## 17. 책장 렌더링용 아이템 조회

3D 책장 또는 노트북의 책장 상태를 실제 slot 기준으로 맞추려면 특정 책장 내부 아이템 목록이 필요합니다.

현재 `GET /v1/spaces/{spaceId}/bookshelves`는 책장 목록용이고, 섹션/아이템 목록까지 포함하는 API가 필요합니다.

```http
GET /v1/bookshelves/{bookshelfId}/items
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "bookshelf": {
    "bookshelfId": 1,
    "spaceId": 10,
    "name": "책장 1",
    "description": "메인 책장",
    "visibilityScope": "PUBLIC",
    "displayOrder": 1,
    "bodyInventoryId": 501,
    "skinInventoryId": 502
  },
  "sections": [
    {
      "sectionId": 11,
      "rowNo": 0,
      "columnNo": 0,
      "maxItemCount": 6,
      "alignment": "LEFT"
    }
  ],
  "items": [
    {
      "bookshelfItemId": 30001,
      "itemType": "BOOK",
      "sectionId": 11,
      "slotNo": 1,
      "displayStyle": "FRONT",
      "userBookId": 9001,
      "book": {
        "bookId": 101,
        "title": "완벽하지 않은 여름",
        "author": "서은재",
        "coverImageUrl": "https://cdn.dotshelf.digital/books/101.png"
      }
    },
    {
      "bookshelfItemId": 30002,
      "itemType": "INTERIOR_OBJECT",
      "sectionId": 11,
      "slotNo": 2,
      "displayStyle": "DEFAULT",
      "assetInventoryId": 7001,
      "asset": {
        "assetId": 201,
        "code": "DESK_LAMP_BLUE",
        "name": "푸른 조명",
        "previewImageUrl": "https://cdn.dotshelf.digital/assets/201.png"
      }
    }
  ]
}
```

## 우선순위

1. `GET /v1/books/search` 응답 안정화: 현재 검색 버튼에서 실제 호출 중입니다.
2. 책 상세/리뷰: 모달 기능 완성을 위해 필요합니다.
3. 추천/랭킹 책 API: 책 시장 mock 제거에 필요합니다.
4. 지갑 조회/내 책 목록: 구매, 보유 페이지, 책장에 있음 상태에 필요합니다.
5. 다른 공간 목록/오브젝트 카테고리: 각 탭 mock 제거에 필요합니다.
6. 책장 아이템 조회: 3D 책장 slot 렌더링 정확도에 필요합니다.

## 현재 Unity wrapper에 이미 있는 주요 API

```http
GET    /health

POST   /v1/auth/guest-session
POST   /v1/auth/social-login
POST   /v1/auth/token/refresh
POST   /v1/auth/logout
POST   /v1/auth/guest-migration

GET    /v1/books/search
GET    /v1/books/{bookId}
POST   /v1/bookshelves/{bookshelfId}/books
POST   /v1/bookshelves/{bookshelfId}/books/kakao
PATCH  /v1/user-books/{userBookId}
POST   /v1/user-books/{userBookId}/move
POST   /v1/user-books/{userBookId}/archive
DELETE /v1/user-books/{userBookId}
GET    /v1/user-books/archived
POST   /v1/user-books/{userBookId}/like
DELETE /v1/user-books/{userBookId}/like

GET    /v1/assets
GET    /v1/assets/{assetId}
GET    /v1/assets/me/inventory
POST   /v1/assets/{assetId}/wishlist
DELETE /v1/assets/{assetId}/wishlist
POST   /v1/assets/{assetId}/purchase

GET    /v1/spaces/me
GET    /v1/spaces/{spaceId}
GET    /v1/spaces/{spaceId}/dashboard
POST   /v1/spaces/{spaceId}/visits
POST   /v1/spaces/{spaceId}/like
DELETE /v1/spaces/{spaceId}/like
PUT    /v1/spaces/me/layout
PATCH  /v1/spaces/me/theme
PATCH  /v1/spaces/me/visibility
PATCH  /v1/spaces/me/guestbook-visibility
GET    /v1/spaces/templates

GET    /v1/spaces/{spaceId}/bookshelves
POST   /v1/bookshelves
PATCH  /v1/bookshelves/{bookshelfId}
DELETE /v1/bookshelves/{bookshelfId}
PUT    /v1/bookshelves/order
PATCH  /v1/bookshelves/{bookshelfId}/sections/{sectionId}/alignment
POST   /v1/bookshelves/{bookshelfId}/interior-objects
PATCH  /v1/bookshelf-items/{itemId}

POST   /v1/payments/page-charge
GET    /v1/payments/purchases
GET    /v1/payments/methods

GET    /v1/users/me
PATCH  /v1/users/me
DELETE /v1/users/me
GET    /v1/users/{userId}
POST   /v1/users/{targetUserId}/follow
DELETE /v1/users/{targetUserId}/follow
```

## 현재 문서 기준으로 추가가 필요한 API

```http
GET    /v1/books/recommended
GET    /v1/books/ranking
GET    /v1/books/{bookId}/reviews
POST   /v1/books/{bookId}/reviews
GET    /v1/user-books
GET    /v1/payments/wallet
GET    /v1/spaces
GET    /v1/assets/categories
GET    /v1/bookshelves/{bookshelfId}/items
```
