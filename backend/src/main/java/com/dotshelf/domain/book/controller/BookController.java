package com.dotshelf.domain.book.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.book.dto.BookDtos.AddBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.AddKakaoBookToShelfRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewRequest;
import com.dotshelf.domain.book.dto.BookDtos.BookReviewResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchListResponse;
import com.dotshelf.domain.book.dto.BookDtos.MoveUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UpdateUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UserBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookPlacementListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookResponse;
import com.dotshelf.domain.book.service.BookReviewService;
import com.dotshelf.domain.book.service.BookService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Book", description = "도서 검색 / 내 책 (user_books) 관리 / 좋아요")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1")
public class BookController {

    private final BookService bookService;
    private final BookReviewService bookReviewService;

    @Operation(summary = "도서 검색", description = "로컬 도서와 Kakao 도서 검색 결과를 함께 조회합니다.")
    @GetMapping("/books/search")
    public ApiResponse<BookSearchListResponse> search(
            @RequestParam(required = false) String keyword,
            @RequestParam(defaultValue = "1") int page,
            @RequestParam(defaultValue = "10") int size
    ) {
        return ApiResponse.success(bookService.searchBooks(keyword, page, size));
    }

    @Operation(
            summary = "도서 카탈로그 (비인증)",
            description = "Unity 클라이언트의 책장 초기 로딩용 비인증 엔드포인트. "
                    + "현재 지원 카테고리는 RANKING 만이며 미지정 시에도 RANKING 으로 동작한다. "
                    + "그 외 값이 들어오면 400 INVALID_INPUT 으로 응답한다."
    )
    @GetMapping("/books")
    public ApiResponse<BookListResponse> books(
            @RequestParam(required = false) String category,
            @RequestParam(defaultValue = "48") int limit
    ) {
        if (category != null && !"RANKING".equalsIgnoreCase(category)) {
            throw new CustomException(
                    ErrorCode.INVALID_INPUT,
                    "category 는 RANKING 만 지원합니다. 받은 값: " + category
            );
        }
        return ApiResponse.success(
                bookService.getRanking(null, null, null, null, "POPULAR", limit)
        );
    }

    @Operation(summary = "추천 도서 조회", description = "현재 사용자에게 추천할 도서 목록을 지정한 개수만큼 조회합니다.")
    @GetMapping("/books/recommended")
    public ApiResponse<BookListResponse> recommended(
            @CurrentUser Long userId,
            @RequestParam(defaultValue = "12") int limit
    ) {
        return ApiResponse.success(bookService.getRecommended(userId, limit));
    }

    @Operation(summary = "도서 랭킹 조회", description = "장르, 가격 유형, 정렬 조건으로 랭킹 도서 목록을 커서 기반으로 조회합니다.")
    @GetMapping("/books/ranking")
    public ApiResponse<BookListResponse> ranking(
            @CurrentUser Long userId,
            @RequestParam(required = false) String genre,
            @RequestParam(required = false) String priceType,
            @RequestParam(defaultValue = "POPULAR") String sort,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(bookService.getRanking(userId, genre, priceType, cursor, sort, limit));
    }

    @Operation(summary = "도서 상세 조회", description = "도서 ID로 도서 상세 정보와 현재 사용자의 상호작용 상태를 조회합니다.")
    @GetMapping("/books/{bookId}")
    public ApiResponse<BookResponse> getBook(@CurrentUser Long userId, @PathVariable Long bookId) {
        return ApiResponse.success(bookService.getBook(userId, bookId));
    }

    @Operation(summary = "도서 리뷰 목록 조회", description = "도서 ID로 리뷰 목록을 정렬 조건과 커서 기반 페이지로 조회합니다.")
    @GetMapping("/books/{bookId}/reviews")
    public ApiResponse<BookReviewListResponse> getReviews(
            @PathVariable Long bookId,
            @RequestParam(defaultValue = "POPULAR") String sort,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(bookReviewService.getReviews(bookId, sort, cursor, limit));
    }

    @Operation(summary = "도서 리뷰 작성", description = "현재 사용자가 지정한 도서에 리뷰를 작성합니다.")
    @PostMapping("/books/{bookId}/reviews")
    public ApiResponse<BookReviewResponse> createReview(
            @CurrentUser Long userId,
            @PathVariable Long bookId,
            @RequestBody @Valid BookReviewRequest request
    ) {
        return ApiResponse.success(bookReviewService.createReview(userId, bookId, request));
    }

