package com.dotshelf.domain.bookshelf.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfItemRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfItemResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfListResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfRenderResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.BookshelfResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.CreateBookshelfRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.InteriorObjectRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.ReorderBookshelvesRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionAlignmentRequest;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.SectionResponse;
import com.dotshelf.domain.bookshelf.dto.BookshelfDtos.UpdateBookshelfRequest;
import com.dotshelf.domain.bookshelf.service.BookshelfService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Bookshelf", description = "책장 / 섹션 / 배치 / 인테리어 오브젝트")
@RestController
@RequiredArgsConstructor
public class BookshelfController {

    private final BookshelfService bookshelfService;

    @GetMapping("/v1/spaces/{spaceId}/bookshelves")
    public ApiResponse<BookshelfListResponse> getBookshelves(@PathVariable Long spaceId) {
        return ApiResponse.success(bookshelfService.getBookshelves(spaceId));
    }

    @GetMapping("/v1/bookshelves/{bookshelfId}/items")
    public ApiResponse<BookshelfRenderResponse> getItems(@PathVariable Long bookshelfId) {
        return ApiResponse.success(bookshelfService.getItems(bookshelfId));
    }

    @PostMapping("/v1/bookshelves")
    public ApiResponse<BookshelfResponse> create(
            @CurrentUser Long userId,
            @RequestBody @Valid CreateBookshelfRequest request
    ) {
        return ApiResponse.success(bookshelfService.create(userId, request));
    }

    @PatchMapping("/v1/bookshelves/{bookshelfId}")
    public ApiResponse<BookshelfResponse> update(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId,
            @RequestBody @Valid UpdateBookshelfRequest request
    ) {
        return ApiResponse.success(bookshelfService.update(userId, bookshelfId, request));
    }

    @DeleteMapping("/v1/bookshelves/{bookshelfId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long bookshelfId) {
        bookshelfService.delete(userId, bookshelfId);
        return ApiResponse.success();
    }

    @PutMapping("/v1/bookshelves/order")
    public ApiResponse<Void> reorder(@CurrentUser Long userId, @RequestBody ReorderBookshelvesRequest request) {
        bookshelfService.reorder(userId, request);
        return ApiResponse.success();
    }

    @PatchMapping("/v1/bookshelves/{bookshelfId}/sections/{sectionId}/alignment")
    public ApiResponse<SectionResponse> updateSectionAlignment(
            @CurrentUser Long userId,
            @PathVariable Long sectionId,
            @RequestBody @Valid SectionAlignmentRequest request
    ) {
        return ApiResponse.success(bookshelfService.updateSectionAlignment(userId, sectionId, request));
    }

    @PostMapping("/v1/bookshelves/{bookshelfId}/interior-objects")
    public ApiResponse<BookshelfItemResponse> addInteriorObject(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId,
            @RequestBody @Valid InteriorObjectRequest request
    ) {
        return ApiResponse.success(bookshelfService.addInteriorObject(userId, bookshelfId, request));
    }

    @PatchMapping("/v1/bookshelf-items/{itemId}")
    public ApiResponse<BookshelfItemResponse> moveItem(
            @CurrentUser Long userId,
            @PathVariable Long itemId,
            @RequestBody @Valid BookshelfItemRequest request
    ) {
        return ApiResponse.success(bookshelfService.moveItem(
                userId,
                itemId,
                request.bookshelfId(),
                request.sectionId(),
                request.slotNo(),
                request.displayStyle()
        ));
    }
}
