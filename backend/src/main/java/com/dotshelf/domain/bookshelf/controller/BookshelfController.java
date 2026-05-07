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

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Bookshelf", description = "책장 / 섹션 / 배치 / 인테리어 오브젝트")
@RestController
@RequiredArgsConstructor
public class BookshelfController {

    private final BookshelfService bookshelfService;

    @Operation(summary = "공간 책장 목록 조회", description = "지정한 공간에 배치된 책장 목록을 조회합니다.")
    @GetMapping("/v1/spaces/{spaceId}/bookshelves")
    public ApiResponse<BookshelfListResponse> getBookshelves(@PathVariable Long spaceId) {
        return ApiResponse.success(bookshelfService.getBookshelves(spaceId));
    }

    @Operation(summary = "책장 아이템 렌더링 조회", description = "책장에 배치된 책과 인테리어 오브젝트를 렌더링용 구조로 조회합니다.")
    @GetMapping("/v1/bookshelves/{bookshelfId}/items")
    public ApiResponse<BookshelfRenderResponse> getItems(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId
    ) {
        return ApiResponse.success(bookshelfService.getItems(userId, bookshelfId));
    }

    @Operation(summary = "책장 생성", description = "현재 사용자의 공간에 새 책장을 생성합니다.")
    @PostMapping("/v1/bookshelves")
    public ApiResponse<BookshelfResponse> create(
            @CurrentUser Long userId,
            @RequestBody @Valid CreateBookshelfRequest request
    ) {
        return ApiResponse.success(bookshelfService.create(userId, request));
    }

    @Operation(summary = "책장 수정", description = "책장 이름, 위치 등 책장 속성을 수정합니다.")
    @PatchMapping("/v1/bookshelves/{bookshelfId}")
    public ApiResponse<BookshelfResponse> update(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId,
            @RequestBody @Valid UpdateBookshelfRequest request
    ) {
        return ApiResponse.success(bookshelfService.update(userId, bookshelfId, request));
    }

    @Operation(summary = "책장 삭제", description = "현재 사용자가 소유한 책장을 삭제합니다.")
    @DeleteMapping("/v1/bookshelves/{bookshelfId}")
    public ApiResponse<Void> delete(@CurrentUser Long userId, @PathVariable Long bookshelfId) {
        bookshelfService.delete(userId, bookshelfId);
        return ApiResponse.success();
    }

    @Operation(summary = "책장 순서 변경", description = "현재 사용자의 책장 표시 순서를 요청한 순서대로 저장합니다.")
    @PutMapping("/v1/bookshelves/order")
    public ApiResponse<Void> reorder(@CurrentUser Long userId, @RequestBody ReorderBookshelvesRequest request) {
        bookshelfService.reorder(userId, request);
        return ApiResponse.success();
    }

    @Operation(summary = "책장 섹션 정렬 변경", description = "책장 안의 특정 섹션 정렬 방식을 수정합니다.")
    @PatchMapping("/v1/bookshelves/{bookshelfId}/sections/{sectionId}/alignment")
    public ApiResponse<SectionResponse> updateSectionAlignment(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId,
            @PathVariable Long sectionId,
            @RequestBody @Valid SectionAlignmentRequest request
    ) {
        return ApiResponse.success(bookshelfService.updateSectionAlignment(userId, bookshelfId, sectionId, request));
    }

    @Operation(summary = "인테리어 오브젝트 배치", description = "책장에 인테리어 오브젝트를 추가하고 배치 정보를 반환합니다.")
    @PostMapping("/v1/bookshelves/{bookshelfId}/interior-objects")
    public ApiResponse<BookshelfItemResponse> addInteriorObject(
            @CurrentUser Long userId,
            @PathVariable Long bookshelfId,
            @RequestBody @Valid InteriorObjectRequest request
    ) {
        return ApiResponse.success(bookshelfService.addInteriorObject(userId, bookshelfId, request));
    }

    @Operation(summary = "책장 아이템 이동", description = "책장에 배치된 책 또는 오브젝트의 위치, 방향, 표시 방식을 변경합니다.")
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
                request.facingDirection(),
                request.displayStyle()
        ));
    }

    @Operation(summary = "책장 아이템 제거", description = "책장에 배치된 책 또는 오브젝트를 책장에서 제거합니다.")
    @DeleteMapping("/v1/bookshelf-items/{itemId}")
    public ApiResponse<BookshelfItemResponse> removeItem(
            @CurrentUser Long userId,
            @PathVariable Long itemId
    ) {
        return ApiResponse.success(bookshelfService.removeItem(userId, itemId));
    }
}
