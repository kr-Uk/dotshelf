package com.dotshelf.domain.notification.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationListResponse;
import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationResponse;
import com.dotshelf.domain.notification.service.NotificationService;
import com.dotshelf.global.common.ApiResponse;
import com.dotshelf.global.common.CurrentUser;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;

@Tag(name = "Notification", description = "알림 조회 / 읽음 처리")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/notifications")
public class NotificationController {

    private final NotificationService notificationService;

    @Operation(summary = "알림 목록 조회", description = "현재 사용자의 알림 목록을 조회합니다.")
    @GetMapping
    public ApiResponse<NotificationListResponse> getNotifications(@CurrentUser Long userId) {
        return ApiResponse.success(notificationService.getNotifications(userId));
    }

    @Operation(summary = "알림 읽음 처리", description = "지정한 알림을 읽음 상태로 변경하고 변경된 알림 정보를 반환합니다.")
    @PatchMapping("/{notificationId}/read")
    public ApiResponse<NotificationResponse> read(@CurrentUser Long userId, @PathVariable Long notificationId) {
        return ApiResponse.success(notificationService.read(userId, notificationId));
    }

    @Operation(summary = "모든 알림 읽음 처리", description = "현재 사용자의 모든 알림을 읽음 상태로 변경합니다.")
    @PatchMapping("/read-all")
    public ApiResponse<Void> readAll(@CurrentUser Long userId) {
        notificationService.readAll(userId);
        return ApiResponse.success();
    }
}
