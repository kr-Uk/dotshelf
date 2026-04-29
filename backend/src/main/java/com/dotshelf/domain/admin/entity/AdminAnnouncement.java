package com.dotshelf.domain.admin.entity;

import java.time.Instant;

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
@Table(name = "admin_announcements", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AdminAnnouncement extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 150)
    private String title;

    @Column(nullable = false, columnDefinition = "text")
    private String body;

    @Column(nullable = false, length = 30)
    private String audience;

    private Instant startsAt;

    private Instant endsAt;

    @Column(nullable = false)
    private boolean isUrgent = false;

    @Column(nullable = false)
    private Long createdByUserId;

    @Builder
    private AdminAnnouncement(String title, String body, String audience, Instant startsAt,
                              Instant endsAt, boolean isUrgent, Long createdByUserId) {
        this.title = title;
        this.body = body;
        this.audience = audience;
        this.startsAt = startsAt;
        this.endsAt = endsAt;
        this.isUrgent = isUrgent;
        this.createdByUserId = createdByUserId;
    }
}
