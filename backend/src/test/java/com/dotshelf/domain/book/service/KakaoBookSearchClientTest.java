package com.dotshelf.domain.book.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.springframework.test.web.client.response.MockRestResponseCreators.withServerError;
import static org.springframework.test.web.client.response.MockRestResponseCreators.withSuccess;

import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.http.HttpMethod;
import org.springframework.http.MediaType;
import org.springframework.test.web.client.MockRestServiceServer;
import org.springframework.web.client.RestClient;

import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;
import com.dotshelf.global.security.SocialLoginProperties;

class KakaoBookSearchClientTest {

    @Test
    @DisplayName("Kakao 검색 요청은 필수 query/header를 보내고 응답 document를 정규화해서 매핑한다")
    void search_success_mapsDocumentsAndRequestParameters() {
        RestClient.Builder builder = RestClient.builder();
        MockRestServiceServer server = MockRestServiceServer.bindTo(builder).build();
        KakaoBookSearchClient client = new KakaoBookSearchClient(builder, properties("rest-key"));
        server.expect(request -> {
            assertThat(request.getMethod()).isEqualTo(HttpMethod.GET);
            assertThat(request.getURI().getScheme()).isEqualTo("https");
            assertThat(request.getURI().getHost()).isEqualTo("dapi.kakao.com");
            assertThat(request.getURI().getPath()).isEqualTo("/v3/search/book");
            assertThat(decodedQuery(request.getURI().getRawQuery()))
                    .contains("query=데미안")
                    .contains("target=title")
                    .contains("page=2")
                    .contains("size=3");
            assertThat(request.getHeaders().getFirst("Authorization")).isEqualTo("KakaoAK rest-key");
        }).andRespond(withSuccess("""
                {
                  "meta": {"is_end": true},
                  "documents": [
                    {
                      "title": "<b>데미안</b>&amp;청춘",
                      "contents": "<p>성장 소설</p>",
                      "url": "https://book.example/demian",
                      "isbn": "1234567890 9791190000001",
                      "datetime": "2026-05-07T15:30:00.000+09:00",
                      "authors": ["헤르만 헤세", "번역가"],
                      "publisher": "민음사",
                      "thumbnail": "https://img.example/demian.jpg"
                    }
                  ]
                }
                """, MediaType.APPLICATION_JSON));

        var result = client.search("데미안", 2, 3);

        assertThat(result.isEnd()).isTrue();
        assertThat(result.books()).hasSize(1);
        var book = result.books().getFirst();
        assertThat(book.source()).isEqualTo("KAKAO");
        assertThat(book.title()).isEqualTo("데미안&청춘");
        assertThat(book.description()).isEqualTo("성장 소설");
        assertThat(book.isbn10()).isEqualTo("1234567890");
        assertThat(book.isbn13()).isEqualTo("9791190000001");
        assertThat(book.author()).isEqualTo("헤르만 헤세, 번역가");
        assertThat(book.publishedDate()).hasToString("2026-05-07");
        assertThat(book.coverImageUrl()).isEqualTo("https://img.example/demian.jpg");
        server.verify();
    }

    @Test
    @DisplayName("Kakao 응답의 빈 저자/ISBN/날짜/이미지는 안전한 기본값 또는 null로 매핑한다")
    void search_missingOptionalFields_usesDefaultsAndNulls() {
        RestClient.Builder builder = RestClient.builder();
        MockRestServiceServer server = MockRestServiceServer.bindTo(builder).build();
        KakaoBookSearchClient client = new KakaoBookSearchClient(builder, properties("rest-key"));
        server.expect(request -> {
        }).andRespond(withSuccess("""
                {
                  "meta": {"is_end": false},
                  "documents": [
                    {
                      "title": "  무명 도서  ",
                      "contents": "",
                      "url": "",
                      "isbn": " ",
                      "datetime": "not-a-date",
                      "authors": [],
                      "publisher": " ",
                      "thumbnail": " "
                    }
                  ]
                }
                """, MediaType.APPLICATION_JSON));

        var result = client.search("무명", 1, 1);

        assertThat(result.isEnd()).isFalse();
        var book = result.books().getFirst();
        assertThat(book.title()).isEqualTo("무명 도서");
        assertThat(book.author()).isEqualTo("작가 미상");
        assertThat(book.isbn10()).isNull();
        assertThat(book.isbn13()).isNull();
        assertThat(book.publisher()).isNull();
        assertThat(book.publishedDate()).isNull();
        assertThat(book.coverImageUrl()).isNull();
        assertThat(book.sourceUrl()).isNull();
        server.verify();
    }

    @Test
    @DisplayName("Kakao REST API key가 없으면 provider 호출 없이 BOOK_SEARCH_UNAVAILABLE을 던진다")
    void search_missingRestApiKey_throwsBookSearchUnavailable() {
        KakaoBookSearchClient client = new KakaoBookSearchClient(RestClient.builder(), properties(null));

        assertThatThrownBy(() -> client.search("데미안", 1, 10))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.BOOK_SEARCH_UNAVAILABLE);
    }

    @Test
    @DisplayName("Kakao provider 장애는 BOOK_SEARCH_UNAVAILABLE로 변환한다")
    void search_providerFailure_throwsBookSearchUnavailable() {
        RestClient.Builder builder = RestClient.builder();
        MockRestServiceServer server = MockRestServiceServer.bindTo(builder).build();
        KakaoBookSearchClient client = new KakaoBookSearchClient(builder, properties("rest-key"));
        server.expect(request -> {
        }).andRespond(withServerError());

        assertThatThrownBy(() -> client.search("데미안", 1, 10))
                .isInstanceOf(CustomException.class)
                .extracting("errorCode")
                .isEqualTo(ErrorCode.BOOK_SEARCH_UNAVAILABLE);
        server.verify();
    }

    private SocialLoginProperties properties(String restApiKey) {
        return new SocialLoginProperties(
                null,
                new SocialLoginProperties.Kakao(restApiKey, null, null, null, null),
                null
        );
    }

    private String decodedQuery(String rawQuery) {
        return URLDecoder.decode(rawQuery, StandardCharsets.UTF_8);
    }
}