    @Operation(summary = "내 책장에 도서 추가", description = "기존 도서를 현재 사용자의 지정한 책장에 추가합니다.")
    @PostMapping("/bookshelves/{bookshelfId}/books")
    public ApiResponse<UserBookResponse> addToShelf(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId,
            @RequestBody @Valid AddBookToShelfRequest request
    ) {
        return ApiResponse.success(bookService.addToShelf(userId, bookshelfId, request));
    }

    @Operation(summary = "Kakao 도서 책장 추가", description = "Kakao 검색 결과에서 선택한 도서를 저장하거나 재사용한 뒤 내 책장에 추가합니다.")
    @PostMapping("/bookshelves/{bookshelfId}/books/kakao")
    public ApiResponse<UserBookResponse> addKakaoBookToShelf(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId,
            @RequestBody @Valid AddKakaoBookToShelfRequest request
    ) {
        return ApiResponse.success(bookService.addKakaoBookToShelf(userId, bookshelfId, request));
    }

    @Operation(summary = "내 책 정보 수정", description = "현재 사용자의 보유 도서 읽기 상태, 메모 등 사용자별 책 정보를 수정합니다.")
    @PatchMapping("/user-books/{userBookId}")
    public ApiResponse<UserBookResponse> updateUserBook(
            @CurrentUser Long userId,
            @PathVariable Long userBookId,
            @RequestBody UpdateUserBookRequest request
    ) {
        return ApiResponse.success(bookService.updateUserBook(userId, userBookId, request));
    }

    @Operation(summary = "내 책 이동", description = "현재 사용자의 보유 도서를 다른 책장 또는 섹션 위치로 이동합니다.")
    @PostMapping("/user-books/{userBookId}/move")
    public ApiResponse<UserBookResponse> move(
            @CurrentUser Long userId,
            @PathVariable Long userBookId,
            @RequestBody @Valid MoveUserBookRequest request
    ) {
        return ApiResponse.success(bookService.move(userId, userBookId, request));
    }

    @Operation(summary = "내 책 보관 처리", description = "현재 사용자의 보유 도서를 보관 상태로 변경합니다.")
    @PostMapping("/user-books/{userBookId}/archive")
    public ApiResponse<UserBookResponse> archive(@CurrentUser Long userId, @PathVariable Long userBookId) {
        return ApiResponse.success(bookService.archive(userId, userBookId));
    }

    @Operation(summary = "내 책 삭제", description = "현재 사용자의 보유 도서를 삭제합니다.")
    @DeleteMapping("/user-books/{userBookId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long userBookId) {
        bookService.delete(userId, userBookId);
        return ApiResponse.success();
    }

    @Operation(summary = "보관한 내 책 조회", description = "현재 사용자가 보관 처리한 보유 도서 목록을 조회합니다.")
    @GetMapping("/user-books/archived")
    public ApiResponse<UserBookListResponse> getArchived(@CurrentUser Long userId) {
        return ApiResponse.success(bookService.getArchived(userId));
    }

    @Operation(summary = "내 책 배치 목록 조회", description = "현재 사용자의 보유 도서를 상태, 책장, 섹션 조건으로 조회합니다.")
    @GetMapping("/user-books")
    public ApiResponse<UserBookPlacementListResponse> getUserBooks(
            @CurrentUser Long userId,
            @RequestParam(required = false) String storageStatus,
            @RequestParam(required = false) String readingStatus,
            @RequestParam(required = false) Long bookshelfId,
            @RequestParam(required = false) Long sectionId,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "50") int limit
    ) {
        return ApiResponse.success(bookService.getUserBooks(
                userId,
                storageStatus,
                readingStatus,
                bookshelfId,
                sectionId,
                limit
        ));
    }

    @Operation(summary = "내 책 좋아요", description = "현재 사용자의 보유 도서에 좋아요를 추가하고 현재 토글 상태를 반환합니다.")
    @PostMapping("/user-books/{userBookId}/like")
    public ApiResponse<ToggleResponse> like(@CurrentUser Long userId, @PathVariable Long userBookId) {
        return ApiResponse.success(bookService.like(userId, userBookId));
    }

    @Operation(summary = "내 책 좋아요 취소", description = "현재 사용자의 보유 도서 좋아요를 취소하고 현재 토글 상태를 반환합니다.")
    @DeleteMapping("/user-books/{userBookId}/like")
    public ApiResponse<ToggleResponse> unlike(@CurrentUser Long userId, @PathVariable Long userBookId) {
        return ApiResponse.success(bookService.unlike(userId, userBookId));
    }
}
