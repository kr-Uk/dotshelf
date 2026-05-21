package com.dotshelf.domain.notification.dto;

import java.util.List;

import com.dotshelf.domain.notification.entity.Notification;

public final class NotificationDtos {

    private NotificationDtos() {
    }

    public record NotificationResponse(
            Long id,
            Long senderUserId,
            String type,
            String targetType,
            Long targetId,
            String message,
            boolean read
    ) {

        public static NotificationResponse from(Notification notification) {
            return new NotificationResponse(
                    notification.getId(),
                    notification.getSenderUserId(),
                    notification.getType(),
                    notification.getTargetType(),
                    notification.getTargetId(),
                    notification.getMessage(),
                    notification.isRead()
            );
        }
    }

    public record NotificationListResponse(List<NotificationResponse> notifications) {
    }
}
