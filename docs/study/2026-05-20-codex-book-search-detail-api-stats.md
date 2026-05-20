# 책 검색 상세 통계 API 연동

- **브랜치**: `codex/book-search-detail-api-stats`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-05-20 ~ 2026-05-20
- **관련 커밋**: `677da6fe`

## TL;DR

책 검색 결과와 상세 모달에서 별점, 리뷰 수, 조회 수가 임의 fallback 값으로 보이던 흐름을 API 기반으로 바꿨다.
백엔드 검색 응답에 `reviewCount`, `viewCount`를 추가하고, Unity는 검색 응답과 `GET /v1/books/{bookId}` 상세 응답을 사용해 UI 통계를 갱신한다.
서버에 아직 저장되지 않은 Kakao 원본 책은 잘못된 `/v1/books/{id}` 호출을 하지 않도록 서버 책 ID가 확인되는 경우에만 상세 API를 호출한다.

## 무엇을 했나

### 1. 책 검색 API 응답 통계 확장

- 사용자 관점 효과: 검색 결과에서 리뷰 수와 조회 수가 실제 서버 데이터로 표시된다.
- 시스템 관점 효과: `BookSearchResponse`가 상세 응답과 같은 통계 필드를 제공하므로 프론트가 임의 계산을 하지 않아도 된다.
- 주요 파일: `backend/src/main/java/com/dotshelf/domain/book/dto/BookDtos.java`, `backend/src/main/java/com/dotshelf/domain/book/service/BookService.java`

### 2. Unity 상세 모달 API 갱신

- 사용자 관점 효과: 검색 결과에서 책을 클릭하면 상세 모달의 별점, 리뷰 수, 조회 수가 서버 상세 응답으로 갱신된다.
- 시스템 관점 효과: `NotebookRuntimeUseCase`와 `ApiNotebookRuntimeRepository`에 책 상세 조회 경로를 추가해 UI가 기존 API client를 통해 상세 데이터를 가져온다.
- 주요 파일: `frontend/Assets/App/Features/Notebook/Presentation/NotebookToolkitController.cs`, `frontend/Assets/App/Infrastructure/Api/ApiNotebookRuntimeRepository.cs`

### 3. 하드코딩 fallback 제거

- 사용자 관점 효과: 리뷰가 없는 책이 `4.2점`, `리뷰 1개`, `조회수 N회`처럼 실제와 다르게 보이지 않는다.
- 시스템 관점 효과: 검색 응답의 `avgRating`, `reviewCount`, `viewCount`가 없으면 0으로 표시하고, 서버 값이 들어오면 그대로 반영한다.

### 4. 회귀 테스트 보강

- 사용자 관점 효과: 검색 API와 상세 통계 연동이 다시 fallback으로 돌아가는 위험을 줄인다.
- 시스템 관점 효과: 백엔드 controller/service 테스트와 Unity use case delegation 테스트를 추가했다.
- 주요 파일: `BookControllerTest.java`, `BookServiceTest.java`, `NotebookRuntimeUseCaseTests.cs`

## 왜 했나

책 검색 후 선택한 상세 영역에는 즐겨찾기, 별점, 리뷰 조회, 리뷰 보기 같은 기능이 있는데, 통계 값 일부가 실제 API 응답이 아니라 UI fallback으로 만들어지고 있었다.
이 상태에서는 리뷰가 하나도 없는 책도 임의 평점과 리뷰 수를 가진 것처럼 보이기 때문에 사용자 신뢰가 떨어진다.

또한 `GET /v1/books/{bookId}`는 이미 조회 수 증가와 상세 통계 응답 역할을 갖고 있었으므로, 프론트가 책 클릭 시 이 API를 호출하면 별도 API를 만들지 않고 기존 백엔드 계약을 활용할 수 있었다.

## 어떻게 구현했나

### 검색 응답에 통계 필드 추가

`BookSearchResponse`에 리뷰 수와 조회 수를 추가하고, 로컬 검색 결과를 만들 때 활성 리뷰 수를 조회해서 함께 매핑했다.

```java
List<Book> books = bookRepository.findTop20ByTitleSearchContainingOrAuthorContainingIgnoreCase(titleSearch, query);
Map<Long, Long> reviewCounts = reviewCounts(books);
return books.stream()
        .map(book -> BookSearchResponse.from(book, reviewCounts.getOrDefault(book.getId(), 0L)))
        .toList();
```

