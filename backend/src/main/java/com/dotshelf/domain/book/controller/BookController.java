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
import com.dotshelf.domain.book.dto.BookDtos.BookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.BookResponse;
import com.dotshelf.domain.book.dto.BookDtos.MoveUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UpdateUserBookRequest;
import com.dotshelf.domain.book.dto.BookDtos.UserBookListResponse;
import com.dotshelf.domain.book.dto.BookDtos.UserBookResponse;
import com.dotshelf.domain.book.service.BookService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@RestController
@RequiredArgsConstructor
@RequestMapping("/v1")
public class BookController {

    private final BookService bookService;

    @GetMapping("/books/search")
    public ApiResponse<BookListResponse> search(@RequestParam(required = false) String keyword) {
        return ApiResponse.success(bookService.search(keyword));
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
