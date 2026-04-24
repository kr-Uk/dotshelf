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

import com.dotshelf.domain.space.dto.SpaceDtos.DashboardResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveRequest;
import com.dotshelf.domain.space.dto.SpaceDtos.LayoutSaveResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceResponse;
import com.dotshelf.domain.space.dto.SpaceDtos.SpaceTemplateListResponse;
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

    @GetMapping("/{spaceId}")
    public ApiResponse<SpaceResponse> getSpace(@PathVariable Long spaceId) {
        return ApiResponse.success(spaceService.getSpace(spaceId));
    }

    @GetMapping("/{spaceId}/dashboard")
    public ApiResponse<DashboardResponse> getDashboard(
            @PathVariable Long spaceId,
            @RequestParam(defaultValue = "daily") String period
    ) {
        return ApiResponse.success(spaceService.getDashboard(spaceId, period));
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
}