핵심 포인트:
- 검색 결과의 로컬 책은 DB에 존재하므로 `book.getViewCount()`와 리뷰 집계가 가능하다.
- Kakao 원본 검색 결과는 아직 서버 책 ID가 없을 수 있으므로 기본 통계는 0으로 유지한다.
- 상세 API와 검색 API가 같은 DTO 계열을 공유하므로 Unity DTO는 기존 `BookListItemDto`의 `reviewCount`, `viewCount` 필드를 그대로 활용한다.

### 상세 모달에서 서버 상세 조회

책 상세 모달을 열 때 서버 책 ID가 있으면 `LoadBookDetail`을 호출하고, 성공 응답으로 `BookViewModel`의 통계 값을 교체한다.

```csharp
private void RequestBookDetail(BookViewModel book)
{
    if (book == null || requestedBookDetailIds.Contains(book.Id))
    {
        return;
    }

    if (notebookRuntimeUseCase == null || !TryResolveBookApiId(book, out var bookId))
    {
        return;
    }

    requestedBookDetailIds.Add(book.Id);
    notebookRuntimeUseCase.LoadBookDetail(bookId, ...);
}
```

핵심 포인트:
- 요청 중복은 막지만 성공/실패 후에는 다시 요청할 수 있게 풀어둔다. 상세 조회가 조회 수 증가와 연결되어 있기 때문이다.
- `ServerBookId`가 있거나 `LOCAL` 소스인 경우에만 `/v1/books/{bookId}`를 호출한다.
- Kakao ISBN 같은 문자열을 서버 책 ID로 오인해 잘못된 상세 API를 호출하지 않도록 `TryResolveBookApiId`를 보수적으로 바꿨다.

### UI fallback 제거

기존 검색 결과 매핑은 API 값이 없으면 평점 `4.2`, 리뷰 수 `1`, 조회 수 `reviews * 12` 같은 값을 만들어냈다.
이번 변경에서는 API 값이 없으면 0으로 두고, 상세 응답이 도착하면 해당 값으로 갱신한다.

## 배운 것

- **검색 응답과 상세 응답의 계약 정렬**: 같은 화면에서 쓰이는 데이터는 API별로 필드 의미가 달라지면 프론트 fallback이 늘어난다. 검색 응답에도 통계 필드를 명시하면 UI가 단순해진다.
- **외부 검색 결과와 내부 책 ID 구분**: Kakao 검색 결과의 ISBN은 숫자처럼 보여도 서버의 `bookId`가 아니다. API 호출 조건은 문자열 파싱보다 소스와 서버 ID 존재 여부를 기준으로 판단해야 한다.
- **상세 조회의 부수 효과**: `GET /v1/books/{bookId}`가 조회 수 증가를 포함하므로, 캐싱이나 중복 방지는 사용자 행동과 통계 정책을 같이 고려해야 한다.
- **테스트 위치 선택**: API 응답 필드 추가는 controller JSON 검증과 service 집계 검증을 같이 두는 편이 안전하다.

## 더 공부할 것

- [ ] 검색 API에서 로컬 책과 Kakao 책을 섞을 때 통계 필드를 어디까지 동일하게 맞춰야 하는가
- [ ] 조회 수 증가 API를 순수 상세 조회와 분리해야 하는 기준은 무엇인가
- [ ] Unity UI에서 API 응답 후 기존 `BookViewModel`을 교체하는 패턴과 mutable update 패턴의 장단점
- [ ] 리뷰 생성 직후 검색/상세 통계 재조회 전략과 optimistic update 전략 비교

## 참고

- 관련 커밋: `677da6fe`
- 검증:
  - `git diff --check`
  - `backend`: `./gradlew.bat test --tests com.dotshelf.domain.book.service.BookServiceTest --tests com.dotshelf.domain.book.controller.BookControllerTest`
  - `frontend`: `dotnet build frontend\App.Features.csproj --no-restore`
  - `frontend`: `dotnet build frontend\App.Infrastructure.csproj --no-restore`
  - `frontend`: `dotnet test frontend\App.Tests.EditMode.csproj --no-restore --verbosity normal`
