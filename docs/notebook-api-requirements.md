# 노트북 화면 API 요구사항

이 문서는 Unity 노트북 화면에서 사용하는 API와 요청/응답 데이터를 백엔드와 맞추기 위한 기준 문서입니다.

현재 노트북 화면은 `책 시장`, `다른 공간`, `꾸미기 시장` 3개 탭으로 구성되어 있습니다. 헤더의 `피드` 탭과 노트북 내부의 별도 피드 화면은 제거된 상태입니다.

## 공통 규칙

- Base URL: `https://api.dotshelf.digital`
- 인증: 로그인 또는 게스트 세션 이후 `Authorization: Bearer {accessToken}` 사용
- Unity API 모드:
  - `RealApi`: 실제 서버 API 호출
  - `MockApi`: 별도 HTTP mock 서버가 있을 때만 사용
  - `LocalMock`: HTTP 호출 없이 로컬 no-op/mock repository 사용
- 노트북의 서버 데이터는 `Application/UseCase -> Infrastructure Repository -> API Client` 경계를 통해 호출합니다. `Features` 레이어가 `Infrastructure/API`를 직접 참조하지 않아야 합니다.

공통 성공 응답:

```json
{
  "code": "SUCCESS",
  "httpStatus": 200,
  "message": "요청이 성공했습니다.",
  "result": {},
  "success": true
}
```

공통 실패 응답:

```json
{
  "code": "A001",
  "httpStatus": 401,
  "message": "인증이 필요합니다.",
  "success": false
}
```

## 1. 서버 상태 확인

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

검색창 입력만으로 API를 호출하지 않습니다. 사용자가 `검색` 버튼을 눌렀을 때 전체 책 검색 API를 호출하고, 추천/인기 섹션 대신 검색 결과 그리드만 보여줍니다.

```http
GET /v1/books/search?keyword={keyword}&page=1&size=24
Authorization: Bearer {accessToken}
```

### Query

| 필드 | 필수 | 타입 | 설명 |
|---|---:|---|---|
| `keyword` | 예 | string | 제목, 저자, 출판사 검색어 |
| `page` | 아니오 | int | 기본 1 |
| `size` | 아니오 | int | 기본 24 |

