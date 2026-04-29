package com.dotshelf.domain.admin.entity;

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
@Table(name = "admin_audit_logs", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AdminAuditLog extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long actorUserId;

    @Column(nullable = false, length = 80)
    private String action;

    @Column(nullable = false, length = 80)
    private String targetType;

    private Long targetId;

    @Column(columnDefinition = "text")
    private String beforePayload;

    @Column(columnDefinition = "text")
    private String afterPayload;

    @Column(length = 500)
    private String reason;

    @Column(length = 45)
    private String ipAddress;

    @Builder
    private AdminAuditLog(Long actorUserId, String action, String targetType, Long targetId,
                          String beforePayload, String afterPayload, String reason, String ipAddress) {
        this.actorUserId = actorUserId;
        this.action = action;
        this.targetType = targetType;
        this.targetId = targetId;
        this.beforePayload = beforePayload;
        this.afterPayload = afterPayload;
        this.reason = reason;
        this.ipAddress = ipAddress;
    }
}
