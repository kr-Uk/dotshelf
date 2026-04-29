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
@Table(name = "admin_user_notes", schema = "admin")
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AdminUserNote extends BaseEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long userId;

    @Column(nullable = false)
    private Long authorUserId;

    @Column(nullable = false, length = 1000)
    private String body;

    @Builder
    private AdminUserNote(Long userId, Long authorUserId, String body) {
        this.userId = userId;
        this.authorUserId = authorUserId;
        this.body = body;
    }
}