### Response

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
      "description": "완벽하지 않은 계절 속에서 관계가 회복되는 이야기입니다.",
      "genre": "소설",
      "coverImageUrl": "https://cdn.dotshelf.digital/books/101.png",
      "pricePages": 1400,
      "avgRating": 4.9,
      "reviewCount": 923,
      "viewCount": 18420,
      "recordCount": 9231,
      "placementCount": 4112,
      "hasPaidCover": true,
      "owned": false,
      "source": "LOCAL"
    }
  ],
  "nextCursor": null
}
```

`source`가 `LOCAL`이면 서버에 저장된 책이므로 `bookId`로 상세/책장 추가를 진행합니다. `source`가 `KAKAO`이면 외부 검색 결과이므로 책장 추가 시 `POST /v1/bookshelves/{bookshelfId}/books/kakao`로 저장과 추가를 함께 요청합니다.

## 3. 오늘의 추천 책

```http
GET /v1/books/recommended?limit=12
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "items": [
    {
      "id": 201,
      "bookId": "201",
      "title": "조용한 심장",
      "author": "Eliot Mara",
      "publisher": "Oakrow Press",
      "description": "내면의 리듬을 되찾는 심리 에세이입니다.",
      "genre": "심리",
      "coverImageUrl": "https://cdn.dotshelf.digital/books/201.png",
      "pricePages": 1200,
      "avgRating": 4.7,
      "reviewCount": 612,
      "viewCount": 13980,
      "recordCount": 3410,
      "placementCount": 2201,
      "hasPaidCover": true,
      "owned": false
    }
  ]
}
```

## 4. 지금 인기 있는 책

```http
GET /v1/books/ranking?genre=ALL&priceType=ALL&sort=POPULAR&cursor=&limit=20
Authorization: Bearer {accessToken}
```

### Query

| 필드 | 필수 | 타입 | 예시 | 설명 |
|---|---:|---|---|---|
| `genre` | 아니오 | string | `ALL` | 장르 필터 |
| `priceType` | 아니오 | string | `ALL`, `FREE`, `PAID` | 무료/유료 필터 |
| `sort` | 아니오 | string | `POPULAR`, `RATING`, `REVIEW`, `RECORD`, `RECENT` | 랭킹 정렬. 현재 UI에서는 조회순을 노출하지 않습니다. |
| `cursor` | 아니오 | string | `book_101` | 다음 페이지 커서 |
| `limit` | 아니오 | int | `20` | 조회 개수 |

### Response

추천 책 응답과 동일한 책 필드를 사용하고, 필요하면 `rank`를 추가합니다.

## 5. 책 상세

책 카드를 클릭하면 페이지 이동이 아니라 모달로 상세를 엽니다. 상세 모달에는 표지, 제목, 저자, 출판사, 줄거리, 가격, 책장 추가 버튼, 즐겨찾기, 리뷰 버튼을 표시합니다.

```http
GET /v1/books/{bookId}
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "id": 101,
  "bookId": "101",
  "title": "완벽하지 않은 여름",
  "author": "서은재",
  "publisher": "백지출판",
  "description": "완벽하지 않은 계절 속에서 관계가 회복되는 이야기입니다.",
  "genre": "소설",
  "coverImageUrl": "https://cdn.dotshelf.digital/books/101.png",
  "pricePages": 1400,
  "avgRating": 4.9,
  "reviewCount": 923,
  "viewCount": 18420,
  "recordCount": 9231,
  "placementCount": 4112,
  "hasPaidCover": true,
  "owned": false,
  "userBookId": 991
}
```

## 6. 책장 추가

서버에 저장된 책:

```http
POST /v1/bookshelves/{bookshelfId}/books
Authorization: Bearer {accessToken}
Content-Type: application/json
```

```json
{
  "bookId": 101,
  "sectionId": 10,
  "slotNo": 1,
  "displayStyle": "COVER"
}
```

외부 검색 결과 책:

```http
POST /v1/bookshelves/{bookshelfId}/books/kakao
Authorization: Bearer {accessToken}
Content-Type: application/json
```

```json
{
  "title": "외부 검색 책",
  "author": "저자",
  "publisher": "출판사",
  "isbn": "9790000000001",
  "isbn13": "9790000000001",
  "description": "외부 검색 결과 설명",
  "coverImageUrl": "https://example.com/cover.png",
  "sectionId": 10,
  "slotNo": 1,
  "displayStyle": "COVER"
}
```

## 7. 책 리뷰

리뷰 버튼을 누르면 책 상세 모달 오른쪽에 리뷰 패널을 엽니다. 기본은 인기 리뷰 1~3개만 보여주고, `전체보기`를 누르면 전체 리뷰 목록으로 전환합니다.

```http
GET /v1/books/{bookId}/reviews?sort=POPULAR&limit=20
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "reviews": [
    {
      "reviewId": 1,
      "userId": 33,
      "nickname": "한줄독자",
      "rating": 4.5,
      "content": "문장 흐름이 좋아서 끝까지 읽기 편했습니다."
    }
  ],
  "nextCursor": null
}
```

리뷰 작성:

```http
POST /v1/books/{bookId}/reviews
Authorization: Bearer {accessToken}
Content-Type: application/json
```

```json
{
  "rating": 4.5,
  "content": "이 책을 읽고 느낀 점입니다."
}
```

## 8. 즐겨찾기

현재 Unity에서는 책장에 추가되어 `userBookId`가 있는 책만 즐겨찾기 토글을 허용합니다.

```http
POST /v1/user-books/{userBookId}/like
Authorization: Bearer {accessToken}
```

```http
DELETE /v1/user-books/{userBookId}/like
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "active": true,
  "count": 12
}
```

## 9. 페이지 지갑과 충전

헤더의 페이지 지갑은 서버 지갑을 기준으로 표시합니다. 지갑 영역을 클릭하면 잔액, 결제수단, 오브젝트 구매내역 패널을 엽니다.

```http
GET /v1/payments/wallet
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "userId": 1,
  "balancePages": 4820,
  "updatedAt": "2026-04-30T10:00:00Z"
}
```

충전:

```http
POST /v1/payments/page-charge
Authorization: Bearer {accessToken}
Content-Type: application/json
```

```json
{
  "pagesAmount": 1000,
  "amountKrw": 1000,
  "platform": "UNITY"
}
```

### Response

```json
{
  "userId": 1,
  "balancePages": 5820,
  "updatedAt": "2026-04-30T10:01:00Z"
}
```

구매내역:

```http
GET /v1/payments/purchases
Authorization: Bearer {accessToken}
```

```json
{
  "purchases": [
    {
      "id": 71,
      "assetId": 1003,
      "pricePages": 1200,
      "status": "COMPLETED"
    }
  ]
}
```

결제수단:

```http
GET /v1/payments/methods
Authorization: Bearer {accessToken}
```

```json
{
  "methods": ["KAKAO_PAY", "CARD"]
}
```

## 10. 다른 공간

```http
GET /v1/spaces?sort=POPULAR&limit=12
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "spaces": [
    {
      "spaceId": 21,
      "ownerUserId": 7,
      "ownerNickname": "문장수집가",
      "spaceName": "밤의 서재",
      "description": "조용한 독서 방",
      "followerCount": 120,
      "likeCount": 88,
      "visitCount": 310,
      "following": false,
      "liked": false
    }
  ],
  "nextCursor": null
}
```

좋아요/팔로우:

```http
POST /v1/spaces/{spaceId}/like
DELETE /v1/spaces/{spaceId}/like
POST /v1/users/{targetUserId}/follow
DELETE /v1/users/{targetUserId}/follow
Authorization: Bearer {accessToken}
```

## 11. 꾸미기 시장

```http
GET /v1/assets?category={category}&sort=popular&limit=24
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "assets": [
    {
      "id": 1003,
      "code": "desk_lamp_blue",
      "name": "블루 데스크 램프",
      "categoryCode": "INTERIOR",
      "assetType": "PROP",
      "tier": "RARE",
      "previewImageUrl": "https://cdn.dotshelf.digital/assets/1003.png",
      "pricePages": 1200,
      "owned": false,
      "wished": false
    }
  ]
}
```

찜:

```http
POST /v1/assets/{assetId}/wishlist
DELETE /v1/assets/{assetId}/wishlist
Authorization: Bearer {accessToken}
```

구매:

```http
POST /v1/assets/{assetId}/purchase
Authorization: Bearer {accessToken}
```

### Response

```json
{
  "assetId": 1003,
  "inventoryId": 501,
  "pricePages": 1200
}
```

## Unity에서 API 연동 확인 방법

1. `AppApiConfig.apiMode`가 `RealApi`인지 확인합니다.
2. 콘솔에서 `Dotshelf API mode: RealApi, baseUrl=https://api.dotshelf.digital` 로그를 확인합니다.
3. 노트북 진입 시 다음 로그가 순서대로 나오는지 확인합니다.
   - `Dotshelf API health OK`
   - `Book catalog API loaded`
   - `Bookshelf catalog loaded`
4. 책 시장에서 검색 버튼을 누르고 `GET /v1/books/search` 응답 데이터만 검색 결과 그리드에 보이는지 확인합니다.
5. 오브젝트 구매 후 지갑 잔액이 다시 동기화되고 `GET /v1/payments/purchases` 패널에 구매내역이 표시되는지 확인합니다.
