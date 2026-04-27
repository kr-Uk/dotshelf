package com.dotshelf.domain.notification.entity;

import java.time.LocalDateTime;

import com.dotshelf.global.common.BaseEntity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Getter
@Entity
@Table(name = "notifications")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Notification extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long receiverUserId;

    private Long senderUserId;

    @Column(nullable = false, length = 50)
    private String type;

    @Column(length = 50)
    private String targetType;

    private Long targetId;

    @Column(nullable = false, length = 255)
    private String message;

    @Column(nullable = false)
    private boolean isRead = false;

    private LocalDateTime readAt;

    @Builder
    private Notification(Long receiverUserId, Long senderUserId, String type, String targetType, Long targetId, String message) {
        this.receiverUserId = receiverUserId;
        this.senderUserId = senderUserId;
        this.type = type;
        this.targetType = targetType;
        this.targetId = targetId;
        this.message = message;
    }

    public void read() {
        this.isRead = true;
        this.readAt = LocalDateTime.now();
    }
}
