package com.dotshelf.domain.space.controller;

import org.springframework.http.MediaType;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RequestPart;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.multipart.MultipartFile;

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
import com.dotshelf.domain.space.dto.SpaceDtos.ThumbnailUploadResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.VisibilityRequest;
import com.dotshelf.domain.space.service.SpaceService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;
import com.dotshelf.global.common.ToggleResponse;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;

@Tag(name = "Space", description = "공간 / 레이아웃 / 공개 범위 / 방문")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/spaces")
public class SpaceController {

    private final SpaceService spaceService;

    @Operation(summary = "내 공간 조회", description = "현재 사용자의 개인 공간 정보를 조회합니다.")
    @GetMapping("/me")
    public ApiResponse<SpaceResponse> getMySpace(@CurrentUser Long userId) {
        return ApiResponse.success(spaceService.getMySpace(userId));
    }

    @Operation(summary = "공간 목록 조회", description = "키워드, 정렬 조건, 개수 제한으로 공개 공간 목록을 조회합니다.")
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

    @Operation(summary = "공간 상세 조회", description = "공간 ID로 공개 범위와 방문자 기준 권한을 반영한 공간 상세 정보를 조회합니다.")
    @GetMapping("/{spaceId}")
    public ApiResponse<SpaceResponse> getSpace(@CurrentUser Long currentUserId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.getSpace(currentUserId, spaceId));
    }

    @Operation(summary = "공간 대시보드 조회", description = "공간 방문, 좋아요 등 대시보드 지표를 기간 조건으로 조회합니다.")
    @GetMapping("/{spaceId}/dashboard")
    public ApiResponse<DashboardResponse> getDashboard(
            @CurrentUser Long currentUserId,
            @PathVariable Long spaceId,
            @RequestParam(defaultValue = "daily") String period
    ) {
        return ApiResponse.success(spaceService.getDashboard(currentUserId, spaceId, period));
    }

    @Operation(summary = "공간 보유 도서 조회", description = "지정한 공간에 공개된 보유 도서를 정렬 조건과 커서 기반 페이지로 조회합니다.")
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

    @Operation(summary = "공간 방문 기록", description = "현재 사용자의 공간 방문을 기록하고 현재 방문 토글 상태를 반환합니다.")
    @PostMapping("/{spaceId}/visits")
    public ApiResponse<ToggleResponse> recordVisit(@CurrentUser Long userId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.recordVisit(userId, spaceId));
    }

    @Operation(summary = "공간 좋아요", description = "지정한 공간에 좋아요를 추가하고 현재 토글 상태를 반환합니다.")
    @PostMapping("/{spaceId}/like")
    public ApiResponse<ToggleResponse> like(@CurrentUser Long userId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.like(userId, spaceId));
    }

    @Operation(summary = "공간 좋아요 취소", description = "지정한 공간의 좋아요를 취소하고 현재 토글 상태를 반환합니다.")
    @DeleteMapping("/{spaceId}/like")
    public ApiResponse<ToggleResponse> unlike(@CurrentUser Long userId, @PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.unlike(userId, spaceId));
    }

    @Operation(summary = "내 공간 레이아웃 저장", description = "현재 사용자의 공간 레이아웃 배치 정보를 저장합니다.")
    @PutMapping("/me/layout")
    public ApiResponse<LayoutSaveResponse> saveLayout(
            @CurrentUser Long userId,
            @RequestBody @Valid LayoutSaveRequest request
    ) {
        return ApiResponse.success(spaceService.saveMyLayout(userId, request));
    }

    @Operation(
            summary = "내 공간 썸네일 업로드",
            description = "Unity 가 방 배치 변경 시 자동 캡처한 이미지를 multipart 로 업로드한다. "
                    + "BE 가 S3 에 저장 후 공간의 thumbnailUrl 을 갱신하고 갱신된 layoutVersion 을 반환한다."
    )
    @PostMapping(value = "/me/thumbnail", consumes = MediaType.MULTIPART_FORM_DATA_VALUE)
    public ApiResponse<ThumbnailUploadResponse> uploadThumbnail(
            @CurrentUser Long userId,
            @RequestPart("file") MultipartFile file
    ) {
        return ApiResponse.success(spaceService.uploadThumbnail(userId, file));
    }

    @Operation(summary = "내 공간 테마 적용", description = "현재 사용자의 공간에 테마 적용 요청을 처리합니다.")
    @PatchMapping("/me/theme")
    public ApiResponse<SpaceResponse> applyTheme(@CurrentUser Long userId, @RequestBody ThemeApplyRequest request) {
        return ApiResponse.success(spaceService.applyTheme(userId, request));
    }

    // 카테고리별 개별 적용 6 endpoint — 기능명세서 §1.1, API 명세서 260502 갱신.
    // body: { inventoryId: long? } — null이면 해당 카테고리 unset(기본 외형 복귀).
    // response.themeReset == true면 클라이언트가 룸 전체 reload.

    @Operation(summary = "벽지 변경", description = "현재 사용자의 공간 벽지를 지정한 인벤토리 에셋으로 변경하거나 기본값으로 되돌립니다.")
    @PatchMapping("/me/wallpaper")
    public ApiResponse<SpaceMutationResponse> changeWallpaper(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeWallpaper(userId, request));
    }

    @Operation(summary = "바닥 변경", description = "현재 사용자의 공간 바닥을 지정한 인벤토리 에셋으로 변경하거나 기본값으로 되돌립니다.")
    @PatchMapping("/me/floor")
    public ApiResponse<SpaceMutationResponse> changeFloor(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeFloor(userId, request));
    }

    @Operation(summary = "책상 변경", description = "현재 사용자의 공간 책상을 지정한 인벤토리 에셋으로 변경하거나 기본값으로 되돌립니다.")
    @PatchMapping("/me/desk")
    public ApiResponse<SpaceMutationResponse> changeDesk(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeDesk(userId, request));
    }

    @Operation(summary = "대시보드 오브젝트 변경", description = "현재 사용자의 공간 대시보드 오브젝트를 지정한 인벤토리 에셋으로 변경하거나 기본값으로 되돌립니다.")
    @PatchMapping("/me/dashboard")
    public ApiResponse<SpaceMutationResponse> changeDashboard(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeDashboard(userId, request));
    }

    @Operation(summary = "실내 조명 변경", description = "현재 사용자의 공간 실내 조명을 지정한 인벤토리 에셋으로 변경하거나 기본값으로 되돌립니다.")
    @PatchMapping("/me/interior-light")
    public ApiResponse<SpaceMutationResponse> changeInteriorLight(
            @CurrentUser Long userId,
            @RequestBody CategoryInventoryRequest request
    ) {
        return ApiResponse.success(spaceService.changeInteriorLight(userId, request));
    }

    @Operation(summary = "실외 조명 변경", description = "현재 사용자의 공간 실외 조명을 지정한 인벤토리 에셋으로 변경하거나 기본값으로 되돌립니다.")
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
    @Operation(summary = "테마 적용 모드 실행", description = "인벤토리 테마를 지정한 적용 모드로 현재 사용자의 공간에 적용합니다.")
    @PostMapping("/me/theme/{inventoryId}/apply")
    public ApiResponse<SpaceMutationResponse> applyThemeWithMode(
            @CurrentUser Long userId,
            @PathVariable Long inventoryId,
            @RequestParam(name = "mode") ThemeApplyMode mode
    ) {
        return ApiResponse.success(spaceService.applyThemeWithMode(userId, inventoryId, mode));
    }

    @Operation(summary = "공간 공개 범위 변경", description = "현재 사용자의 공간 공개 범위를 변경합니다.")
    @PatchMapping("/me/visibility")
    public ApiResponse<SpaceResponse> updateVisibility(
            @CurrentUser Long userId,
            @RequestBody @Valid VisibilityRequest request
    ) {
        return ApiResponse.success(spaceService.updateVisibility(userId, request));
    }

    @Operation(summary = "방명록 공개 범위 변경", description = "현재 사용자의 공간 방명록 공개 범위를 변경합니다.")
    @PatchMapping("/me/guestbook-visibility")
    public ApiResponse<SpaceResponse> updateGuestbookVisibility(
            @CurrentUser Long userId,
            @RequestBody @Valid VisibilityRequest request
    ) {
        return ApiResponse.success(spaceService.updateGuestbookVisibility(userId, request));
    }

    @Operation(summary = "공간 템플릿 조회", description = "공간 생성 또는 꾸미기에서 사용할 수 있는 템플릿 목록을 조회합니다.")
    @GetMapping("/templates")
    public ApiResponse<SpaceTemplateListResponse> getTemplates() {
        return ApiResponse.success(spaceService.getTemplates());
    }

    @Operation(summary = "방명록 포스트잇 허용 목록 조회", description = "현재 사용자의 방명록 포스트잇에서 허용된 에셋 목록을 조회합니다.")
    @GetMapping("/me/guestbook-postit-whitelist")
    public ApiResponse<GuestbookPostItWhitelistResponse> getGuestbookPostItWhitelist(@CurrentUser Long userId) {
        return ApiResponse.success(spaceService.getGuestbookPostItWhitelist(userId));
    }

    @Operation(summary = "방명록 포스트잇 허용 목록 수정", description = "현재 사용자의 방명록 포스트잇 허용 에셋 목록을 저장합니다.")
    @PutMapping("/me/guestbook-postit-whitelist")
    public ApiResponse<GuestbookPostItWhitelistResponse> updateGuestbookPostItWhitelist(
            @CurrentUser Long userId,
            @RequestBody @Valid GuestbookPostItWhitelistRequest request
    ) {
        return ApiResponse.success(spaceService.updateGuestbookPostItWhitelist(userId, request));
    }
}
