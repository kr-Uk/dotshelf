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
import com.dotshelf.domain.book.dto.BookDtos.BookResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookSearchListResponse;
import com.dotshelf.domain.book.dto.BookDtos.MoveUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UpdateUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UserBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookResponse;
import com.dotshelf.domain.book.service.BookService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

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

    @Operation(summary = "도서 검색", description = "로컬 도서와 Kakao 도서 검색 결과를 함께 조회합니다.")
    @GetMapping("/books/search")
    public ApiResponse<BookSearchListResponse> search(
            @RequestParam(required = false) String keyword,
            @RequestParam(defaultValue = "1") int page,
            @RequestParam(defaultValue = "10") int size
    ) {
        return ApiResponse.success(bookService.searchBooks(keyword, page, size));
    }

    @GetMapping("/books/{bookId}")
    public ApiResponse<BookResponse> getBook(@PathVariable Long bookId) {
        return ApiResponse.success(bookService.getBook(bookId));
    }

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

    @PatchMapping("/user-books/{userBookId}")
    public ApiResponse<UserBookResponse> updateUserBook(
            @CurrentUser Long userId,
            @PathVariable Long userBookId,
            @RequestBody UpdateUserBookRequest request
    ) {
        return ApiResponse.success(bookService.updateUserBook(userId, userBookId, request));
    }

    @PostMapping("/user-books/{userBookId}/move")
    public ApiResponse<UserBookResponse> move(
            @CurrentUser Long userId,
            @PathVariable Long userBookId,
            @RequestBody @Valid MoveUserBookRequest request
    ) {
        return ApiResponse.success(bookService.move(userId, userBookId, request));
    }

    @PostMapping("/user-books/{userBookId}/archive")
    public ApiResponse<UserBookResponse> archive(@CurrentUser Long userId, @PathVariable Long userBookId) {
        return ApiResponse.success(bookService.archive(userId, userBookId));
    }

    @DeleteMapping("/user-books/{userBookId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long userBookId) {
        bookService.delete(userId, userBookId);
        return ApiResponse.success();
    }

    @GetMapping("/user-books/archived")
    public ApiResponse<UserBookListResponse> getArchived(@CurrentUser Long userId) {
        return ApiResponse.success(bookService.getArchived(userId));
    }

    @PostMapping("/user-books/{userBookId}/like")
    public ApiResponse<ToggleResponse> like(@CurrentUser Long userId, @PathVariable Long userBookId) {
        return ApiResponse.success(bookService.like(userId, userBookId));
    }

    @DeleteMapping("/user-books/{userBookId}/like")
    public ApiResponse<ToggleResponse> unlike(@CurrentUser Long userId, @PathVariable Long userBookId) {
        return ApiResponse.success(bookService.unlike(userId, userBookId));
    }
}
