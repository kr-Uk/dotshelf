package com.dotshelf.domain.space.controller;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.book.dto.BookDtos.OwnedBookListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.CategoryInventoryRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.DashboardResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.GuestbookPostItWhitelistResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceMutationResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateListResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyMode;
import com.dotshelf.domain.space.dto.SpaceDtos.ThemeApplyRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.VisibilityRequest;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Space", description = "공간 / 레이아웃 / 공개 범위 / 방문")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/spaces")
public class SpaceController {

    private final SpaceService spaceService;

    @GetMapping("/me")
    public ApiResponse<SpaceResponse> getMySpace(@CurrentUser Long userId) {
        return ApiResponse.success(spaceService.getMySpace(userId));
    }

    @GetMapping
    public ApiResponse<SpaceListResponse> getSpaces(
            @CurrentUser Long userId,
            @RequestParam(required = false) String keyword,
            @RequestParam(defaultValue = "POPULAR") String sort,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(spaceService.getSpaces(userId, keyword, sort, limit));
    }

    @GetMapping("/{spaceId}")
    public ApiResponse<SpaceResponse> getSpace(@CurrentUser Long currentUserId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.getSpace(currentUserId, spaceId));
    }

    @GetMapping("/{spaceId}/dashboard")
    public ApiResponse<DashboardResponse> getDashboard(
            @CurrentUser Long currentUserId,
            @PathVariable Long spaceId,
            @RequestParam(defaultValue = "daily") String period
    ) {
        return ApiResponse.success(spaceService.getDashboard(currentUserId, spaceId, period));
    }

    @GetMapping("/{spaceId}/books")
    public ApiResponse<OwnedBookListResponse> getOwnedBooks(
            @CurrentUser Long currentUserId,
            @PathVariable Long spaceId,
            @RequestParam(defaultValue = "ADDED_DESC") String sort,
            @RequestParam(required = false) String cursor,
            @RequestParam(defaultValue = "20") int limit
    ) {
        return ApiResponse.success(spaceService.getOwnedBooks(currentUserId, spaceId, sort, cursor, limit));
    }

    @PostMapping("/{spaceId}/visits")
    public ApiResponse<ToggleResponse> recordVisit(@CurrentUser Long userId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.recordVisit(userId, spaceId));
    }

    @PostMapping("/{spaceId}/like")
    public ApiResponse<ToggleResponse> like(@CurrentUser Long userId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.like(userId, spaceId));
    }

    @DeleteMapping("/{spaceId}/like")
    public ApiResponse<ToggleResponse> unlike(@CurrentUser Long userId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.unlike(userId, spaceId));
    }

    @PutMapping("/me/layout")
    public ApiResponse<LayoutSaveResponse> saveLayout(
            @CurrentUser Long userId,
            @RequestBody @Valid LayoutSaveRequest request
    ) {
        return ApiResponse.success(spaceService.saveMyLayout(userId, request));
    }

    @PatchMapping("/me/theme")
    public ApiResponse<SpaceResponse> applyTheme(@CurrentUser Long userId, @RequestBody ThemeApplyRequest request) {
        return ApiResponse.success(spaceService.applyTheme(userId, request));
    }

    // 카테고리별 개별 적용 6 endpoint — 기능명세서 §1.1, API 명세서 260502 갱신.
    // body: { inventoryId: long? } — null이면 해당 카테고리 unset(기본 외형 복귀).
    // response.themeReset == true면 클라이언트가 룸 전체 reload.

    @PatchMapping("/me/wallpaper")
    public ApiResponse<SpaceMutationResponse> changeWallpaper(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeWallpaper(userId, request));
    }

    @PatchMapping("/me/floor")
    public ApiResponse<SpaceMutationResponse> changeFloor(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeFloor(userId, request));
    }

    @PatchMapping("/me/desk")
    public ApiResponse<SpaceMutationResponse> changeDesk(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeDesk(userId, request));
    }

    @PatchMapping("/me/dashboard")
    public ApiResponse<SpaceMutationResponse> changeDashboard(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeDashboard(userId, request));
    }

    @PatchMapping("/me/interior-light")
    public ApiResponse<SpaceMutationResponse> changeInteriorLight(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeInteriorLight(userId, request));
    }

    @PatchMapping("/me/exterior-light")
    public ApiResponse<SpaceMutationResponse> changeExteriorLight(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeExteriorLight(userId, request));
    }

    /**
     * 전체 테마 적용 (mode 명시). 기존 PATCH /me/theme는 mode 없는 단순 적용으로 유지.
     * mode = RESET | KEEP_BLOCKERS_ONLY
     */
    @PostMapping("/me/theme/{inventoryId}/apply")
    public ApiResponse<SpaceMutationResponse> applyThemeWithMode(
            @CurrentUser Long userId,
            @PathVariable Long inventoryId,
            @RequestParam(name = "mode") ThemeApplyMode mode
    ) {
        return ApiResponse.success(spaceService.applyThemeWithMode(userId, inventoryId, mode));
    }

    @PatchMapping("/me/visibility")
    public ApiResponse<SpaceResponse> updateVisibility(
            @CurrentUser Long userId,
            @RequestBody @Valid VisibilityRequest request
    ) {
        return ApiResponse.success(spaceService.updateVisibility(userId, request));
    }

    @PatchMapping("/me/guestbook-visibility")
    public ApiResponse<SpaceResponse> updateGuestbookVisibility(
            @CurrentUser Long userId,
            @RequestBody @Valid VisibilityRequest request
    ) {
        return ApiResponse.success(spaceService.updateGuestbookVisibility(userId, request));
    }

    @GetMapping("/templates")
    public ApiResponse<SpaceTemplateListResponse> getTemplates() {
        return ApiResponse.success(spaceService.getTemplates());
    }

    @GetMapping("/me/guestbook-postit-whitelist")
    public ApiResponse<GuestbookPostItWhitelistResponse> getGuestbookPostItWhitelist(@CurrentUser Long userId) {
        return ApiResponse.success(spaceService.getGuestbookPostItWhitelist(userId));
    }

    @PutMapping("/me/guestbook-postit-whitelist")
    public ApiResponse<GuestbookPostItWhitelistResponse> updateGuestbookPostItWhitelist(
            @CurrentUser Long userId,
            @RequestBody @Valid GuestbookPostItWhitelistRequest request
    ) {
        return ApiResponse.success(spaceService.updateGuestbookPostItWhitelist(userId, request));
    }
}
