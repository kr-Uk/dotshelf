package com.dotshelf.domain.book.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.math.BigDecimal;
import java.util.List;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;

import com.dotshelf.domain.book.dto.BookDtos.BookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookPlacementListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookPlacementResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookResponse;
import com.dotshelf.domain.book.service.BookReviewService;
import com.dotshelf.domain.book.service.BookService;
import com.dotshelf.global.common.CurrentUserArgumentResolver;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.GlobalExceptionHandler;

@ExtendWith(MockitoExtension.class)
class BookControllerTest {

    @Mock
    private BookService bookService;

    @Mock
    private BookReviewService bookReviewService;

    private MockMvc mockMvc;

    @BeforeEach
    void setUp() {
        mockMvc = MockMvcBuilders
                .standaloneSetup(new BookController(bookService, bookReviewService))
                .setCustomArgumentResolvers(new CurrentUserArgumentResolver(true, "X-Test-Admin-User-Id"))
                .setControllerAdvice(new GlobalExceptionHandler())
                .build();
    }

    @Test
    @DisplayName("도서 검색은 keyword/page/size를 서비스로 전달한다")
    void search_passesQuery() throws Exception {
        given(bookService.searchBooks("clean", 2, 5))
                .willReturn(new BookSearchListResponse(List.of(searchResponse(1L)), 2, 5, true));

        mockMvc.perform(get("/v1/books/search")
                        .param("keyword", "clean")
                        .param("page", "2")
                        .param("size", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.books[0].title").value("Book 1"));
    }

    @Test
    @DisplayName("비인증 카탈로그는 RANKING만 허용한다")
    void books_rejectsUnsupportedCategory() throws Exception {
        mockMvc.perform(get("/v1/books").param("category", "NEW"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.code").value("C001"));
    }

    @Test
    @DisplayName("랭킹 조회는 필터와 current userId를 전달한다")
    void ranking_passesFilters() throws Exception {
        given(bookService.getRanking(1L, "novel", "FREE", "c1", "POPULAR", 10))
                .willReturn(new BookListResponse(List.of(bookResponse(1L)), "next"));

        mockMvc.perform(get("/v1/books/ranking")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("genre", "novel")
                        .param("priceType", "FREE")
                        .param("cursor", "c1")
                        .param("limit", "10"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.items[0].id").value(1))
                .andExpect(jsonPath("$.result.nextCursor").value("next"));
    }

    @Test
    @DisplayName("도서 상세 조회는 bookId와 current userId를 전달한다")
    void getBook_passesIds() throws Exception {
        given(bookService.getBook(1L, 99L)).willReturn(bookResponse(99L));

        mockMvc.perform(get("/v1/books/99").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(99));
    }

    @Test
    @DisplayName("리뷰 생성은 validation과 service 호출을 수행한다")
    void createReview_validatesAndCallsService() throws Exception {
        given(bookReviewService.createReview(any(), any(), any(BookReviewRequest.class)))
                .willReturn(new BookReviewResponse(5L, 9L, 1L, "tester", null, BigDecimal.valueOf(4.5), "good", 0, false, null, null));

        mockMvc.perform(post("/v1/books/9/reviews")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"rating":4.5,"content":"good"}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.reviewId").value(5));

        mockMvc.perform(post("/v1/books/9/reviews")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"rating":0.1,"content":" "}
                                """))
                .andExpect(status().isBadRequest());
    }

    @Test
    @DisplayName("책장에 책 추가는 필수 필드를 검증한다")
    void addToShelf_validatesRequiredFields() throws Exception {
        mockMvc.perform(post("/v1/bookshelves/3/books")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"sectionId":2,"slotNo":1}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("bookId")));
    }

    @Test
    @DisplayName("Kakao 도서 추가는 title/section/slot 필수값을 검증한다")
    void addKakaoBookToShelf_validatesRequiredFields() throws Exception {
        mockMvc.perform(post("/v1/bookshelves/3/books/kakao")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"isbn13":"1234567890123"}
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.message").value(org.hamcrest.Matchers.containsString("title")));
    }

    @Test
    @DisplayName("user book 수정/이동/보관은 서비스 결과를 반환한다")
    void userBookMutations_returnServiceResponses() throws Exception {
        given(bookService.updateUserBook(any(), any(), any())).willReturn(userBookResponse(7L));
        given(bookService.move(any(), any(), any())).willReturn(userBookResponse(7L));
        given(bookService.archive(1L, 7L)).willReturn(userBookResponse(7L));

        mockMvc.perform(patch("/v1/user-books/7")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"readingStatus":"READING","ownerRating":4.0}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.readingStatus").value("READING"));

        mockMvc.perform(post("/v1/user-books/7/move")
                        .header("X-Test-Admin-User-Id", "1")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {"bookshelfId":3,"sectionId":2,"slotNo":1}
                                """))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(7));

        mockMvc.perform(post("/v1/user-books/7/archive").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.id").value(7));
    }

    @Test
    @DisplayName("보관/목록/좋아요 API는 current userId를 전달한다")
    void userBookQueriesAndLikes_passCurrentUser() throws Exception {
        given(bookService.getArchived(1L)).willReturn(new UserBookListResponse(List.of(userBookResponse(7L))));
        given(bookService.getUserBooks(1L, "ACTIVE", "READING", 3L, 2L, 10))
                .willReturn(new UserBookPlacementListResponse(List.of(
                        new UserBookPlacementResponse(7L, 1L, 9L, "READING", "ACTIVE", null, false, null, null, null, null, null, null)
                ), null));
        given(bookService.like(1L, 7L)).willReturn(new ToggleResponse(true, 1));
        given(bookService.unlike(1L, 7L)).willReturn(new ToggleResponse(false, 0));

        mockMvc.perform(get("/v1/user-books/archived").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.books[0].id").value(7));

        mockMvc.perform(get("/v1/user-books")
                        .header("X-Test-Admin-User-Id", "1")
                        .param("storageStatus", "ACTIVE")
                        .param("readingStatus", "READING")
                        .param("bookshelfId", "3")
                        .param("sectionId", "2")
                        .param("limit", "10"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.books[0].bookId").value(9));

        mockMvc.perform(post("/v1/user-books/7/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(true));

        mockMvc.perform(delete("/v1/user-books/7/like").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.active").value(false));
    }

    @Test
    @DisplayName("user book 삭제는 성공 envelope만 반환한다")
    void deleteUserBook_returnsSuccessEnvelope() throws Exception {
        mockMvc.perform(delete("/v1/user-books/7").header("X-Test-Admin-User-Id", "1"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.result").doesNotExist());

        then(bookService).should().delete(1L, 7L);
    }

    @Test
    @DisplayName("리뷰 목록 조회는 sort/cursor/limit을 전달한다")
    void getReviews_passesPagination() throws Exception {
        given(bookReviewService.getReviews(9L, "RECENT", "c1", 5))
                .willReturn(new BookReviewListResponse(List.of(), null));

        mockMvc.perform(get("/v1/books/9/reviews")
                        .param("sort", "RECENT")
                        .param("cursor", "c1")
                        .param("limit", "5"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.result.reviews").isArray());
    }

    private BookSearchResponse searchResponse(Long id) {
        return new BookSearchResponse(id, "LOCAL", "978", null, "Book " + id, "author", "publisher", null, null, null, null);
    }

    private BookResponse bookResponse(Long id) {
        return new BookResponse(id, "LOCAL", null, String.valueOf(id), "978", "978", null,
                "Book " + id, "author", "publisher", null, null, null, null,
                0, BigDecimal.ZERO, 0, 0, 0, 0, 0, 0, false, false, null, null);
    }

    private UserBookResponse userBookResponse(Long id) {
        return new UserBookResponse(id, 1L, 9L, "READING", "ACTIVE", null, false, null);
    }
}
