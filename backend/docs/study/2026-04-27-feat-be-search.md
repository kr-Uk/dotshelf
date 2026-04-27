# Kakao 도서 검색과 책장 추가 연동

- **브랜치**: `feat/be/search`
- **범위**: `origin/dev..HEAD` (커밋 1개)
- **기간**: 2026-04-27 ~ 2026-04-27
- **관련 커밋**: `e82a8a0`

## TL;DR

Kakao 도서 검색 API를 백엔드에서 호출하고, 검색 결과를 사용자가 선택했을 때만 `books`에 저장하도록 구현했다. `books.title_search` 컬럼을 추가해 띄어쓰기 없는 로컬 검색을 준비했고, Kakao 결과와 로컬 결과는 ISBN 기준으로 중복 제거한다. 저장 시에는 `isbn13`을 우선 사용하고, 없으면 `isbn10`으로 기존 도서를 재사용한다.

## 무엇을 했나

### 1. 도서 검색 API 확장

- 사용자 관점 효과: `GET /v1/books/search?keyword=해리&page=1&size=10`로 로컬 도서와 Kakao 도서 검색 결과를 함께 받을 수 있다.
- 시스템 관점 효과: 검색만으로는 `books`에 데이터가 쌓이지 않고, 선택 후 추가할 때만 저장된다.
- 주요 파일: `BookController.java`, `BookService.java`, `KakaoBookSearchClient.java`, `BookDtos.java`

### 2. Kakao 검색 도서 책장 추가

- 사용자 관점 효과: 검색 결과에서 책을 선택하면 바로 내 책장에 추가할 수 있다.
- 시스템 관점 효과: `books`에 이미 있는 도서는 재사용하고, 없을 때만 새로 저장한 뒤 기존 `user_books`/`bookshelf_items` 흐름을 태운다.
- 주요 파일: `BookService.java`, `BookRepository.java`, `BookDtos.java`

### 3. 제목 정규화 검색 컬럼

- 사용자 관점 효과: `포터와 마`처럼 띄어쓰기 차이가 있어도 로컬 DB에 저장된 책은 검색될 수 있는 기반이 생겼다.
- 시스템 관점 효과: Flyway `V2` 마이그레이션으로 `title_search`를 추가하고 기존 데이터도 backfill한다.
- 주요 파일: `Book.java`, `V2__expand_add_books_title_search.sql`, `schema.sql`

## 왜 했나

책 추가 플로우에서 가장 중요한 것은 "검색은 가볍게, 저장은 선택 순간에" 처리하는 것이다. 검색할 때마다 `books`에 저장하면 사용자가 스쳐 지나간 검색 결과까지 DB에 누적되고, 실제 사용자 책장과 무관한 데이터가 많아진다.

반대로 사용자가 책을 추가하는 순간에는 ISBN을 기준으로 `books`를 재사용해야 한다. 같은 책이 여러 사용자에게 추가될 때 `books`는 하나만 유지하고, 사용자별 소유/상태/배치는 `user_books`와 `bookshelf_items`가 담당하는 현재 도메인 구조와 맞기 때문이다.

## 어떻게 구현했나

### Kakao API 호출을 서버 클라이언트로 캡슐화

`KakaoBookSearchClient`는 Kakao Daum 도서 검색 API의 인증 헤더와 응답 파싱을 맡는다. REST API 키는 기존 `social.kakao.rest-api-key` 설정을 재사용하고, 키가 없거나 Kakao 호출이 실패하면 `BOOK_SEARCH_UNAVAILABLE`로 변환한다.

```java
public SearchResult search(String keyword, int page, int size) {
    KakaoBookSearchResponse response = restClient.get()
            .uri(uriBuilder -> uriBuilder
                    .scheme("https")
                    .host(BOOK_SEARCH_HOST)
                    .path(BOOK_SEARCH_PATH)
                    .queryParam("query", keyword)
                    .queryParam("target", TITLE_TARGET)
                    .queryParam("page", page)
                    .queryParam("size", size)
                    .build())
            .header("Authorization", "KakaoAK " + restApiKey())
            .retrieve()
            .body(RESPONSE_TYPE);
    ...
}
```

