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

import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;

@Tag(name = "Notification", description = "알림 조회 / 읽음 처리")
@RestController
@RequiredArgsConstructor
@RequestMapping("/v1/notifications")
public class NotificationController {

    private final NotificationService notificationService;

    @GetMapping
    public ApiResponse<NotificationListResponse> getNotifications(@CurrentUser Long userId) {
        return ApiResponse.success(notificationService.getNotifications(userId));
    }

    @PatchMapping("/{notificationId}/read")
    public ApiResponse<NotificationResponse> read(@CurrentUser Long userId, @PathVariable Long notificationId) {
        return ApiResponse.success(notificationService.read(userId, notificationId));
    }

    @PatchMapping("/read-all")
    public ApiResponse<Void> readAll(@CurrentUser Long userId) {
        notificationService.readAll(userId);
        return ApiResponse.success();
    }
}
