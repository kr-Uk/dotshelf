package com.dotshelf.domain.book.service;

import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.format.DateTimeParseException;
import java.util.List;
import java.util.regex.Pattern;

import org.springframework.core.ParameterizedTypeReference;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;
import org.springframework.web.util.HtmlUtils;

import com.dotshelf.domain.book.dto.BookDtos.BookSearchResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.SocialLoginProperties;

import lombok.extern.slf4j.Slf4j;

@Slf4j
@Component
public class KakaoBookSearchClient {

    private static final String BOOK_SEARCH_HOST = "dapi.kakao.com";
    private static final String BOOK_SEARCH_PATH = "/v3/search/book";
    private static final String TITLE_TARGET = "title";
    private static final Pattern HTML_TAG = Pattern.compile("<[^>]+>");
    private static final ParameterizedTypeReference<KakaoBookSearchResponse> RESPONSE_TYPE = new ParameterizedTypeReference<>() {
    };

    private final RestClient restClient;
    private final SocialLoginProperties properties;

    public KakaoBookSearchClient(RestClient.Builder restClientBuilder, SocialLoginProperties properties) {
        this.restClient = restClientBuilder.build();
        this.properties = properties;
    }

    public SearchResult search(String keyword, int page, int size) {
        try {
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
            if (response == null) {
                return new SearchResult(List.of(), true);
            }
            return new SearchResult(
                    response.documents().stream()
                            .map(this::toBookSearchResponse)
                            .toList(),
                    response.meta().isEnd()
            );
        } catch (RestClientException e) {
            log.warn("Kakao book search request failed (keyword={})", keyword, e);
            throw new CustomException(ErrorCode.BOOK_SEARCH_UNAVAILABLE);
        }
    }

    private BookSearchResponse toBookSearchResponse(KakaoBookDocument document) {
        IsbnPair isbnPair = parseIsbn(document.isbn());
        return new BookSearchResponse(
                null,
                "KAKAO",
                isbnPair.isbn13(),
                isbnPair.isbn10(),
                cleanText(document.title()),
                authorName(document.authors()),
                cleanText(document.publisher()),
                publishedDate(document.datetime()),
                cleanText(document.contents()),
                blankToNull(document.thumbnail()),
                blankToNull(document.url())
        );
    }

    private String restApiKey() {
        String restApiKey = properties.kakao().restApiKey();
        if (restApiKey == null || restApiKey.isBlank()) {
            throw new CustomException(ErrorCode.BOOK_SEARCH_UNAVAILABLE, "KAKAO_REST_API_KEY 설정이 필요합니다.");
        }
        return restApiKey;
    }

    private IsbnPair parseIsbn(String rawIsbn) {
        if (rawIsbn == null || rawIsbn.isBlank()) {
            return new IsbnPair(null, null);
        }

        String isbn10 = null;
        String isbn13 = null;
        for (String token : rawIsbn.trim().split("\\s+")) {
            if (token.length() == 13) {
                isbn13 = token;
            } else if (token.length() == 10) {
                isbn10 = token;
            }
        }
        return new IsbnPair(isbn10, isbn13);
    }

    private String authorName(List<String> authors) {
        if (authors == null || authors.isEmpty()) {
            return "작가 미상";
        }
        String author = cleanText(String.join(", ", authors));
        return author == null ? "작가 미상" : author;
    }

    private LocalDate publishedDate(String datetime) {
        String value = blankToNull(datetime);
        if (value == null) {
            return null;
        }
        try {
            return OffsetDateTime.parse(value).toLocalDate();
        } catch (DateTimeParseException e) {
            return null;
        }
    }

    private String cleanText(String value) {
        String text = blankToNull(value);
        if (text == null) {
            return null;
        }
        return HtmlUtils.htmlUnescape(HTML_TAG.matcher(text).replaceAll("")).trim();
    }

    private String blankToNull(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        return value.trim();
    }

    public record SearchResult(List<BookSearchResponse> books, boolean isEnd) {
    }

    private record KakaoBookSearchResponse(KakaoMeta meta, List<KakaoBookDocument> documents) {

        private KakaoBookSearchResponse {
            meta = meta == null ? new KakaoMeta(true) : meta;
            documents = documents == null ? List.of() : documents;
        }
    }

    private record KakaoMeta(Boolean is_end) {

        private boolean isEnd() {
            return Boolean.TRUE.equals(is_end);
        }
    }

    private record KakaoBookDocument(
            String title,
            String contents,
            String url,
            String isbn,
            String datetime,
            List<String> authors,
            String publisher,
            String thumbnail
    ) {
    }

    private record IsbnPair(String isbn10, String isbn13) {
    }
}
