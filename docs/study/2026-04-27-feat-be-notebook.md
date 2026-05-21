# 노트북 화면 API 요구사항 반영

- **브랜치**: `feat/be/notebook`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-04-27
- **관련 문서**: `docs/notebook-api-requirements.md`

## TL;DR

Unity 노트북 화면에서 mock으로 쓰던 책 시장, 다른 공간, 꾸미기 시장 데이터를 실제 API로 받을 수 있게 백엔드 엔드포인트를 추가했다.
책 상세 리뷰는 `reading_records`와 분리해 신규 `book_reviews` 테이블로 관리하고, `favorite` 기능은 이번 범위에서 제외했다.
Kakao 검색 결과처럼 아직 DB에 없는 책은 선택 시점에 저장 후 `user_books`와 책장 아이템을 연결하는 흐름으로 문서화했다.

## 무엇을 했나

### 1. 책 시장 API 확장

- 사용자 관점 효과: 추천 책, 랭킹 책, 책 상세, 리뷰 목록/작성, 내 책 보유 상태를 실제 API로 조회할 수 있다.
- 시스템 관점 효과: `BookController`에 추천/랭킹/리뷰/내 책 목록 API를 추가하고 `BookService`, `BookReviewService`로 역할을 나눴다.
- 주요 파일: `BookController.java`, `BookService.java`, `BookReviewService.java`, `BookDtos.java`

### 2. 책 리뷰 저장 구조 추가

- 사용자 관점 효과: 책 상세 모달에서 별점과 짧은 리뷰를 남길 수 있다.
- 시스템 관점 효과: 독후감 성격의 `reading_records`와 별점 리뷰 성격의 `book_reviews`를 분리했다.
- 주요 파일: `BookReview.java`, `BookReviewRepository.java`, `V3__expand_create_book_reviews.sql`

### 3. 노트북 부가 API 추가

- 사용자 관점 효과: 보유 페이지, 다른 공간, 꾸미기 카테고리, 책장 slot 렌더링 데이터를 API로 받을 수 있다.
- 시스템 관점 효과: `GET /v1/payments/wallet`, `GET /v1/spaces`, `GET /v1/assets/categories`, `GET /v1/bookshelves/{bookshelfId}/items`를 추가했다.
- 주요 파일: `PaymentController.java`, `SpaceController.java`, `AssetController.java`, `BookshelfController.java`

## 왜 했나

프론트 요구사항 문서에는 노트북 화면의 mock 제거를 위해 책 추천/랭킹, 리뷰, 내 책 목록, 지갑, 공간 목록, 에셋 카테고리, 책장 아이템 조회 API가 필요하다고 정리되어 있었다. 기존 백엔드는 검색과 책장 추가 중심 API는 있었지만, 노트북 화면이 실제 데이터를 렌더링하기 위한 목록성 API와 책 상세 리뷰 API가 부족했다.

또한 `favorite`는 사용하지 않기로 결정했기 때문에, 새 테이블이나 엔드포인트를 만들지 않고 기존 `user_books` 좋아요만 유지하도록 문서와 응답 계약을 정리했다.

## 어떻게 구현했나

### 책 리뷰와 평점 집계

`book_reviews`는 `book_id + user_id` unique 제약을 둬 사용자가 같은 책에 활성 리뷰를 중복 작성하지 못하게 했다. 리뷰 작성 시 `Book.addReviewRating()`으로 `books.rating_sum`, `books.rating_count`를 함께 갱신해 추천/랭킹 응답에서 평균 평점을 계산할 수 있게 했다.

```java
BookReview review = bookReviewRepository.save(BookReview.builder()
        .bookId(bookId)
        .userId(currentUserId)
        .rating(request.rating())
        .content(request.content().trim())
        .build());
book.addReviewRating(request.rating());
```

핵심 포인트:
- `reading_records`는 독후감/독서 기록, `book_reviews`는 책 상세의 별점 리뷰로 분리한다.
- 리뷰 작성은 인증 사용자 기준으로만 허용한다.
- 현재는 작성/조회만 구현했고, 수정/삭제/좋아요는 별도 범위로 남겼다.

### 노트북 책 응답 보강

기존 `BookResponse`, `BookSearchResponse`에 Unity가 쓰는 `bookId`, `isbn`, `avgRating`, `reviewCount`, `viewCount`, `placementCount`, `owned`, `userBookId`, `rank` 같은 필드를 추가했다. 검색 응답은 기존 클라이언트를 깨지 않도록 `books`와 `items`를 함께 내려주도록 확장했다.

핵심 포인트:
- `source=LOCAL`은 DB 책이므로 `id/bookId`가 존재한다.
- `source=KAKAO`는 아직 저장 전일 수 있어 `id/bookId`가 없을 수 있다.
- Kakao 결과는 `POST /v1/bookshelves/{bookshelfId}/books/kakao`에서 저장/연결한다.

### 책장 렌더링 조회

`GET /v1/bookshelves/{bookshelfId}/items`는 책장, 섹션, 아이템을 한 번에 내려준다. 아이템이 책이면 `user_books -> books`를 따라 책 요약을 붙이고, 꾸미기 오브젝트면 `user_asset_inventories -> assets`를 따라 에셋 요약을 붙인다.

핵심 포인트:
- 3D 렌더링에 필요한 `sectionId`, `slotNo`, `displayStyle`을 유지한다.
- 책과 에셋은 같은 `bookshelf_items`에 들어가므로 응답에서 `book`, `asset`을 선택적으로 제공한다.

## 배운 것

- **도메인 의미 분리**: 이름이 비슷해도 `reading_records`와 `book_reviews`는 사용자 경험이 다르다. 독후감은 보유 책 중심 기록이고, 리뷰는 책 상세에 붙는 공개 별점 데이터다.
- **하위 호환 응답 확장**: 기존 `books` 필드를 쓰는 클라이언트가 있을 때 새 `items` 필드를 추가하면 프론트 전환 중에도 API 변경 리스크를 낮출 수 있다.
- **외부 검색 결과 저장 시점**: Kakao 검색 결과를 모두 즉시 저장하지 않고, 사용자가 내 책장에 추가하는 순간에만 `books`에 저장하면 DB 오염과 불필요한 저장을 줄일 수 있다.
- **MVP 랭킹 구현**: 별도 통계 테이블이 없을 때는 `viewCount`, `bookshelfAddCount`, `ratingCount` 같은 현재 보유 컬럼으로 우선 정렬을 만들고, 이후 트래픽이 늘면 QueryDSL/집계 테이블로 옮기는 전략이 가능하다.

## 더 공부할 것

- [ ] 리뷰 수정/삭제가 추가될 때 `rating_sum`, `rating_count`를 정확히 보정하는 방법
- [ ] 추천/랭킹을 DB 정렬 또는 QueryDSL projection으로 옮기는 기준
- [ ] 커서 페이지네이션을 실제로 적용할 때 `nextCursor`를 안정적으로 설계하는 방법
- [ ] 책장 아이템 조회에서 공개 범위와 소유권 검증을 어느 레이어에서 강제할지
- [ ] `book_reviews`에 신고/숨김/소프트 삭제 정책을 붙일 때 필요한 상태 모델

## 검증

- `./gradlew.bat build -x test --console=plain` 통과
- `./gradlew.bat test --console=plain` 통과
- Docker Desktop이 실행 중이 아니어서 로컬 부팅 smoke는 수행하지 못했다.

## 참고

- 관련 커밋: `5534333`
- 관련 문서: `docs/notebook-api-requirements.md`
