package com.dotshelf.domain.notification.service;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationListResponse;
import com.dotshelf.domain.notification.dto.NotificationDtos.NotificationResponse;
import com.dotshelf.domain.notification.entity.Notification;
import com.dotshelf.domain.notification.repository.NotificationRepository;
import com.dotshelf.domain.user.service.UserService;
import com.dotshelf.global.error.CustomException;
import com.dotshelf.global.error.ErrorCode;

import lombok.RequiredArgsConstructor;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class NotificationService {

    private final NotificationRepository notificationRepository;
    private final UserService userService;

    public NotificationListResponse getNotifications(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        return new NotificationListResponse(notificationRepository.findByReceiverUserIdOrderByIdDesc(currentUserId)
                .stream()
                .map(NotificationResponse::from)
                .toList());
    }

    @Transactional
    public NotificationResponse read(Long userId, Long notificationId) {
        Long currentUserId = userService.requireUserId(userId);
        Notification notification = notificationRepository.findById(notificationId)
                .orElseThrow(() -> new CustomException(ErrorCode.NOTIFICATION_NOT_FOUND));
        if (!notification.getReceiverUserId().equals(currentUserId)) {
            throw new CustomException(ErrorCode.FORBIDDEN);
        }
        notification.read();
        return NotificationResponse.from(notification);
    }

    @Transactional
    public void readAll(Long userId) {
        Long currentUserId = userService.requireUserId(userId);
        notificationRepository.findByReceiverUserIdAndIsReadFalse(currentUserId).forEach(Notification::read);
    }
}