핵심 포인트:
- Kakao 문서의 `Authorization: KakaoAK ${REST_API_KEY}` 형식을 서버에서만 사용한다.
- Kakao 응답의 `isbn`은 공백으로 ISBN10/ISBN13이 같이 올 수 있어 길이 기준으로 분리한다.
- Kakao `title`, `contents`에 포함될 수 있는 HTML 태그를 제거하고 unescape한다.

### 검색 결과 병합과 중복 제거

1페이지에서는 로컬 DB의 `title_search` 검색 결과를 먼저 붙이고, 이후 Kakao 검색 결과를 붙인다. 중복은 `isbn13`, `isbn10`, 제목+저자 순서로 제거한다.

```java
private String searchResultKey(BookSearchResponse book) {
    String isbn13 = trimToNull(book.isbn13());
    if (isbn13 != null) {
        return "isbn13:" + isbn13;
    }
    String isbn10 = trimToNull(book.isbn10());
    if (isbn10 != null) {
        return "isbn10:" + isbn10;
    }
    return "title:" + Book.normalizeTitleSearch(book.title()) + ":" + normalizeAuthor(book.author());
}
```

### 선택 순간에만 `books` 저장

`POST /v1/bookshelves/{bookshelfId}/books/kakao`는 선택된 Kakao 검색 결과를 받아 `isbn13` 우선, `isbn10` 보조로 기존 도서를 찾는다. 없으면 `Book.builder()`로 새 도서를 만들고, 이후 기존 `addToShelf`를 재사용한다.

핵심 포인트:
- `books`는 도서 원장, `user_books`는 사용자 소유/독서 상태, `bookshelf_items`는 책장 배치라는 역할을 유지한다.
- 기존 `addToShelf`를 재사용해 `bookshelfAddCount`, 기존 배치 제거, 새 배치 생성 흐름이 하나로 유지된다.

### Flyway expand 마이그레이션

`title_search`는 nullable 컬럼으로 추가했다. 무중단 배포 중 구 버전 앱도 같은 DB를 볼 수 있으므로, 새 컬럼 추가와 backfill만 하는 expand 변경으로 처리했다.

```sql
ALTER TABLE public.books
    ADD COLUMN title_search character varying(255);

UPDATE public.books
SET title_search = lower(regexp_replace(title, '[[:space:]]+', '', 'g'))
WHERE title IS NOT NULL
  AND title_search IS NULL;
```

## 배운 것

- **외부 검색과 내부 저장은 분리해야 한다**: 검색 결과는 임시 데이터이고, 사용자가 선택한 순간부터 우리 도메인의 `Book`이 된다.
- **ISBN은 도서 중복 제거의 핵심 키다**: ISBN13을 우선하고, 오래된 도서나 응답 누락을 위해 ISBN10 보조 조회가 필요하다.
- **Flyway expand 변경은 배포 안정성을 높인다**: 컬럼 추가와 backfill은 구 버전 코드와 공존 가능하지만, NOT NULL 강화나 삭제는 별도 배포로 나눠야 한다.
- **검색 UX는 정규화 컬럼이 책임진다**: 원문 제목은 그대로 보존하고, `title_search`처럼 검색 전용 값을 따로 두면 표시와 검색 요구를 분리할 수 있다.

## 더 공부할 것

- [ ] Kakao 검색 API rate limit과 장애 시 fallback 전략은 어떻게 잡을까?
- [ ] `title_search LIKE '%keyword%'`가 데이터가 많아졌을 때 어떤 인덱싱 전략이 필요할까?
- [ ] ISBN이 없는 도서 또는 복수 ISBN이 잘못 들어온 도서는 어떤 기준으로 중복 제거할까?
- [ ] 외부 API 응답 원문을 `book_external_mappings`에 저장할지, 현재처럼 필요한 필드만 저장할지의 트레이드오프는 무엇인가?

## 참고

- Kakao Daum 도서 검색 API: `GET https://dapi.kakao.com/v3/search/book`
- 관련 커밋: `e82a8a0 [BE] Feat: Kakao 도서 검색 및 책장 추가 연동`
